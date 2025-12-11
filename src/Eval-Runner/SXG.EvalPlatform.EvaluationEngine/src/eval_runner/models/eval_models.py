"""
Data models for the evaluation runner.
"""

from dataclasses import dataclass, field
from typing import List, Dict, Any, Optional
from datetime import datetime
from enum import Enum
import json
import logging

logger = logging.getLogger(__name__)

class EvaluationStatus(Enum):
    """Evaluation status enumeration."""
    PENDING = "Pending"
    IN_PROGRESS = "InProgress" 
    COMPLETED = "Completed"
    FAILED = "Failed"

@dataclass
class QueueMessage:
    """Message received from Azure Storage Queue."""
    eval_run_id: str
    metrics_configuration_id: str
    requested_at: Optional[datetime] = None
    priority: str = "Normal"
    
    @classmethod
    def from_json(cls, message_body: str) -> 'QueueMessage':
        """
        Create QueueMessage from JSON string with enhanced error handling.
        
        Args:
            message_body: JSON string or already parsed dict
            
        Returns:
            QueueMessage instance
            
        Raises:
            json.JSONDecodeError: When JSON parsing fails
            KeyError: When required fields are missing
        """
        try:
            # Handle both string and already-parsed dict inputs
            if isinstance(message_body, str):
                data = json.loads(message_body)
            elif isinstance(message_body, dict):
                data = message_body
            else:
                raise ValueError(f"Expected string or dict, got {type(message_body)}")
                
        except json.JSONDecodeError as e:
            logger.error(f"Failed to parse queue message JSON. Content: {repr(message_body[:200])}")
            logger.error(f"JSON Error: {e}")
            raise
        
        # Helper function to get value supporting both camelCase and PascalCase
        def get_field(camel_case: str, pascal_case: str, required: bool = True) -> str:
            if pascal_case in data:
                return str(data[pascal_case])
            elif camel_case in data:
                return str(data[camel_case])
            elif required:
                available_keys = list(data.keys()) if isinstance(data, dict) else []
                raise KeyError(f"Required field missing: expected '{pascal_case}' or '{camel_case}'. Available keys: {available_keys}")
            return ""
        
        try:
            # Parse requested_at if provided
            requested_at = None
            if 'RequestedAt' in data or 'requestedAt' in data:
                requested_at_str = get_field('requestedAt', 'RequestedAt', False)
                if requested_at_str:
                    requested_at = datetime.fromisoformat(requested_at_str)
            
            return cls(
                eval_run_id=get_field('evalRunId', 'EvalRunId'),
                metrics_configuration_id=get_field('metricsConfigurationId', 'MetricsConfigurationId'),
                requested_at=requested_at,
                priority=get_field('priority', 'Priority', False) or 'Normal'
            )
        except Exception as e:
            logger.error(f"Failed to create QueueMessage from parsed data: {data}")
            logger.error(f"Error: {e}")
            raise

@dataclass
class DatasetItem:
    """Individual item in the evaluation dataset."""
    prompt: str
    ground_truth: str
    actual_response: str
    expected_response: Optional[str] = None
    context: List[str] = field(default_factory=list)
    metadata: Dict[str, Any] = field(default_factory=dict)
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'DatasetItem':
        """Create DatasetItem from dictionary."""
        # Helper function to get field value with fallback names
        def get_field(*field_names: str) -> Any:
            for field_name in field_names:
                if field_name in data:
                    return data[field_name]
            return None
        
        # Handle various field name formats - prioritize the actual API field names first
        prompt = get_field('query', 'prompt', 'Prompt', 'question', 'Question', 'input', 'Input') or ''
        ground_truth = get_field('groundTruth', 'GroundTruth', 'ground_truth', 'expectedAnswer', 'ExpectedAnswer') or ''
        actual_response = get_field('actualResponse', 'ActualResponse', 'actual_response', 'response', 'Response', 'answer', 'Answer') or ''
        expected_response = get_field('expectedResponse', 'ExpectedResponse', 'expected_response')
        context = get_field('context', 'Context') or []
        metadata = get_field('metadata', 'Metadata') or {}
        
        # Ensure string fields are strings
        prompt = str(prompt) if prompt is not None else ''
        ground_truth = str(ground_truth) if ground_truth is not None else ''
        actual_response = str(actual_response) if actual_response is not None else ''
        expected_response = str(expected_response) if expected_response is not None else None
        
        return cls(
            prompt=prompt,
            ground_truth=ground_truth,
            actual_response=actual_response,
            expected_response=expected_response,
            context=context if isinstance(context, list) else [],
            metadata=metadata if isinstance(metadata, dict) else {}
        )
    
    @property
    def actual_outcome(self) -> str:
        """Compatibility property for existing evaluators."""
        return self.actual_response
    
    @property  
    def expected_outcome(self) -> str:
        """Compatibility property for existing evaluators."""
        return self.ground_truth

