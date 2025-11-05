# SXG Evaluation Platform - Comprehensive Evaluator Suite

This document provides a complete overview of the 20+ evaluation metrics implemented across 4 categories.

## Overview

The evaluation platform now includes a comprehensive suite of evaluators designed to assess different aspects of AI system performance:

- **Agent Evaluators** (3): Conversational AI assessment
- **RAG Evaluators** (4): Retrieval-Augmented Generation quality
- **Safety Evaluators** (5): Content moderation and risk assessment  
- **Text Similarity Evaluators** (6): Text comparison and similarity metrics
- **Original Sample Metrics** (3): Basic evaluation examples

**Total: 21 Evaluation Metrics**

## Agent Evaluators

### 1. IntentResolutionEvaluator (`intent_resolution`)
Evaluates how well an AI agent understands and resolves user intents.

**Key Features:**
- Intent extraction from user queries and agent responses
- Confidence-based scoring with configurable thresholds
- Multi-intent support and partial matching
- Detailed analysis of resolution accuracy

**Configuration:**
```python
{
    "confidence_threshold": 0.7,
    "partial_match_weight": 0.5,
    "use_fuzzy_matching": True
}
```

### 2. ToolCallAccuracyEvaluator (`tool_call_accuracy`)
Assesses the accuracy of function/tool calls made by AI agents.

**Key Features:**
- Function name validation and parameter matching
- JSON schema validation for complex parameters
- Weighted scoring for different validation aspects
- Support for multiple tool calls per interaction

**Configuration:**
```python
{
    "validate_parameters": True,
    "parameter_weight": 0.6,
    "strict_mode": False
}
```

### 3. TaskAdherenceEvaluator (`task_adherence`)
Measures how well an agent follows given instructions and completes tasks.

**Key Features:**
- Instruction parsing and requirement extraction
- Step-by-step compliance checking
- Weighted scoring based on instruction importance
- Support for complex, multi-step tasks

**Configuration:**
```python
{
    "instruction_weights": {"must": 1.0, "should": 0.7, "could": 0.3},
    "completion_threshold": 0.8
}
```

## RAG Evaluators

### 4. RetrievalEvaluator (`retrieval_quality`) 
Evaluates document retrieval quality in RAG systems.

**Key Features:**
- Precision, Recall, and F1 metrics at various K values
- Ranking quality assessment using normalized DCG
- Support for semantic similarity matching
- Comprehensive retrieval performance analysis

**Configuration:**
```python
{
    "top_k": 5,
    "relevance_threshold": 0.7,
    "use_semantic_similarity": False
}
```

### 5. GroundednessEvaluator (`groundedness`)
Assesses how well responses are grounded in source material.

**Key Features:**
- Claim extraction and verification
- Entailment checking against source documents
- Citation quality evaluation
- Contradiction detection

**Configuration:**
```python
{
    "claim_extraction_method": "sentence",
    "entailment_threshold": 0.7,
    "require_citation": False
}
```

### 6. DocumentRetrievalEvaluator (`document_retrieval`)
Specialized evaluator for document retrieval effectiveness.

**Key Features:**
- Multi-K evaluation (precision@K, recall@K, F1@K)
- Mean Reciprocal Rank (MRR) calculation
- Comprehensive retrieval metrics
- Document relevance assessment

### 7. GroundednessProEvaluator (`groundedness_pro`)
Enhanced version of GroundednessEvaluator with advanced features.

**Key Features:**
- Advanced claim decomposition
- Contradiction detection between response and sources
- Enhanced entailment analysis
- Professional-grade grounding assessment

## Safety Evaluators

### 8. HateSpeechEvaluator (`hate_speech`)
Detects and evaluates hate speech content.

**Key Features:**
- Multi-category hate speech detection (race, religion, gender, etc.)
- Severity level assessment (low, medium, high, severe)
- Pattern-based and keyword-based detection
- Target group identification

**Configuration:**
```python
{
    "severity_levels": ["low", "medium", "high", "severe"],
    "target_groups": ["race", "religion", "gender", "sexuality"],
    "detection_threshold": 0.7
}
```

### 9. ToxicityEvaluator (`toxicity`)
Evaluates toxic language and harmful behavior.

**Key Features:**
- Multiple toxicity categories (insult, threat, profanity, harassment, spam)
- Context-aware analysis
- Severity threshold configuration
- Comprehensive toxicity scoring

### 10. BiasEvaluator (`bias`)
Detects various forms of bias and discrimination.

**Key Features:**
- Multiple bias types (gender, racial, age, religious, socioeconomic, cognitive)
- Pattern-based bias detection
- Fairness scoring (inverted bias score)
- Comprehensive bias analysis

### 11. ContentSafetyEvaluator (`content_safety`)
Comprehensive content safety across multiple risk categories.

**Key Features:**
- Multi-risk assessment (violence, self-harm, sexual, harassment, illegal)
- Age appropriateness evaluation
- Combined safety scoring
- Detailed risk analysis

