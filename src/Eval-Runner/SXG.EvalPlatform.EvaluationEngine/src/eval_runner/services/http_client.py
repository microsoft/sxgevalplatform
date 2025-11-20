"""
Simple HTTP client for calling existing evaluation platform APIs.
"""

import asyncio
import aiohttp
import logging
import time
from typing import Optional, Dict, Any, List
from ..config.settings import app_settings

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
        """Get or create HTTP session with connection pooling."""
        session_lock = self._get_session_lock()
        async with session_lock:
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
    
    async def close(self):
        """Close the HTTP session and cleanup resources."""
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
        
    async def fetch_enriched_dataset(self, eval_run_id: str) -> Optional[Dict[str, Any]]:
        """
        Fetch enriched dataset from existing API.
        
        Args:
            eval_run_id: The evaluation run ID
            
        Returns:
            Enriched dataset response data or None if failed
        """
        endpoint = app_settings.api_endpoints.enriched_dataset_endpoint.replace('{EvalRunId}', eval_run_id)
        url = f"{self.base_url}/{endpoint.lstrip('/')}"
        
        # Start telemetry timing
        start_time = time.time()
        
        logger.info(f"Starting API call: fetch_enriched_dataset")
        logger.info(f"[LOCATION] Endpoint: {endpoint}")
        logger.info(f"[LINK] Full URL: {url}")
        logger.info(f"[CLIPBOARD] Eval Run ID: {eval_run_id}")
        
        try:
            session = await self._get_session()
            headers = {
                'Content-Type': 'application/json'
            }
            
            logger.debug(f"[OUTBOX] Request headers: {headers}")
            
            async with session.get(url, headers=headers) as response:
                response_time = time.time() - start_time
                response_text = await response.text()
                
                # Log response details
                logger.info(f"[INBOX] Response status: {response.status}")
                logger.info(f"[TIMER] Response time: {response_time:.3f}s")
                logger.debug(f"[PAGE] Response headers: {dict(response.headers)}")
                logger.debug(f"[MEMO] Response body length: {len(response_text)} characters")
                
                if response.status == 200:
                    try:
                        data = await response.json()
                        # Handle both camelCase and PascalCase field names
                        enriched_data = data.get('enrichedDataset') or data.get('EnrichedDataset', [])
                        dataset_count = len(enriched_data)
                        
                        logger.info(f"[SUCCESS] Successfully fetched dataset with {dataset_count} items")
                        logger.info(f"  Dataset structure: {list(data.keys()) if isinstance(data, dict) else 'Non-dict response'}")
                        
                        return data
                    except Exception as parse_error:
                        logger.error(f"[ERROR] JSON parsing failed: {str(parse_error)}")
                        logger.error(f"[PAGE] Raw response (first 500 chars): {response_text[:500]}")
                        return None
                else:
                    logger.error(f"[ERROR] API call failed - Status: {response.status}")
                    logger.error(f"[LINK] Failed URL: {url}")
                    logger.error(f"[PAGE] Error response: {response_text}")
                    logger.error(f"[TIMER] Failed after: {response_time:.3f}s")
                    return None
                        
        except asyncio.TimeoutError:
            response_time = time.time() - start_time
            logger.error(f"[TIMEOUT] Timeout fetching enriched dataset - took {response_time:.3f}s")
            logger.error(f"[LINK] Timeout URL: {url}")
            logger.error(f"[CLIPBOARD] Eval Run ID: {eval_run_id}")
            return None
        except Exception as e:
            response_time = time.time() - start_time
            logger.error(f"[CRASH] Unexpected error fetching enriched dataset: {str(e)}")
            logger.error(f"[LINK] Error URL: {url}")
            logger.error(f"[CLIPBOARD] Eval Run ID: {eval_run_id}")
            logger.error(f"[TIMER] Failed after: {response_time:.3f}s")
            logger.error(f"[BUG] Error type: {type(e).__name__}")
            return None
    
    async def fetch_metrics_configuration(self, metrics_configuration_id: str) -> Optional[Any]:
        """
        Fetch metrics configuration from existing API.
        
        Args:
            metrics_configuration_id: The metrics configuration ID

        Returns:
            Metrics configuration data or None if failed
        """
        endpoint = app_settings.api_endpoints.metrics_configuration_endpoint.replace('{MetricsConfigurationId}', metrics_configuration_id)
        url = f"{self.base_url}/{endpoint.lstrip('/')}"
        
        # Start telemetry timing
        start_time = time.time()
        
        logger.info(f"[WEB] Starting API call: fetch_metrics_configuration")
        logger.info(f"[LOCATION] Endpoint: {endpoint}")
        logger.info(f"[LINK] Full URL: {url}")
        logger.info(f"[CLIPBOARD] Metrics Config ID: {metrics_configuration_id}")
        
        try:
            session = await self._get_session()
            headers = {
                'Content-Type': 'application/json'
            }
            
            logger.debug(f"[OUTBOX] Request headers: {headers}")
            
            async with session.get(url, headers=headers) as response:
                response_time = time.time() - start_time
                response_text = await response.text()
                
                # Log response details
                logger.info(f"[INBOX] Response status: {response.status}")
                logger.info(f"[TIMER] Response time: {response_time:.3f}s")
                logger.debug(f"[PAGE] Response headers: {dict(response.headers)}")
                logger.debug(f"[MEMO] Response body length: {len(response_text)} characters")
                
                if response.status == 200:
                    try:
                        data = await response.json()
                        
                        # Handle new array format or old nested format
                        if isinstance(data, list):
                            # New format: direct array of metrics
                            metrics_count = len(data)
                            logger.info(f"[SUCCESS] Successfully fetched metrics configuration with {metrics_count} metrics (array format)")
                            logger.debug(f"  Metrics array structure: {[type(item).__name__ for item in data[:3]]}")  # Show first 3 types
                        else:
                            # Old format: nested object
                            metrics_config = data.get('metricsConfiguration') or data.get('MetricsConfiguration', [])
                            metrics_count = len(metrics_config)
                            logger.info(f"[SUCCESS] Successfully fetched metrics configuration with {metrics_count} metrics (nested format)")
                            logger.debug(f"  Nested structure keys: {list(data.keys()) if isinstance(data, dict) else 'Non-dict response'}")
                        
                        return data
                    except Exception as parse_error:
                        logger.error(f"[ERROR] JSON parsing failed: {str(parse_error)}")
                        logger.error(f"[PAGE] Raw response (first 500 chars): {response_text[:500]}")
                        return None
                else:
                    logger.error(f"[ERROR] API call failed - Status: {response.status}")
                    logger.error(f"[LINK] Failed URL: {url}")
                    logger.error(f"[PAGE] Error response: {response_text}")
                    logger.error(f"[TIMER] Failed after: {response_time:.3f}s")
                    return None
                        
        except asyncio.TimeoutError:
            response_time = time.time() - start_time
            logger.error(f"[TIMEOUT] Timeout fetching metrics configuration - took {response_time:.3f}s")
            logger.error(f"[LINK] Timeout URL: {url}")
            logger.error(f"[CLIPBOARD] Metrics Config ID: {metrics_configuration_id}")
            return None
        except Exception as e:
            response_time = time.time() - start_time
            logger.error(f"[CRASH] Unexpected error fetching metrics configuration: {str(e)}")
            logger.error(f"[LINK] Error URL: {url}")
            logger.error(f"[CLIPBOARD] Metrics Config ID: {metrics_configuration_id}")
            logger.error(f"[TIMER] Failed after: {response_time:.3f}s")
            logger.error(f"[BUG] Error type: {type(e).__name__}")
            return None
    
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
        
        payload = {"status": status}
        
        # Start telemetry timing
        start_time = time.time()
        
        logger.info(f"[WEB] Starting API call: update_evaluation_status")
        logger.info(f"[LOCATION] Endpoint: {endpoint}")
        logger.info(f"[LINK] Full URL: {url}")
        logger.info(f"[CLIPBOARD] Eval Run ID: {eval_run_id}")
        logger.info(f"  Status to update: {status}")
        logger.debug(f"[OUTBOX] Request payload: {payload}")
        
        try:
            session = await self._get_session()
            headers = {
                'Content-Type': 'application/json'
            }
            
            logger.debug(f"[OUTBOX] Request headers: {headers}")
            
            async with session.put(url, headers=headers, json=payload) as response:
                response_time = time.time() - start_time
                response_text = await response.text()
                
                # Log response details
                logger.info(f"[INBOX] Response status: {response.status}")
                logger.info(f"[TIMER] Response time: {response_time:.3f}s")
                logger.debug(f"[PAGE] Response headers: {dict(response.headers)}")
                logger.debug(f"[MEMO] Response body: {response_text}")
                
                if response.status in [200, 204]:
                    logger.info(f"[SUCCESS] Successfully updated status for eval run: {eval_run_id}")
                    return True
                elif response.status == 400:
                    # Check if this is the "already in terminal state" error
                    if "already in a terminal state" in response_text:
                        logger.warning(f"[WARNING] Evaluation run {eval_run_id} is already in terminal state - skipping status update")
                        logger.info(f"  Terminal state response: {response_text}")
                        return True  # Treat as success since the evaluation is already completed
                    else:
                        logger.error(f"[ERROR] Bad request updating status - Status: {response.status}")
                        logger.error(f"[LINK] Failed URL: {url}")
                        logger.error(f"[OUTBOX] Request payload: {payload}")
                        logger.error(f"[PAGE] Error response: {response_text}")
                        logger.error(f"[TIMER] Failed after: {response_time:.3f}s")
                        return False
                else:
                    logger.error(f"[ERROR] API call failed - Status: {response.status}")
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
            session = await self._get_session()
            headers = {
                'Content-Type': 'application/json'
            }
            
            logger.debug(f"[OUTBOX] Request headers: {headers}")
            
            async with session.post(url, headers=headers, json=results_data) as response:
                response_time = time.time() - start_time
                response_text = await response.text()
                
                # Log response details
                logger.info(f"[INBOX] Response status: {response.status}")
                logger.info(f"[TIMER] Response time: {response_time:.3f}s")
                logger.debug(f"[PAGE] Response headers: {dict(response.headers)}")
                logger.debug(f"[MEMO] Response body length: {len(response_text)} characters")
                
                if response.status in [200, 201, 204]:
                    logger.info(f"[SUCCESS] Successfully posted evaluation results for eval run: {eval_run_id}")
                    logger.info(f"[CELEBRATION] API Response: {response_text[:200]}..." if len(response_text) > 200 else response_text)
                    return True
                else:
                    logger.error(f"[ERROR] API call failed - Status: {response.status}")
                    logger.error(f"[LINK] Failed URL: {url}")
                    logger.error(f"[OUTBOX] Request payload keys: {list(results_data.keys()) if results_data else 'None'}")
                    logger.error(f"[PAGE] Error response: {response_text}")
                    logger.error(f"[TIMER] Failed after: {response_time:.3f}s")
                    
                    # Additional debugging for common error scenarios
                    if response.status == 400:
                        logger.error(f"[PROHIBITED] Bad Request - Check payload format and required fields")
                    elif response.status == 401:
                        logger.error(f"[SECURE] Unauthorized - Check authentication credentials")
                    elif response.status == 403:
                        logger.error(f"[BLOCKED] Forbidden - Check authorization permissions")
                    elif response.status == 404:
                        logger.error(f"[MAGNIFY] Not Found - Check if endpoint URL and eval run ID are correct")
                        logger.error(f"[CLIPBOARD] Eval Run ID used: {eval_run_id}")
                        logger.error(f"[LINK] Full URL attempted: {url}")
                    elif response.status == 500:
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