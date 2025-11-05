"""
Simplified Azure Storage services for evaluation engine.
"""

import asyncio
import json
import logging
from typing import Optional, Callable, Any, Dict
from datetime import datetime

from azure.storage.queue.aio import QueueServiceClient, QueueClient
from azure.storage.blob.aio import BlobServiceClient
from azure.core.exceptions import AzureError
from azure.identity.aio import DefaultAzureCredential

from ..config.settings import app_settings
from ..models.eval_models import QueueMessage
from ..exceptions import ConfigurationError

logger = logging.getLogger(__name__)


class AzureQueueService:
    """Service for interacting with Azure Storage Queue with connection pooling."""
    
    def __init__(self):
        """Initialize the queue service with Managed Identity or connection string."""
        self.config = app_settings.azure_storage
        self.queue_name = self.config.queue_name
        self.success_queue_name = self.config.success_queue_name
        self.failure_queue_name = self.config.failure_queue_name
        self.queue_client: Optional[QueueClient] = None
        self.success_queue_client: Optional[QueueClient] = None
        self.failure_queue_client: Optional[QueueClient] = None
        self.service_client: Optional[QueueServiceClient] = None
        self.credential: Optional[DefaultAzureCredential] = None
        self._initialized = False
        
    async def initialize(self) -> None:
        """Initialize the queue client with connection reuse."""
        if self._initialized:
            return
            
        try:
            if self.config.use_managed_identity:
                # Use Managed Identity with connection pooling
                self.credential = DefaultAzureCredential()
                account_url = f"https://{self.config.account_name}.queue.core.windows.net"
                self.service_client = QueueServiceClient(
                    account_url=account_url, 
                    credential=self.credential,
                    connection_timeout=30,  # Optimize connection timeout
                    read_timeout=60        # Optimize read timeout
                )
                logger.info(f"Using Managed Identity for Queue Service: {account_url}")
            else:
                # Fallback to connection string with connection pooling
                if not self.config.connection_string:
                    raise ConfigurationError("Connection string is required when Managed Identity is disabled")
                self.service_client = QueueServiceClient.from_connection_string(
                    self.config.connection_string,
                    connection_timeout=30,
                    read_timeout=60
                )
                logger.info("Using Connection String for Queue Service")
            
            # Initialize main processing queue
            self.queue_client = self.service_client.get_queue_client(self.queue_name)
            
            # Initialize success and failure logging queues
            self.success_queue_client = self.service_client.get_queue_client(self.success_queue_name)
            self.failure_queue_client = self.service_client.get_queue_client(self.failure_queue_name)
            
            # Ensure all queues exist (only once during initialization)
            queues_to_create = [
                (self.queue_client, self.queue_name),
                (self.success_queue_client, self.success_queue_name),
                (self.failure_queue_client, self.failure_queue_name)
            ]
            
            for queue_client, queue_name in queues_to_create:
                try:
                    await queue_client.create_queue()
                    logger.debug(f"Queue created or already exists: {queue_name}")
                except Exception as e:
                    # Queue might already exist - log but don't fail
                    logger.debug(f"Queue creation result for {queue_name}: {str(e)}")
                
            self._initialized = True
            logger.info(f"✅ Connected to queues: {self.queue_name}, {self.success_queue_name}, {self.failure_queue_name}")
        except AzureError as e:
            logger.error(f"Failed to initialize queue service: {e}")
            raise
    
    async def listen_for_messages(self, message_handler: Callable[[QueueMessage], Any]) -> None:
        """
        Listen for messages and process them with the provided handler.
        
        Args:
            message_handler: Function to handle received messages
        """
        if not self.queue_client:
            await self.initialize()
            
        if not self.queue_client:
            raise RuntimeError("Queue client not initialized")
            
        logger.info("Starting queue message listener...")
        
        while True:
            try:
                # Receive messages from queue
                messages = self.queue_client.receive_messages(
                    max_messages=1,
                    visibility_timeout=app_settings.evaluation.queue_visibility_timeout_seconds
                )
                
                async for message in messages:
                    queue_message = None
                    try:
                        # Parse message content and log complete message to telemetry
                        from ..utils.logging_helper import log_structured
                        
                        # Log complete raw message for telemetry and debugging
                        log_structured(
                            logger,
                            logging.INFO,
                            f"Received queue message from Azure Storage",
                            raw_message_content=message.content,
                            message_id=getattr(message, 'id', 'unknown'),
                            message_pop_receipt=getattr(message, 'pop_receipt', 'unknown'),
                            message_dequeue_count=getattr(message, 'dequeue_count', 0),
                            message_insertion_time=str(getattr(message, 'insertion_time', 'unknown')),
                            message_expiration_time=str(getattr(message, 'expiration_time', 'unknown')),
                            queue_name=self.queue_name
                        )
                        
                        queue_message = QueueMessage.from_json(message.content)
                        
                        # Log parsed message details
                        log_structured(
                            logger,
                            logging.INFO,
                            f"Successfully parsed queue message for evaluation",
                            eval_run_id=queue_message.eval_run_id,
                            metrics_configuration_id=queue_message.metrics_configuration_id,
                            enriched_dataset_id=queue_message.enriched_dataset_id,
                            agent_id=queue_message.agent_id,
                            dataset_id=queue_message.dataset_id,
                            requested_at=str(queue_message.requested_at),
                            priority=queue_message.priority
                        )
                        
                        # Process message and check if all steps completed successfully
                        processing_successful_raw = await message_handler(queue_message)
                        
                        # Debug logging for processing result
                        log_structured(
                            logger,
                            logging.INFO,
                            f"Message processing result received",
                            eval_run_id=queue_message.eval_run_id,
                            processing_successful_raw=processing_successful_raw,
                            processing_successful_raw_type=type(processing_successful_raw).__name__,
                            processing_result_is_none=processing_successful_raw is None,
                            processing_result_is_bool=isinstance(processing_successful_raw, bool)
                        )
                        
                        # Force conversion to boolean to handle any null/None cases
                        if processing_successful_raw is None:
                            logger.error(f"❌ CRITICAL: Message handler returned None for {queue_message.eval_run_id}! Forcing to False.")
                            processing_successful = False
                        elif isinstance(processing_successful_raw, bool):
                            processing_successful = processing_successful_raw
                        else:
                            logger.warning(f"⚠️  Message handler returned non-boolean type {type(processing_successful_raw).__name__}: {processing_successful_raw}. Converting to boolean.")
                            processing_successful = bool(processing_successful_raw)
                        
                        # Final validation logging
                        log_structured(
                            logger,
                            logging.INFO,
                            f"Final message processing result after validation",
                            eval_run_id=queue_message.eval_run_id,
                            processing_successful=processing_successful,
                            processing_successful_type=type(processing_successful).__name__,
                            processing_result_is_none=processing_successful is None,
                            processing_result_is_bool=isinstance(processing_successful, bool),
                            conversion_occurred=processing_successful != processing_successful_raw
                        )
                        
                        if processing_successful:
                            # Log success to the success queue
                            await self.log_success_message(queue_message, message.content, {
                                "processing_time": datetime.utcnow().isoformat(),
                                "message_dequeue_count": getattr(message, 'dequeue_count', 1),
                                "processing_result": processing_successful_raw
                            })
                            
                            # Delete message only after ALL steps completed successfully
                            await self.queue_client.delete_message(message)
                            logger.info(f"✅ All steps completed successfully. Deleted message: {queue_message.eval_run_id}")
                        else:
                            # Check message dequeue count to prevent infinite retries
                            dequeue_count = getattr(message, 'dequeue_count', 1)
                            max_retries = getattr(app_settings.evaluation, 'max_message_retries', 3)
                            
                            if dequeue_count is not None and dequeue_count >= max_retries:
                                # Log failure to the failure queue for final failure
                                await self.log_failure_message(queue_message, message.content, {
                                    "failure_reason": "exceeded_max_retries",
                                    "max_retries": max_retries,
                                    "final_dequeue_count": dequeue_count,
                                    "processing_result": processing_successful_raw,
                                    "failure_time": datetime.utcnow().isoformat()
                                })
                                
                                # Move to poison message handling - delete to prevent infinite loop
                                logger.error(f"🚨 Message {queue_message.eval_run_id} exceeded max retries ({max_retries}). Deleting poison message.")
                                await self.queue_client.delete_message(message)
                            else:
                                # Keep message in queue for retry (will become visible again after visibility timeout)
                                logger.warning(f"⚠️  Processing failed for {queue_message.eval_run_id}. Message kept in queue for retry (attempt {dequeue_count}/{max_retries}).")
                        
                    except KeyError as e:
                        # Log detailed parsing error with structured data
                        log_structured(
                            logger,
                            logging.ERROR,
                            f"Queue message parsing failed - missing required field",
                            error_type="KeyError",
                            error_message=str(e),
                            raw_message_content=message.content,
                            message_id=getattr(message, 'id', 'unknown'),
                            message_dequeue_count=getattr(message, 'dequeue_count', 0),
                            action_taken="message_kept_for_retry"
                        )
                        
                        # Log to failure queue if we can't parse after max retries
                        dequeue_count = getattr(message, 'dequeue_count', 1)
                        max_retries = getattr(app_settings.evaluation, 'max_message_retries', 3)
                        if dequeue_count >= max_retries:
                            try:
                                # Create a minimal failure log for messages with missing fields
                                failure_log = {
                                    "status": "failure",
                                    "timestamp": datetime.utcnow().isoformat(),
                                    "failure_reason": "missing_required_field",
                                    "error_message": str(e),
                                    "raw_message_content": message.content,
                                    "message_id": getattr(message, 'id', 'unknown'),
                                    "dequeue_count": dequeue_count
                                }
                                if self.failure_queue_client:
                                    await self.failure_queue_client.send_message(json.dumps(failure_log))
                                    logger.info(f"❌ Logged KeyError failure to queue {self.failure_queue_name}")
                            except Exception as log_error:
                                logger.error(f"Failed to log KeyError failure: {log_error}")
                        
                        # Don't delete message - invalid format might be temporary issue
                    except json.JSONDecodeError as e:
                        # Log detailed JSON parsing error
                        log_structured(
                            logger,
                            logging.ERROR,
                            f"Queue message JSON parsing failed",
                            error_type="JSONDecodeError",
                            error_message=str(e),
                            error_line_number=getattr(e, 'lineno', 'unknown'),
                            error_column=getattr(e, 'colno', 'unknown'),
                            raw_message_content=message.content,
                            message_id=getattr(message, 'id', 'unknown'),
                            message_dequeue_count=getattr(message, 'dequeue_count', 0),
                            action_taken="message_kept_for_retry"
                        )
                        
                        # Log to failure queue if we can't parse after max retries
                        dequeue_count = getattr(message, 'dequeue_count', 1)
                        max_retries = getattr(app_settings.evaluation, 'max_message_retries', 3)
                        if dequeue_count >= max_retries:
                            try:
                                # Create a minimal failure log for unparseable messages
                                failure_log = {
                                    "status": "failure", 
                                    "timestamp": datetime.utcnow().isoformat(),
                                    "failure_reason": "json_parsing_failed",
                                    "error_message": str(e),
                                    "raw_message_content": message.content,
                                    "message_id": getattr(message, 'id', 'unknown'),
                                    "dequeue_count": dequeue_count
                                }
                                if self.failure_queue_client:
                                    await self.failure_queue_client.send_message(json.dumps(failure_log))
                                    logger.info(f"❌ Logged JSON parsing failure to queue {self.failure_queue_name}")
                            except Exception as log_error:
                                logger.error(f"Failed to log JSON parsing failure: {log_error}")
                        
                        # Don't delete message - JSON parsing might be temporary issue
                    except Exception as e:
                        eval_run_id = queue_message.eval_run_id if queue_message else "unknown"
                        # Log detailed unexpected error with full context
                        log_structured(
                            logger,
                            logging.ERROR,
                            f"Unexpected error processing queue message",
                            error_type=type(e).__name__,
                            error_message=str(e),
                            eval_run_id=eval_run_id,
                            raw_message_content=message.content if queue_message is None else "parsed_successfully",
                            message_id=getattr(message, 'id', 'unknown'),
                            message_dequeue_count=getattr(message, 'dequeue_count', 0),
                            action_taken="message_kept_for_retry"
                        )
                        
                        # Log to failure queue if we can't process after max retries
                        dequeue_count = getattr(message, 'dequeue_count', 1)
                        max_retries = getattr(app_settings.evaluation, 'max_message_retries', 3)
                        if dequeue_count >= max_retries:
                            try:
                                # Create failure log for messages with processing errors
                                failure_log = {
                                    "status": "failure",
                                    "timestamp": datetime.utcnow().isoformat(),
                                    "failure_reason": "processing_error",
                                    "error_type": type(e).__name__,
                                    "error_message": str(e),
                                    "eval_run_id": eval_run_id,
                                    "raw_message_content": message.content,
                                    "message_id": getattr(message, 'id', 'unknown'),
                                    "dequeue_count": dequeue_count
                                }
                                if self.failure_queue_client:
                                    await self.failure_queue_client.send_message(json.dumps(failure_log))
                                    logger.info(f"❌ Logged processing failure to queue {self.failure_queue_name}")
                            except Exception as log_error:
                                logger.error(f"Failed to log processing failure: {log_error}")
                        
                        # Don't delete message - let it retry after visibility timeout
                        
            except Exception as e:
                logger.error(f"Error receiving messages: {str(e)}")
                
            # Wait before polling again
            await asyncio.sleep(app_settings.evaluation.queue_polling_interval_seconds)
    
    async def log_success_message(self, queue_message: QueueMessage, raw_original_message: str, processing_result: Optional[Dict[str, Any]] = None) -> None:
        """
        Log successfully processed message to the success queue.
        
        Args:
            queue_message: The parsed original message that was processed
            raw_original_message: The raw JSON string from the original queue message
            processing_result: Optional additional result data
        """
        if not self.success_queue_client:
            await self.initialize()
            
        if not self.success_queue_client:
            logger.error("Success queue client not available after initialization")
            return
            
        try:
            success_log = {
                "status": "success",
                "timestamp": datetime.utcnow().isoformat(),
                "original_message": {
                    "eval_run_id": queue_message.eval_run_id,
                    "metrics_configuration_id": queue_message.metrics_configuration_id,
                    "enriched_dataset_id": queue_message.enriched_dataset_id,
                    "agent_id": queue_message.agent_id,
                    "dataset_id": queue_message.dataset_id,
                    "requested_at": queue_message.requested_at.isoformat() if queue_message.requested_at else None,
                    "priority": queue_message.priority
                },
                "raw_original_message": raw_original_message,
                "processing_result": processing_result or {}
            }
            
            message_content = json.dumps(success_log)
            await self.success_queue_client.send_message(message_content)
            
            logger.info(f"✅ Logged success to queue {self.success_queue_name}: {queue_message.eval_run_id}")
            
        except Exception as e:
            logger.error(f"Failed to log success message to queue {self.success_queue_name}: {str(e)}")
            # Don't raise - logging failure shouldn't break main processing
    
    async def log_failure_message(self, queue_message: QueueMessage, raw_original_message: str, error_info: Optional[Dict[str, Any]] = None) -> None:
        """
        Log failed processed message to the failure queue.
        
        Args:
            queue_message: The parsed original message that failed processing
            raw_original_message: The raw JSON string from the original queue message
            error_info: Optional error information
        """
        if not self.failure_queue_client:
            await self.initialize()
            
        if not self.failure_queue_client:
            logger.error("Failure queue client not available after initialization")
            return
            
        try:
            failure_log = {
                "status": "failure",
                "timestamp": datetime.utcnow().isoformat(),
                "original_message": {
                    "eval_run_id": queue_message.eval_run_id,
                    "metrics_configuration_id": queue_message.metrics_configuration_id,
                    "enriched_dataset_id": queue_message.enriched_dataset_id,
                    "agent_id": queue_message.agent_id,
                    "dataset_id": queue_message.dataset_id,
                    "requested_at": queue_message.requested_at.isoformat() if queue_message.requested_at else None,
                    "priority": queue_message.priority
                },
                "raw_original_message": raw_original_message,
                "error_info": error_info or {}
            }
            
            message_content = json.dumps(failure_log)
            await self.failure_queue_client.send_message(message_content)
            
            logger.info(f"❌ Logged failure to queue {self.failure_queue_name}: {queue_message.eval_run_id}")
            
        except Exception as e:
            logger.error(f"Failed to log failure message to queue {self.failure_queue_name}: {str(e)}")
            # Don't raise - logging failure shouldn't break main processing
    
    async def close(self) -> None:
        """Close all queue clients and credential."""
        if self.queue_client:
            await self.queue_client.close()
        if self.success_queue_client:
            await self.success_queue_client.close()
        if self.failure_queue_client:
            await self.failure_queue_client.close()
        if self.credential:
            await self.credential.close()