@dataclass
class Dataset:
    """Evaluation dataset containing multiple items."""
    items: List[DatasetItem]
    name: str = ""
    description: str = ""
    
    @classmethod
    def from_json(cls, data: List[Dict[str, Any]]) -> 'Dataset':
        """Create Dataset from JSON data (enriched dataset format)."""
        items = []
        for item_data in data:
            items.append(DatasetItem.from_dict(item_data))
        
        return cls(items=items)

@dataclass
class EnrichedDatasetResponse:
    """Response from enriched dataset API."""
    eval_run_id: str
    agent_id: str
    enriched_dataset: List[DatasetItem]
    created_at: Optional[datetime] = None
    last_updated: Optional[datetime] = None
    
    @classmethod
    def from_json(cls, data: Dict[str, Any]) -> 'EnrichedDatasetResponse':
        """Create EnrichedDatasetResponse from JSON data."""
        # Helper function to get field value with fallback names
        def get_field(*field_names: str) -> Any:
            for field_name in field_names:
                if field_name in data:
                    return data[field_name]
            return None
        
        # Parse dataset items
        dataset_items = []
        enriched_data = get_field('enrichedDataset', 'EnrichedDataset')
        if enriched_data:
            for i, item_data in enumerate(enriched_data):
                try:
                    dataset_items.append(DatasetItem.from_dict(item_data))
                except Exception as e:
                    # Log the problematic item for debugging
                    available_keys = list(item_data.keys()) if isinstance(item_data, dict) else "Not a dict"
                    raise ValueError(f"Failed to parse dataset item {i}: {str(e)}. Available keys: {available_keys}. Item data: {item_data}")
        
        # Handle both camelCase and PascalCase field names
        eval_run_id = get_field('evalRunId', 'EvalRunId')
        agent_id = get_field('agentId', 'AgentId')
        created_at_str = get_field('createdAt', 'CreatedAt')
        last_updated_str = get_field('lastUpdated', 'LastUpdated')
        
        return cls(
            eval_run_id=eval_run_id,
            agent_id=agent_id,
            enriched_dataset=dataset_items,
            created_at=datetime.fromisoformat(created_at_str.replace('Z', '+00:00')) if created_at_str else None,
            last_updated=datetime.fromisoformat(last_updated_str.replace('Z', '+00:00')) if last_updated_str else None
        )
    
    def to_dataset(self) -> Dataset:
        """Convert to Dataset for backwards compatibility."""
        return Dataset(items=self.enriched_dataset)

