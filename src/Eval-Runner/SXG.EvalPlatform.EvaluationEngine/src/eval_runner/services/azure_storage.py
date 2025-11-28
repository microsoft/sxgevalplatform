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
            logger.info(f"[SUCCESS] Connected to queues: {self.queue_name}, {self.success_queue_name}, {self.failure_queue_name}")
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
                
                # Count messages received
                message_count = 0
                async for message in messages:
                    message_count += 1
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
                            message_content_type=type(message.content).__name__,
                            message_content_length=len(str(message.content)),
                            message_id=getattr(message, 'id', 'unknown'),
                            message_pop_receipt=getattr(message, 'pop_receipt', 'unknown'),
                            message_dequeue_count=getattr(message, 'dequeue_count', 0),
                            message_insertion_time=str(getattr(message, 'insertion_time', 'unknown')),
                            message_expiration_time=str(getattr(message, 'expiration_time', 'unknown')),
                            queue_name=self.queue_name
                        )
                        
                        # Try to handle potential double-JSON encoding issues and other corruptions
                        message_content = message.content
                        recovery_attempted = False
                        
                        if isinstance(message_content, str):
                            # Method 1: Check if the content looks like double-encoded JSON
                            if message_content.startswith('"') and message_content.endswith('"'):
                                try:
                                    # Attempt to decode once more in case of double-encoding
                                    message_content = json.loads(message_content)
                                    logger.warning(f"[WARNING]  Detected and fixed double-encoded JSON message for dequeue_count: {getattr(message, 'dequeue_count', 0)}")
                                    recovery_attempted = True
                                except json.JSONDecodeError:
                                    # If that fails, use original content
                                    pass
                            
                            # Method 2: Check if content appears to be base64 encoded
                            elif len(message_content) > 50 and message_content.replace('+', '').replace('/', '').replace('=', '').isalnum():
                                try:
                                    import base64
                                    decoded = base64.b64decode(message_content).decode('utf-8')
                                    json.loads(decoded)  # Test if it's valid JSON
                                    message_content = decoded
                                    logger.warning(f"[WARNING]  Detected and fixed base64-encoded message for dequeue_count: {getattr(message, 'dequeue_count', 0)}")
                                    recovery_attempted = True
                                except:
                                    # If base64 decoding fails, use original content
                                    pass
                        
                        queue_message = QueueMessage.from_json(message_content)
                        
                        # Log parsed message details
                        log_structured(
                            logger,
                            logging.INFO,
                            f"Successfully parsed queue message for evaluation",
                            eval_run_id=queue_message.eval_run_id,
                            metrics_configuration_id=queue_message.metrics_configuration_id,
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
                            logger.error(f"[ERROR] CRITICAL: Message handler returned None for {queue_message.eval_run_id}! Forcing to False.")
                            processing_successful = False
                        elif isinstance(processing_successful_raw, bool):
                            processing_successful = processing_successful_raw
                        else:
                            logger.warning(f"[WARNING]  Message handler returned non-boolean type {type(processing_successful_raw).__name__}: {processing_successful_raw}. Converting to boolean.")
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
                            logger.info(f"[SUCCESS] All steps completed successfully. Deleted message: {queue_message.eval_run_id}")
                        else:
                            # Check message dequeue count to prevent infinite retries
                            dequeue_count = getattr(message, 'dequeue_count', 1)
                            max_retries = getattr(app_settings.evaluation, 'max_message_retries', 3)
                            
                            if dequeue_count is not None and dequeue_count >= max_retries:
                                # Maximum retries exhausted - update status to failed and log failure
                                await self._handle_final_failure(queue_message, {
                                    "failure_reason": "exceeded_max_retries",
                                    "max_retries": max_retries,
                                    "final_dequeue_count": dequeue_count,
                                    "processing_result": processing_successful_raw,
                                    "failure_time": datetime.utcnow().isoformat()
                                })
                                
                                # Move to poison message handling - delete to prevent infinite loop
                                logger.error(f"  Message {queue_message.eval_run_id} exceeded max retries ({max_retries}). Deleting poison message.")
                                await self.queue_client.delete_message(message)
                            else:
                                # Keep message in queue for retry (will become visible again after visibility timeout)
                                logger.warning(f"[WARNING]  Processing failed for {queue_message.eval_run_id}. Message kept in queue for retry (attempt {dequeue_count}/{max_retries}).")
                        
                    except KeyError as e:
                        # Enhanced KeyError handling - analyze what fields are available vs missing
                        from ..utils.logging_helper import log_structured
                        
                        # Try to parse the JSON to see what fields are available
                        try:
                            if isinstance(message.content, str):
                                parsed_data = json.loads(message.content)
                            else:
                                parsed_data = message.content
                            available_keys = list(parsed_data.keys()) if isinstance(parsed_data, dict) else []
                        except:
                            parsed_data = message.content
                            available_keys = []
                        
                        log_structured(
                            logger,
                            logging.ERROR,
                            f"Queue message missing required field - analyzing message structure",
                            error_type="KeyError",
                            missing_field=str(e),
                            available_keys=available_keys,
                            raw_message_content=str(message.content)[:500] if len(str(message.content)) > 500 else message.content,
                            parsed_message_type=type(parsed_data).__name__,
                            message_id=getattr(message, 'id', 'unknown'),
                            message_dequeue_count=getattr(message, 'dequeue_count', 0),
                            action_taken="message_kept_for_retry"
                        )
                        
                        # Log to failure queue if we can't parse after max retries
                        dequeue_count = getattr(message, 'dequeue_count', 1)
                        max_retries = getattr(app_settings.evaluation, 'max_message_retries', 3)
                        if dequeue_count >= max_retries:
                            try:
                                # Handle final failure with status update
                                await self._handle_final_failure(queue_message, {
                                    "failure_reason": "missing_required_field",
                                    "missing_field": str(e),
                                    "available_keys": available_keys,
                                    "dequeue_count": dequeue_count,
                                    "eval_run_id": "unknown"  # Add eval_run_id to failure details
                                })
                                    
                                # Delete the malformed message after max retries
                                await self.queue_client.delete_message(message)
                                logger.warning(f"   Deleted malformed message after max retries: {getattr(message, 'id', 'unknown')}")
                                
                            except Exception as log_error:
                                logger.error(f"Failed to log KeyError failure: {log_error}")
                        else:
                            logger.warning(f"[WARNING]  Missing required field {e} for message (attempt {dequeue_count}/{max_retries}). Will retry after visibility timeout.")
                        
                        # Don't delete message - invalid format might be temporary issue
                    except json.JSONDecodeError as e:
                        # Enhanced JSON parsing error handling
                        dequeue_count = getattr(message, 'dequeue_count', 1)
                        recovery_attempted = False  # Initialize recovery tracking variable
                        
                        # Log detailed JSON parsing error with content analysis
                        from ..utils.logging_helper import log_structured
                        
                        # Try to detect common corruption patterns
                        content_str = str(message.content)
                        is_double_encoded = content_str.startswith('"') and content_str.endswith('"') and '\\' in content_str
                        looks_like_base64 = len(content_str) > 50 and content_str.replace('+', '').replace('/', '').replace('=', '').isalnum()
                        
                        log_structured(
                            logger,
                            logging.ERROR,
                            f"Queue message JSON parsing failed - analyzing content corruption",
                            error_type="JSONDecodeError",
                            error_message=str(e),
                            error_line_number=getattr(e, 'lineno', 'unknown'),
                            error_column=getattr(e, 'colno', 'unknown'),
                            raw_message_content=message.content,
                            raw_message_content_type=type(message.content).__name__,
                            raw_message_content_length=len(content_str),
                            content_starts_with_quote=content_str.startswith('"'),
                            content_ends_with_quote=content_str.endswith('"'),
                            content_has_backslashes=('\\' in content_str),
                            appears_double_encoded=is_double_encoded,
                            appears_base64_encoded=looks_like_base64,
                            content_first_100_chars=content_str[:100] if len(content_str) > 100 else content_str,
                            content_last_50_chars=content_str[-50:] if len(content_str) > 50 else content_str,
                            message_id=getattr(message, 'id', 'unknown'),
                            message_dequeue_count=dequeue_count,
                            action_taken="message_kept_for_retry"
                        )
                        
                        # Log to failure queue if we can't parse after max retries
                        dequeue_count = getattr(message, 'dequeue_count', 1)
                        max_retries = getattr(app_settings.evaluation, 'max_message_retries', 3)
                        
                        if dequeue_count >= max_retries:
                            try:
                                # Handle final failure with status update
                                await self._handle_final_failure(queue_message, {
                                    "failure_reason": "json_parsing_failed_max_retries",
                                    "error_message": str(e),
                                    "dequeue_count": dequeue_count,
                                    "recovery_attempted": recovery_attempted,
                                    "eval_run_id": "unknown"  # Add eval_run_id to failure details
                                })
                                    
                                # Delete the corrupted message to prevent infinite retry loops
                                await self.queue_client.delete_message(message)
                                logger.warning(f"   Deleted corrupted message after max retries: {getattr(message, 'id', 'unknown')}")
                                
                            except Exception as log_error:
                                logger.error(f"Failed to log JSON parsing failure: {log_error}")
                        else:
                            logger.warning(f"[WARNING]  JSON parsing failed for message (attempt {dequeue_count}/{max_retries}). Will retry after visibility timeout.")
                    except Exception as e:
                        eval_run_id = queue_message.eval_run_id if queue_message else "unknown"
                        # Import log_structured for this scope
                        from ..utils.logging_helper import log_structured
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
                                # Handle final failure with status update
                                await self._handle_final_failure(queue_message, {
                                    "failure_reason": "processing_error",
                                    "error_type": type(e).__name__,
                                    "error_message": str(e),
                                    "eval_run_id": eval_run_id,
                                    "dequeue_count": dequeue_count
                                })
                            except Exception as log_error:
                                logger.error(f"Failed to handle final processing failure: {log_error}")
                        
                        # Don't delete message - let it retry after visibility timeout
                        
                # Log message count after processing all messages in this batch
                logger.info(f"Received {message_count} message(s) from queue {self.queue_name}")
                        
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
                    "requested_at": queue_message.requested_at.isoformat() if queue_message.requested_at else None,
                    "priority": queue_message.priority
                },
                "raw_original_message": raw_original_message,
                "processing_result": processing_result or {}
            }
            
            message_content = json.dumps(success_log)
            await self.success_queue_client.send_message(message_content)
            
            logger.info(f"[SUCCESS] Logged success to queue {self.success_queue_name}: {queue_message.eval_run_id}")
            
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
                    "requested_at": queue_message.requested_at.isoformat() if queue_message.requested_at else None,
                    "priority": queue_message.priority
                },
                "raw_original_message": raw_original_message,
                "error_info": error_info or {}
            }
            
            message_content = json.dumps(failure_log)
            await self.failure_queue_client.send_message(message_content)
            
            logger.info(f"[ERROR] Logged failure to queue {self.failure_queue_name}: {queue_message.eval_run_id}")
            
        except Exception as e:
            logger.error(f"Failed to log failure message to queue {self.failure_queue_name}: {str(e)}")
    
    async def _handle_final_failure(self, queue_message: Optional[QueueMessage], failure_details: Dict[str, Any]) -> None:
        """
        Handle final failure by updating status to EvalRunFailed and logging to failure queue.
        
        Args:
            queue_message: The parsed original message that failed processing (None if parsing failed)
            failure_details: Details about the failure
        """
        try:
            # Extract eval_run_id from queue_message or failure_details
            eval_run_id = None
            if queue_message and hasattr(queue_message, 'eval_run_id'):
                eval_run_id = queue_message.eval_run_id
            elif 'eval_run_id' in failure_details:
                eval_run_id = failure_details['eval_run_id']
            
            # Only update status if we have a valid eval_run_id
            if eval_run_id and eval_run_id != 'unknown':
                # First, update the evaluation run status to failed
                from .http_client import get_api_client
                api_client = get_api_client()
                
                logger.info(f"  Updating evaluation run {eval_run_id} status to EvalRunFailed due to exhausted retries")
                
                try:
                    status_updated = await api_client.update_evaluation_status(eval_run_id, "EvalRunFailed")
                    if status_updated:
                        logger.info(f"[SUCCESS] Successfully updated status to EvalRunFailed for eval run: {eval_run_id}")
                    else:
                        logger.warning(f"[WARNING] Failed to update status to EvalRunFailed for eval run: {eval_run_id}")
                except Exception as status_error:
                    logger.error(f"[ERROR] Error updating status to EvalRunFailed for {eval_run_id}: {status_error}")
            else:
                logger.warning(f"[WARNING] Cannot update status to EvalRunFailed - no valid eval_run_id found")
            
            # Then, log the failure details to the failure queue
            if queue_message:
                await self.log_failure_message(queue_message, getattr(queue_message, '_raw_content', ''), failure_details)
            else:
                # Create a minimal failure log when we don't have a parsed queue message
                failure_log = {
                    "status": "failure",
                    "timestamp": datetime.utcnow().isoformat(),
                    **failure_details
                }
                if self.failure_queue_client:
                    await self.failure_queue_client.send_message(json.dumps(failure_log))
                    logger.info(f"[ERROR] Logged unparseable message failure to queue {self.failure_queue_name}")
            
            logger.info(f"  Final failure handling completed for eval run: {eval_run_id or 'unknown'}")
            
        except Exception as e:
            logger.error(f"[ERROR] Error in final failure handling: {str(e)}")
            # Still try to log to failure queue even if status update failed
            try:
                if queue_message:
                    await self.log_failure_message(queue_message, getattr(queue_message, '_raw_content', ''), failure_details)
                else:
                    failure_log = {"status": "failure", "timestamp": datetime.utcnow().isoformat(), **failure_details}
                    if self.failure_queue_client:
                        await self.failure_queue_client.send_message(json.dumps(failure_log))
            except Exception as log_error:
                logger.error(f"[ERROR] Failed to log final failure: {log_error}")
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