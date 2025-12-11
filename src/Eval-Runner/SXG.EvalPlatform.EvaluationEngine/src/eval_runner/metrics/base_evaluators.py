"""
Base classes for Azure AI evaluators to reduce code duplication.
"""

from abc import ABC, abstractmethod
from typing import Any, Dict, Optional, Type, TypeVar
import logging
import time
from opentelemetry import trace, metrics

from azure.ai.evaluation import (
    AzureOpenAIModelConfiguration, 
    IntentResolutionEvaluator as AzureIntentResolutionEvaluator,
    ToolCallAccuracyEvaluator as AzureToolCallAccuracyEvaluator,
    TaskAdherenceEvaluator as AzureTaskAdherenceEvaluator,
    RelevanceEvaluator as AzureRelevanceEvaluator,
    CoherenceEvaluator as AzureCoherenceEvaluator,
    GroundednessEvaluator as AzureGroundednessEvaluator,
    FluencyEvaluator as AzureFluencyEvaluator,
    ResponseCompletenessEvaluator as AzureResponseCompletenessEvaluator,
    HateUnfairnessEvaluator as AzureHateUnfairnessEvaluator,
    SexualEvaluator as AzureSexualEvaluator,
    ViolenceEvaluator as AzureViolenceEvaluator,
    SelfHarmEvaluator as AzureSelfHarmEvaluator,
    SimilarityEvaluator as AzureSimilarityEvaluator,
    BleuScoreEvaluator as AzureBleuScoreEvaluator,
    GleuScoreEvaluator as AzureGleuScoreEvaluator,
    MeteorScoreEvaluator as AzureMeteorScoreEvaluator,
)

from ..azure_ai_config import azure_ai_config
from ..models.eval_models import DatasetItem
from ..exceptions import ConfigurationError
from .evaluation_result import EvaluationResult

logger = logging.getLogger(__name__)

# OpenTelemetry setup
tracer = trace.get_tracer(__name__)
meter = metrics.get_meter(__name__)

# Custom metrics for evaluator performance
evaluator_duration = meter.create_histogram(
    "evaluator_duration_seconds",
    description="Time taken for evaluator execution",
    unit="s"
)

evaluator_success_counter = meter.create_counter(
    "evaluator_success_total",
    description="Total number of successful evaluator executions"
)

evaluator_failure_counter = meter.create_counter(
    "evaluator_failure_total", 
    description="Total number of failed evaluator executions"
)

T = TypeVar('T')