@dataclass
class MetricConfig:
    """Configuration for a single metric."""
    metric_name: str
    category_name: Optional[str]
    threshold: float
    _original_name: Optional[str] = None  # For debugging purposes
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'MetricConfig':
        """Create MetricConfig from dictionary."""
        # Handle both old and new field names
        metric_name = data.get('metricName') or data.get('MetricName', '')
        threshold = data.get('threshold') or data.get('Threshold', 0.0)
        
        # Normalize API metric name to match registry names
        normalized_name = cls._normalize_api_metric_name(metric_name)
        
        config = cls(
            metric_name=normalized_name,
            category_name=data.get('CategoryName'),  # CategoryName is optional in API response
            threshold=float(threshold)
        )
        
        # Store original name for debugging
        config._original_name = metric_name
        
        return config

    @staticmethod
    def _normalize_api_metric_name(api_name: str) -> str:
        """
        Normalize API metric names to match registry metric names.
        
        Args:
            api_name: Metric name from API response
            
        Returns:
            Normalized name that matches registry
        """
        # Define explicit mappings for API names to registry names
        api_to_registry_mappings = {
            # Text similarity metrics
            'F1 Score': 'f1_score',
            'BLEU': 'bleu',
            'GLEU': 'gleu', 
            'METEOR': 'meteor',
            'ROUGE (Precision)': 'rouge_(precision)',
            'ROUGE (Recall)': 'rouge_(recall)',
            'ROUGE (F1)': 'rouge_(f1)',
            
            # Additional mappings for Score suffix variants
            'F1Score': 'f1_score',
            'BleuScore': 'bleu',
            'GleuScore': 'gleu',
            'MeteorScore': 'meteor',
            'RougePrecision': 'rouge_(precision)',
            'RougeRecall': 'rouge_(recall)',
            'RougeF1': 'rouge_(f1)',
            
            # Handle typos in API names
            'RogueScore': 'rouge_score',  # Typo: Rogue -> Rouge
            'Voilence': 'violence',       # Typo: Voilence -> Violence
            
            # Additional simple name variants
            'Bleu': 'bleu',
            'Completeness': 'responsecompleteness',
            'Accuracy': 'f1_score',  # Map generic accuracy to F1 score
            
            # Model-based metrics (these use lowercase)
            'Coherence': 'coherence',
            'Fluency': 'fluency',
            'Similarity': 'similarity',
            'Groundedness': 'groundedness',
            'Relevance': 'relevance',
            
            # Response completeness
            'ResponseCompleteness': 'responsecompleteness',
            
            # Agentic metrics (these need to be added to registry)
            'IntentResolution': 'intent_resolution',
            'ToolCallAccuracy': 'tool_call_accuracy', 
            'TaskAdherence': 'task_adherence',
            
            # Safety metrics
            'HateUnfairness': 'hate_unfairness',
            'Violence': 'violence',
            'SelfHarm': 'self_harm',
            'Sexual': 'sexual',
            
            # Other metrics - indirect_attack disabled
            # 'Retrieval': 'indirect_attack',  # Disabled - evaluator not available
        }
        
        # First try exact mapping
        if api_name in api_to_registry_mappings:
            return api_to_registry_mappings[api_name]
        
        # Fallback to basic normalization only for known patterns
        normalized = api_name.lower().replace(' ', '_').replace('(', '_').replace(')', '_')
        normalized = normalized.replace('__', '_').strip('_')
        
        # Don't normalize unknown metrics - return original to make issues more obvious
        if api_name not in ['F1 Score', 'BLEU', 'GLEU', 'METEOR', 'ROUGE (Precision)', 'ROUGE (Recall)', 'ROUGE (F1)',
                           'Coherence', 'Fluency', 'Similarity', 'Groundedness', 'Relevance', 'ResponseCompleteness',
                           'IntentResolution', 'ToolCallAccuracy', 'TaskAdherence', 'HateUnfairness', 'Violence', 
                           'SelfHarm', 'Sexual', 'Bleu', 'Completeness', 'Accuracy']:
            logger.warning(f"Unknown metric '{api_name}' received from API - check metrics configuration endpoint")
            return api_name  # Return original name to make the issue more obvious
        
        return normalized

@dataclass
class MetricsConfigurationResponse:
    """Response from metrics configuration API."""
    eval_run_id: str
    agent_id: str
    metrics_configuration_id: str
    metrics_configuration: List[MetricConfig]
    last_updated: Optional[datetime] = None
    
    @classmethod
    def from_json(cls, data: Any) -> 'MetricsConfigurationResponse':
        """Create MetricsConfigurationResponse from JSON data."""
        metrics_config = []
        
        # Handle new simple array format
        if isinstance(data, list):
            # New format: direct array of metric configurations
            for metric_data in data:
                metrics_config.append(MetricConfig.from_dict(metric_data))
            
            # Create response with default values since array format doesn't include metadata
            return cls(
                eval_run_id="",  # Not provided in new array format
                agent_id="",     # Not provided in new array format
                metrics_configuration_id="", # Not provided in new array format
                metrics_configuration=metrics_config,
                last_updated=None
            )
        
        # Handle old nested format (fallback)
        elif isinstance(data, dict):
            # Helper function to get field value with fallback names
            def get_field(*field_names: str) -> Any:
                for field_name in field_names:
                    if field_name in data:
                        return data[field_name]
                return None
            
            # Handle both camelCase and PascalCase for metrics configuration array
            metrics_data = get_field('metricsConfiguration', 'MetricsConfiguration')
            if metrics_data:
                for metric_data in metrics_data:
                    metrics_config.append(MetricConfig.from_dict(metric_data))
            
            # Handle both camelCase and PascalCase field names
            eval_run_id = get_field('evalRunId', 'EvalRunId') or ""
            agent_id = get_field('agentId', 'AgentId') or ""
            metrics_config_id = get_field('metricsConfigurationId', 'MetricsConfigurationId') or ""
            last_updated_str = get_field('lastUpdated', 'LastUpdated')
            
            return cls(
                eval_run_id=eval_run_id,
                agent_id=agent_id,
                metrics_configuration_id=metrics_config_id,
                metrics_configuration=metrics_config,
                last_updated=datetime.fromisoformat(last_updated_str.replace('Z', '+00:00')) if last_updated_str else None
            )
        
        else:
            raise ValueError(f"Unexpected data format for metrics configuration: {type(data)}")

