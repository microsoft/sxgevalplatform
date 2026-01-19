"""
Diagnostics module for validating external dependencies and application health.
"""

import asyncio
import logging
import time
from dataclasses import dataclass
from typing import Dict, List, Optional, Any
from enum import Enum

from eval_runner.config.settings import app_settings
from eval_runner.services.auth_token_provider import AuthTokenProvider
# Note: Import actual service classes when available - using dynamic imports for now

logger = logging.getLogger(__name__)


class HealthStatus(Enum):
    """Health check status enumeration."""
    HEALTHY = "healthy"
    UNHEALTHY = "unhealthy"
    UNKNOWN = "unknown"


@dataclass
class DiagnosticResult:
    """Result of a diagnostic check."""
    service_name: str
    status: HealthStatus
    message: str
    details: Optional[Dict[str, Any]] = None
    duration_ms: Optional[float] = None
    error: Optional[Exception] = None


@dataclass
class DiagnosticSummary:
    """Summary of all diagnostic checks."""
    overall_status: HealthStatus
    checks: List[DiagnosticResult]
    total_duration_ms: float
    healthy_count: int
    unhealthy_count: int
    timestamp: float


class DiagnosticsService:
    """Service for validating application dependencies and health."""
    
    def __init__(self):
        """Initialize the diagnostics service."""
        self._auth_provider: Optional[AuthTokenProvider] = None
        self._storage_service: Optional[Any] = None
        self._http_client: Optional[Any] = None
    
    async def run_all_checks(self, include_deep_checks: bool = True) -> DiagnosticSummary:
        """
        Run all diagnostic checks.
        
        Args:
            include_deep_checks: Whether to include slower, more comprehensive checks
            
        Returns:
            DiagnosticSummary with results of all checks
        """
        logger.info("[DIAGNOSTICS_START] Starting comprehensive diagnostics...")
        start_time = time.time()
        
        checks = []
        
        # Basic configuration checks (fast)
        checks.append(await self._check_configuration())
        
        # Authentication checks
        checks.append(await self._check_authentication_setup())
        if include_deep_checks:
            checks.append(await self._check_token_acquisition())
        
        # Storage checks
        checks.append(await self._check_storage_configuration())
        if include_deep_checks:
            checks.append(await self._check_storage_connectivity())
        
        # API endpoint checks
        if include_deep_checks:
            checks.append(await self._check_api_connectivity())
            checks.append(await self._check_default_configuration_endpoint())
        
        # Azure OpenAI checks
        if include_deep_checks:
            checks.append(await self._check_azure_openai_config())
        
        # Calculate summary
        total_duration = (time.time() - start_time) * 1000
        healthy_count = sum(1 for check in checks if check.status == HealthStatus.HEALTHY)
        unhealthy_count = sum(1 for check in checks if check.status == HealthStatus.UNHEALTHY)
        
        overall_status = (
            HealthStatus.HEALTHY if unhealthy_count == 0 
            else HealthStatus.UNHEALTHY
        )
        
        summary = DiagnosticSummary(
            overall_status=overall_status,
            checks=checks,
            total_duration_ms=total_duration,
            healthy_count=healthy_count,
            unhealthy_count=unhealthy_count,
            timestamp=time.time()
        )
        
        self._log_summary(summary)
        return summary
    
    async def _check_configuration(self) -> DiagnosticResult:
        """Check basic application configuration."""
        start_time = time.time()
        
        try:
            errors = []
            
            # Check critical configuration
            if not app_settings.azure_storage.account_name:
                errors.append("Storage account name not configured")
            
            if not app_settings.azure_storage.queue_name:
                errors.append("Storage queue name not configured")
            
            if not app_settings.api_endpoints.base_url:
                errors.append("API base URL not configured")
            
            if not app_settings.api_authentication.tenant_id:
                errors.append("Tenant ID not configured")
            
            if not app_settings.api_authentication.scope:
                errors.append("API scope not configured")
            
            duration_ms = (time.time() - start_time) * 1000
            
            if errors:
                logger.error(f"[DIAGNOSTICS_CHECK] Configuration errors found: {'; '.join(errors)}")
                return DiagnosticResult(
                    service_name="Configuration",
                    status=HealthStatus.UNHEALTHY,
                    message=f"Configuration errors: {'; '.join(errors)}",
                    details={"errors": errors},
                    duration_ms=duration_ms
                )
            
            logger.info(f"[DIAGNOSTICS_CHECK] All required configuration present - validated in {duration_ms:.1f}ms")
            return DiagnosticResult(
                service_name="Configuration",
                status=HealthStatus.HEALTHY,
                message="All required configuration present",
                duration_ms=duration_ms
            )
            
        except Exception as e:
            duration_ms = (time.time() - start_time) * 1000
            return DiagnosticResult(
                service_name="Configuration",
                status=HealthStatus.UNHEALTHY,
                message=f"Configuration check failed: {str(e)}",
                error=e,
                duration_ms=duration_ms
            )
    
    async def _check_authentication_setup(self) -> DiagnosticResult:
        """Check authentication provider initialization."""
        start_time = time.time()
        
        try:
            # Always initialize auth provider (just stores config, no token acquisition)
            self._auth_provider = AuthTokenProvider(
                client_id=app_settings.api_authentication.client_id,
                tenant_id=app_settings.api_authentication.tenant_id,
                scope=app_settings.api_authentication.scope,
                use_managed_identity=app_settings.managed_identity.use_managed_identity,
                enable_caching=app_settings.api_authentication.enable_token_caching,
                refresh_buffer_seconds=app_settings.api_authentication.token_refresh_buffer_seconds
            )
            
            duration_ms = (time.time() - start_time) * 1000
            
            auth_type = "managed identity" if app_settings.managed_identity.use_managed_identity else "service principal"
            auth_enabled = app_settings.api_authentication.enable_authentication
            
            status_msg = f"Authentication provider initialized with {auth_type}"
            if not auth_enabled:
                status_msg += " (authentication disabled - no tokens will be acquired)"
            
            logger.info(f"[DIAGNOSTICS_CHECK] {status_msg} in {duration_ms:.1f}ms")
            return DiagnosticResult(
                service_name="Authentication Setup",
                status=HealthStatus.HEALTHY,
                message=status_msg,
                details={
                    "authentication_enabled": auth_enabled,
                    "use_managed_identity": app_settings.managed_identity.use_managed_identity,
                    "tenant_id": app_settings.api_authentication.tenant_id,
                    "scope": app_settings.api_authentication.scope
                },
                duration_ms=duration_ms
            )
            
        except Exception as e:
            duration_ms = (time.time() - start_time) * 1000
            return DiagnosticResult(
                service_name="Authentication Setup",
                status=HealthStatus.UNHEALTHY,
                message=f"Failed to initialize authentication provider: {str(e)}",
                error=e,
                duration_ms=duration_ms
            )
    
    async def _check_token_acquisition(self) -> DiagnosticResult:
        """Check if we can acquire authentication tokens."""
        start_time = time.time()
        
        # Check if authentication is enabled
        if not app_settings.api_authentication.enable_authentication:
            logger.info("[DIAGNOSTICS_CHECK] Authentication disabled - skipping token acquisition test")
            return DiagnosticResult(
                service_name="Token Acquisition",
                status=HealthStatus.HEALTHY,
                message="Authentication disabled - token acquisition skipped",
                details={
                    "authentication_enabled": False,
                    "reason": "Feature flag disabled"
                },
                duration_ms=(time.time() - start_time) * 1000
            )
        
        try:
            if not self._auth_provider:
                return DiagnosticResult(
                    service_name="Token Acquisition",
                    status=HealthStatus.UNHEALTHY,
                    message="Authentication provider not initialized",
                    duration_ms=0
                )
            
            # Try to acquire a token
            token = await self._auth_provider.get_token()
            duration_ms = (time.time() - start_time) * 1000
            
            if token and len(token) > 50:  # Basic token validation
                logger.info(f"[DIAGNOSTICS_CHECK] Successfully acquired token in {duration_ms:.1f}ms")
                return DiagnosticResult(
                    service_name="Token Acquisition",
                    status=HealthStatus.HEALTHY,
                    message="Successfully acquired authentication token",
                    details={
                        "token_length": len(token),
                        "token_preview": token[:20] + "..."
                    },
                    duration_ms=duration_ms
                )
            else:
                logger.error(f"[DIAGNOSTICS_CHECK] Invalid token received in {duration_ms:.1f}ms")
                return DiagnosticResult(
                    service_name="Token Acquisition",
                    status=HealthStatus.UNHEALTHY,
                    message="Token acquisition returned invalid token",
                    duration_ms=duration_ms
                )
            
        except Exception as e:
            duration_ms = (time.time() - start_time) * 1000
            return DiagnosticResult(
                service_name="Token Acquisition",
                status=HealthStatus.UNHEALTHY,
                message=f"Failed to acquire authentication token: {str(e)}",
                error=e,
                duration_ms=duration_ms
            )
    
    async def _check_storage_configuration(self) -> DiagnosticResult:
        """Check storage service configuration."""
        start_time = time.time()
        
        try:
            # Initialize storage service (dynamic import to avoid circular dependencies)
            try:
                from eval_runner.services.azure_storage import AzureQueueService
                self._storage_service = AzureQueueService()
            except ImportError as import_error:
                return DiagnosticResult(
                    service_name="Storage Configuration",
                    status=HealthStatus.UNHEALTHY,
                    message=f"Failed to import storage service: {str(import_error)}",
                    error=import_error,
                    duration_ms=(time.time() - start_time) * 1000
                )
            
            duration_ms = (time.time() - start_time) * 1000
            
            return DiagnosticResult(
                service_name="Storage Configuration",
                status=HealthStatus.HEALTHY,
                message="Storage service configured successfully",
                details={
                    "account_name": app_settings.azure_storage.account_name,
                    "queue_name": app_settings.azure_storage.queue_name,
                    "use_managed_identity": app_settings.managed_identity.use_managed_identity
                },
                duration_ms=duration_ms
            )
            
        except Exception as e:
            duration_ms = (time.time() - start_time) * 1000
            return DiagnosticResult(
                service_name="Storage Configuration",
                status=HealthStatus.UNHEALTHY,
                message=f"Failed to configure storage service: {str(e)}",
                error=e,
                duration_ms=duration_ms
            )
    
    async def _check_storage_connectivity(self) -> DiagnosticResult:
        """Check actual connectivity and queue operations for Azure Storage."""
        start_time = time.time()
        
        try:
            if not self._storage_service:
                return DiagnosticResult(
                    service_name="Storage Connectivity",
                    status=HealthStatus.UNHEALTHY,
                    message="Storage service not initialized",
                    duration_ms=0
                )
            
            # Test 1: Initialize the service (basic connectivity)
            await self._storage_service.initialize()
            init_time = time.time()
            
            # Ensure queue client is available
            if not hasattr(self._storage_service, 'queue_client') or not self._storage_service.queue_client:
                duration_ms = (time.time() - start_time) * 1000
                return DiagnosticResult(
                    service_name="Storage Connectivity",
                    status=HealthStatus.UNHEALTHY,
                    message="Queue client not properly initialized",
                    duration_ms=duration_ms
                )
            
            # Test 2: Test queue read permissions (non-destructive peek operation)
            try:
                # Use peek_messages - this is non-destructive and tests the same permissions
                # Peek doesn't remove messages from queue, just reads them
                # Note: peek_messages is async and returns a list, not an iterator
                messages = await self._storage_service.queue_client.peek_messages(max_messages=1)
                
                # Check if we got any messages (tests permissions)
                message_count = len(messages) if messages else 0
                
                queue_ops_time = time.time()
                
                # Log result with clear messaging about empty vs populated queue
                if message_count > 0:
                    logger.info(f"[DIAGNOSTICS_CHECK] Successfully tested queue read permissions with peek operation (found {message_count} messages)")
                else:
                    logger.info(f"[DIAGNOSTICS_CHECK] Successfully tested queue read permissions with peek operation (queue is empty - this is normal)")
                
            except Exception as queue_error:
                # Check if it's a permission error
                error_msg = str(queue_error).lower()
                if "authorizationpermissionmismatch" in error_msg or "not authorized" in error_msg:
                    duration_ms = (time.time() - start_time) * 1000
                    logger.error(f"[DIAGNOSTICS_CHECK] Permission denied for queue operations - PIM likely expired")
                    return DiagnosticResult(
                        service_name="Storage Connectivity",
                        status=HealthStatus.UNHEALTHY,
                        message="Queue read permissions denied - check PIM activation",
                        details={
                            "error_type": "AuthorizationPermissionMismatch",
                            "queue_name": app_settings.azure_storage.queue_name,
                            "account_name": app_settings.azure_storage.account_name,
                            "suggestion": "Activate PIM role for Storage Queue Data Message Receiver",
                            "error_details": str(queue_error),
                            "test_method": "peek_messages (non-destructive)"
                        },
                        error=queue_error,
                        duration_ms=duration_ms
                    )
                else:
                    # Re-raise if it's not a permission issue
                    raise queue_error
            
            duration_ms = (time.time() - start_time) * 1000
            
            logger.info(f"[DIAGNOSTICS_CHECK] Connected and verified queue operations in {duration_ms:.1f}ms")
            return DiagnosticResult(
                service_name="Storage Connectivity",
                status=HealthStatus.HEALTHY,
                message="Successfully connected to Azure Storage with queue read permissions",
                details={
                    "init_time_ms": round((init_time - start_time) * 1000),
                    "queue_test_time_ms": round((queue_ops_time - init_time) * 1000),
                    "queue_name": app_settings.azure_storage.queue_name,
                    "test_method": "peek_messages (non-destructive)",
                    "messages_in_queue": message_count,
                    "queue_status": "populated" if message_count > 0 else "empty"
                },
                duration_ms=duration_ms
            )
            
        except Exception as e:
            duration_ms = (time.time() - start_time) * 1000
            error_msg = str(e)
            logger.error(f"[DIAGNOSTICS_CHECK] Failed connectivity test: {error_msg}")
            
            # Add specific error context for common async issues
            if "coroutine" in error_msg.lower():
                logger.error(f"[DIAGNOSTICS_CHECK] Detected async/await issue - this should not crash the application")
                return DiagnosticResult(
                    service_name="Storage Connectivity",
                    status=HealthStatus.UNHEALTHY,
                    message=f"Storage test failed due to async error (non-fatal): {error_msg}",
                    details={
                        "error_type": "AsyncAwaitError",
                        "queue_name": app_settings.azure_storage.queue_name,
                        "account_name": app_settings.azure_storage.account_name,
                        "suggestion": "Check async/await usage in diagnostics"
                    },
                    error=e,
                    duration_ms=duration_ms
                )
            
            return DiagnosticResult(
                service_name="Storage Connectivity",
                status=HealthStatus.UNHEALTHY,
                message=f"Failed to connect to Azure Storage: {error_msg}",
                error=e,
                duration_ms=duration_ms
            )
    
    async def _check_api_connectivity(self) -> DiagnosticResult:
        """Check connectivity to the Eval API."""
        start_time = time.time()
        
        try:
            # Test basic connectivity - auth provider check only needed if auth enabled
            auth_enabled = app_settings.api_authentication.enable_authentication
            
            if auth_enabled and not self._auth_provider:
                return DiagnosticResult(
                    service_name="API Connectivity",
                    status=HealthStatus.UNHEALTHY,
                    message="Authentication provider not initialized",
                    duration_ms=0
                )
            
            try:
                from eval_runner.services.http_client import get_api_client
                self._http_client = get_api_client()
            except ImportError as import_error:
                return DiagnosticResult(
                    service_name="API Connectivity",
                    status=HealthStatus.UNHEALTHY,
                    message=f"Failed to import HTTP client: {str(import_error)}",
                    error=import_error,
                    duration_ms=(time.time() - start_time) * 1000
                )
            
            # Get auth headers (will be empty if auth disabled)
            auth_header = {}
            if auth_enabled and self._auth_provider:
                auth_header = await self._auth_provider.get_auth_header()
            
            duration_ms = (time.time() - start_time) * 1000
            
            if auth_enabled:
                logger.info(f"[DIAGNOSTICS_CHECK] API client configured with authentication in {duration_ms:.1f}ms")
                message = "API client configured with authentication"
            else:
                logger.info(f"[DIAGNOSTICS_CHECK] API client configured without authentication in {duration_ms:.1f}ms")
                message = "API client configured without authentication (feature flag disabled)"
            
            return DiagnosticResult(
                service_name="API Connectivity",
                status=HealthStatus.HEALTHY,
                message=message,
                details={
                    "base_url": app_settings.api_endpoints.base_url,
                    "authentication_enabled": auth_enabled,
                    "has_auth_header": "Authorization" in auth_header
                },
                duration_ms=duration_ms
            )
            
        except Exception as e:
            duration_ms = (time.time() - start_time) * 1000
            return DiagnosticResult(
                service_name="API Connectivity",
                status=HealthStatus.UNHEALTHY,
                message=f"Failed to setup API connectivity: {str(e)}",
                error=e,
                duration_ms=duration_ms
            )
    
    async def _check_default_configuration_endpoint(self) -> DiagnosticResult:
        """Check the default configuration endpoint specifically."""
        start_time = time.time()
        
        try:
            # Initialize API client for this check
            from eval_runner.services.http_client import get_api_client
            api_client = get_api_client()
            
            endpoint = "/api/v1/eval/configurations/defaultconfiguration"
            base_url = app_settings.api_endpoints.base_url
            url = f"{base_url.rstrip('/')}{endpoint}"
            
            # Add authentication headers
            headers = {'Content-Type': 'application/json'}
            auth_headers = await api_client._get_auth_headers()
            headers.update(auth_headers)
            
            # Add timeout protection
            try:
                response_text, status, response_headers = await asyncio.wait_for(
                    api_client._make_request('get', url, headers=headers),
                    timeout=20.0
                )
            except asyncio.TimeoutError:
                duration_ms = (time.time() - start_time) * 1000
                return DiagnosticResult(
                    service_name="Default Config Endpoint",
                    status=HealthStatus.UNHEALTHY,
                    message="API call timed out (20 seconds)",
                    details={"endpoint": endpoint, "timeout_seconds": 20},
                    duration_ms=duration_ms
                )
            
            duration_ms = (time.time() - start_time) * 1000
            
            if status == 200:
                logger.info(f"[DIAGNOSTICS_CHECK] Retrieved successfully in {duration_ms:.1f}ms")
                return DiagnosticResult(
                    service_name="Default Config Endpoint",
                    status=HealthStatus.HEALTHY,
                    message="Successfully retrieved default configuration",
                    details={
                        "status_code": status,
                        "response_size": len(response_text),
                        "endpoint": endpoint
                    },
                    duration_ms=duration_ms
                )
            else:
                logger.error(f"[DIAGNOSTICS_CHECK] Failed with status {status} in {duration_ms:.1f}ms")
                return DiagnosticResult(
                    service_name="Default Config Endpoint",
                    status=HealthStatus.UNHEALTHY,
                    message=f"API returned non-200 status: {status}",
                    details={
                        "status_code": status,
                        "response_text": response_text[:500] if response_text else None,
                        "endpoint": endpoint
                    },
                    duration_ms=duration_ms
                )
            
        except Exception as e:
            duration_ms = (time.time() - start_time) * 1000
            return DiagnosticResult(
                service_name="Default Config Endpoint",
                status=HealthStatus.UNHEALTHY,
                message=f"Failed to call default configuration endpoint: {str(e)}",
                error=e,
                duration_ms=duration_ms
            )
    
    async def _check_azure_openai_config(self) -> DiagnosticResult:
        """Check Azure OpenAI configuration."""
        start_time = time.time()
        
        try:
            errors = []
            
            if not app_settings.azure_openai.endpoint:
                errors.append("Azure OpenAI endpoint not configured")
            
            if not app_settings.azure_openai.deployment_name:
                errors.append("Azure OpenAI deployment name not configured")
            
            if not app_settings.azure_openai.resource_name:
                errors.append("Azure OpenAI resource name not configured")
            
            duration_ms = (time.time() - start_time) * 1000
            
            if errors:
                logger.error(f"[DIAGNOSTICS_CHECK] Configuration errors: {'; '.join(errors)}")
                return DiagnosticResult(
                    service_name="Azure OpenAI Config",
                    status=HealthStatus.UNHEALTHY,
                    message=f"Azure OpenAI configuration errors: {'; '.join(errors)}",
                    details={"errors": errors},
                    duration_ms=duration_ms
                )
            
            logger.info(f"[DIAGNOSTICS_CHECK] Configuration valid - checked in {duration_ms:.1f}ms")
            return DiagnosticResult(
                service_name="Azure OpenAI Config",
                status=HealthStatus.HEALTHY,
                message="Azure OpenAI configuration valid",
                details={
                    "deployment_name": app_settings.azure_openai.deployment_name,
                    "resource_name": app_settings.azure_openai.resource_name,
                    "use_managed_identity": app_settings.managed_identity.use_managed_identity
                },
                duration_ms=duration_ms
            )
            
        except Exception as e:
            duration_ms = (time.time() - start_time) * 1000
            return DiagnosticResult(
                service_name="Azure OpenAI Config",
                status=HealthStatus.UNHEALTHY,
                message=f"Azure OpenAI config check failed: {str(e)}",
                error=e,
                duration_ms=duration_ms
            )
    
    def _log_summary(self, summary: DiagnosticSummary) -> None:
        """Log diagnostic summary with appropriate log levels."""
        
        if summary.overall_status == HealthStatus.HEALTHY:
            logger.info(
                f"[DIAGNOSTICS_SUMMARY] ✅ PASSED - All {summary.healthy_count} checks successful "
                f"({summary.total_duration_ms:.1f}ms total)"
            )
        else:
            logger.error(
                f"[DIAGNOSTICS_SUMMARY] ❌ FAILED - {summary.unhealthy_count}/{len(summary.checks)} checks failed "
                f"({summary.total_duration_ms:.1f}ms total)"
            )
        
        # Log each check result
        for check in summary.checks:
            if check.status == HealthStatus.HEALTHY:
                logger.info(
                    f"[DIAGNOSTICS_CHECK] ✅ {check.service_name}: {check.message} "
                    f"({check.duration_ms:.1f}ms)"
                )
            else:
                logger.error(
                    f"[DIAGNOSTICS_CHECK] ❌ {check.service_name}: {check.message} "
                    f"({check.duration_ms:.1f}ms)"
                )
                if check.error:
                    logger.error(f"[DIAGNOSTICS_ERROR] {check.service_name} - {str(check.error)}")
                if check.details:
                    logger.error(f"[DIAGNOSTICS_DETAILS] {check.service_name} - {check.details}")
    
    async def close(self) -> None:
        """Clean up diagnostic service resources."""
        try:
            if self._auth_provider:
                await self._auth_provider.close()
            
            if self._storage_service:
                await self._storage_service.close()
            
            if self._http_client:
                await self._http_client.close()
                
        except Exception as e:
            logger.warning(f"[DIAGNOSTICS_CHECK] Error during cleanup: {e}")


# Global diagnostics instance
_diagnostics_service: Optional[DiagnosticsService] = None


def get_diagnostics_service() -> DiagnosticsService:
    """Get or create the global diagnostics service instance."""
    global _diagnostics_service
    if _diagnostics_service is None:
        _diagnostics_service = DiagnosticsService()
    return _diagnostics_service