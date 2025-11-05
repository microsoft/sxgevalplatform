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
        
        logger.info(f"Fetching enriched dataset for eval run: {eval_run_id}")
        logger.debug(f"API URL: {url}")
        
        try:
            session = await self._get_session()
            headers = {
                'Content-Type': 'application/json'
            }
            
            async with session.get(url, headers=headers) as response:
                if response.status == 200:
                    data = await response.json()
                    # Handle both camelCase and PascalCase field names
                    enriched_data = data.get('enrichedDataset') or data.get('EnrichedDataset', [])
                    dataset_count = len(enriched_data)
                    logger.info(f"Successfully fetched dataset with {dataset_count} items")
                    return data
                else:
                    error_text = await response.text()
                    logger.error(f"Failed to fetch dataset. Status: {response.status}, Error: {error_text}")
                    return None
                        
        except asyncio.TimeoutError:
            logger.error(f"Timeout fetching enriched dataset for eval run: {eval_run_id}")
            return None
        except Exception as e:
            logger.error(f"Error fetching enriched dataset: {str(e)}")
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
        
        logger.info(f"Fetching metrics configuration for eval run: {metrics_configuration_id}")
        logger.debug(f"API URL: {url}")
        
        try:
            session = await self._get_session()
            headers = {
                'Content-Type': 'application/json'
            }
            
            async with session.get(url, headers=headers) as response:
                if response.status == 200:
                    data = await response.json()
                    
                    # Handle new array format or old nested format
                    if isinstance(data, list):
                        # New format: direct array of metrics
                        metrics_count = len(data)
                        logger.info(f"Successfully fetched metrics configuration with {metrics_count} metrics (array format)")
                    else:
                        # Old format: nested object
                        metrics_config = data.get('metricsConfiguration') or data.get('MetricsConfiguration', [])
                        metrics_count = len(metrics_config)
                        logger.info(f"Successfully fetched metrics configuration with {metrics_count} metrics (nested format)")
                    
                    return data
                else:
                    error_text = await response.text()
                    logger.error(f"Failed to fetch metrics configuration. Status: {response.status}, Error: {error_text}")
                    return None
                        
        except asyncio.TimeoutError:
            logger.error(f"Timeout fetching metrics configuration for eval run: {metrics_configuration_id}")
            return None
        except Exception as e:
            logger.error(f"Error fetching metrics configuration: {str(e)}")
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
        
        logger.info(f"Updating status to '{status}' for eval run: {eval_run_id}")
        logger.debug(f"API URL: {url}")
        
        try:
            session = await self._get_session()
            headers = {
                'Content-Type': 'application/json'
            }
            
            async with session.put(url, headers=headers, json=payload) as response:
                if response.status in [200, 204]:
                    logger.info(f"Successfully updated status for eval run: {eval_run_id}")
                    return True
                elif response.status == 400:
                    error_text = await response.text()
                    # Check if this is the "already in terminal state" error
                    if "already in a terminal state" in error_text:
                        logger.warning(f"Evaluation run {eval_run_id} is already in terminal state - skipping status update")
                        return True  # Treat as success since the evaluation is already completed
                    else:
                        logger.error(f"Failed to update status. Status: {response.status}, Error: {error_text}")
                        return False
                else:
                    error_text = await response.text()
                    logger.error(f"Failed to update status. Status: {response.status}, Error: {error_text}")
                    return False
                        
        except asyncio.TimeoutError:
            logger.error(f"Timeout updating status for eval run: {eval_run_id}")
            return False
        except Exception as e:
            logger.error(f"Error updating status: {str(e)}")
            return False

    async def health_check(self) -> bool:
        """
        Check if the API is accessible.
        
        Returns:
            True if API is accessible, False otherwise
        """
        health_url = f"{self.base_url}/health"
        
        try:
            # Use a separate session with shorter timeout for health checks
            timeout = aiohttp.ClientTimeout(total=10)
            async with aiohttp.ClientSession(timeout=timeout) as session:
                async with session.get(health_url) as response:
                    return response.status < 400
        except Exception as e:
            logger.warning(f"API health check failed: {str(e)}")
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