"""
Evaluation runner main package.
"""

# Import configuration
from .config.settings import app_settings

# Import core models
from .models.eval_models import (
    QueueMessage,
    DatasetItem, 
    Dataset,
    EnrichedDatasetResponse,
    MetricConfig,
    MetricsConfigurationResponse,
    EvaluationConfig,
    MetricScore,
    DatasetItemResult,
    MetricSummary,
    EvaluationSummary
)

# Import core services
from .core.evaluation_engine import evaluation_engine
from .services.azure_storage import get_queue_service, get_blob_service
from .services.http_client import api_client

# Import registry to ensure metrics are loaded
from .metrics import registry

__all__ = [
    'app_settings',
    'QueueMessage',
    'DatasetItem',
    'Dataset', 
    'EnrichedDatasetResponse',
    'MetricConfig',
    'MetricsConfigurationResponse',
    'EvaluationConfig',
    'MetricScore',
    'DatasetItemResult',
    'MetricSummary', 
    'EvaluationSummary',
    'evaluation_engine',
    'get_queue_service',
    'get_blob_service',
    'api_client',
    'registry'
]