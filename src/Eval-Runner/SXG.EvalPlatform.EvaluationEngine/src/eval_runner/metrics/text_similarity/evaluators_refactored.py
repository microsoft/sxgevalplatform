"""
Refactored text similarity evaluation metrics using Azure AI Evaluation with base classes.
"""

from typing import Any, Dict, Type

from azure.ai.evaluation import (
    SimilarityEvaluator as AzureSimilarityEvaluator,
    F1ScoreEvaluator as AzureF1ScoreEvaluator,
    RougeScoreEvaluator as AzureRougeScoreEvaluator,
    BleuScoreEvaluator as AzureBleuScoreEvaluator,
    GleuScoreEvaluator as AzureGleuScoreEvaluator,
    MeteorScoreEvaluator as AzureMeteorScoreEvaluator,
)
from azure.ai.evaluation._evaluators._rouge._rouge import RougeType

from ...models.eval_models import DatasetItem
from ..base_evaluators import ModelBasedEvaluator, StatisticalEvaluator


class SimilarityEvaluator(ModelBasedEvaluator):
    """Azure AI Similarity evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("similarity")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureSimilarityEvaluator
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Call similarity evaluator with ground truth."""
        return evaluator(
            query=item.prompt,
            response=item.actual_response,
            ground_truth=item.ground_truth
        )
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract similarity evaluation results using Microsoft's Likert scale logic."""
        score = float(azure_result.get("similarity", 0.0))
        reasoning = str(azure_result.get("similarity_reason", "No reasoning provided"))
        
        # Use Microsoft's standard: Likert scale 1-5, score >= threshold (default 3)
        passed = score >= self.threshold
        
        return score, reasoning, passed


class F1ScoreEvaluator(StatisticalEvaluator):
    """Azure AI F1 Score evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("f1_score")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureF1ScoreEvaluator
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Call F1 score evaluator with ground truth."""
        return evaluator(
            response=item.actual_response,
            ground_truth=item.ground_truth
        )
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract F1 score evaluation results."""
        score = float(azure_result.get("f1_score", 0.0))
        reasoning = f"F1 Score: {score:.3f} (Precision and Recall harmonic mean)"
        passed = score >= self.threshold
        
        return score, reasoning, passed


class RougeScoreEvaluator(StatisticalEvaluator):
    """Azure AI ROUGE Score evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("rouge_score")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureRougeScoreEvaluator
        
    def _create_evaluator(self) -> Any:
        """Create evaluator with ROUGE-L type."""
        evaluator_class = self._get_evaluator_class()
        return evaluator_class(rouge_type=RougeType.ROUGE_L)
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Call ROUGE score evaluator with ground truth."""
        return evaluator(
            response=item.actual_response,
            ground_truth=item.ground_truth
        )
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract ROUGE score evaluation results."""
        # ROUGE returns multiple scores (rouge1, rouge2, rougeL, rougeLsum)
        rouge_l_score = float(azure_result.get("rouge_l", 0.0))
        rouge1_score = float(azure_result.get("rouge_1", 0.0))
        rouge2_score = float(azure_result.get("rouge_2", 0.0))
        
        # Use ROUGE-L as the primary score
        score = rouge_l_score
        reasoning = f"ROUGE-L: {rouge_l_score:.3f}, ROUGE-1: {rouge1_score:.3f}, ROUGE-2: {rouge2_score:.3f}"
        passed = score >= self.threshold
        
        return score, reasoning, passed


class BleuScoreEvaluator(StatisticalEvaluator):
    """Azure AI BLEU Score evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("bleu")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureBleuScoreEvaluator
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Call BLEU score evaluator with ground truth."""
        return evaluator(
            response=item.actual_response,
            ground_truth=item.ground_truth
        )
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract BLEU score evaluation results."""
        score = float(azure_result.get("bleu", 0.0))
        reasoning = f"BLEU Score: {score:.3f} (N-gram precision measure)"
        passed = score >= self.threshold
        
        return score, reasoning, passed


class GleuScoreEvaluator(StatisticalEvaluator):
    """Azure AI GLEU Score evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("gleu")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureGleuScoreEvaluator
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Call GLEU score evaluator with ground truth."""
        return evaluator(
            response=item.actual_response,
            ground_truth=item.ground_truth
        )
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract GLEU score evaluation results."""
        score = float(azure_result.get("gleu", 0.0))
        reasoning = f"GLEU Score: {score:.3f} (Sentence-level BLEU variant)"
        passed = score >= self.threshold
        
        return score, reasoning, passed


