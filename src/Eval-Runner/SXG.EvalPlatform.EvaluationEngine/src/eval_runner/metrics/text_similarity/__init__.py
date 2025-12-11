"""
Text Similarity evaluation metrics package using Azure AI Evaluation.

This package contains evaluators for text similarity that measure:
- Semantic similarity between texts
- BLEU score (n-gram precision)
- GLEU score (sentence-level BLEU variant)
- METEOR score (alignment-based with synonyms)
"""

from .evaluators_refactored import (
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

__all__ = [
    'SimilarityEvaluator',
    'F1ScoreEvaluator',
    'RougeScoreEvaluator',
    'BleuScoreEvaluator',
    'GleuScoreEvaluator',
    'MeteorScoreEvaluator',
    'RougePrecisionEvaluator',
    'RougeRecallEvaluator',
    'RougeF1Evaluator'
]