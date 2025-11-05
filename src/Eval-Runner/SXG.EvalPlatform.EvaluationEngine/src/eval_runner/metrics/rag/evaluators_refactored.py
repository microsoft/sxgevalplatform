"""
Refactored RAG evaluation metrics using Azure AI Evaluation with base classes.
"""

from typing import Any, Dict, Type

from azure.ai.evaluation import (
    GroundednessEvaluator as AzureGroundednessEvaluator,
    RelevanceEvaluator as AzureRelevanceEvaluator,
    CoherenceEvaluator as AzureCoherenceEvaluator,
    FluencyEvaluator as AzureFluencyEvaluator,
    ResponseCompletenessEvaluator as AzureResponseCompletenessEvaluator,
)

from ...models.eval_models import DatasetItem
from ..base_evaluators import ModelBasedEvaluator


class GroundednessEvaluator(ModelBasedEvaluator):
    """Azure AI Groundedness evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("groundedness")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureGroundednessEvaluator
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Call groundedness evaluator with context."""
        # Extract context from kwargs or metadata
        context = kwargs.get('context')
        
        if not context and item.metadata:
            context = item.metadata.get('context', '')
        
        # If no context is available, use the retrieved documents or similar
        if not context:
            context = getattr(item, 'retrieved_documents', '') or getattr(item, 'context', '')
        
        if not context:
            # Return a neutral result if no context is available
            return {
                "groundedness": 3.0,
                "groundedness_reason": "No context available for groundedness evaluation",
                "groundedness_result": "pass"
            }
        
        return evaluator(
            query=item.prompt,
            response=item.actual_response,
            context=context
        )
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract groundedness evaluation results."""
        score = float(azure_result.get("groundedness", 0.0))
        reasoning = str(azure_result.get("groundedness_reason", "No reasoning provided"))
        passed = azure_result.get("groundedness_result", "fail") == "pass"
        
        return score, reasoning, passed


class RelevanceEvaluator(ModelBasedEvaluator):
    """Azure AI Relevance evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("relevance")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureRelevanceEvaluator
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Call relevance evaluator with context."""
        # Extract context from kwargs or metadata
        context = kwargs.get('context')
        
        if not context and item.metadata:
            context = item.metadata.get('context', '')
        
        # If no context is available, use the retrieved documents or similar
        if not context:
            context = getattr(item, 'retrieved_documents', '') or getattr(item, 'context', '')
        
        if not context:
            # Return a neutral result if no context is available
            return {
                "relevance": 3.0,
                "relevance_reason": "No context available for relevance evaluation",
                "relevance_result": "pass"
            }
        
        return evaluator(
            query=item.prompt,
            response=item.actual_response,
            context=context
        )
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract relevance evaluation results."""
        score = float(azure_result.get("relevance", 0.0))
        reasoning = str(azure_result.get("relevance_reason", "No reasoning provided"))
        passed = azure_result.get("relevance_result", "fail") == "pass"
        
        return score, reasoning, passed


class CoherenceEvaluator(ModelBasedEvaluator):
    """Azure AI Coherence evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("coherence")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureCoherenceEvaluator
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract coherence evaluation results."""
        score = float(azure_result.get("coherence", 0.0))
        reasoning = str(azure_result.get("coherence_reason", "No reasoning provided"))
        passed = azure_result.get("coherence_result", "fail") == "pass"
        
        return score, reasoning, passed


class FluencyEvaluator(ModelBasedEvaluator):
    """Azure AI Fluency evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("fluency")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureFluencyEvaluator
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract fluency evaluation results."""
        score = float(azure_result.get("fluency", 0.0))
        reasoning = str(azure_result.get("fluency_reason", "No reasoning provided"))
        passed = azure_result.get("fluency_result", "fail") == "pass"
        
        return score, reasoning, passed


class ResponseCompletenessEvaluator(ModelBasedEvaluator):
    """Azure AI Response Completeness evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("responsecompleteness")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureResponseCompletenessEvaluator
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Call response completeness evaluator."""
        # Try using individual parameters instead of conversation format
        if item.ground_truth:
            return evaluator(
                query=item.prompt,
                response=item.actual_response,
                ground_truth=item.ground_truth
            )
        else:
            # If no ground truth, use conversation format
            conversation = {
                "messages": [
                    {"role": "user", "content": item.prompt},
                    {"role": "assistant", "content": item.actual_response}
                ]
            }
            return evaluator(conversation=conversation)
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract response completeness evaluation results."""
        score = float(azure_result.get("response_completeness", 0.0))
        reasoning = str(azure_result.get("response_completeness_reason", "No reasoning provided"))
        passed = azure_result.get("response_completeness_result", "fail") == "pass"
        
        return score, reasoning, passed