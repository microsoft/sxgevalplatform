"""
Azure AI Evaluation interface for unified metrics management.

This module provides a unified interface for all Azure AI evaluation metrics,
including adapters and registry for managing evaluators by category.
"""

import logging
from typing import Any, Dict, List, Optional, Protocol, runtime_checkable

from ..models.eval_models import DatasetItem, MetricScore
from .evaluation_result import EvaluationResult

logger = logging.getLogger(__name__)


@runtime_checkable
class AzureEvaluator(Protocol):
    """Protocol for Azure AI evaluators."""
    
    @property
    def name(self) -> str:
        """Get the evaluator name."""
        ...
    
    def evaluate(self, item: DatasetItem, **kwargs) -> EvaluationResult:
        """Evaluate a dataset item."""
        ...


class MetricAdapter:
    """
    Adapter class to convert Azure AI evaluators to our metric interface.
    """
    
    def __init__(self, evaluator: AzureEvaluator, category: str, description: str = ""):
        """
        Initialize the adapter.
        
        Args:
            evaluator: Azure AI evaluator instance
            category: Category name (agentic, rag, risk_and_safety, text_similarity)
            description: Metric description
        """
        if not isinstance(evaluator, AzureEvaluator):
            raise TypeError(f"evaluator must implement AzureEvaluator protocol, got {type(evaluator)}")
        
        self.evaluator = evaluator
        self.category = category
        self.description = description
    
    @property
    def name(self) -> str:
        """Get the metric name."""
        return self.evaluator.name
    
    def evaluate(self, item: DatasetItem) -> MetricScore:
        """
        Evaluate a dataset item using the Azure AI evaluator.
        
        Args:
            item: Dataset item to evaluate
            
        Returns:
            MetricScore with evaluation results
        """
        try:
            # Call the Azure AI evaluator
            result = self.evaluator.evaluate(item)
            
            # Convert EvaluationResult to MetricScore
            # Use evaluator-specific fallback logic if passed determination not provided
            if result.details and "passed" in result.details:
                passed = result.details["passed"]
            else:
                # Fallback logic based on evaluator type
                if any(safety_name in self.name.lower() for safety_name in ['violence', 'sexual', 'self_harm', 'hate_unfairness']):
                    # Safety evaluators: higher normalized score = safer (inverted scale)
                    passed = result.score >= 0.5
                else:
                    # Model-based and statistical evaluators: higher score = better
                    passed = result.score >= 0.5
            
            return MetricScore(
                metric_name=self.name,
                score=result.score,
                reason=result.reasoning,
                passed=passed,
                details=result.details or {}
            )
            
        except Exception as e:
            logger.error(f"Error evaluating {self.name}: {e}")
            return MetricScore(
                metric_name=self.name,
                score=0.0,
                reason=f"Evaluation failed: {str(e)}",
                passed=False,
                details={"error": str(e)}
            )


