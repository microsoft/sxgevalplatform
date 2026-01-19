"""
Authentication token provider for Eval API endpoints using managed identity.
Supports app-to-app authentication flow with token caching and automatic refresh.
"""

import asyncio
import time
import logging
from typing import Optional, Dict, Any
from datetime import datetime, timedelta
from azure.identity.aio import DefaultAzureCredential, ManagedIdentityCredential
from azure.core.credentials import AccessToken
from azure.core.exceptions import ClientAuthenticationError

from ..config.settings import app_settings

logger = logging.getLogger(__name__)


class AuthTokenProvider:
    """Provides Azure AD access tokens for API authentication with caching and auto-refresh."""
    
    def __init__(
        self,
        client_id: str,
        tenant_id: str,
        scope: str,
        use_managed_identity: bool = True,
        enable_caching: bool = True,
        refresh_buffer_seconds: int = 300
    ):
        """
        Initialize the authentication token provider.
        
        Args:
            client_id: Client application ID (SXG-EvalPlatform-EvalRunnerApp)
            tenant_id: Azure AD tenant ID
            scope: API scope (e.g., api://ac2b08ba-4232-438f-b333-0300df1de14d/.default)
            use_managed_identity: Use managed identity (True) or connection string (False)
            enable_caching: Enable token caching to reduce auth calls
            refresh_buffer_seconds: Refresh token this many seconds before expiry
        """
        self.client_id = client_id
        self.tenant_id = tenant_id
        self.scope = scope
        self.use_managed_identity = use_managed_identity
        self.enable_caching = enable_caching
        self.refresh_buffer_seconds = refresh_buffer_seconds
        
        # Token cache
        self._cached_token: Optional[AccessToken] = None
        self._token_lock = asyncio.Lock()
        
        # Credential instance
        self._credential: Optional[Any] = None
        
        logger.info(
            f"Initialized AuthTokenProvider - "
            f"ClientId: {client_id}, "
            f"TenantId: {tenant_id}, "
            f"Scope: {scope}, "
            f"UseManagedIdentity: {use_managed_identity}, "
            f"Caching: {enable_caching}"
        )
    
    def _get_credential(self) -> Any:
        """Get or create Azure credential instance."""
        if self._credential is None:
            # Get managed identity settings from centralized config
            mi_config = app_settings.managed_identity
            
            if mi_config.use_default_azure_credentials:
                # Use DefaultAzureCredential for flexible authentication
                self._credential = DefaultAzureCredential(
                    additionally_allowed_tenants=[self.tenant_id] if self.tenant_id else []
                ) # CodeQL [SM05137] justification - Not used in production
                logger.info("Created DefaultAzureCredential for flexible authentication")
            elif mi_config.client_id:
                # Use User-Assigned Managed Identity with specific client_id
                self._credential = ManagedIdentityCredential(client_id=mi_config.client_id)
                logger.info(f"Created ManagedIdentityCredential with client_id: {mi_config.client_id}")
            else:
                # Use System-Assigned Managed Identity (no client_id)
                self._credential = ManagedIdentityCredential()
                logger.info("Created ManagedIdentityCredential for system-assigned identity")
        
        return self._credential
    
    def _is_token_expired(self, token: Optional[AccessToken]) -> bool:
        """
        Check if token is expired or about to expire.
        
        Args:
            token: Access token to check
            
        Returns:
            True if token is None, expired, or within refresh buffer of expiry
        """
        if token is None:
            return True
        
        # Get current time and expiry time
        current_time = datetime.now().timestamp()
        expiry_time = token.expires_on
        
        # Check if token will expire within the refresh buffer
        time_until_expiry = expiry_time - current_time
        
        if time_until_expiry <= self.refresh_buffer_seconds:
            logger.info(
                f"Token expired or expiring soon - "
                f"Time until expiry: {time_until_expiry}s, "
                f"Refresh buffer: {self.refresh_buffer_seconds}s"
            )
            return True
        
        logger.debug(f"Token valid for {time_until_expiry}s")
        return False
    
    async def get_token(self) -> str:
        """
        Get valid access token, using cache if available and valid.
        
        Returns:
            Bearer token string
            
        Raises:
            ClientAuthenticationError: If authentication fails
        """
        async with self._token_lock:
            # Check if we have a valid cached token
            if (self.enable_caching and self._cached_token is not None and 
                not self._is_token_expired(self._cached_token)):
                logger.debug("Using cached token")
                return self._cached_token.token
            
            # Need to acquire new token
            logger.info("Acquiring new access token...")
            start_time = time.time()
            
            try:
                credential = self._get_credential()
                
                # Request token with telemetry
                logger.info(f"Requesting token for scope: {self.scope}")
                token = await credential.get_token(self.scope)
                
                elapsed_time = time.time() - start_time
                
                # Calculate token lifetime
                token_lifetime = token.expires_on - datetime.now().timestamp()
                
                logger.info(
                    f"✅ Successfully acquired access token - "
                    f"Acquisition time: {elapsed_time:.2f}s, "
                    f"Token lifetime: {token_lifetime:.0f}s ({token_lifetime/3600:.1f}h), "
                    f"Expires at: {datetime.fromtimestamp(token.expires_on).isoformat()}"
                )
                
                # Cache the token
                if self.enable_caching:
                    self._cached_token = token
                    logger.debug("Token cached for reuse")
                
                return token.token
                
            except ClientAuthenticationError as e:
                logger.error(
                    f"❌ Authentication failed - "
                    f"Error: {str(e)}, "
                    f"ClientId: {self.client_id}, "
                    f"TenantId: {self.tenant_id}, "
                    f"Scope: {self.scope}"
                )
                raise
            except Exception as e:
                logger.error(
                    f"❌ Unexpected error acquiring token - "
                    f"Error type: {type(e).__name__}, "
                    f"Error: {str(e)}"
                )
                raise
    
    async def get_auth_header(self) -> Dict[str, str]:
        """
        Get authorization header with bearer token.
        
        Returns:
            Dictionary with Authorization header
            
        Raises:
            ClientAuthenticationError: If authentication fails
        """
        token = await self.get_token()
        return {"Authorization": f"Bearer {token}"}
    
    async def close(self):
        """Close the credential and cleanup resources."""
        if self._credential is not None:
            try:
                await self._credential.close()
                logger.info("Closed authentication credential")
            except Exception as e:
                logger.warning(f"Error closing credential: {e}")
            finally:
                self._credential = None
                self._cached_token = None
