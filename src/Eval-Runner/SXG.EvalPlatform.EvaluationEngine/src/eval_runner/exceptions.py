"""
Custom exceptions for the SXG Evaluation Platform.
"""

class EvaluationEngineError(Exception):
    """Base exception for all evaluation engine errors."""
    pass

class ConfigurationError(EvaluationEngineError):
    """Raised when there's a configuration issue."""
    pass

class MetricError(EvaluationEngineError):
    """Raised when there's an issue with metric execution."""
    pass

class MetricRegistrationError(MetricError):
    """Raised when there's an issue registering a metric."""
    pass

class MetricExecutionError(MetricError):
    """Raised when a metric fails to execute."""
    pass

class AzureServiceError(EvaluationEngineError):
    """Raised when there's an issue with Azure services."""
    pass

class QueueServiceError(AzureServiceError):
    """Raised when there's an issue with Azure Queue Service."""
    pass

class BlobServiceError(AzureServiceError):
    """Raised when there's an issue with Azure Blob Service."""
    pass

class ApiClientError(EvaluationEngineError):
    """Raised when there's an issue with API client operations."""
    pass

class EvaluationTimeoutError(EvaluationEngineError):
    """Raised when an evaluation operation times out."""
    pass

class DataValidationError(EvaluationEngineError):
    """Raised when data validation fails."""
    pass