### 12. PrivacyEvaluator (`privacy`)
Evaluates content for privacy violations and PII disclosure.

**Key Features:**
- PII detection (email, phone, SSN, credit cards, addresses, names)
- Privacy risk calculation
- Confidence-based scoring
- Comprehensive PII analysis

## Text Similarity Evaluators

### 13. BLEUEvaluator (`bleu`)
Implements BLEU (Bilingual Evaluation Understudy) scoring.

**Key Features:**
- N-gram precision calculation (1-4 grams)
- Brevity penalty application
- Configurable weights and smoothing
- Industry-standard text evaluation

**Configuration:**
```python
{
    "n_grams": [1, 2, 3, 4],
    "weights": [0.25, 0.25, 0.25, 0.25],
    "smoothing": False
}
```

### 14. ROUGEEvaluator (`rouge`)
Implements ROUGE (Recall-Oriented Understudy for Gisting Evaluation) metrics.

**Key Features:**
- ROUGE-1, ROUGE-2, and ROUGE-L calculation
- Precision, Recall, and F1 for each variant
- Stemming support
- Longest Common Subsequence analysis

### 15. SemanticSimilarityEvaluator (`semantic_similarity`)
Evaluates semantic similarity using various methods.

**Key Features:**
- Multiple similarity methods (Jaccard, Cosine, Word Overlap)
- Stop word removal and normalization
- Synonym support (configurable)
- Comprehensive similarity analysis

### 16. EditDistanceEvaluator (`edit_distance`)
Calculates text similarity using edit distance.

**Key Features:**
- Levenshtein and Damerau-Levenshtein distance
- Normalized similarity scoring
- Case sensitivity options
- Character and string level analysis

### 17. F1ScoreEvaluator (`f1_score`)
Token-level F1 score evaluation.

**Key Features:**
- Token and character level evaluation
- Precision, Recall, and F1 calculation
- Exact match detection
- Preprocessing options

### 18. BERTScoreEvaluator (`bert_score`)
Simulated BERT Score evaluation (production would use actual BERT).

**Key Features:**
- Embedding-based similarity (simulated)
- Precision, Recall, and F1 calculation
- Position-weighted scoring
- Model configuration options

## Original Sample Metrics

### 19. AccuracyMetric (`accuracy`)
Basic accuracy evaluation with multiple comparison methods.

### 20. RelevanceMetric (`relevance`) 
Relevance scoring for information retrieval tasks.

### 21. CompletenessMetric (`completeness`)
Completeness assessment for responses and outputs.

## Usage Examples

### Basic Usage
```python
from eval_runner.metrics import MetricFactory

# Create a metric instance
config = MetricConfig(
    name="intent_eval",
    type="intent_resolution",
    parameters={"confidence_threshold": 0.8}
)
metric = MetricFactory.create_metric(config)

# Evaluate a dataset item
result = await metric.evaluate(dataset_item)
print(f"Score: {result.score}, Reason: {result.reason}")
```

### Batch Evaluation
```python
from eval_runner.metrics import AsyncMetricRunner

# Run multiple metrics
metrics = [
    MetricFactory.create_metric(intent_config),
    MetricFactory.create_metric(safety_config),
    MetricFactory.create_metric(similarity_config)
]

runner = AsyncMetricRunner()
results = await runner.run_metrics(dataset_item, metrics)
```

### Available Metric Types
```python
from eval_runner.metrics import MetricFactory

# Get all registered metric types
available_types = MetricFactory.get_registered_types()
print("Available metrics:", available_types)
```

## Metric Registry

All metrics are automatically registered when the package is imported:

```python
from eval_runner import metrics  # Auto-registers all metrics

# Or register manually
from eval_runner.metrics import register_all_metrics
register_all_metrics()
```

## Configuration and Customization

Each evaluator supports extensive configuration through the `MetricConfig` parameters. Common configuration options include:

- **Thresholds**: Confidence, similarity, detection thresholds
- **Methods**: Algorithm variants, comparison methods
- **Weights**: Scoring weights for different components  
- **Modes**: Strict vs lenient evaluation modes
- **Features**: Enable/disable specific evaluation features

## Performance Considerations

- **Async Support**: All evaluators support async/await for scalable evaluation
- **Batch Processing**: AsyncMetricRunner enables concurrent metric execution
- **Resource Management**: Memory-efficient processing for large datasets
- **Error Handling**: Comprehensive error handling with detailed context

## Extensibility

The evaluation framework is designed for easy extension:

1. **Inherit from BaseMetric**: Implement `evaluate()` and `configure()` methods
2. **Register with Factory**: Use `MetricFactory.register_metric()` 
3. **Add Configuration**: Define parameter schema and validation
4. **Handle Errors**: Use custom exceptions from `exceptions` module

This comprehensive suite provides robust evaluation capabilities for AI systems across multiple domains and use cases.