"""
Refactored agentic evaluation metrics using Azure AI Evaluation with base classes.
"""

from typing import Any, Dict, Type

from azure.ai.evaluation import (
    IntentResolutionEvaluator as AzureIntentResolutionEvaluator,
    ToolCallAccuracyEvaluator as AzureToolCallAccuracyEvaluator,
    TaskAdherenceEvaluator as AzureTaskAdherenceEvaluator,
)

from ...models.eval_models import DatasetItem
from ..base_evaluators import ModelBasedEvaluator


class IntentResolutionEvaluator(ModelBasedEvaluator):
    """Azure AI Intent Resolution evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("intent_resolution")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureIntentResolutionEvaluator
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract intent resolution evaluation results."""
        score = float(azure_result.get("intent_resolution", 0.0))
        reasoning = str(azure_result.get("intent_resolution_reason", "No reasoning provided"))
        passed = azure_result.get("intent_resolution_result", "fail") == "pass"
        
        return score, reasoning, passed


class ToolCallAccuracyEvaluator(ModelBasedEvaluator):
    """Azure AI Tool Call Accuracy evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("tool_call_accuracy")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureToolCallAccuracyEvaluator
    
    def _call_evaluator(self, evaluator: Any, item: DatasetItem, **kwargs) -> Dict[str, Any]:
        """Call tool call accuracy evaluator with specific parameters."""
        # Extract tool calls and definitions from kwargs or metadata
        tool_calls = kwargs.get('tool_calls')
        tool_definitions = kwargs.get('tool_definitions')
        
        if not tool_calls and item.metadata:
            tool_calls = item.metadata.get('tool_calls', [])
            tool_definitions = item.metadata.get('tool_definitions', [])
        
        if not tool_calls:
            # Return a neutral result if no tool calls are available
            return {
                "tool_call_accuracy": 3.0,
                "tool_call_accuracy_reason": "No tool calls detected in the response. Agent may not have needed tools for this query.",
                "tool_call_accuracy_result": "pass"
            }
        
        return evaluator(
            query=item.prompt,
            tool_calls=tool_calls or [],
            tool_definitions=tool_definitions or []
        )
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract tool call accuracy evaluation results."""
        score = float(azure_result.get("tool_call_accuracy", 0.0))
        reasoning = str(azure_result.get("tool_call_accuracy_reason", "No reasoning provided"))
        passed = azure_result.get("tool_call_accuracy_result", "fail") == "pass"
        
        return score, reasoning, passed


class TaskAdherenceEvaluator(ModelBasedEvaluator):
    """Azure AI Task Adherence evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("task_adherence")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureTaskAdherenceEvaluator
    
    def _extract_result(self, azure_result: Dict[str, Any]) -> tuple[float, str, bool]:
        """Extract task adherence evaluation results."""
        score = float(azure_result.get("task_adherence", 0.0))
        reasoning = str(azure_result.get("task_adherence_reason", "No reasoning provided"))
        passed = azure_result.get("task_adherence_result", "fail") == "pass"
        
        return score, reasoning, passed