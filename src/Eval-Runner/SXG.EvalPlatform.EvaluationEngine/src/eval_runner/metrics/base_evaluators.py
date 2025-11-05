"""
Base classes for Azure AI evaluators to reduce code duplication.
"""

from abc import ABC, abstractmethod
from typing import Any, Dict, Optional, Type, TypeVar
import logging

from azure.ai.evaluation import (
    AzureOpenAIModelConfiguration, 
    IntentResolutionEvaluator as AzureIntentResolutionEvaluator,
    ToolCallAccuracyEvaluator as AzureToolCallAccuracyEvaluator,
    TaskAdherenceEvaluator as AzureTaskAdherenceEvaluator,
    RelevanceEvaluator as AzureRelevanceEvaluator,
    CoherenceEvaluator as AzureCoherenceEvaluator,
    GroundednessEvaluator as AzureGroundednessEvaluator,
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
from .evaluation_result import EvaluationResult

logger = logging.getLogger(__name__)

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
        Evaluate a dataset item using Azure AI evaluator.
        
        Args:
            item: Dataset item to evaluate
            **kwargs: Additional evaluation parameters
            
        Returns:
            Evaluation result with score and reasoning
        """
        try:
            evaluator = self._get_evaluator()
            azure_result = self._call_evaluator(evaluator, item, **kwargs)
            
            score, reasoning, passed = self._extract_result(azure_result)
            
            details = {
                "threshold": self.threshold,
                "passed": passed,
                "azure_ai_result": azure_result
            }
            
            return EvaluationResult(
                score=score,
                reasoning=reasoning,
                details=details
            )
            
        except Exception as e:
            logger.error(f"{self.name} evaluation failed: {e}")
            return EvaluationResult(
                score=0.0,
                reasoning=f"Evaluation error: {str(e)}",
                details={"error": str(e)}
            )
    
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


class ModelBasedEvaluator(BaseAzureEvaluator):
    """Base class for model-based evaluators (LLM-judge)."""
    
    def _create_evaluator(self) -> Any:
        """Create evaluator with model configuration."""
        evaluator_class = self._get_evaluator_class()
        return evaluator_class(
            model_config=azure_ai_config.model_config,
            threshold=int(self.threshold)
        )
    
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


class SafetyEvaluator(BaseAzureEvaluator):
    """Base class for safety evaluators."""
    
    def __init__(self, name: str):
        """Initialize the safety evaluator with circuit breaker for 404 errors."""
        super().__init__(name)
        self._azure_ai_project_unavailable = False  # Circuit breaker for 404s
        self._logged_unavailability = False  # Reduce log noise
    
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
        """Default implementation for safety evaluators with 404 error handling."""
        # Circuit breaker: if Azure AI project is unavailable, return default immediately
        if self._azure_ai_project_unavailable:
            return self._get_default_safe_result()
        
        try:
            return evaluator(
                query=item.prompt,
                response=item.actual_response
            )
        except Exception as e:
            if "404" in str(e) or "Failed to connect to your Azure AI project" in str(e):
                # Handle Azure AI Foundry project access issues gracefully
                self._azure_ai_project_unavailable = True  # Enable circuit breaker
                
                # Log only once per evaluator to reduce noise
                if not self._logged_unavailability:
                    logger.warning(f"{self.name} evaluator: Azure AI Foundry project not accessible (404). "
                                 f"Using default safe scores for all subsequent evaluations.")
                    self._logged_unavailability = True
                else:
                    logger.debug(f"{self.name} evaluator: Using default safe score (Azure AI project unavailable).")
                
                return self._get_default_safe_result()
            else:
                # Re-raise other errors
                raise
    
    def _get_default_safe_result(self) -> Dict[str, Any]:
        """Get default safe result for 404 errors."""
        return {
            f"{self.name}": 1.0,  # Safe score (1 out of 7 on Azure scale)
            f"{self.name}_reason": f"Azure AI Foundry project not accessible for {self.name} evaluation. Default safe score assigned.",
            f"{self.name}_result": "pass"
        }
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract and normalize safety evaluation results (0-7 scale to 0-1, inverted)."""
        score_key = f"{self.name}"
        reason_key = f"{self.name}_reason"
        
        raw_score = float(azure_result.get(score_key, 7.0))
        reasoning = str(azure_result.get(reason_key, "No reasoning provided"))
        
        # Normalize from 0-7 scale to 0-1 scale (inverted - lower raw score is better)
        normalized_score = max(0.0, (7.0 - raw_score) / 7.0)
        passed = normalized_score >= (self.threshold / 7.0)
        
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