class BaseAzureEvaluator(ABC):
    """Base class for all Azure AI evaluators."""
    
    def __init__(self, name: str):
        """
        Initialize the base evaluator.
        
        Args:
            name: Name of the evaluator
        """
        self.name = name
        self.threshold = azure_ai_config.get_evaluator_threshold(name)
        self._evaluator: Optional[Any] = None
    
    @abstractmethod
    def _create_evaluator(self) -> Any:
        """Create the Azure AI evaluator instance."""
        pass
    
    @abstractmethod
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """
        Extract score, reasoning, and pass status from Azure result.
        
        Args:
            azure_result: Result from Azure AI evaluator
            
        Returns:
            Tuple of (score, reasoning, passed)
        """
        pass
    
    def _get_evaluator(self) -> Any:
        """Get or create the Azure AI evaluator instance."""
        if self._evaluator is None:
            self._evaluator = self._create_evaluator()
        return self._evaluator
    
    def evaluate(self, item: DatasetItem, **kwargs) -> EvaluationResult:
        """
        Evaluate a dataset item using Azure AI evaluator with comprehensive telemetry.
        
        Args:
            item: Dataset item to evaluate
            **kwargs: Additional evaluation parameters
            
        Returns:
            Evaluation result with score and reasoning
        """
        start_time = time.time()
        
        with tracer.start_as_current_span(
            f"evaluator.{self.name}.evaluate",
            attributes={
                "evaluator.name": self.name,
                "evaluator.threshold": self.threshold,
                "item.has_prompt": bool(item.prompt),
                "item.has_response": bool(item.actual_response),
                "item.has_ground_truth": bool(item.ground_truth),
                "item.has_context": bool(getattr(item, 'context', None)),
                "kwargs.count": len(kwargs)
            }
        ) as span:
            try:
                evaluator = self._get_evaluator()
                azure_result = self._call_evaluator(evaluator, item, **kwargs)
                
                score, reasoning, passed = self._extract_result(azure_result)
                
                # Calculate duration
                duration = time.time() - start_time
                
                # Record success metrics
                evaluator_success_counter.add(
                    1,
                    attributes={
                        "evaluator.name": self.name,
                        "evaluator.type": self.__class__.__bases__[0].__name__,
                        "result.passed": str(passed)
                    }
                )
                
                evaluator_duration.record(
                    duration,
                    attributes={
                        "evaluator.name": self.name,
                        "evaluator.type": self.__class__.__bases__[0].__name__,
                        "result.status": "success"
                    }
                )
                
                # Add span attributes for success
                span.set_attributes({
                    "evaluator.score": score,
                    "evaluator.passed": passed,
                    "evaluator.duration_seconds": duration,
                    "evaluator.result.status": "success"
                })
                
                # Structured logging for success
                logger.info(
                    f"Evaluator success: {self.name}",
                    extra={
                        "evaluator_name": self.name,
                        "evaluator_type": self.__class__.__bases__[0].__name__,
                        "score": score,
                        "threshold": self.threshold,
                        "passed": passed,
                        "duration_seconds": duration,
                        "reasoning_length": len(reasoning),
                        "azure_result_keys": list(azure_result.keys()) if azure_result else []
                    }
                )
                
                details = {
                    "threshold": self.threshold,
                    "passed": passed,
                    "azure_ai_result": azure_result,
                    "duration_seconds": duration
                }
                
                return EvaluationResult(
                    score=score,
                    reasoning=reasoning,
                    details=details
                )
                
            except Exception as e:
                # Calculate duration for failed evaluation
                duration = time.time() - start_time
                
                # Determine error type for better categorization
                error_type = type(e).__name__
                error_category = self._categorize_error(e)
                
                # Record failure metrics
                evaluator_failure_counter.add(
                    1,
                    attributes={
                        "evaluator.name": self.name,
                        "evaluator.type": self.__class__.__bases__[0].__name__,
                        "error.type": error_type,
                        "error.category": error_category
                    }
                )
                
                evaluator_duration.record(
                    duration,
                    attributes={
                        "evaluator.name": self.name,
                        "evaluator.type": self.__class__.__bases__[0].__name__,
                        "result.status": "failure"
                    }
                )
                
                # Add span attributes for failure
                span.set_attributes({
                    "evaluator.duration_seconds": duration,
                    "evaluator.result.status": "failure",
                    "error.type": error_type,
                    "error.category": error_category,
                    "error.message": str(e)[:500]  # Truncate long error messages
                })
                span.record_exception(e)
                span.set_status(trace.Status(trace.StatusCode.ERROR, str(e)))
                
                # Comprehensive structured logging for failures
                logger.error(
                    f"Evaluator failure: {self.name} - {error_category}",
                    extra={
                        "evaluator_name": self.name,
                        "evaluator_type": self.__class__.__bases__[0].__name__,
                        "error_type": error_type,
                        "error_category": error_category,
                        "error_message": str(e),
                        "duration_seconds": duration,
                        "threshold": self.threshold,
                        "item_has_prompt": bool(item.prompt),
                        "item_has_response": bool(item.actual_response),
                        "item_has_ground_truth": bool(item.ground_truth),
                        "item_prompt_length": len(item.prompt) if item.prompt else 0,
                        "item_response_length": len(item.actual_response) if item.actual_response else 0,
                        "kwargs_provided": list(kwargs.keys()) if kwargs else []
                    },
                    exc_info=True
                )
                
                # Re-raise the exception - no fake scores!
                raise
    
    @abstractmethod
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """
        Call the Azure AI evaluator with appropriate parameters.
        
        Args:
            evaluator: Azure AI evaluator instance
            item: Dataset item to evaluate
            **kwargs: Additional parameters
            
        Returns:
            Azure AI evaluation result
        """
        pass

    def _categorize_error(self, error: Exception) -> str:
        """
        Categorize errors for better telemetry and debugging.
        
        Args:
            error: The exception to categorize
            
        Returns:
            Error category string for telemetry
        """
        error_msg = str(error).lower()
        error_type = type(error).__name__
        
        # Configuration and setup errors
        if isinstance(error, ConfigurationError):
            return "configuration_error"
        
        # Azure AI connectivity issues
        if "404" in error_msg or "failed to connect" in error_msg:
            if "region" in error_msg and "content" in error_msg:
                return "unsupported_region"
            elif "project" in error_msg:
                return "project_not_found"
            else:
                return "azure_ai_connectivity"
        
        # Authentication and authorization
        if "401" in error_msg or "unauthorized" in error_msg:
            return "authentication_error"
        if "403" in error_msg or "forbidden" in error_msg:
            return "authorization_error"
        
        # Rate limiting and quotas
        if "429" in error_msg or "rate limit" in error_msg or "quota" in error_msg:
            return "rate_limit_exceeded"
        
        # Timeout issues
        if "timeout" in error_msg or "timed out" in error_msg:
            return "timeout_error"
        
        # Data validation errors
        if isinstance(error, ValueError):
            if "requires" in error_msg and "evaluation" in error_msg:
                return "missing_required_data"
            return "data_validation_error"
        
        # Network-related issues
        if "network" in error_msg or "connection" in error_msg:
            return "network_error"
        
        # Model/API specific errors
        if "model" in error_msg or "deployment" in error_msg:
            return "model_error"
        
        # Catch-all for unknown errors
        return f"unknown_error_{error_type.lower()}"


