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
from ..services.azure_storage import get_blob_service
from ..models.eval_models import (
    QueueMessage, Dataset, EnrichedDatasetResponse, MetricsConfigurationResponse, EvaluationConfig,
    DatasetItem, MetricScore, DatasetItemResult, MetricSummary, EvaluationSummary
)
from ..metrics.simple_interface import registry
from ..utils.logging_helper import (
    log_operation_start, log_operation_success, log_operation_error, log_evaluation_result,
    trace_operation, add_span_event, set_span_status
)

logger = logging.getLogger(__name__)


class EvaluationEngine:
    """Main evaluation engine for processing queue messages and running evaluations."""
    
    def __init__(self):
        """Initialize the evaluation engine."""
        self.api_client = api_client
        self.get_blob_service = get_blob_service
        self._processing_lock = asyncio.Lock()  # Ensure only one evaluation runs at a time
        
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
            
            # Step tracking
            steps_completed = []
            
            try:
                # Start distributed tracing for the entire evaluation
                with trace_operation(
                    "evaluation_processing",
                    eval_run_id=eval_run_id,
                    metrics_configuration_id=metrics_configuration_id,
                    enriched_dataset_id=queue_message.enriched_dataset_id,
                    agent_id=queue_message.agent_id,
                    dataset_id=queue_message.dataset_id,
                    priority=queue_message.priority
                ) as main_span:
                    # Log evaluation start with complete context
                    log_operation_start(
                    logger,
                    "evaluation_processing",
                    eval_run_id=eval_run_id,
                    metrics_configuration_id=metrics_configuration_id,
                    enriched_dataset_id=queue_message.enriched_dataset_id,
                    agent_id=queue_message.agent_id,
                    dataset_id=queue_message.dataset_id,
                    priority=queue_message.priority,
                    requested_at=str(queue_message.requested_at)
                )
                
                add_span_event(main_span, "evaluation.started")
                
                # Step 1: Fetch dataset and metrics configuration
                with trace_operation(
                    "fetch_data_step",
                    eval_run_id=eval_run_id,
                    step_number=1,
                    step_name="fetch_dataset_and_metrics"
                ) as step1_span:
                    
                    log_operation_start(
                        logger,
                        "fetch_data_step",
                        eval_run_id=eval_run_id,
                        step_number=1,
                        step_name="fetch_dataset_and_metrics"
                    )
                    
                    try:
                        dataset_data, metrics_config_data = await asyncio.gather(
                            self.api_client.fetch_enriched_dataset(eval_run_id),
                            self.api_client.fetch_metrics_configuration(metrics_configuration_id)
                        )
                        
                        if not dataset_data:
                            raise ValueError("API returned empty/null dataset response")
                        if not metrics_config_data:
                            raise ValueError("API returned empty/null metrics configuration response")
                        
                        add_span_event(step1_span, "data.fetched", 
                                     dataset_size=len(str(dataset_data)) if dataset_data else 0,
                                     metrics_config_size=len(str(metrics_config_data)) if metrics_config_data else 0)
                        
                        # Log successful data fetch with details
                        from ..utils.logging_helper import log_structured
                        log_structured(
                            logger,
                            logging.INFO,
                            f"Successfully fetched data from APIs",
                            eval_run_id=eval_run_id,
                            step_number=1,
                            dataset_response_size=len(str(dataset_data)) if dataset_data else 0,
                            metrics_config_response_size=len(str(metrics_config_data)) if metrics_config_data else 0,
                            raw_metrics_config=metrics_config_data  # Log complete metrics config for debugging
                        )
                        
                        steps_completed.append("fetch_data")
                        set_span_status(step1_span, True)
                        log_operation_success(logger, "fetch_data_step", eval_run_id=eval_run_id, step_number=1)
                        
                    except Exception as e:
                        # Log detailed step failure
                        set_span_status(step1_span, False, str(e))
                        add_span_event(step1_span, "error.occurred", error_type=type(e).__name__, error_message=str(e))
                        
                        log_operation_error(
                            logger,
                            "fetch_data_step",
                            e,
                            eval_run_id=eval_run_id,
                            step_number=1,
                            step_name="fetch_dataset_and_metrics",
                            metrics_configuration_id=metrics_configuration_id,
                            error_details=str(e),
                            stack_trace=traceback.format_exc()
                        )
                        raise
                
                # Step 2: Parse the data
                with trace_operation(
                    "parse_data_step",
                    eval_run_id=eval_run_id,
                    step_number=2,
                    step_name="parse_dataset_and_metrics"
                ) as step2_span:
                    
                    log_operation_start(
                        logger,
                        "parse_data_step",
                        eval_run_id=eval_run_id,
                        step_number=2,
                        step_name="parse_dataset_and_metrics"
                    )
                    
                    try:
                        enriched_dataset_response = EnrichedDatasetResponse.from_json(dataset_data)
                        dataset = enriched_dataset_response.to_dataset()
                        metrics_response = MetricsConfigurationResponse.from_json(metrics_config_data)
                        
                        add_span_event(step2_span, "data.parsed", 
                                     dataset_items=len(dataset.items),
                                     metrics_count=len(metrics_response.metrics_configuration))
                        
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
                        
                        log_structured(
                            logger,
                            logging.INFO,
                            f"Successfully parsed evaluation data",
                            eval_run_id=eval_run_id,
                            step_number=2,
                            dataset_items_count=len(dataset.items),
                            metrics_count=len(metrics_response.metrics_configuration),
                            parsed_metrics=parsed_metrics
                        )
                        
                        steps_completed.append("parse_data")
                        set_span_status(step2_span, True)
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
                        set_span_status(step2_span, False, str(e))
                        add_span_event(step2_span, "error.occurred", error_type=type(e).__name__, error_message=str(e))
                        
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
                with trace_operation(
                    "update_status_started_step",
                    eval_run_id=eval_run_id,
                    step_number=3,
                    step_name="update_status_started"
                ) as step3_span:
                    
                    log_operation_start(
                        logger,
                        "update_status_started_step",
                        eval_run_id=eval_run_id,
                        step_number=3,
                        step_name="update_status_started",
                        target_status="EvalRunStarted"
                    )
                    
                    try:
                        await self._update_evaluation_status(eval_run_id, "EvalRunStarted")
                        
                        add_span_event(step3_span, "status.updated", 
                                     new_status="EvalRunStarted")
                        
                        steps_completed.append("update_status_started")
                        set_span_status(step3_span, True)
                        log_operation_success(
                            logger, 
                            "update_status_started_step", 
                            eval_run_id=eval_run_id, 
                            step_number=3,
                            status_updated_to="EvalRunStarted"
                        )
                        
                    except Exception as e:
                        set_span_status(step3_span, False, str(e))
                        add_span_event(step3_span, "error.occurred", 
                                     error_type=type(e).__name__, 
                                     error_message=str(e))
                        
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
                with trace_operation(
                    "run_evaluations_step",
                    eval_run_id=eval_run_id,
                    step_number=4,
                    step_name="run_evaluations"
                ) as step4_span:
                    
                    log_operation_start(
                        logger,
                        "run_evaluations_step",
                        eval_run_id=eval_run_id,
                        step_number=4,
                        step_name="run_evaluations",
                        items_to_evaluate=len(dataset.items),
                        metrics_to_run=len(metrics_response.metrics_configuration)
                    )
                    
                    try:
                        results = await self._run_evaluations(dataset, metrics_response.metrics_configuration)
                        
                        add_span_event(step4_span, "evaluations.completed", 
                                     items_processed=len(results))
                        
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
                            log_structured(
                                logger,
                                logging.INFO,
                                f"Evaluation execution completed successfully - at least one metric succeeded",
                                eval_run_id=eval_run_id,
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
                            set_span_status(step4_span, True)
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
                            log_structured(
                                logger,
                                logging.ERROR,
                                f"Evaluation execution failed - ALL metrics failed",
                                eval_run_id=eval_run_id,
                                step_number=4,
                                total_items_processed=len(results),
                                total_scores_computed=total_scores,
                                successful_evaluations=successful_evaluations,
                                failed_evaluations=failed_evaluations,
                                failed_metrics_details=failed_metrics_details,
                                step_result="TOTAL_FAILURE"
                            )
                            
                            # Don't add to steps_completed - this will mark the step as failed
                            set_span_status(step4_span, False, f"All {len(failed_metrics_details)} metrics failed")
                            add_span_event(step4_span, "all_metrics_failed", 
                                         failed_metrics_count=len(failed_metrics_details),
                                         failed_metrics_details=failed_metrics_details)
                            
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
                        set_span_status(step4_span, False, str(e))
                        add_span_event(step4_span, "error.occurred", error_type=type(e).__name__, error_message=str(e))
                        
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
                with trace_operation(
                    "generate_summary_step",
                    eval_run_id=eval_run_id,
                    step_number=5,
                    step_name="generate_summary"
                ) as step5_span:
                    
                    log_operation_start(
                        logger,
                        "generate_summary_step",
                        eval_run_id=eval_run_id,
                        step_number=5,
                        step_name="generate_summary"
                    )
                    
                    try:
                        execution_time = time.time() - start_time
                        summary = self._generate_summary(
                            queue_message, 
                            results, 
                            metrics_response.metrics_configuration,
                            execution_time
                        )
                        
                        add_span_event(step5_span, "summary.generated", 
                                     execution_time=execution_time,
                                     metrics_count=len(summary.metric_summaries))
                        
                        # Log summary generation details
                        log_structured(
                            logger,
                            logging.INFO,
                            f"Successfully generated evaluation summary",
                            eval_run_id=eval_run_id,
                            step_number=5,
                            execution_time_seconds=execution_time,
                            summary_metrics_count=len(summary.metric_summaries),
                            overall_pass_percentage=summary.overall_pass_percentage
                        )
                        
                        steps_completed.append("generate_summary")
                        set_span_status(step5_span, True)
                        log_operation_success(
                            logger, 
                            "generate_summary_step", 
                            eval_run_id=eval_run_id, 
                            step_number=5,
                            execution_time_seconds=execution_time
                        )
                        
                    except Exception as e:
                        set_span_status(step5_span, False, str(e))
                        add_span_event(step5_span, "error.occurred", error_type=type(e).__name__, error_message=str(e))
                        
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
                
                # Step 6: Store results
                with trace_operation(
                    "store_results_step",
                    eval_run_id=eval_run_id,
                    step_number=6,
                    step_name="store_results"
                ) as step6_span:
                    
                    log_operation_start(
                        logger,
                        "store_results_step",
                        eval_run_id=eval_run_id,
                        step_number=6,
                        step_name="store_results",
                        agent_id=queue_message.agent_id
                    )
                    
                    try:
                        await self._store_results(queue_message.agent_id, eval_run_id, summary, results)
                        
                        add_span_event(step6_span, "results.stored", 
                                     agent_id=queue_message.agent_id,
                                     results_count=len(results))
                        
                        log_structured(
                            logger,
                            logging.INFO,
                            f"Successfully stored evaluation results",
                            eval_run_id=eval_run_id,
                            step_number=6,
                            agent_id=queue_message.agent_id,
                            results_count=len(results),
                            summary_generated=True
                        )
                        
                        steps_completed.append("store_results")
                        set_span_status(step6_span, True)
                        log_operation_success(
                            logger, 
                            "store_results_step", 
                            eval_run_id=eval_run_id, 
                            step_number=6,
                            agent_id=queue_message.agent_id
                        )
                        
                    except Exception as e:
                        set_span_status(step6_span, False, str(e))
                        add_span_event(step6_span, "error.occurred", error_type=type(e).__name__, error_message=str(e))
                        
                        log_operation_error(
                            logger,
                            "store_results_step",
                            e,
                            eval_run_id=eval_run_id,
                            step_number=6,
                            step_name="store_results",
                            agent_id=queue_message.agent_id,
                            error_details=str(e),
                            stack_trace=traceback.format_exc()
                        )
                        raise
                
                # Step 7: Update status to completed
                with trace_operation(
                    "update_status_step",
                    eval_run_id=eval_run_id,
                    step_number=7,
                    step_name="update_status"
                ) as step7_span:
                    
                    log_operation_start(
                        logger,
                        "update_status_step",
                        eval_run_id=eval_run_id,
                        step_number=7,
                        step_name="update_status",
                        target_status="EvalRunCompleted"
                    )
                    
                    try:
                        await self._update_evaluation_status(eval_run_id, "EvalRunCompleted")
                        
                        add_span_event(step7_span, "status.updated", 
                                     new_status="EvalRunCompleted")
                        
                        steps_completed.append("update_status")
                        set_span_status(step7_span, True)
                        log_operation_success(
                            logger, 
                            "update_status_step", 
                            eval_run_id=eval_run_id, 
                            step_number=7,
                            status_updated_to="EvalRunCompleted"
                        )
                        
                    except Exception as e:
                        # Don't raise here - status update failure shouldn't prevent message deletion
                        # if all other critical steps succeeded
                        set_span_status(step7_span, False, str(e))
                        add_span_event(step7_span, "error.occurred", 
                                     error_type=type(e).__name__, 
                                     error_message=str(e),
                                     severity="warning")
                        
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
                            stack_trace=traceback.format_exc()
                        )
                
                # All steps completed - wrap up the evaluation
                execution_time = time.time() - start_time
                
                # Return True only if all critical steps completed (status update is not critical)
                critical_steps = ["fetch_data", "parse_data", "run_evaluations", "generate_summary", "store_results"]
                all_critical_steps_completed = all(step in steps_completed for step in critical_steps)
                
                if all_critical_steps_completed:
                    # Log overall success with complete telemetry
                    add_span_event(main_span, "evaluation.completed_successfully", 
                                 total_steps=len(steps_completed),
                                 execution_time=execution_time)
                    
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
                    
                    set_span_status(main_span, True)
                else:
                    # Log partial failure
                    missing_steps = [step for step in critical_steps if step not in steps_completed]
                    add_span_event(main_span, "evaluation.partially_completed", 
                                 completed_steps=len(steps_completed),
                                 missing_steps=missing_steps)
                    
                    log_operation_error(
                        logger,
                        "evaluation_processing", 
                        Exception(f"Not all critical steps completed"),
                        eval_run_id=eval_run_id,
                        completed_steps=steps_completed,
                        missing_critical_steps=missing_steps,
                        execution_time_seconds=execution_time
                    )

                    set_span_status(main_span, False, f"Missing steps: {missing_steps}")
                
                return all_critical_steps_completed
            
            except Exception as e:
                # Handle any unhandled exceptions to ensure we always return a boolean
                eval_run_id = getattr(queue_message, 'eval_run_id', 'unknown')
                logger.error(f"❌ Unhandled exception in process_queue_message for {eval_run_id}: {str(e)}")
                logger.error(f"Exception type: {type(e).__name__}")
                logger.error(f"Stack trace: {traceback.format_exc()}")
                
                # Log structured error for debugging
                from ..utils.logging_helper import log_structured
                log_structured(
                    logger,
                    logging.ERROR,
                    f"Unhandled exception in evaluation processing",
                    eval_run_id=eval_run_id,
                    error_type=type(e).__name__,
                    error_message=str(e),
                    stack_trace=traceback.format_exc(),
                    critical_failure=True
                )
                
                # Always return False for unhandled exceptions
                return False
    
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
        dataset_semaphore = asyncio.Semaphore(3)  # Limit concurrent dataset items to avoid resource overload
        
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
        semaphore = asyncio.Semaphore(8)  # Increased from 5 to 8 for better throughput
        
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
            
            # Determine pass/fail based on threshold
            passed = score.score >= threshold
            
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
        execution_time: float
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
        
        return EvaluationSummary(
            eval_run_id=queue_message.eval_run_id,
            agent_id=queue_message.agent_id,
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
    ) -> None:
        """
        Store evaluation results in Azure Blob Storage.
        
        Args:
            agent_id: Agent ID for container
            eval_run_id: Evaluation run ID
            summary: Evaluation summary
            results: Detailed results
        """
        try:
            # Get blob service
            blob_service = self.get_blob_service()
            
            # Store summary
            summary_url = await blob_service.upload_evaluation_results(
                agent_id, 
                eval_run_id, 
                summary.to_dict()
            )
            logger.info(f"Stored summary at: {summary_url}")
            
            # Store detailed dataset
            detailed_data = {
                'evalRunId': eval_run_id,
                'agentId': agent_id,
                'results': [result.to_dict() for result in results],
                'timestamp': datetime.now().isoformat()
            }
            
            dataset_url = await blob_service.upload_detailed_dataset(
                agent_id,
                eval_run_id,
                detailed_data
            )
            logger.info(f"Stored detailed dataset at: {dataset_url}")
            
        except Exception as e:
            logger.error(f"Error storing results: {str(e)}")
            raise

    async def _update_evaluation_status(self, eval_run_id: str, status: str) -> None:
        """
        Update evaluation run status via API.
        
        Args:
            eval_run_id: Evaluation run ID
            status: Status to update to (e.g., "EvalRunCompleted")
        """
        try:
            success = await self.api_client.update_evaluation_status(eval_run_id, status)
            if success:
                logger.info(f"Successfully updated status to '{status}' for eval run: {eval_run_id}")
            else:
                logger.warning(f"Failed to update status for eval run: {eval_run_id}")
        except Exception as e:
            logger.error(f"Error updating evaluation status: {str(e)}")
            # Don't raise - we don't want to fail the entire evaluation just for a status update


# Global instance
evaluation_engine = EvaluationEngine()