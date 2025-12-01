"""
Simplified evaluation engine for processing queue messages and running evaluations.
"""

import asyncio
import logging
import time
import traceback
from typing import List, Dict, Any, Optional
from datetime import datetime

from ..services.http_client import api_client
from ..config.settings import app_settings
from ..models.eval_models import (
    QueueMessage, Dataset, EnrichedDatasetResponse, MetricsConfigurationResponse, EvaluationConfig,
    DatasetItem, MetricScore, DatasetItemResult, MetricSummary, EvaluationSummary
)
from ..metrics.simple_interface import registry
from ..utils.logging_helper import (
    log_operation_start, log_operation_success, log_operation_error, log_evaluation_result,
    log_eval_run, set_eval_run_context, clear_eval_run_context, eval_workflow_step
)

logger = logging.getLogger(__name__)


class EvaluationEngine:
    """Main evaluation engine for processing queue messages and running evaluations."""
    
    def __init__(self):
        """Initialize the evaluation engine."""
        self.api_client = api_client
        self._processing_lock = asyncio.Lock()  # Ensure only one evaluation runs at a time
        
    async def _fetch_with_retry(self, fetch_function, *args, operation_name: str, eval_run_id_for_logging: str, max_retries: int = 3, base_delay: int = 60):
        """
        Execute an API fetch operation with exponential backoff retry logic.
        
        Args:
            fetch_function: The API function to call
            *args: Arguments to pass to the fetch function
            operation_name: Name of the operation for logging
            eval_run_id_for_logging: Eval run ID for logging context
            max_retries: Maximum number of retry attempts (default: 3)
            base_delay: Base delay in seconds (default: 60), will be exponentially increased
            
        Returns:
            The result from the successful API call
            
        Raises:
            Exception: If all retry attempts fail
        """
        last_exception = None
        
        for attempt in range(max_retries + 1):  # +1 for initial attempt
            start_time = time.time()  # Initialize start_time for each attempt
            try:
                if attempt > 0:
                    # Calculate exponential backoff delay: 60s, 120s, 240s
                    retry_delay = base_delay * (2 ** (attempt - 1))
                    
                    log_operation_start(
                        logger,
                        f"{operation_name}_retry",
                        eval_run_id=eval_run_id_for_logging,
                        attempt=attempt,
                        max_attempts=max_retries,
                        operation=operation_name,
                        retry_delay_seconds=retry_delay,
                        backoff_strategy="exponential"
                    )
                
                # Execute the API call with telemetry
                result = await fetch_function(*args)
                call_duration = time.time() - start_time
                
                if attempt > 0:
                    # Log successful retry with comprehensive telemetry
                    log_operation_success(
                        logger,
                        f"{operation_name}_retry",
                        eval_run_id=eval_run_id_for_logging,
                        attempt=attempt,
                        operation=operation_name,
                        success_after_retries=True,
                        call_duration_seconds=call_duration,
                        result_received=True,
                        backoff_strategy="exponential"
                    )
                else:
                    # Log successful first attempt
                    log_eval_run(
                        logger,
                        logging.INFO,
                        f"{operation_name} succeeded on first attempt",
                        operation=operation_name,
                        call_duration_seconds=call_duration,
                        attempt=1,
                        retry_not_needed=True
                    )
                
                return result
                
            except Exception as e:
                last_exception = e
                call_duration = time.time() - start_time
                
                # Extract detailed error information for telemetry
                error_details = self._extract_api_error_details(e)
                
                if attempt < max_retries:
                    # Calculate exponential backoff delay for next attempt
                    retry_delay = base_delay * (2 ** attempt)
                    
                    # Extract detailed error information for telemetry
                    error_details = self._extract_api_error_details(e)
                    
                    # Log retry attempt with comprehensive telemetry
                    # Note: error_details dict is logged separately to avoid KeyError from unpacking
                    log_operation_error(
                        logger,
                        f"{operation_name}_attempt",
                        e,
                        eval_run_id=eval_run_id_for_logging,
                        attempt=attempt + 1,
                        max_attempts=max_retries,
                        operation=operation_name,
                        retry_delay_seconds=retry_delay,
                        will_retry=True,
                        error_details=str(e),
                        call_duration_seconds=call_duration,
                        backoff_strategy="exponential",
                        api_error_type=error_details.get('error_type', 'unknown'),
                        api_status_code=error_details.get('api_status_code'),
                        api_response_body=error_details.get('api_response_body'),
                        is_retryable=error_details.get('is_retryable', True)
                    )
                    
                    # Enhanced logging with exponential backoff info
                    log_eval_run(
                        logger,
                        logging.WARNING,
                        f"{operation_name} attempt {attempt + 1} failed, retrying with exponential backoff",
                        operation=operation_name,
                        attempt=attempt + 1,
                        max_attempts=max_retries + 1,
                        retry_delay_seconds=retry_delay,
                        next_retry_in_seconds=retry_delay,
                        backoff_pattern=f"Attempt {attempt + 2}: {retry_delay}s delay",
                        total_retries_remaining=max_retries - attempt,
                        error_type=type(e).__name__,
                        error_message=str(e),
                        call_duration_seconds=call_duration,
                        # Include API-specific error details
                        **error_details
                    )
                    
                    # Wait with exponential backoff
                    logger.info(f"Waiting {retry_delay} seconds before retry attempt {attempt + 2}/{max_retries + 1} for {operation_name} (exponential backoff)")
                    await asyncio.sleep(retry_delay)
                else:
                    # Extract detailed error information for telemetry
                    error_details = self._extract_api_error_details(e)
                    
                    # Log final failure with comprehensive telemetry
                    # Note: error_details dict is logged separately to avoid KeyError from unpacking
                    log_operation_error(
                        logger,
                        f"{operation_name}_final_failure",
                        e,
                        eval_run_id=eval_run_id_for_logging,
                        attempt=attempt + 1,
                        max_attempts=max_retries,
                        operation=operation_name,
                        all_retries_exhausted=True,
                        error_details=str(e),
                        stack_trace=traceback.format_exc(),
                        call_duration_seconds=call_duration,
                        backoff_strategy="exponential",
                        retry_history=[f"60s", f"120s", f"240s"][:max_retries],
                        api_error_type=error_details.get('error_type', 'unknown'),
                        api_status_code=error_details.get('api_status_code'),
                        api_response_body=error_details.get('api_response_body'),
                        is_retryable=error_details.get('is_retryable', True)
                    )
        
        # All retries exhausted, raise the last exception
        if last_exception is not None:
            raise last_exception
        else:
            raise Exception(f"All {max_retries} retry attempts failed for {operation_name}")

    def _extract_api_error_details(self, exception: Exception) -> dict:
        """
        Extract detailed error information from API exceptions for telemetry.
        
        Args:
            exception: The exception from the API call
            
        Returns:
            Dictionary with detailed error telemetry
        """
        error_details = {
            'error_type': type(exception).__name__,
            'error_message': str(exception),
            'api_status_code': None,
            'api_response_body': None,
            'api_response_headers': None,
            'api_error_type': 'unknown',
            'is_retryable': True,  # Default to retryable unless we know otherwise
            'failure_category': 'api_communication_error'
        }
        
        # Check if it's an HTTP-related exception with response details
        try:
            if hasattr(exception, 'response') and getattr(exception, 'response', None) is not None:
                response = getattr(exception, 'response')
                
                # Status code
                if hasattr(response, 'status_code'):
                    error_details['api_status_code'] = response.status_code
                    
                    # Categorize based on status code
                    if 400 <= response.status_code < 500:
                        error_details['api_error_type'] = 'client_error'
                        error_details['is_retryable'] = response.status_code in [408, 429, 502, 503, 504]  # Only retry specific 4xx codes
                    elif 500 <= response.status_code < 600:
                        error_details['api_error_type'] = 'server_error'
                        error_details['is_retryable'] = True  # Generally retry server errors
                    else:
                        error_details['api_error_type'] = 'http_error'
                
                # Response body (truncate if too long)
                try:
                    if hasattr(response, 'text'):
                        response_text = str(response.text) if hasattr(response.text, '__call__') else str(response.text)
                        if len(response_text) > 1000:  # Truncate long responses
                            error_details['api_response_body'] = response_text[:1000] + "... (truncated)"
                        else:
                            error_details['api_response_body'] = response_text
                    elif hasattr(response, 'content'):
                        content = str(response.content)[:1000]
                        error_details['api_response_body'] = content
                except Exception:
                    error_details['api_response_body'] = "Could not read response body"
                
                # Response headers (exclude sensitive ones)
                try:
                    if hasattr(response, 'headers'):
                        safe_headers = {}
                        headers = getattr(response, 'headers', {})
                        if hasattr(headers, 'items'):
                            for key, value in headers.items():
                                # Exclude potentially sensitive headers
                                if key.lower() not in ['authorization', 'cookie', 'set-cookie', 'x-api-key']:
                                    safe_headers[key] = str(value)
                        error_details['api_response_headers'] = safe_headers
                except Exception:
                    error_details['api_response_headers'] = {}
        except Exception:
            # If we can't read response details safely, just continue
            pass
        
        # Handle timeout exceptions
        if 'timeout' in str(exception).lower() or type(exception).__name__ in ['TimeoutError', 'ConnectTimeoutError', 'ReadTimeoutError']:
            error_details['api_error_type'] = 'timeout_error'
            error_details['is_retryable'] = True
            error_details['failure_category'] = 'api_timeout_error'
        
        # Handle connection exceptions
        elif 'connection' in str(exception).lower() or type(exception).__name__ in ['ConnectionError', 'ConnectError']:
            error_details['api_error_type'] = 'connection_error'
            error_details['is_retryable'] = True
            error_details['failure_category'] = 'api_connection_error'
        
        # Handle DNS/network exceptions
        elif any(term in str(exception).lower() for term in ['dns', 'network', 'host', 'resolve']):
            error_details['api_error_type'] = 'network_error'
            error_details['is_retryable'] = True
            error_details['failure_category'] = 'api_network_error'
        
        # Add additional context
        error_details['exception_module'] = getattr(type(exception), '__module__', 'unknown')
        error_details['exception_class'] = type(exception).__name__
        
        return error_details
        
    async def process_queue_message(self, queue_message: QueueMessage) -> bool:
        """
        Process a queue message by running evaluation and storing results.
        Ensures only one evaluation runs at a time.
        Tracks all processing steps and only returns True if ALL steps succeed.
        
        Args:
            queue_message: Queue message containing evaluation request
            
        Returns:
            bool: True if all steps completed successfully, False otherwise
        """
        async with self._processing_lock:
            eval_run_id = queue_message.eval_run_id
            metrics_configuration_id = queue_message.metrics_configuration_id; 
            start_time = time.time()
            
            # Set eval run context for all subsequent logs
            set_eval_run_context(eval_run_id)
            
            # Step tracking
            steps_completed = []
            
            try:
                # Log evaluation start with complete context
                log_eval_run(
                    logger,
                    logging.INFO,
                    f"Starting evaluation processing for eval_run_id: {eval_run_id}",
                    metricsConfigurationId=metrics_configuration_id,
                    priority=queue_message.priority,
                    requestedAt=str(queue_message.requested_at),
                    processingStartTime=start_time
                )
                
                # Log evaluation start with complete context
                log_operation_start(
                    logger,
                    "evaluation_processing",
                    eval_run_id=eval_run_id,
                    metrics_configuration_id=metrics_configuration_id,
                    priority=queue_message.priority,
                    requested_at=str(queue_message.requested_at)
                )
                
                # Step 1: Fetch dataset and metrics configuration
                with eval_workflow_step("fetch_dataset_and_config", "1/7") as op_id:
                    
                    try:
                        # Fetch data with retry logic - SEQUENTIAL to avoid session race conditions
                        # Note: HTTP client methods now validate and raise exceptions on failures,
                        # so retry logic will automatically trigger on empty/invalid responses
                        # Changed from parallel (asyncio.gather) to sequential to eliminate
                        # race condition where both requests share the same HTTP session
                        
                        logger.info("Fetching dataset and metrics configuration sequentially to avoid session conflicts")
                        
                        # Fetch dataset first
                        dataset_data = await self._fetch_with_retry(
                            self.api_client.fetch_enriched_dataset,
                            eval_run_id,
                            operation_name="fetch_enriched_dataset",
                            eval_run_id_for_logging=eval_run_id
                        )
                        
                        # Then fetch metrics configuration
                        metrics_config_data = await self._fetch_with_retry(
                            self.api_client.fetch_metrics_configuration,
                            metrics_configuration_id,
                            operation_name="fetch_metrics_configuration",
                            eval_run_id_for_logging=eval_run_id
                        )
                        
                        # Validation is now handled inside HTTP client methods
                        # If we reach this point, both responses are valid and non-empty
                        
                        # Log successful data fetch with details
                        log_eval_run(
                            logger,
                            logging.INFO,
                            f"Successfully fetched data from APIs (with exponential backoff retry logic)",
                            datasetResponseSize=len(str(dataset_data)) if dataset_data else 0,
                            metricsConfigResponseSize=len(str(metrics_config_data)) if metrics_config_data else 0,
                            rawMetricsConfig=metrics_config_data,  # Log complete metrics config for debugging
                            retryEnabled=True,
                            maxRetries=3,
                            baseRetryDelaySeconds=60,
                            backoffStrategy="exponential",
                            retryPattern="60s, 120s, 240s"
                        )
                        
                        steps_completed.append("fetch_data")
                        
                    except Exception as e:
                        # Log detailed step failure
                        log_eval_run(
                            logger,
                            logging.ERROR,
                            f"Failed to fetch dataset and metrics configuration: {str(e)}",
                            errorType=type(e).__name__,
                            errorMessage=str(e),
                            stackTrace=traceback.format_exc()
                        )
                        raise
                
                # Step 2: Parse the data
                with eval_workflow_step("parse_data", "2/7") as op_id:
                    try:
                        enriched_dataset_response = EnrichedDatasetResponse.from_json(dataset_data)
                        dataset = enriched_dataset_response.to_dataset()
                        metrics_response = MetricsConfigurationResponse.from_json(metrics_config_data)
                        
                        # Log detailed parsing results
                        parsed_metrics = []
                        for i, config in enumerate(metrics_response.metrics_configuration):
                            original_name = getattr(config, '_original_name', 'unknown')
                            parsed_metrics.append({
                                'index': i + 1,
                                'original_name': original_name,
                                'normalized_name': config.metric_name,
                                'threshold': config.threshold
                            })
                        
                        log_eval_run(
                            logger,
                            logging.INFO,
                            f"Successfully parsed evaluation data",
                            step_number=2,
                            dataset_items_count=len(dataset.items),
                            metrics_count=len(metrics_response.metrics_configuration),
                            parsed_metrics=parsed_metrics
                        )
                        
                        steps_completed.append("parse_data")
                        log_operation_success(
                            logger, 
                            "parse_data_step", 
                            eval_run_id=eval_run_id, 
                            step_number=2,
                            items_parsed=len(dataset.items),
                            metrics_parsed=len(metrics_response.metrics_configuration)
                        )
                            
                    except Exception as e:
                        # Log detailed parsing failure with data context
                        log_operation_error(
                            logger,
                            "parse_data_step",
                            e,
                            eval_run_id=eval_run_id,
                            step_number=2,
                            step_name="parse_dataset_and_metrics",
                            error_details=str(e),
                            raw_dataset_sample=str(dataset_data)[:500] if dataset_data else "null",
                            raw_metrics_config=metrics_config_data,
                            stack_trace=traceback.format_exc()
                        )
                        raise
                
                # Step 3: Update status to EvalRunStarted
                with eval_workflow_step("update_status_started", "3/7") as op_id:
                    try:
                        await self._fetch_with_retry(
                            self._update_evaluation_status,
                            eval_run_id,
                            "EvalRunStarted",
                            operation_name="update_status_started",
                            eval_run_id_for_logging=eval_run_id
                        )
                        
                        steps_completed.append("update_status_started")
                        log_operation_success(
                            logger, 
                            "update_status_started_step", 
                            eval_run_id=eval_run_id, 
                            step_number=3,
                            status_updated_to="EvalRunStarted"
                        )
                        
                    except Exception as e:
                        log_operation_error(
                            logger,
                            "update_status_started_step",
                            e,
                            eval_run_id=eval_run_id,
                            step_number=3,
                            step_name="update_status_started",
                            target_status="EvalRunStarted",
                            error_details=str(e),
                            stack_trace=traceback.format_exc()
                        )
                        raise
                
                # Step 4: Run evaluations
                with eval_workflow_step("run_evaluations", "4/7") as op_id:
                    try:
                        results = await self._run_evaluations(dataset, metrics_response.metrics_configuration)
                        
                        # Count successful evaluations and collect failure details
                        successful_evaluations = 0
                        failed_evaluations = 0
                        total_scores = 0
                        failed_metrics_details = []
                        
                        for item_result in results:
                            for score in item_result.metric_scores:
                                total_scores += 1
                                if score.score is not None and score.score > 0:
                                    successful_evaluations += 1
                                else:
                                    failed_evaluations += 1
                                    # Collect details of failed metrics
                                    if score.metric_name not in [f['metric_name'] for f in failed_metrics_details]:
                                        failed_metrics_details.append({
                                            'metric_name': score.metric_name,
                                            'failure_reason': score.reason,
                                            'failure_count': 1
                                        })
                                    else:
                                        # Increment count for existing failed metric
                                        for failed_metric in failed_metrics_details:
                                            if failed_metric['metric_name'] == score.metric_name:
                                                failed_metric['failure_count'] += 1
                                                break
                        
                        # Determine if step should be marked as successful
                        # Step fails only if ALL metrics failed (no successful evaluations)
                        step_successful = successful_evaluations > 0
                        
                        if step_successful:
                            log_eval_run(
                                logger,
                                logging.INFO,
                                f"Evaluation execution completed successfully - at least one metric succeeded",
                                step_number=4,
                                total_items_processed=len(results),
                                total_scores_computed=total_scores,
                                successful_evaluations=successful_evaluations,
                                failed_evaluations=failed_evaluations,
                                success_rate=successful_evaluations / total_scores if total_scores > 0 else 0,
                                failed_metrics_details=failed_metrics_details if failed_metrics_details else None,
                                step_result="SUCCESS_WITH_PARTIAL_FAILURES" if failed_evaluations > 0 else "SUCCESS"
                            )
                            
                            steps_completed.append("run_evaluations")
                            log_operation_success(
                                logger, 
                                "run_evaluations_step", 
                                eval_run_id=eval_run_id, 
                                step_number=4,
                                evaluations_completed=successful_evaluations,
                                evaluations_failed=failed_evaluations,
                                failed_metrics_details=failed_metrics_details
                            )
                        else:
                            # All metrics failed - mark step as failed
                            log_eval_run(
                                logger,
                                logging.ERROR,
                                f"Evaluation execution failed - ALL metrics failed",
                                step_number=4,
                                total_items_processed=len(results),
                                total_scores_computed=total_scores,
                                successful_evaluations=successful_evaluations,
                                failed_evaluations=failed_evaluations,
                                failed_metrics_details=failed_metrics_details,
                                step_result="TOTAL_FAILURE"
                            )
                            
                            # Don't add to steps_completed - this will mark the step as failed
                            log_operation_error(
                                logger,
                                "run_evaluations_step",
                                Exception(f"All {len(failed_metrics_details)} metrics failed"),
                                eval_run_id=eval_run_id,
                                step_number=4,
                                step_name="run_evaluations",
                                items_to_evaluate=len(dataset.items),
                                metrics_to_run=len(metrics_response.metrics_configuration),
                                failed_metrics_details=failed_metrics_details,
                                error_details="All configured metrics failed to evaluate successfully"
                            )
                            raise Exception(f"All {len(failed_metrics_details)} metrics failed: {[m['metric_name'] for m in failed_metrics_details]}")
                        
                    except Exception as e:
                        # Log detailed evaluation failure
                        log_operation_error(
                            logger,
                            "run_evaluations_step",
                            e,
                            eval_run_id=eval_run_id,
                            step_number=4,
                            step_name="run_evaluations",
                            items_to_evaluate=len(dataset.items),
                            metrics_to_run=len(metrics_response.metrics_configuration),
                            error_details=str(e),
                            stack_trace=traceback.format_exc()
                        )
                        raise
                
                # Step 5: Generate summary
                with eval_workflow_step("generate_summary", "5/7") as op_id:
                    try:
                        execution_time = time.time() - start_time
                        summary = self._generate_summary(
                            queue_message, 
                            results, 
                            metrics_response.metrics_configuration,
                            execution_time,
                            metrics_response
                        )
                        
                        # Log summary generation details
                        log_eval_run(
                            logger,
                            logging.INFO,
                            f"Successfully generated evaluation summary",
                            step_number=5,
                            execution_time_seconds=execution_time,
                            summary_metrics_count=len(summary.metric_summaries),
                            overall_pass_percentage=summary.overall_pass_percentage
                        )
                        
                        steps_completed.append("generate_summary")
                        log_operation_success(
                            logger, 
                            "generate_summary_step", 
                            eval_run_id=eval_run_id, 
                            step_number=5,
                            execution_time_seconds=execution_time
                        )
                        
                    except Exception as e:
                        log_operation_error(
                            logger,
                            "generate_summary_step",
                            e,
                            eval_run_id=eval_run_id,
                            step_number=5,
                            step_name="generate_summary",
                            error_details=str(e),
                            stack_trace=traceback.format_exc()
                        )
                        raise
                
                # Step 6: Post results to API
                with eval_workflow_step("post_results", "6/7") as op_id:
                    try:
                        await self._fetch_with_retry(
                            self._store_results,
                            summary.agent_id,
                            eval_run_id,
                            summary,
                            results,
                            operation_name="post_results",
                            eval_run_id_for_logging=eval_run_id
                        )
                        
                        log_eval_run(
                            logger,
                            logging.INFO,
                            f"Successfully posted evaluation results to API",
                            step_number=6,
                            agent_id=summary.agent_id,
                            results_count=len(results),
                            summary_generated=True
                        )
                        
                        steps_completed.append("post_results")
                        log_operation_success(
                            logger, 
                            "post_results_step", 
                            eval_run_id=eval_run_id, 
                            step_number=6,
                            agent_id=summary.agent_id
                        )
                        
                    except Exception as e:
                        log_operation_error(
                            logger,
                            "post_results_step",
                            e,
                            eval_run_id=eval_run_id,
                            step_number=6,
                            step_name="post_results",
                            agent_id=summary.agent_id,
                            error_details=str(e),
                            stack_trace=traceback.format_exc()
                        )
                        raise
                
                # Step 7: Update status to completed
                with eval_workflow_step("update_status_completed", "7/7") as op_id:
                    try:
                        await self._fetch_with_retry(
                            self._update_evaluation_status,
                            eval_run_id,
                            "EvalRunCompleted",
                            operation_name="update_status_completed",
                            eval_run_id_for_logging=eval_run_id
                        )
                        
                        steps_completed.append("update_status")
                        log_operation_success(
                            logger, 
                            "update_status_step", 
                            eval_run_id=eval_run_id, 
                            step_number=7,
                            status_updated_to="EvalRunCompleted"
                        )
                        
                    except Exception as e:
                        # Don't raise here for status updates - status update failure shouldn't prevent message deletion
                        # if all other critical steps succeeded, but log it as an error
                        log_operation_error(
                            logger,
                            "update_status_step",
                            e,
                            eval_run_id=eval_run_id,
                            step_number=7,
                            step_name="update_status",
                            target_status="EvalRunCompleted",
                            error_details=str(e),
                            severity="warning",
                            continue_processing=True,
                            stack_trace=traceback.format_exc(),
                            retry_attempts_made=True,
                            all_retries_failed=True
                        )
                
                # All steps completed - wrap up the evaluation
                execution_time = time.time() - start_time
                
                # Return True only if all critical steps completed (status update is not critical)
                critical_steps = ["fetch_data", "parse_data", "run_evaluations", "generate_summary", "post_results"]
                all_critical_steps_completed = all(step in steps_completed for step in critical_steps)
                
                if all_critical_steps_completed:
                    # Log overall success with complete telemetry
                    log_operation_success(
                        logger,
                        "evaluation_processing",
                        duration_ms=execution_time * 1000,
                        eval_run_id=eval_run_id,
                        all_steps_completed=steps_completed,
                        critical_steps_completed=critical_steps,
                        status_update_completed="update_status" in steps_completed,
                        total_execution_time_seconds=execution_time
                    )
                else:
                    # Log partial failure
                    missing_steps = [step for step in critical_steps if step not in steps_completed]
                    
                    log_operation_error(
                        logger,
                        "evaluation_processing", 
                        Exception(f"Not all critical steps completed"),
                        eval_run_id=eval_run_id,
                        completed_steps=steps_completed,
                        missing_critical_steps=missing_steps,
                        execution_time_seconds=execution_time
                    )
                
                return all_critical_steps_completed
            
            except Exception as e:
                # Clear eval run context on unhandled exceptions
                clear_eval_run_context()
                
                # Handle any unhandled exceptions to ensure we always return a boolean
                eval_run_id = getattr(queue_message, 'eval_run_id', 'unknown')
                logger.error(f"[ERROR] Unhandled exception in process_queue_message for {eval_run_id}: {str(e)}")
                logger.error(f"Exception type: {type(e).__name__}")
                logger.error(f"Stack trace: {traceback.format_exc()}")
                
                # Log structured error for debugging
                log_eval_run(
                    logger,
                    logging.ERROR,
                    f"Unhandled exception in evaluation processing",
                    errorType=type(e).__name__,
                    errorMessage=str(e),
                    stackTrace=traceback.format_exc(),
                    criticalFailure=True
                )
                
                # Always return False for unhandled exceptions
                return False
            
            finally:
                # Always clear eval run context when processing completes
                clear_eval_run_context()
    
    async def _run_evaluations(
        self, 
        dataset: Dataset, 
        metrics_config: List
    ) -> List[DatasetItemResult]:
        """
        Run evaluations for all dataset items and metrics concurrently.
        
        Args:
            dataset: Dataset to evaluate
            metrics_config: List of metric configurations
            
        Returns:
            List of evaluation results per dataset item
        """
        # Get available metrics from registry
        available_metrics = registry.get_all_metrics_flat()
        
        logger.info(f"Available metrics: {list(available_metrics.keys())}")
        logger.info(f"Processing {len(dataset.items)} dataset items with {len(metrics_config)} metrics each")
        
        # Process dataset items concurrently with controlled concurrency
        dataset_semaphore = asyncio.Semaphore(app_settings.evaluation.max_parallel_prompts)  # Use configured max parallel prompts
        
        async def process_dataset_item(i, item):
            async with dataset_semaphore:
                logger.debug(f"Processing dataset item {i+1}/{len(dataset.items)}")
                
                # Run metrics for this item
                metric_scores = await self._evaluate_item_metrics(item, metrics_config, available_metrics)
                
                # Create result
                return DatasetItemResult(
                    prompt=item.prompt,
                    ground_truth=item.ground_truth,
                    actual_response=item.actual_response,
                    context=item.context,
                    metric_scores=metric_scores
                )
        
        # Create tasks for all dataset items
        tasks = [process_dataset_item(i, item) for i, item in enumerate(dataset.items)]
        
        # Execute all dataset items concurrently
        results = await asyncio.gather(*tasks, return_exceptions=True)
        
        # Handle any exceptions from dataset item processing
        processed_results = []
        for i, result in enumerate(results):
            if isinstance(result, Exception):
                logger.error(f"Error processing dataset item {i+1}: {str(result)}")
                # Create a failure result for this item
                item = dataset.items[i]
                failure_scores = []
                for config in metrics_config:
                    failure_scores.append(MetricScore(
                        metric_name=config.metric_name,
                        score=0.0,
                        reason=f"Dataset item processing failed: {str(result)}",
                        passed=False,
                        details={
                            'error_type': type(result).__name__,
                            'error_message': str(result),
                            'failure_category': 'dataset_item_processing_error',
                            'evaluation_attempted': False
                        }
                    ))
                
                processed_results.append(DatasetItemResult(
                    prompt=item.prompt,
                    ground_truth=item.ground_truth,
                    actual_response=item.actual_response,
                    context=item.context,
                    metric_scores=failure_scores
                ))
            else:
                processed_results.append(result)
        
        return processed_results
    
    async def _evaluate_item_metrics(
        self, 
        item: DatasetItem, 
        metrics_config: List,
        available_metrics: Dict[str, Any]
    ) -> List[MetricScore]:
        """
        Evaluate all configured metrics for a single dataset item concurrently.
        
        Args:
            item: Dataset item to evaluate
            metrics_config: List of metric configurations
            available_metrics: Available metric instances
            
        Returns:
            List of metric scores
        """
        # Run metrics in parallel with optimized concurrency
        # Higher concurrency for metrics since they're typically I/O bound
        semaphore = asyncio.Semaphore(app_settings.evaluation.max_parallel_metrics)  # Use configured max parallel metrics
        
        async def evaluate_metric_with_timeout(config):
            async with semaphore:
                try:
                    # Add timeout to prevent hanging metrics
                    return await asyncio.wait_for(
                        self._evaluate_single_metric(item, config, available_metrics),
                        timeout=30.0  # 30 second timeout per metric
                    )
                except asyncio.TimeoutError:
                    logger.warning(f"Metric {config.metric_name} evaluation timed out after 30 seconds")
                    return MetricScore(
                        metric_name=config.metric_name,
                        score=0.0,
                        reason="Evaluation timed out after 30 seconds",
                        passed=False,
                        details={
                            'error_type': 'TimeoutError',
                            'error_message': 'Metric evaluation exceeded 30 second timeout',
                            'evaluation_attempted': True,
                            'failure_category': 'metric_timeout_error',
                            'timeout_seconds': 30.0
                        }
                    )
        
        # Create tasks for all metrics
        tasks = [evaluate_metric_with_timeout(config) for config in metrics_config]
        
        # Execute all metrics concurrently
        scores = await asyncio.gather(*tasks, return_exceptions=True)
        
        # Process results and collect detailed failure information
        metric_scores = []
        for i, score in enumerate(scores):
            if isinstance(score, Exception):
                metric_name = metrics_config[i].metric_name
                error_details = {
                    'error_type': type(score).__name__,
                    'error_message': str(score),
                    'metric_name': metric_name
                }
                
                logger.error(f"Error evaluating metric {metric_name}: {str(score)}", extra=error_details)
                
                # Create detailed failure score
                metric_scores.append(MetricScore(
                    metric_name=metric_name,
                    score=0.0,
                    reason=f"Evaluation failed ({type(score).__name__}): {str(score)}",
                    passed=False,
                    details={
                        'error_type': type(score).__name__,
                        'error_message': str(score),
                        'evaluation_attempted': True,
                        'failure_category': 'metric_execution_error'
                    }
                ))
            else:
                metric_scores.append(score)
        
        return metric_scores
    
    def _normalize_metric_name(self, metric_name: str, available_metrics: Dict[str, Any]) -> str:
        """
        Normalize metric name to match available metrics.
        
        Args:
            metric_name: Original metric name from configuration (already normalized by MetricConfig)
            available_metrics: Available metric instances
            
        Returns:
            Normalized metric name or original if no match found
        """
        # First try exact match (metric_name should already be normalized by MetricConfig)
        if metric_name in available_metrics:
            return metric_name
            
        # If still no match, try some fallback normalizations
        # Convert any remaining spaces or special chars
        fallback_name = metric_name.lower().replace(' ', '_').replace('-', '_')
        if fallback_name in available_metrics:
            return fallback_name
        
        # Return original name if no mapping found
        return metric_name

    async def _evaluate_single_metric(
        self, 
        item: DatasetItem, 
        config, 
        available_metrics: Dict[str, Any]
    ) -> MetricScore:
        """
        Evaluate a single metric for a dataset item.
        
        Args:
            item: Dataset item to evaluate
            config: Metric configuration
            available_metrics: Available metric instances
            
        Returns:
            Metric score with pass/fail determination
        """
        original_metric_name = config.metric_name
        threshold = config.threshold
        
        # Normalize metric name to handle naming variations
        metric_name = self._normalize_metric_name(original_metric_name, available_metrics)
        
        # Find metric instance
        metric = available_metrics.get(metric_name)
        if not metric:
            logger.warning(f"Metric '{original_metric_name}' (normalized to '{metric_name}') not found in registry")
            logger.debug(f"Available metrics: {list(available_metrics.keys())}")
            return MetricScore(
                metric_name=original_metric_name,  # Keep original name in result
                score=0.0,
                reason=f"Metric '{original_metric_name}' not available in registry",
                passed=False,
                details={
                    'original_name': original_metric_name,
                    'normalized_name': metric_name,
                    'available_metrics': list(available_metrics.keys()),
                    'failure_category': 'metric_not_found',
                    'evaluation_attempted': False
                }
            )
        
        try:
            # Run metric evaluation
            score = metric.evaluate(item)
            
            # Trust the evaluator's own passed determination - don't override!
            # Different evaluator types use different threshold logic:
            # - Safety: raw_score <= threshold (0-7 scale, lower = safer)
            # - Model-based: score >= threshold (1-5 Likert, higher = better) 
            # - Statistical: score >= threshold (0-1 scale, higher = better)
            passed = score.passed
            
            # Update score with pass/fail info
            return MetricScore(
                metric_name=original_metric_name,  # Keep original name in result
                score=score.score,
                reason=score.reason,
                passed=passed,
                details={
                    **score.details,
                    'threshold': threshold,
                    'passed': passed
                }
            )
            
        except Exception as e:
            error_type = type(e).__name__
            error_message = str(e)
            logger.error(f"Error evaluating metric {original_metric_name} (normalized to {metric_name}): {error_message}")
            return MetricScore(
                metric_name=original_metric_name,  # Keep original name in result
                score=0.0,
                reason=f"Evaluation error ({error_type}): {error_message}",
                passed=False,
                details={
                    'original_name': original_metric_name,
                    'normalized_name': metric_name,
                    'error_type': error_type,
                    'error_message': error_message,
                    'threshold': threshold,
                    'failure_category': 'metric_evaluation_error',
                    'evaluation_attempted': True
                }
            )
    
    def _generate_summary(
        self,
        queue_message: QueueMessage,
        results: List[DatasetItemResult],
        metrics_config: List,
        execution_time: float,
        metrics_response
    ) -> EvaluationSummary:
        """
        Generate evaluation summary with statistics.
        
        Args:
            queue_message: Original queue message
            results: Evaluation results
            metrics_config: Metrics configuration
            execution_time: Execution time in seconds
            
        Returns:
            Evaluation summary
        """
        # Calculate per-metric summaries
        metric_summaries = []
        
        for config in metrics_config:
            metric_name = config.metric_name
            
            # Collect scores for this metric across all items
            scores = []
            passed_count = 0
            
            for result in results:
                for score in result.metric_scores:
                    if score.metric_name == metric_name:
                        scores.append(score.score)
                        if score.passed:
                            passed_count += 1
                        break
            
            if scores:
                average_score = sum(scores) / len(scores)
                total_count = len(scores)
                failed_count = total_count - passed_count
                pass_percentage = (passed_count / total_count) * 100 if total_count > 0 else 0
            else:
                average_score = 0.0
                total_count = 0
                passed_count = 0
                failed_count = 0
                pass_percentage = 0.0
            
            metric_summary = MetricSummary(
                metric_name=metric_name,
                category_name=config.category_name,
                threshold=config.threshold,
                average_score=average_score,
                passed_count=passed_count,
                failed_count=failed_count,
                total_count=total_count,
                pass_percentage=pass_percentage
            )
            metric_summaries.append(metric_summary)
        
        # Calculate overall pass percentage
        total_evaluations = sum(summary.total_count for summary in metric_summaries)
        total_passed = sum(summary.passed_count for summary in metric_summaries)
        overall_pass_percentage = (total_passed / total_evaluations * 100) if total_evaluations > 0 else 0
        
        # Use agent_id from metrics_response since queue_message no longer contains it
        agent_id = metrics_response.agent_id
        
        return EvaluationSummary(
            eval_run_id=queue_message.eval_run_id,
            agent_id=agent_id,
            total_prompts=len(results),
            execution_time_seconds=execution_time,
            metric_summaries=metric_summaries,
            overall_pass_percentage=overall_pass_percentage
        )
    
    async def _store_results(
        self,
        agent_id: str,
        eval_run_id: str,
        summary: EvaluationSummary,
        results: List[DatasetItemResult]
    ) -> bool:
        """
        Post evaluation results to the API instead of storing in Azure Blob Storage.
        
        Args:
            agent_id: Agent ID for the evaluation
            eval_run_id: Evaluation run ID
            summary: Evaluation summary
            results: Detailed results
            
        Returns:
            bool: True if successful, raises exception if failed
        """
        try:
            # Prepare results data for API according to swagger specification
            # The C# API expects JsonElement types, which means we need to send actual JSON objects, not strings
            
            summary_dict = summary.to_dict()
            detailed_results = [result.to_dict() for result in results]
            
            # Validate the data before sending
            if not summary_dict:
                raise ValueError("Summary dictionary is empty")
            if not detailed_results:
                raise ValueError("Detailed results list is empty")
            
            # Send JSON objects directly (not JSON strings) as required by JsonElement
            results_data = {
                'evaluationResultSummary': summary_dict,  # Send as dict/object, not string
                'evaluationResultDataset': detailed_results  # Send as list/array, not string
            }
            
            logger.info(f"Prepared results data: summary_keys={list(summary_dict.keys())}, dataset_items={len(detailed_results)}")
            
            # Post results to API
            logger.info(f"Posting evaluation results to API. Summary dict with {len(summary_dict)} keys, Dataset with {len(detailed_results)} items")
            success = await self.api_client.post_evaluation_results(eval_run_id, results_data)
            
            if success:
                logger.info(f"Successfully posted evaluation results to API for eval run: {eval_run_id}")
                return True
            else:
                logger.error(f"API call returned False - check API response logs above for details")
                raise Exception("Failed to post evaluation results to API")
            
        except Exception as e:
            logger.error(f"Error posting results to API: {str(e)}")
            raise

    async def _update_evaluation_status(self, eval_run_id: str, status: str) -> bool:
        """
        Update evaluation run status via API.
        
        Args:
            eval_run_id: Evaluation run ID
            status: Status to update to (e.g., "EvalRunCompleted")
            
        Returns:
            bool: True if successful, raises exception if failed
        """
        try:
            success = await self.api_client.update_evaluation_status(eval_run_id, status)
            if success:
                logger.info(f"Successfully updated status to '{status}' for eval run: {eval_run_id}")
                return True
            else:
                logger.warning(f"Failed to update status for eval run: {eval_run_id}")
                raise Exception(f"Failed to update status to '{status}' for eval run: {eval_run_id}")
        except Exception as e:
            logger.error(f"Error updating evaluation status: {str(e)}")
            raise


# Global instance
evaluation_engine = EvaluationEngine()