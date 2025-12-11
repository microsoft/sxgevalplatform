"""
API client for communicating with evaluation platform APIs.
"""

import asyncio
import aiohttp
import logging
from typing import Optional, Dict, Any, List

logger = logging.getLogger(__name__)


class EvaluationApiClient:
    """Client for interacting with evaluation platform APIs."""
    
    def __init__(self, base_url: str, timeout_seconds: int = 300):
        """Initialize the API client."""
        self.base_url = base_url.rstrip('/')
        self.timeout = aiohttp.ClientTimeout(total=timeout_seconds)
        
    async def fetch_enriched_dataset(self, eval_run_id: str) -> Optional[List[Dict[str, Any]]]:
        """
        Fetch enriched dataset from API.
        
        Args:
            eval_run_id: The evaluation run ID
            
        Returns:
            List of dataset items or None if failed
        """
        endpoint = f"api/v1/eval/artifacts/enriched-dataset/{eval_run_id}"
        url = f"{self.base_url}/{endpoint}"
        
        logger.info(f"Fetching enriched dataset for eval run: {eval_run_id}")
        
        try:
            async with aiohttp.ClientSession(timeout=self.timeout) as session:
                headers = {
                    'Content-Type': 'application/json'
                }
                
                async with session.get(url, headers=headers) as response:
                    if response.status == 200:
                        data = await response.json()
                        logger.info(f"Successfully fetched dataset with {len(data)} items")
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
    
    async def fetch_metrics_configuration(self, eval_run_id: str) -> Optional[Dict[str, Any]]:
        """
        Fetch metrics configuration from API.
        
        Args:
            eval_run_id: The evaluation run ID
            
        Returns:
            Metrics configuration response or None if failed
        """
        endpoint = f"api/v1/eval/artifacts/metricsconfiguration/{eval_run_id}"
        url = f"{self.base_url}/{endpoint}"
        
        logger.info(f"Fetching metrics configuration for eval run: {eval_run_id}")
        
        try:
            async with aiohttp.ClientSession(timeout=self.timeout) as session:
                headers = {
                    'Content-Type': 'application/json'
                }
                
                async with session.get(url, headers=headers) as response:
                    if response.status == 200:
                        data = await response.json()
                        metrics_count = len(data.get('MetricsConfiguration', [])) if data.get('MetricsConfiguration') else 0
                        logger.info(f"Successfully fetched metrics configuration with {metrics_count} metrics")
                        return data
                    else:
                        error_text = await response.text()
                        logger.error(f"Failed to fetch metrics configuration. Status: {response.status}, Error: {error_text}")
                        return None
                        
        except asyncio.TimeoutError:
            logger.error(f"Timeout fetching metrics configuration for eval run: {eval_run_id}")
            return None
        except Exception as e:
            logger.error(f"Error fetching metrics configuration: {str(e)}")
            return None
    
    async def health_check(self) -> bool:
        """
        Check if the API is accessible.
        
        Returns:
            True if API is accessible, False otherwise
        """
        health_url = f"{self.base_url}/health"
        
        try:
            async with aiohttp.ClientSession(timeout=aiohttp.ClientTimeout(total=10)) as session:
                async with session.get(health_url) as response:
                    return response.status < 400
        except Exception as e:
            logger.warning(f"API health check failed: {str(e)}")
            return False
    
    async def close(self):
        """Close any open connections."""
        # aiohttp sessions are closed automatically in context managers
        pass