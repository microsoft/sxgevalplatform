"""
Simple HTTP client for calling existing evaluation platform APIs.
"""

import asyncio
import aiohttp
import logging
import time
from typing import Optional, Dict, Any, List
from ..config.settings import app_settings
from .auth_token_provider import AuthTokenProvider

logger = logging.getLogger(__name__)


class EvaluationApiClient:
    """Simple client for calling existing evaluation platform APIs with connection pooling."""
    
    def __init__(self):
        """Initialize the API client using app settings."""
        self.base_url = app_settings.api_endpoints.base_url.rstrip('/')
        
        # Optimized timeout configuration
        self.timeout = aiohttp.ClientTimeout(
            total=app_settings.evaluation.timeout_seconds,
            connect=30.0,  # 30 second connection timeout
            sock_read=60.0  # 60 second read timeout
        )
        
        # Connection pool settings (will be created lazily)
        self._connector: Optional[aiohttp.TCPConnector] = None
        
        # Session management
        self._session: Optional[aiohttp.ClientSession] = None
        self._session_created_at: Optional[float] = None
        self._session_lock: Optional[asyncio.Lock] = None
        
        # Authentication token provider
        auth_config = app_settings.api_authentication
        self._auth_provider: Optional[AuthTokenProvider] = None
        if auth_config.use_managed_identity or auth_config.client_id:
            self._auth_provider = AuthTokenProvider(
                client_id=auth_config.client_id,
                tenant_id=auth_config.tenant_id,
                scope=auth_config.scope,
                use_managed_identity=auth_config.use_managed_identity,
                enable_caching=auth_config.enable_token_caching,
                refresh_buffer_seconds=auth_config.token_refresh_buffer_seconds
            )
            logger.info("✅ Authentication provider initialized for Eval API")
        else:
            logger.warning("⚠️  No authentication configured for Eval API - requests will be unauthenticated")
        
    async def _get_auth_headers(self) -> Dict[str, str]:
        """
        Get authentication headers for API requests.
        
        Returns:
            Dictionary with authentication headers (Authorization: Bearer <token>)
        """
        if self._auth_provider is None:
            return {}
        
        try:
            auth_headers = await self._auth_provider.get_auth_header()
            logger.debug("✅ Retrieved authentication header")
            return auth_headers
        except Exception as e:
            logger.error(f"❌ Failed to get authentication token: {str(e)}")
            logger.error(f"Auth error type: {type(e).__name__}")
            raise
    
    def _get_connector(self) -> aiohttp.TCPConnector:
        """Get or create HTTP connector with connection pooling."""
        if self._connector is None:
            self._connector = aiohttp.TCPConnector(
                limit=20,  # Total connection pool size
                limit_per_host=10,  # Max connections per host
                ttl_dns_cache=300,  # DNS cache TTL (5 minutes)
                use_dns_cache=True,
                keepalive_timeout=30,  # Keep alive timeout
                enable_cleanup_closed=True
            )
        return self._connector
    
    def _get_session_lock(self) -> asyncio.Lock:
        """Get or create session lock."""
        if self._session_lock is None:
            self._session_lock = asyncio.Lock()
        return self._session_lock
    
    async def _get_session(self) -> aiohttp.ClientSession:
        """Get or create HTTP session with connection pooling. Must be called with lock held."""
        current_time = time.time()
        
        # Create new session if none exists or if session is too old (1 hour)
        if (self._session is None or 
            self._session.closed or 
            (self._session_created_at and current_time - self._session_created_at > 3600)):
            
            if self._session and not self._session.closed:
                await self._session.close()
            
            connector = self._get_connector()
            self._session = aiohttp.ClientSession(
                timeout=self.timeout,
                connector=connector
            )
            self._session_created_at = current_time
            logger.debug("Created new HTTP session with connection pooling")
        
        return self._session
    
    async def _make_request(self, method: str, url: str, **kwargs) -> tuple:
        """Make HTTP request with session lock held for the entire operation.
        
        Lock is held ONLY during the actual HTTP request, not during retry delays.
        This prevents TOCTOU race conditions while allowing efficient retries.
        
        Args:
            method: HTTP method (get, post, put, etc.)
            url: Request URL
            **kwargs: Additional arguments for the request (headers, json, etc.)
            
        Returns:
            Tuple of (response_text, status_code, headers)
            
        Raises:
            aiohttp.ClientError: On HTTP errors
            asyncio.TimeoutError: On timeout
        """
        async with self._get_session_lock():
            session = await self._get_session()
            
            # Session is guaranteed to be open and valid from _get_session()
            # No need to check - if it's closed here while holding the lock, something is critically wrong
            
            # Make the request while holding the lock
            async with getattr(session, method)(url, **kwargs) as response:
                response_text = await response.text()
                return response_text, response.status, dict(response.headers)
    
    async def close(self):
        """Close the HTTP session and cleanup resources."""
        # Close authentication provider
        if self._auth_provider is not None:
            try:
                await self._auth_provider.close()
                logger.debug("Closed authentication provider")
            except Exception as e:
                logger.warning(f"Error closing authentication provider: {e}")
        
        if self._session_lock is not None:
            async with self._session_lock:
                if self._session and not self._session.closed:
                    await self._session.close()
                    self._session = None
                    self._session_created_at = None
                
                if self._connector and not self._connector.closed:
                    await self._connector.close()
                    self._connector = None
                    
                logger.debug("Closed HTTP session and cleaned up resources")
        else:
            # No lock exists, just clean up directly
            if self._session and not self._session.closed:
                await self._session.close()
                self._session = None
                self._session_created_at = None
            
            if self._connector and not self._connector.closed:
                await self._connector.close()
                self._connector = None
        
    async def fetch_enriched_dataset(self, eval_run_id: str) -> Dict[str, Any]:
        """
        Fetch enriched dataset from existing API.
        Raises exceptions on failures to enable retry logic.
        
        Args:
            eval_run_id: The evaluation run ID
            
        Returns:
            Enriched dataset response data (validated to have non-empty dataset)
            
        Raises:
            ValueError: If response is empty or dataset field is missing/empty
            aiohttp.ClientError: On HTTP errors
            asyncio.TimeoutError: On timeout
        """
        endpoint = app_settings.api_endpoints.enriched_dataset_endpoint.replace('{EvalRunId}', eval_run_id)
        url = f"{self.base_url}{endpoint}"
        
        # Start telemetry timing
        start_time = time.time()
        
        # Enhanced telemetry - log request details
        logger.info(f"[API_REQUEST] Starting API call: fetch_enriched_dataset")
        logger.info(f"[API_METHOD] GET")
        logger.info(f"[API_URL] {url}")
        logger.info(f"[API_ENDPOINT] {endpoint}")
        logger.info(f"[API_PARAMETER] eval_run_id={eval_run_id}")
        logger.info(f"[API_BASE_URL] {self.base_url}")
        
        headers = {
            'Content-Type': 'application/json'
        }
        
        # Add authentication headers
        try:
            auth_headers = await self._get_auth_headers()
            headers.update(auth_headers)
            logger.info(f"[AUTH] Successfully added authentication header to request")
        except Exception as auth_error:
            logger.error(f"[AUTH_ERROR] Failed to get authentication: {str(auth_error)}")
            raise
        
        logger.info(f"[API_HEADERS] {list(headers.keys())}")  # Log header keys only (don't log token)
        logger.info(f"[API_PAYLOAD] None (GET request)")
        
        try:
            # Make request with lock held for entire HTTP operation
            response_text, status, response_headers = await self._make_request('get', url, headers=headers)
            response_time = time.time() - start_time
            
            # Enhanced telemetry - log response details
            logger.info(f"[API_RESPONSE_STATUS] {status}")
            logger.info(f"[API_RESPONSE_TIME] {response_time:.3f}s")
            logger.info(f"[API_RESPONSE_HEADERS] {response_headers}")
            logger.info(f"[API_RESPONSE_BODY_LENGTH] {len(response_text)} characters")
            logger.info(f"[API_RESPONSE_BODY] {response_text[:1000]}{'...' if len(response_text) > 1000 else ''}")
            
            if status != 200:
                error_msg = f"HTTP {status} error fetching dataset"
                logger.error(f"[API_ERROR] {error_msg}")
                logger.error(f"[API_ERROR_URL] {url}")
                logger.error(f"[API_ERROR_STATUS] {status}")
                logger.error(f"[API_ERROR_RESPONSE] {response_text}")
                logger.error(f"[API_ERROR_HEADERS] {response_headers}")
                raise ValueError(f"{error_msg}: {response_text}")
            
            # Parse JSON response
            import json
            try:
                data = json.loads(response_text)
            except Exception as parse_error:
                logger.error(f"[API_ERROR] JSON parsing failed: {str(parse_error)}")
                logger.error(f"[API_ERROR_URL] {url}")
                logger.error(f"[API_ERROR_RESPONSE] {response_text[:500]}")
                raise ValueError(f"Failed to parse JSON response: {str(parse_error)}")
            
            # Validate response has dataset field and is not empty
            enriched_data = data.get('enrichedDataset') or data.get('EnrichedDataset')
            
            if enriched_data is None:
                logger.error(f"[API_ERROR] Dataset field missing from response")
                logger.error(f"[API_ERROR_URL] {url}")
                logger.error(f"[API_ERROR_RESPONSE_KEYS] {list(data.keys())}")
                logger.error(f"[API_ERROR_RESPONSE_BODY] {response_text[:500]}")
                raise ValueError(f"Response missing 'enrichedDataset' or 'EnrichedDataset' field. Keys: {list(data.keys())}")
            
            if not isinstance(enriched_data, list):
                logger.error(f"[API_ERROR] Dataset field is not a list")
                logger.error(f"[API_ERROR_URL] {url}")
                logger.error(f"[API_ERROR_DATASET_TYPE] {type(enriched_data).__name__}")
                logger.error(f"[API_ERROR_DATASET_VALUE] {str(enriched_data)[:200]}")
                raise ValueError(f"Dataset field is not a list: {type(enriched_data).__name__}")
            
            if len(enriched_data) == 0:
                logger.error(f"[API_ERROR] Dataset is empty (0 items)")
                logger.error(f"[API_ERROR_URL] {url}")
                logger.error(f"[API_ERROR_RESPONSE_KEYS] {list(data.keys())}")
                logger.error(f"[API_ERROR_RESPONSE_BODY] {response_text[:500]}")
                raise ValueError("Dataset API returned empty dataset (0 items)")
            
            dataset_count = len(enriched_data)
            logger.info(f"[API_SUCCESS] Successfully fetched dataset with {dataset_count} items")
            logger.info(f"[API_SUCCESS_DATASET_COUNT] {dataset_count}")
            logger.info(f"[API_SUCCESS_RESPONSE_KEYS] {list(data.keys())}")
            logger.info(f"[API_SUCCESS_DURATION] {response_time:.3f}s")
            
            return data
                    
        except asyncio.TimeoutError:
            response_time = time.time() - start_time
            logger.error(f"[API_TIMEOUT] Request timed out after {response_time:.3f}s")
            logger.error(f"[API_TIMEOUT_URL] {url}")
            logger.error(f"[API_TIMEOUT_DURATION] {response_time:.3f}s")
            logger.error(f"[API_TIMEOUT_PARAMETER] eval_run_id={eval_run_id}")
            raise asyncio.TimeoutError(f"Timeout fetching dataset after {response_time:.3f}s: {url}")
        except ValueError:
            # Re-raise ValueError (validation errors)
            raise
        except Exception as e:
            response_time = time.time() - start_time
            logger.error(f"[API_EXCEPTION] Unexpected error: {str(e)}")
            logger.error(f"[API_EXCEPTION_URL] {url}")
            logger.error(f"[API_EXCEPTION_TYPE] {type(e).__name__}")
            logger.error(f"[API_EXCEPTION_DURATION] {response_time:.3f}s")
            logger.error(f"[API_EXCEPTION_PARAMETER] eval_run_id={eval_run_id}")
            import traceback
            logger.error(f"[API_EXCEPTION_TRACE] {traceback.format_exc()}")
            raise
    
    async def fetch_metrics_configuration(self, metrics_configuration_id: str) -> Any:
        """
        Fetch metrics configuration from existing API.
        Raises exceptions on failures to enable retry logic.
        
        Args:
            metrics_configuration_id: The metrics configuration ID

        Returns:
            Metrics configuration data (validated to be non-empty)
            
        Raises:
            ValueError: If response is empty or metrics field is missing/empty
            aiohttp.ClientError: On HTTP errors
            asyncio.TimeoutError: On timeout
        """
        endpoint = app_settings.api_endpoints.metrics_configuration_endpoint.replace('{MetricsConfigurationId}', metrics_configuration_id)
        url = f"{self.base_url}{endpoint}"
        
        # Start telemetry timing
        start_time = time.time()
        
        # Enhanced telemetry - log request details
        logger.info(f"[API_REQUEST] Starting API call: fetch_metrics_configuration")
        logger.info(f"[API_METHOD] GET")
        logger.info(f"[API_URL] {url}")
        logger.info(f"[API_ENDPOINT] {endpoint}")
        logger.info(f"[API_PARAMETER] metrics_configuration_id={metrics_configuration_id}")
        logger.info(f"[API_BASE_URL] {self.base_url}")
        
        headers = {
            'Content-Type': 'application/json'
        }
        
        # Add authentication headers
        try:
            auth_headers = await self._get_auth_headers()
            headers.update(auth_headers)
            logger.info(f"[AUTH] Successfully added authentication header to request")
        except Exception as auth_error:
            logger.error(f"[AUTH_ERROR] Failed to get authentication: {str(auth_error)}")
            raise
        
        logger.info(f"[API_HEADERS] {list(headers.keys())}")  # Log header keys only (don't log token)
        logger.info(f"[API_PAYLOAD] None (GET request)")
        
        try:
            # Make request with lock held for entire HTTP operation
            response_text, status, response_headers = await self._make_request('get', url, headers=headers)
            response_time = time.time() - start_time
            
            # Enhanced telemetry - log response details
            logger.info(f"[API_RESPONSE_STATUS] {status}")
            logger.info(f"[API_RESPONSE_TIME] {response_time:.3f}s")
            logger.info(f"[API_RESPONSE_HEADERS] {response_headers}")
            logger.info(f"[API_RESPONSE_BODY_LENGTH] {len(response_text)} characters")
            logger.info(f"[API_RESPONSE_BODY] {response_text[:1000]}{'...' if len(response_text) > 1000 else ''}")
            
            if status != 200:
                error_msg = f"HTTP {status} error fetching metrics configuration"
                logger.error(f"[API_ERROR] {error_msg}")
                logger.error(f"[API_ERROR_URL] {url}")
                logger.error(f"[API_ERROR_STATUS] {status}")
                logger.error(f"[API_ERROR_RESPONSE] {response_text}")
                logger.error(f"[API_ERROR_HEADERS] {response_headers}")
                raise ValueError(f"{error_msg}: {response_text}")
            
            # Parse JSON response
            import json
            try:
                data = json.loads(response_text)
            except Exception as parse_error:
                logger.error(f"[API_ERROR] JSON parsing failed: {str(parse_error)}")
                logger.error(f"[API_ERROR_URL] {url}")
                logger.error(f"[API_ERROR_RESPONSE] {response_text[:500]}")
                raise ValueError(f"Failed to parse JSON response: {str(parse_error)}")
            
            # Validate response content
            if isinstance(data, list):
                # New format: direct array of metrics
                if len(data) == 0:
                    logger.error(f"[API_ERROR] Metrics configuration is empty (0 metrics)")
                    logger.error(f"[API_ERROR_URL] {url}")
                    logger.error(f"[API_ERROR_RESPONSE] {response_text[:500]}")
                    raise ValueError("Metrics configuration API returned empty array (0 metrics)")
                
                metrics_count = len(data)
                logger.info(f"[API_SUCCESS] Successfully fetched {metrics_count} metrics (array format)")
                logger.info(f"[API_SUCCESS_METRICS_COUNT] {metrics_count}")
                logger.info(f"[API_SUCCESS_FORMAT] array")
                logger.info(f"[API_SUCCESS_SAMPLE] {[type(item).__name__ for item in data[:3]]}")
            else:
                # Old format: nested object
                metrics_config = data.get('metricsConfiguration') or data.get('MetricsConfiguration')
                
                if metrics_config is None:
                    logger.error(f"[API_ERROR] Metrics configuration field missing from response")
                    logger.error(f"[API_ERROR_URL] {url}")
                    logger.error(f"[API_ERROR_RESPONSE_KEYS] {list(data.keys())}")
                    logger.error(f"[API_ERROR_RESPONSE] {response_text[:500]}")
                    raise ValueError(f"Response missing 'metricsConfiguration' or 'MetricsConfiguration' field. Keys: {list(data.keys())}")
                
                if not isinstance(metrics_config, list):
                    logger.error(f"[API_ERROR] Metrics configuration field is not a list")
                    logger.error(f"[API_ERROR_URL] {url}")
                    logger.error(f"[API_ERROR_CONFIG_TYPE] {type(metrics_config).__name__}")
                    raise ValueError(f"Metrics configuration field is not a list: {type(metrics_config).__name__}")
                
                if len(metrics_config) == 0:
                    logger.error(f"[API_ERROR] Metrics configuration is empty (0 metrics)")
                    logger.error(f"[API_ERROR_URL] {url}")
                    logger.error(f"[API_ERROR_RESPONSE_KEYS] {list(data.keys())}")
                    logger.error(f"[API_ERROR_RESPONSE] {response_text[:500]}")
                    raise ValueError("Metrics configuration API returned empty metrics array (0 metrics)")
                
                metrics_count = len(metrics_config)
                logger.info(f"[API_SUCCESS] Successfully fetched {metrics_count} metrics (nested format)")
                logger.info(f"[API_SUCCESS_METRICS_COUNT] {metrics_count}")
                logger.info(f"[API_SUCCESS_FORMAT] nested")
                logger.info(f"[API_SUCCESS_RESPONSE_KEYS] {list(data.keys())}")
            
            logger.info(f"[API_SUCCESS_DURATION] {response_time:.3f}s")
            return data
                        
        except asyncio.TimeoutError as timeout_error:
            response_time = time.time() - start_time
            logger.error(f"[API_TIMEOUT] Request timed out after {response_time:.3f}s")
            logger.error(f"[API_TIMEOUT_URL] {url}")
            logger.error(f"[API_TIMEOUT_DURATION] {response_time:.3f}s")
            logger.error(f"[API_TIMEOUT_PARAMETER] metrics_configuration_id={metrics_configuration_id}")
            raise asyncio.TimeoutError(f"Timeout fetching metrics configuration after {response_time:.3f}s: {url}")
        except ValueError:
            # Re-raise ValueError (validation errors)
            raise
        except Exception as e:
            response_time = time.time() - start_time
            logger.error(f"[API_EXCEPTION] Unexpected error: {str(e)}")
            logger.error(f"[API_EXCEPTION_URL] {url}")
            logger.error(f"[API_EXCEPTION_TYPE] {type(e).__name__}")
            logger.error(f"[API_EXCEPTION_DURATION] {response_time:.3f}s")
            logger.error(f"[API_EXCEPTION_PARAMETER] metrics_configuration_id={metrics_configuration_id}")
            import traceback
            logger.error(f"[API_EXCEPTION_TRACE] {traceback.format_exc()}")
            raise
    
    async def update_evaluation_status(self, eval_run_id: str, status: str) -> bool:
        """
        Update evaluation run status.
        
        Args:
            eval_run_id: The evaluation run ID
            status: The status to update (e.g., "EvalRunCompleted")
            
        Returns:
            True if update was successful, False otherwise
        """
        endpoint = app_settings.api_endpoints.update_status.replace('{evalRunId}', eval_run_id)
        url = f"{self.base_url}{endpoint}"

        payload = {"status": status}        # Start telemetry timing
        start_time = time.time()
        
        logger.info(f"[WEB] Starting API call: update_evaluation_status")
        logger.info(f"[LOCATION] Endpoint: {endpoint}")
        logger.info(f"[LINK] Full URL: {url}")
        logger.info(f"[CLIPBOARD] Eval Run ID: {eval_run_id}")
        logger.info(f"  Status to update: {status}")
        logger.debug(f"[OUTBOX] Request payload: {payload}")
        
        try:
            headers = {
                'Content-Type': 'application/json'
            }
            
            # Add authentication headers
            try:
                auth_headers = await self._get_auth_headers()
                headers.update(auth_headers)
                logger.info(f"[AUTH] Successfully added authentication header to request")
            except Exception as auth_error:
                logger.error(f"[AUTH_ERROR] Failed to get authentication: {str(auth_error)}")
                raise
            
            logger.debug(f"[OUTBOX] Request headers: {list(headers.keys())}")  # Log header keys only (don't log token)
            
            # Make request with lock held for entire HTTP operation
            response_text, status, response_headers = await self._make_request('put', url, headers=headers, json=payload)
            response_time = time.time() - start_time
            
            # Log response details
            logger.info(f"[INBOX] Response status: {status}")
            logger.info(f"[TIMER] Response time: {response_time:.3f}s")
            logger.debug(f"[PAGE] Response headers: {response_headers}")
            logger.debug(f"[MEMO] Response body: {response_text}")
            
            if status in [200, 204]:
                logger.info(f"[SUCCESS] Successfully updated status for eval run: {eval_run_id}")
                return True
            elif status == 400:
                # Check if this is the "already in terminal state" error
                if "already in a terminal state" in response_text:
                    logger.warning(f"[WARNING] Evaluation run {eval_run_id} is already in terminal state - skipping status update")
                    logger.info(f"  Terminal state response: {response_text}")
                    return True  # Treat as success since the evaluation is already completed
                else:
                    logger.error(f"[ERROR] Bad request updating status - Status: {status}")
                    logger.error(f"[LINK] Failed URL: {url}")
                    logger.error(f"[OUTBOX] Request payload: {payload}")
                    logger.error(f"[PAGE] Error response: {response_text}")
                    logger.error(f"[TIMER] Failed after: {response_time:.3f}s")
                    return False
            else:
                logger.error(f"[ERROR] API call failed - Status: {status}")
                logger.error(f"[LINK] Failed URL: {url}")
                logger.error(f"[OUTBOX] Request payload: {payload}")
                logger.error(f"[PAGE] Error response: {response_text}")
                logger.error(f"[TIMER] Failed after: {response_time:.3f}s")
                return False
                        
        except asyncio.TimeoutError:
            response_time = time.time() - start_time
            logger.error(f"[TIMEOUT] Timeout updating status - took {response_time:.3f}s")
            logger.error(f"[LINK] Timeout URL: {url}")
            logger.error(f"[CLIPBOARD] Eval Run ID: {eval_run_id}")
            logger.error(f"  Target Status: {status}")
            return False
        except Exception as e:
            response_time = time.time() - start_time
            logger.error(f"[CRASH] Unexpected error updating status: {str(e)}")
            logger.error(f"[LINK] Error URL: {url}")
            logger.error(f"[CLIPBOARD] Eval Run ID: {eval_run_id}")
            logger.error(f"  Target Status: {status}")
            logger.error(f"[TIMER] Failed after: {response_time:.3f}s")
            logger.error(f"[BUG] Error type: {type(e).__name__}")
            return False

    async def post_evaluation_results(self, eval_run_id: str, results_data: Dict[str, Any]) -> bool:
        """
        Post evaluation results to API.
        
        Args:
            eval_run_id: The evaluation run ID
            results_data: The evaluation results data to post
            
        Returns:
            True if post was successful, False otherwise
        """
        endpoint = app_settings.api_endpoints.post_results_endpoint.replace('{evalRunId}', eval_run_id)
        url = f"{self.base_url}{endpoint}"
        
        # Start telemetry timing
        start_time = time.time()
        
        logger.info(f"[WEB] Starting API call: post_evaluation_results")
        logger.info(f"[LOCATION] Endpoint: {endpoint}")
        logger.info(f"[LINK] Full URL: {url}")
        logger.info(f"[CLIPBOARD] Eval Run ID: {eval_run_id}")
        logger.info(f"  Results data keys: {list(results_data.keys()) if results_data else 'None'}")
        
        # Log data structure details
        if results_data:
            for key, value in results_data.items():
                if isinstance(value, str):
                    logger.info(f"[PAGE] {key}: JSON string length = {len(value)} characters")
                    # Try to parse and log structure
                    try:
                        import json
                        parsed = json.loads(value)
                        if isinstance(parsed, list):
                            logger.info(f"  {key}: Contains {len(parsed)} items")
                        elif isinstance(parsed, dict):
                            logger.info(f"  {key}: Dict with keys = {list(parsed.keys())}")
                    except:
                        logger.debug(f"[PAGE] {key}: Not valid JSON, raw content length = {len(value)}")
                elif isinstance(value, (dict, list)):
                    if isinstance(value, list):
                        logger.info(f"  {key}: Array with {len(value)} items")
                    else:
                        logger.info(f"  {key}: Dict with keys = {list(value.keys())}")
                else:
                    logger.info(f"  {key}: {type(value).__name__} = {value}")
        
        # CAPTURE EXACT REQUEST BODY FOR SWAGGER TESTING
        import json
        try:
            exact_json_payload = json.dumps(results_data, indent=2)
            logger.info(f"[MAGNIFY] EXACT REQUEST BODY FOR SWAGGER TESTING:")
            logger.info(f"{'='*80}")
            logger.info(exact_json_payload)
            logger.info(f"{'='*80}")
                
        except Exception as json_error:
            logger.error(f"[ERROR] Could not serialize payload to JSON: {json_error}")
            logger.debug(f"[OUTBOX] Raw payload: {results_data}")
        
        logger.debug(f"[OUTBOX] Request payload sample (first 500 chars): {str(results_data)[:500]}...")
        
        try:
            headers = {
                'Content-Type': 'application/json'
            }
            
            # Add authentication headers
            try:
                auth_headers = await self._get_auth_headers()
                headers.update(auth_headers)
                logger.info(f"[AUTH] Successfully added authentication header to request")
            except Exception as auth_error:
                logger.error(f"[AUTH_ERROR] Failed to get authentication: {str(auth_error)}")
                raise
            
            logger.debug(f"[OUTBOX] Request headers: {list(headers.keys())}")  # Log header keys only (don't log token)
            
            # Make request with lock held for entire HTTP operation
            response_text, status, response_headers = await self._make_request('post', url, headers=headers, json=results_data)
            response_time = time.time() - start_time
            
            # Log response details
            logger.info(f"[INBOX] Response status: {status}")
            logger.info(f"[TIMER] Response time: {response_time:.3f}s")
            logger.debug(f"[PAGE] Response headers: {response_headers}")
            logger.debug(f"[MEMO] Response body length: {len(response_text)} characters")
            
            if status in [200, 201, 204]:
                logger.info(f"[SUCCESS] Successfully posted evaluation results for eval run: {eval_run_id}")
                logger.info(f"[CELEBRATION] API Response: {response_text[:200]}..." if len(response_text) > 200 else response_text)
                return True
            else:
                logger.error(f"[ERROR] API call failed - Status: {status}")
                logger.error(f"[LINK] Failed URL: {url}")
                logger.error(f"[OUTBOX] Request payload keys: {list(results_data.keys()) if results_data else 'None'}")
                logger.error(f"[PAGE] Error response: {response_text}")
                logger.error(f"[TIMER] Failed after: {response_time:.3f}s")
                
                # Additional debugging for common error scenarios
                if status == 400:
                    logger.error(f"[PROHIBITED] Bad Request - Check payload format and required fields")
                elif status == 401:
                    logger.error(f"[SECURE] Unauthorized - Check authentication credentials")
                elif status == 403:
                    logger.error(f"[BLOCKED] Forbidden - Check authorization permissions")
                elif status == 404:
                    logger.error(f"[MAGNIFY] Not Found - Check if endpoint URL and eval run ID are correct")
                    logger.error(f"[CLIPBOARD] Eval Run ID used: {eval_run_id}")
                    logger.error(f"[LINK] Full URL attempted: {url}")
                elif status == 500:
                    logger.error(f"[CRASH] Internal Server Error - API server issue")
                
                return False
                        
        except asyncio.TimeoutError:
            response_time = time.time() - start_time
            logger.error(f"[TIMEOUT] Timeout posting evaluation results - took {response_time:.3f}s")
            logger.error(f"[LINK] Timeout URL: {url}")
            logger.error(f"[CLIPBOARD] Eval Run ID: {eval_run_id}")
            return False
        except Exception as e:
            response_time = time.time() - start_time
            logger.error(f"[CRASH] Unexpected error posting evaluation results: {str(e)}")
            logger.error(f"[LINK] Error URL: {url}")
            logger.error(f"[CLIPBOARD] Eval Run ID: {eval_run_id}")
            logger.error(f"[TIMER] Failed after: {response_time:.3f}s")
            logger.error(f"[BUG] Error type: {type(e).__name__}")
            return False

    async def health_check(self) -> bool:
        """
        Check if the API is accessible.
        
        Returns:
            True if API is accessible, False otherwise
        """
        health_url = f"{self.base_url}/health"
        
        # Start telemetry timing
        start_time = time.time()
        
        logger.info(f"[WEB] Starting API health check")
        logger.info(f"[LINK] Health check URL: {health_url}")
        
        try:
            # Use a separate session with shorter timeout for health checks
            timeout = aiohttp.ClientTimeout(total=10)
            async with aiohttp.ClientSession(timeout=timeout) as session:
                async with session.get(health_url) as response:
                    response_time = time.time() - start_time
                    
                    logger.info(f"[INBOX] Health check response status: {response.status}")
                    logger.info(f"[TIMER] Health check response time: {response_time:.3f}s")
                    
                    is_healthy = response.status < 400
                    
                    if is_healthy:
                        logger.info(f"[SUCCESS] API health check passed")
                    else:
                        response_text = await response.text()
                        logger.warning(f"[WARNING] API health check failed - Status: {response.status}")
                        logger.warning(f"[PAGE] Health check response: {response_text[:200]}")
                    
                    return is_healthy
                    
        except asyncio.TimeoutError:
            response_time = time.time() - start_time
            logger.error(f"[TIMEOUT] API health check timeout after {response_time:.3f}s")
            logger.error(f"[LINK] Timeout URL: {health_url}")
            return False
        except Exception as e:
            response_time = time.time() - start_time
            logger.error(f"[CRASH] API health check failed: {str(e)}")
            logger.error(f"[LINK] Error URL: {health_url}")
            logger.error(f"[TIMER] Failed after: {response_time:.3f}s")
            logger.error(f"[BUG] Error type: {type(e).__name__}")
            return False


# Global instance for easy access (lazy-loaded)
_api_client_instance: Optional[EvaluationApiClient] = None

def get_api_client() -> EvaluationApiClient:
    """Get or create the global API client instance."""
    global _api_client_instance
    if _api_client_instance is None:
        _api_client_instance = EvaluationApiClient()
    return _api_client_instance

# Create a lazy property for backward compatibility
class _APIClientProxy:
    def __getattr__(self, name):
        return getattr(get_api_client(), name)

api_client = _APIClientProxy()