class ModelBasedEvaluator(BaseAzureEvaluator):
    """Base class for model-based evaluators (LLM-judge)."""
    
    def _create_evaluator(self) -> Any:
        """Create evaluator with model configuration and credential."""
        try:
            evaluator_class = self._get_evaluator_class()
            
            # Get the raw model config dictionary
            model_config_dict = azure_ai_config.model_config
            
            logger.info(f"Creating {self.name} evaluator with config: {model_config_dict}")
            
            # Create AzureOpenAIModelConfiguration - it might be a TypedDict
            model_config = AzureOpenAIModelConfiguration(
                azure_endpoint=model_config_dict["azure_endpoint"],
                azure_deployment=model_config_dict["azure_deployment"], 
                api_version=model_config_dict["api_version"]
            )
            
            logger.info(f"Created AzureOpenAIModelConfiguration for {self.name}: endpoint={model_config_dict['azure_endpoint']}, deployment={model_config_dict['azure_deployment']}")
            
            # Create evaluator with model config and credential for managed identity
            return evaluator_class(
                model_config=model_config,
                credential=azure_ai_config.credential,
                threshold=self.threshold  # Preserve float precision for thresholds like 3.0
            )
            
        except Exception as e:
            logger.error(f"Failed to create {self.name} evaluator: {e}")
            logger.error(f"Model config dict: {azure_ai_config.model_config}")
            raise
    
    @abstractmethod
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        pass
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Default implementation for model-based evaluators."""
        return evaluator(
            query=item.prompt,
            response=item.actual_response
        )
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Default implementation for model-based evaluators using Microsoft's Likert scale logic."""
        # Model-based evaluators (Coherence, Fluency, Relevance, etc.) use 1-5 Likert scale
        score = float(azure_result.get(self.name, 0.0))
        reason_key = f"{self.name}_reason" if f"{self.name}_reason" in azure_result else f"gpt_{self.name}_reason"
        reasoning = str(azure_result.get(reason_key, "No reasoning provided"))
        
        # Use Microsoft's standard: score >= threshold for Likert scale evaluators
        passed = score >= self.threshold
        
        logger.debug(f"{self.name}: score={score}, threshold={self.threshold}, passed={passed}")
        
        return score, reasoning, passed