@dataclass
class EvaluationConfig:
    """Complete evaluation configuration."""
    eval_run_id: str
    agent_id: str
    metrics_configuration: List[MetricConfig]
    dataset: Dataset
    
    @classmethod
    def create(cls, queue_message: QueueMessage, metrics_response: MetricsConfigurationResponse, dataset: Dataset) -> 'EvaluationConfig':
        """Create EvaluationConfig from queue message, metrics response, and dataset."""
        # Use agent_id from metrics response since queue message no longer contains it
        agent_id = metrics_response.agent_id
        return cls(
            eval_run_id=queue_message.eval_run_id,
            agent_id=agent_id,
            metrics_configuration=metrics_response.metrics_configuration,
            dataset=dataset
        )

@dataclass
class MetricScore:
    """Score result for a single metric on a dataset item."""
    metric_name: str
    score: float
    reason: str
    passed: bool = False
    details: Dict[str, Any] = field(default_factory=dict)
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        return {
            'metricName': self.metric_name,
            'score': self.score,
            'reason': self.reason,
            'passed': self.passed,
            'details': self.details
        }

@dataclass
class DatasetItemResult:
    """Evaluation result for a single dataset item."""
    prompt: str
    ground_truth: str
    actual_response: str
    context: List[str]
    metric_scores: List[MetricScore]
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        return {
            'prompt': self.prompt,
            'groundTruth': self.ground_truth,
            'actualResponse': self.actual_response,
            'context': self.context,
            'metricScores': [score.to_dict() for score in self.metric_scores]
        }

@dataclass
class MetricSummary:
    """Summary statistics for a single metric across all dataset items."""
    metric_name: str
    category_name: str
    threshold: float
    average_score: float
    passed_count: int
    failed_count: int
    total_count: int
    pass_percentage: float
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        return {
            'metricName': self.metric_name,
            'categoryName': self.category_name,
            'threshold': self.threshold,
            'averageScore': self.average_score,
            'passedCount': self.passed_count,
            'failedCount': self.failed_count,
            'totalCount': self.total_count,
            'passPercentage': self.pass_percentage
        }

@dataclass
class EvaluationSummary:
    """Complete evaluation summary with results and statistics."""
    eval_run_id: str
    agent_id: str
    total_prompts: int
    execution_time_seconds: float
    metric_summaries: List[MetricSummary]
    overall_pass_percentage: float
    status: str = "Completed"
    timestamp: datetime = field(default_factory=datetime.now)
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        return {
            'evalRunId': self.eval_run_id,
            'agentId': self.agent_id,
            'totalPrompts': self.total_prompts,
            'executionTimeSeconds': self.execution_time_seconds,
            'metricSummaries': [summary.to_dict() for summary in self.metric_summaries],
            'overallPassPercentage': self.overall_pass_percentage,
            'status': self.status,
            'timestamp': self.timestamp.isoformat()
        }

@dataclass
class ItemEvaluationResult:
    """Evaluation result for a single dataset item."""
    prompt: str
    actual_outcome: Any
    expected_outcome: Any
    metric_scores: List[MetricScore]
    metadata: Dict[str, Any] = field(default_factory=dict)
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        return {
            'prompt': self.prompt,
            'actualOutcome': self.actual_outcome,
            'expectedOutcome': self.expected_outcome,
            'metricScores': [score.to_dict() for score in self.metric_scores],
            'metadata': self.metadata
        }

@dataclass
class EvaluationResult:
    """Complete evaluation result."""
    eval_run_id: str
    status: EvaluationStatus
    item_results: List[ItemEvaluationResult]
    summary_metrics: Dict[str, float] = field(default_factory=dict)
    start_time: datetime = field(default_factory=datetime.now)
    end_time: Optional[datetime] = None
    error_message: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        return {
            'evalRunId': self.eval_run_id,
            'status': self.status.value,
            'itemResults': [item.to_dict() for item in self.item_results],
            'summaryMetrics': self.summary_metrics,
            'startTime': self.start_time.isoformat(),
            'endTime': self.end_time.isoformat() if self.end_time else None,
            'errorMessage': self.error_message,
            'metadata': self.metadata
        }
    
    def to_json(self) -> str:
        """Convert to JSON string."""
        return json.dumps(self.to_dict(), indent=2)

@dataclass
class StatusUpdate:
    """Status update message."""
    eval_run_id: str
    status: EvaluationStatus
    message: Optional[str] = None
    timestamp: datetime = field(default_factory=datetime.now)
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for JSON serialization."""
        return {
            'evalRunId': self.eval_run_id,
            'status': self.status.value,
            'message': self.message,
            'timestamp': self.timestamp.isoformat()
        }