class AzureAIMetricsRegistry:
    """
    Registry for managing Azure AI evaluation metrics by category.
    """
    
    def __init__(self):
        """Initialize the registry."""
        self._metrics: Dict[str, Dict[str, MetricAdapter]] = {}
        self._load_all_metrics()
    
    def register_metric(self, metric_adapter: MetricAdapter) -> None:
        """Register a metric adapter in the registry."""
        if metric_adapter.category not in self._metrics:
            self._metrics[metric_adapter.category] = {}
        self._metrics[metric_adapter.category][metric_adapter.name] = metric_adapter
    
    def get_metric(self, category: str, name: str) -> Optional[MetricAdapter]:
        """Get a specific metric by category and name."""
        return self._metrics.get(category, {}).get(name)
    
    def get_metrics_by_category(self, category: str) -> Dict[str, MetricAdapter]:
        """Get all metrics in a category."""
        return self._metrics.get(category, {})
    
    def get_all_metrics(self) -> Dict[str, Dict[str, MetricAdapter]]:
        """Get all registered metrics organized by category."""
        return self._metrics.copy()
    
    def get_all_metrics_flat(self) -> Dict[str, MetricAdapter]:
        """Get all registered metrics in a flat dictionary (for backwards compatibility)."""
        flat_metrics = {}
        for category_metrics in self._metrics.values():
            flat_metrics.update(category_metrics)
        return flat_metrics
    
    def list_categories(self) -> List[str]:
        """List all available categories."""
        return list(self._metrics.keys())
        
    def list_metrics(self, category: Optional[str] = None) -> List[str]:
        """List metric names, optionally filtered by category."""
        if category:
            return list(self._metrics.get(category, {}).keys())
        
        all_names = []
        for cat_metrics in self._metrics.values():
            all_names.extend(cat_metrics.keys())
        return all_names
    
    def _load_all_metrics(self) -> None:
        """Load all available Azure AI metrics from their respective modules."""
        # Import and register agentic metrics
        try:
            from .agentic.evaluators_refactored import (
                IntentResolutionEvaluator, 
                ToolCallAccuracyEvaluator,
                TaskAdherenceEvaluator
            )
            self.register_metric(MetricAdapter(
                IntentResolutionEvaluator(), 
                "agentic",
                "Measures how well the system identifies and understands user intent"
            ))
            self.register_metric(MetricAdapter(
                ToolCallAccuracyEvaluator(),
                "agentic", 
                "Evaluates accuracy and efficiency of tool calls made by the agent"
            ))
            self.register_metric(MetricAdapter(
                TaskAdherenceEvaluator(),
                "agentic",
                "Assesses how well the agent adheres to assigned tasks and instructions"
            ))
        except ImportError as e:
            logger.warning(f"Could not load agentic metrics: {e}")
        
        # Import and register RAG metrics
        try:
            from .rag.evaluators_refactored import (
                RelevanceEvaluator,
                CoherenceEvaluator, 
                GroundednessEvaluator,
                FluencyEvaluator,
                ResponseCompletenessEvaluator
            )
            self.register_metric(MetricAdapter(
                RelevanceEvaluator(),
                "rag",
                "Measures how relevant the response is to the user query"
            ))
            self.register_metric(MetricAdapter(
                CoherenceEvaluator(),
                "rag",
                "Evaluates logical consistency and flow of the response"
            ))
            self.register_metric(MetricAdapter(
                GroundednessEvaluator(),
                "rag", 
                "Assesses how well the response is grounded in the provided context"
            ))
            self.register_metric(MetricAdapter(
                FluencyEvaluator(),
                "rag",
                "Measures fluency and naturalness of the response"
            ))
            self.register_metric(MetricAdapter(
                ResponseCompletenessEvaluator(),
                "rag",
                "Evaluates how complete the response is in addressing the query"
            ))
        except ImportError as e:
            logger.warning(f"Could not load RAG metrics: {e}")
        
        # Import and register risk & safety metrics
        try:
            from .risk_and_safety.evaluators_refactored import (
                HateUnfairnessEvaluator,
                SexualEvaluator,
                ViolenceEvaluator,
                SelfHarmEvaluator,
                # IndirectAttackEvaluator  # TODO: Re-enable in the future
            )
            self.register_metric(MetricAdapter(
                HateUnfairnessEvaluator(),
                "risk_and_safety",
                "Detects hate speech, discrimination, and unfair content"
            ))
            self.register_metric(MetricAdapter(
                SexualEvaluator(),
                "risk_and_safety",
                "Identifies inappropriate sexual content"
            ))
            self.register_metric(MetricAdapter(
                ViolenceEvaluator(),
                "risk_and_safety",
                "Detects violent or harmful content"
            ))
            self.register_metric(MetricAdapter(
                SelfHarmEvaluator(),
                "risk_and_safety",
                "Identifies self-harm or suicide-related content"
            ))
            # TODO: Re-enable IndirectAttackEvaluator in the future
            # self.register_metric(MetricAdapter(
            #     IndirectAttackEvaluator(),
            #     "risk_and_safety",
            #     "Detects indirect prompt injection attacks"
            # ))
        except ImportError as e:
            logger.warning(f"Could not load risk & safety metrics: {e}")
        
        # Import and register text similarity metrics
        try:
            from .text_similarity.evaluators_refactored import (
                SimilarityEvaluator,
                F1ScoreEvaluator,
                RougeScoreEvaluator,
                BleuScoreEvaluator,
                GleuScoreEvaluator,
                MeteorScoreEvaluator,
                RougePrecisionEvaluator,
                RougeRecallEvaluator,
                RougeF1Evaluator
            )
            self.register_metric(MetricAdapter(
                SimilarityEvaluator(),
                "text_similarity",
                "Measures semantic similarity between response and ground truth"
            ))
            self.register_metric(MetricAdapter(
                F1ScoreEvaluator(),
                "text_similarity",
                "Computes F1 score (harmonic mean of precision and recall)"
            ))
            self.register_metric(MetricAdapter(
                RougeScoreEvaluator(),
                "text_similarity",
                "Computes ROUGE scores for summary evaluation"
            ))
            self.register_metric(MetricAdapter(
                BleuScoreEvaluator(),
                "text_similarity", 
                "Computes BLEU score (n-gram precision measure)"
            ))
            self.register_metric(MetricAdapter(
                GleuScoreEvaluator(),
                "text_similarity",
                "Computes GLEU score (sentence-level BLEU variant)"
            ))
            self.register_metric(MetricAdapter(
                MeteorScoreEvaluator(),
                "text_similarity",
                "Computes METEOR score (alignment-based with synonyms)"
            ))
            self.register_metric(MetricAdapter(
                RougePrecisionEvaluator(),
                "text_similarity",
                "Computes ROUGE precision scores"
            ))
            self.register_metric(MetricAdapter(
                RougeRecallEvaluator(),
                "text_similarity",
                "Computes ROUGE recall scores"
            ))
            self.register_metric(MetricAdapter(
                RougeF1Evaluator(),
                "text_similarity",
                "Computes ROUGE F1 scores"
            ))
        except ImportError as e:
            logger.warning(f"Could not load text similarity metrics: {e}")


# Global registry instance
registry = AzureAIMetricsRegistry()