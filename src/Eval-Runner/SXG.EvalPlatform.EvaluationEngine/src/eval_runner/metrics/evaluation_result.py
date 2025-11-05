"""
Evaluation result data structure for Azure AI evaluators.
"""

from typing import Any, Dict, Optional
from dataclasses import dataclass


@dataclass
class EvaluationResult:
    """
    Standardized evaluation result structure.
    
    This provides a consistent interface between Azure AI evaluators
    and our evaluation system.
    """
    score: float
    reasoning: str
    details: Optional[Dict[str, Any]] = None
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary representation."""
        return {
            'score': self.score,
            'reasoning': self.reasoning,
            'details': self.details or {}
        }