class MeteorScoreEvaluator(StatisticalEvaluator):
    """Azure AI METEOR Score evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("meteor")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureMeteorScoreEvaluator
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Call METEOR score evaluator with ground truth."""
        return evaluator(
            response=item.actual_response,
            ground_truth=item.ground_truth
        )
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract METEOR score evaluation results."""
        score = float(azure_result.get("meteor", 0.0))
        reasoning = f"METEOR Score: {score:.3f} (Alignment-based with synonyms and paraphrases)"
        passed = score >= self.threshold
        
        return score, reasoning, passed


class RougePrecisionEvaluator(StatisticalEvaluator):
    """ROUGE Precision evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("rouge_(precision)")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureRougeScoreEvaluator
        
    def _create_evaluator(self) -> Any:
        """Create evaluator with ROUGE-L type."""
        evaluator_class = self._get_evaluator_class()
        return evaluator_class(rouge_type=RougeType.ROUGE_L)
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Call ROUGE score evaluator with ground truth."""
        return evaluator(
            response=item.actual_response,
            ground_truth=item.ground_truth
        )
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract ROUGE precision evaluation results."""
        rouge_l_precision = float(azure_result.get("rouge_l_precision", 0.0))
        rouge1_precision = float(azure_result.get("rouge_1_precision", 0.0))
        
        # Use ROUGE-L precision as the primary score
        score = rouge_l_precision
        reasoning = f"ROUGE-L Precision: {rouge_l_precision:.3f}, ROUGE-1 Precision: {rouge1_precision:.3f}"
        passed = score >= self.threshold
        
        return score, reasoning, passed


class RougeRecallEvaluator(StatisticalEvaluator):
    """ROUGE Recall evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("rouge_(recall)")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureRougeScoreEvaluator
        
    def _create_evaluator(self) -> Any:
        """Create evaluator with ROUGE-L type."""
        evaluator_class = self._get_evaluator_class()
        return evaluator_class(rouge_type=RougeType.ROUGE_L)
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Call ROUGE score evaluator with ground truth."""
        return evaluator(
            response=item.actual_response,
            ground_truth=item.ground_truth
        )
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract ROUGE recall evaluation results."""
        rouge_l_recall = float(azure_result.get("rouge_l_recall", 0.0))
        rouge1_recall = float(azure_result.get("rouge_1_recall", 0.0))
        
        # Use ROUGE-L recall as the primary score
        score = rouge_l_recall
        reasoning = f"ROUGE-L Recall: {rouge_l_recall:.3f}, ROUGE-1 Recall: {rouge1_recall:.3f}"
        passed = score >= self.threshold
        
        return score, reasoning, passed


class RougeF1Evaluator(StatisticalEvaluator):
    """ROUGE F1 evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("rouge_(f1)")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureRougeScoreEvaluator
        
    def _create_evaluator(self) -> Any:
        """Create evaluator with ROUGE-L type."""
        evaluator_class = self._get_evaluator_class()
        return evaluator_class(rouge_type=RougeType.ROUGE_L)
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Call ROUGE score evaluator with ground truth."""
        return evaluator(
            response=item.actual_response,
            ground_truth=item.ground_truth
        )
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract ROUGE F1 evaluation results."""
        rouge_l_f1 = float(azure_result.get("rouge_l", 0.0))  # ROUGE-L is the F1 score
        rouge1_f1 = float(azure_result.get("rouge_1", 0.0))   # ROUGE-1 is the F1 score
        
        # Use ROUGE-L F1 as the primary score
        score = rouge_l_f1
        reasoning = f"ROUGE-L F1: {rouge_l_f1:.3f}, ROUGE-1 F1: {rouge1_f1:.3f}"
        passed = score >= self.threshold
        
        return score, reasoning, passed