class AzureBlobService:
    """Service for interacting with Azure Blob Storage."""
    
    def __init__(self):
        """Initialize the blob service with Managed Identity or connection string."""
        self.config = app_settings.azure_storage
        self.credential: Optional[DefaultAzureCredential] = None
        
        if self.config.use_managed_identity:
            # Use Managed Identity
            self.credential = DefaultAzureCredential()
            account_url = f"https://{self.config.account_name}.blob.core.windows.net"
            self.service_client = BlobServiceClient(account_url=account_url, credential=self.credential)
            logger.info(f"Using Managed Identity for Blob Service: {account_url}")
        else:
            # Fallback to connection string
            if not self.config.connection_string:
                raise ConfigurationError("Connection string is required when Managed Identity is disabled")
            self.service_client = BlobServiceClient.from_connection_string(self.config.connection_string)
            logger.info("Using Connection String for Blob Service")
        
    def get_container_name(self, agent_id: str) -> str:
        """Get container name for agent following Azure Storage naming rules."""
        return self._trim_and_remove_spaces(agent_id)
    
    def _trim_and_remove_spaces(self, input_str: str) -> str:
        """
        Convert C# TrimAndRemoveSpaces function to Python.
        Sanitizes container names for Azure Blob Storage.
        
        Args:
            input_str: Input string to sanitize
            
        Returns:
            Sanitized container name compliant with Azure Storage rules
            
        Raises:
            ValueError: If input is null or empty
        """
        if not input_str or input_str.isspace():
            raise ValueError("Input cannot be null or empty")
        
        # Step 1: Remove whitespace and convert to lowercase
        result = ''.join(c for c in input_str if not c.isspace()).lower()
        
        # Step 2: Replace underscores with hyphens and remove invalid characters
        # Azure Blob Storage container names can only contain lowercase letters, numbers, and hyphens
        new_result = []
        for c in result:
            if c.islower() or c.isdigit():
                new_result.append(c)
            elif c == '_':
                new_result.append('-')
            else:
                new_result.append('-')  # Replace other invalid characters with hyphens
        result = ''.join(new_result)
        
        # Step 3: Remove consecutive hyphens
        while '--' in result:
            result = result.replace('--', '-')
        
        # Step 4: Ensure it starts and ends with alphanumeric characters
        result = result.strip('-')
        
        # Step 5: Ensure minimum length of 3 characters
        if len(result) < 3:
            result = result.ljust(3, '0')  # Pad with zeros if too short
        
        # Step 6: Ensure maximum length of 63 characters
        if len(result) > 63:
            result = result[:63]
            # Ensure it still ends with alphanumeric after truncation
            result = result.rstrip('-')
            if len(result) < 3:
                result = result.ljust(3, '0')
        
        # Final validation: ensure it starts and ends with alphanumeric
        if not result[0].isalnum():
            result = 'a' + result[1:]
        if not result[-1].isalnum():
            result = result[:-1] + 'z'
        
        return result
        
    async def upload_evaluation_results(self, agent_id: str, eval_run_id: str, summary_data: Dict[str, Any]) -> str:
        """
        Upload evaluation summary results to blob storage.
        
        Args:
            agent_id: Agent ID for container selection
            eval_run_id: Evaluation run ID for file naming
            summary_data: Evaluation summary data
            
        Returns:
            Blob URL of uploaded file
        """
        container_name = self.get_container_name(agent_id)
        blob_name = f"evaluation-results/{eval_run_id}_summary.json"
        
        logger.debug(f"Uploading to container: {container_name}, blob: {blob_name}")
        
        try:
            # Get container client
            container_client = self.service_client.get_container_client(container_name)
            
            # Ensure container exists
            try:
                await container_client.create_container()
            except Exception:
                # Container might already exist
                pass
            
            # Upload data
            blob_data = json.dumps(summary_data, indent=2, default=str)
            blob_client = container_client.get_blob_client(blob_name)
            
            await blob_client.upload_blob(
                blob_data,
                blob_type="BlockBlob",
                overwrite=True,
                content_type="application/json"
            )
            
            blob_url = blob_client.url
            logger.info(f"Uploaded evaluation results to: {blob_url}")
            return blob_url
            
        except AzureError as e:
            logger.error(f"Failed to upload evaluation results: {e}")
            raise
    
    async def upload_detailed_dataset(self, agent_id: str, eval_run_id: str, dataset_data: Dict[str, Any]) -> str:
        """
        Upload detailed dataset results to blob storage.
        
        Args:
            agent_id: Agent ID for container selection
            eval_run_id: Evaluation run ID for file naming
            dataset_data: Detailed dataset with scores
            
        Returns:
            Blob URL of uploaded file
        """
        container_name = self.get_container_name(agent_id)
        blob_name = f"evaluation-results/{eval_run_id}_dataset.json"
        
        logger.debug(f"Uploading to container: {container_name}, blob: {blob_name}")
        
        try:
            # Get container client
            container_client = self.service_client.get_container_client(container_name)
            
            # Ensure container exists
            try:
                await container_client.create_container()
            except Exception:
                # Container might already exist
                pass
            
            # Upload data
            blob_data = json.dumps(dataset_data, indent=2, default=str)
            blob_client = container_client.get_blob_client(blob_name)
            
            await blob_client.upload_blob(
                blob_data,
                blob_type="BlockBlob",
                overwrite=True,
                content_type="application/json"
            )
            
            blob_url = blob_client.url
            logger.info(f"Uploaded detailed dataset to: {blob_url}")
            return blob_url
            
        except AzureError as e:
            logger.error(f"Failed to upload detailed dataset: {e}")
            raise
    
    async def close(self) -> None:
        """Close the blob service client and credential."""
        if self.service_client:
            await self.service_client.close()
        if self.credential:
            await self.credential.close()


# Global instances - will be initialized when needed
queue_service = None
blob_service = None

def get_queue_service() -> AzureQueueService:
    """Get or create queue service instance."""
    global queue_service
    if queue_service is None:
        queue_service = AzureQueueService()
    return queue_service

def get_blob_service() -> AzureBlobService:
    """Get or create blob service instance."""
    global blob_service
    if blob_service is None:
        blob_service = AzureBlobService()
    return blob_service