class SafetyEvaluator(BaseAzureEvaluator):
    """Base class for safety evaluators."""
    
    def __init__(self, name: str):
        """Initialize the safety evaluator."""
        super().__init__(name)
    
    def _create_evaluator(self) -> Any:
        """Create evaluator with Azure AI project configuration and credential."""
        evaluator_class = self._get_evaluator_class()
        return evaluator_class(
            azure_ai_project=azure_ai_config.azure_ai_project,
            credential=azure_ai_config.credential,
            threshold=int(self.threshold)
        )
    
    @abstractmethod
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        pass
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Default implementation for safety evaluators with enhanced error telemetry."""
        try:
            return evaluator(
                query=item.prompt,
                response=item.actual_response
            )
        except Exception as e:
            error_msg = str(e)
            
            # Enhanced structured logging with context
            logger.error(
                f"Azure AI safety evaluation failed: {self.name}",
                extra={
                    "evaluator_name": self.name,
                    "evaluator_type": "SafetyEvaluator",
                    "error_message": error_msg,
                    "error_type": type(e).__name__,
                    "azure_ai_project": azure_ai_config.azure_ai_project is not None,
                    "has_credentials": azure_ai_config.credential is not None,
                    "threshold": self.threshold,
                    "item_prompt_length": len(item.prompt) if item.prompt else 0,
                    "item_response_length": len(item.actual_response) if item.actual_response else 0
                }
            )
            
            # Provide clear, actionable error messages for common Azure AI issues
            if "404" in error_msg or "Failed to connect to your Azure AI project" in error_msg:
                if "content harm" in error_msg.lower() and "region" in error_msg.lower():
                    raise ConfigurationError(
                        f"{self.name} evaluation failed: Content safety evaluation not supported in this region. "
                        f"Try westus2, westcentralus, or other supported regions. Original error: {error_msg}"
                    )
                elif "404" in error_msg:
                    raise ConfigurationError(
                        f"{self.name} evaluation failed: Azure AI Foundry project not found or not accessible. "
                        f"Check project configuration and permissions. Original error: {error_msg}"
                    )
                else:
                    raise ConfigurationError(
                        f"{self.name} evaluation failed: Azure AI Foundry project connection failed. "
                        f"Verify project configuration and network connectivity. Original error: {error_msg}"
                    )
            else:
                # Re-raise other errors with context
                raise RuntimeError(f"{self.name} evaluation failed: {error_msg}") from e
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract and normalize safety evaluation results according to Microsoft Azure AI documentation."""
        score_key = f"{self.name}"
        reason_key = f"{self.name}_reason"
        
        raw_score_value = azure_result.get(score_key, 7.0)
        reasoning = str(azure_result.get(reason_key, "No reasoning provided"))
        
        # Handle both numeric scores and text severity levels
        if isinstance(raw_score_value, str):
            # Map text severity levels to Azure AI 0-7 numeric scale (per Microsoft docs)
            severity_mapping = {
                "very low": 0.0,   # Azure AI: 0-1 range -> use 0 for "very low"
                "low": 2.0,       # Azure AI: 2-3 range -> use 2 for "low"
                "medium": 4.0,    # Azure AI: 4-5 range -> use 4 for "medium"
                "high": 6.0,      # Azure AI: 6-7 range -> use 6 for "high"
                "very high": 7.0  # Azure AI: highest severity
            }
            raw_score = severity_mapping.get(raw_score_value.lower(), 7.0)
            logger.info(f"{self.name} evaluation: '{raw_score_value}' mapped to Azure AI score {raw_score}")
        else:
            raw_score = float(raw_score_value)
        
        # Use Azure AI standard: 0-7 scale where <= threshold passes (per Microsoft docs)
        # Default threshold is 3: scores 0,1,2,3 pass; scores 4,5,6,7 fail
        passed = raw_score <= self.threshold
        
        # For API compatibility, normalize to 0-1 scale but preserve Azure AI pass/fail logic
        # Higher normalized score = safer content (inverted from Azure's 0-7 scale)
        normalized_score = max(0.0, (7.0 - raw_score) / 7.0)
        
        logger.debug(f"{self.name}: raw_score={raw_score}, threshold={self.threshold}, passed={passed}, normalized={normalized_score:.3f}")
        
        return normalized_score, reasoning, passed


class StatisticalEvaluator(BaseAzureEvaluator):
    """Base class for statistical evaluators (no model required)."""
    
    def _create_evaluator(self) -> Any:
        """Create evaluator without model configuration."""
        evaluator_class = self._get_evaluator_class()
        return evaluator_class()
    
    @abstractmethod  
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        pass
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Default implementation for statistical evaluators."""
        return evaluator(
            response=item.actual_response,
            ground_truth=item.ground_truth
        )
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract statistical evaluation results."""
        score = float(azure_result.get(self.name, 0.0))
        reasoning = f"{self.name.upper()} score of {score:.3f}. {'Good' if score >= self.threshold else 'Limited'} match with ground truth."
        passed = score >= self.threshold
        
        return score, reasoning, passed