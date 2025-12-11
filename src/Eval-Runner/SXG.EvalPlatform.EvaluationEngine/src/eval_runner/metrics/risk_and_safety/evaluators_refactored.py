"""
Refactored risk & safety evaluation metrics using Azure AI Evaluation with base classes.
"""

from typing import Any, Type

from azure.ai.evaluation import (
    ViolenceEvaluator as AzureViolenceEvaluator,
    SexualEvaluator as AzureSexualEvaluator,
    SelfHarmEvaluator as AzureSelfHarmEvaluator,
    HateUnfairnessEvaluator as AzureHateUnfairnessEvaluator,
    IndirectAttackEvaluator as AzureIndirectAttackEvaluator,
)

from ..base_evaluators import SafetyEvaluator


class ViolenceEvaluator(SafetyEvaluator):
    """Azure AI Violence evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("violence")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureViolenceEvaluator


class SexualEvaluator(SafetyEvaluator):
    """Azure AI Sexual evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("sexual")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureSexualEvaluator


class SelfHarmEvaluator(SafetyEvaluator):
    """Azure AI Self Harm evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("self_harm")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureSelfHarmEvaluator


class HateUnfairnessEvaluator(SafetyEvaluator):
    """Azure AI Hate Unfairness evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("hate_unfairness")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureHateUnfairnessEvaluator


class IndirectAttackEvaluator(SafetyEvaluator):
    """Azure AI Indirect Attack evaluator."""
    
    def __init__(self):
        """Initialize the evaluator."""
        super().__init__("indirect_attack")
    
    def _get_evaluator_class(self) -> Type[Any]:
        """Get the Azure AI evaluator class."""
        return AzureIndirectAttackEvaluator