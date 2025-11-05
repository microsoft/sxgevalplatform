# API Documentation

## 📋 Overview

This document provides comprehensive API documentation for the SXG Evaluation Platform Evaluation Engine, covering all interfaces for queue messages, configuration APIs, status updates, and internal service interfaces.

## 🔄 Queue Message Interface

### Queue Message Format

The evaluation engine processes messages from Azure Storage Queue with the following structure:

```json
{
  "eval_run_id": "eval-12345",
  "agent_id": "agent-67890",
  "dataset_id": "dataset-abcde",
  "enriched_dataset_id": "enriched-fghij",
  "metrics_configuration_id": "metrics-config-klmno",
  "requested_at": "2025-10-31T10:30:00Z",
  "priority": "high"
}
```

### Queue Message Schema

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `eval_run_id` | string | Yes | Unique identifier for the evaluation run |
| `agent_id` | string | Yes | Identifier for the AI agent being evaluated |
| `dataset_id` | string | Yes | Original dataset identifier |
| `enriched_dataset_id` | string | Yes | Processed/enriched dataset identifier |
| `metrics_configuration_id` | string | Yes | Configuration for metrics to execute |
| `requested_at` | ISO8601 | Yes | Timestamp when evaluation was requested |
| `priority` | string | No | Processing priority: "low", "normal", "high" |

### Message Processing Flow

1. **Message Validation**: Validate required fields and format
2. **Fetch Configuration**: Retrieve dataset and metrics configuration
3. **Process Evaluation**: Execute metrics against dataset items
4. **Store Results**: Save results to Azure Blob Storage
5. **Update Status**: Notify completion via status API
6. **Delete Message**: Remove from queue on success

## 🔧 Configuration API Interface

### Enriched Dataset API

**Endpoint**: `GET /api/v1/evaluations/{eval_run_id}/dataset`

**Response Format**:
```json
{
  "enrichedDataset": [
    {
      "prompt": "What is machine learning?",
      "ground_truth": "Machine learning is a subset of AI...",
      "actual_response": "ML is a method of data analysis...",
      "context": "Educational AI assistant conversation"
    }
  ],
  "metadata": {
    "total_items": 100,
    "created_at": "2025-10-31T10:00:00Z",
    "version": "1.0"
  }
}
```

### Dataset Item Schema

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `prompt` | string | Yes | Input prompt/question for the AI agent |
| `ground_truth` | string | Yes | Expected/correct response |
| `actual_response` | string | Yes | Actual response from the AI agent |
| `context` | string | No | Additional context for the evaluation |

### Metrics Configuration API

**Endpoint**: `GET /api/v1/metrics-configurations/{metrics_configuration_id}`

**Response Format**:
```json
{
  "metricsConfiguration": [
    {
      "metricOriginalName": "Groundedness",
      "threshold": 0.7,
      "weight": 1.0,
      "enabled": true,
      "parameters": {
        "azure_ai_project": {
          "subscription_id": "sub-123",
          "resource_group_name": "rg-eval",
          "project_name": "eval-project"
        }
      }
    }
  ],
  "metadata": {
    "total_metrics": 5,
    "created_at": "2025-10-31T09:00:00Z",
    "version": "2.0"
  }
}
```

### Metrics Configuration Schema

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `metricOriginalName` | string | Yes | Original metric name (e.g., "Groundedness") |
| `threshold` | float | Yes | Pass/fail threshold (0.0-1.0) |
| `weight` | float | No | Metric weight for overall scoring |
| `enabled` | boolean | No | Whether metric is enabled (default: true) |
| `parameters` | object | No | Metric-specific configuration parameters |

## 📊 Status Update API Interface

### Status Update API

**Endpoint**: `PUT /api/v1/evaluations/{eval_run_id}/status`

**Request Format**:
```json
{
  "status": "EvalRunCompleted",
  "timestamp": "2025-10-31T11:00:00Z",
  "message": "Evaluation completed successfully"
}
```

**Response Format**:
```json
{
  "success": true,
  "message": "Status updated successfully",
  "current_status": "EvalRunCompleted"
}
```

### Status Values

| Status | Description |
|--------|-------------|
| `EvalRunRequested` | Initial status when evaluation is requested |
| `EvalRunStarted` | Evaluation processing has begun |
| `EvalRunInProgress` | Evaluation is currently running |
| `EvalRunCompleted` | Evaluation completed successfully |
| `EvalRunFailed` | Evaluation failed with errors |

## 🏗️ Internal Service Interfaces

### Evaluation Engine Interface

#### Main Processing Method

```python
async def process_queue_message(
    self, 
    queue_message: QueueMessage
) -> bool:
    """
    Process a queue message and execute evaluation.
    
    Args:
        queue_message: Parsed queue message with evaluation request
        
    Returns:
        bool: True if processing completed successfully
        
    Raises:
        EvaluationProcessingError: If evaluation fails
        ConfigurationError: If configuration is invalid
    """
```

#### Dataset Processing

```python
async def _run_evaluations(
    self, 
    dataset: Dataset, 
    metrics_config: List[MetricConfig]
) -> List[DatasetItemResult]:
    """
    Execute evaluations for dataset with concurrent processing.
    
    Performance: 3x concurrent dataset items, 8x concurrent metrics
    Timeout: 30 seconds per metric evaluation
    """
```

### HTTP Client Interface

#### API Client Methods

```python
class EvaluationApiClient:
    async def fetch_enriched_dataset(
        self, 
        eval_run_id: str
    ) -> Optional[Dict[str, Any]]:
        """Fetch enriched dataset with connection pooling."""
        
    async def fetch_metrics_configuration(
        self, 
        metrics_configuration_id: str
    ) -> Optional[Any]:
        """Fetch metrics configuration with connection pooling."""
        
    async def update_evaluation_status(
        self, 
        eval_run_id: str, 
        status: str
    ) -> bool:
        """Update evaluation status with retry logic."""
```

### Azure Storage Interface

#### Queue Service Methods

```python
class QueueService:
    async def listen_for_messages(
        self, 
        message_handler: Callable[[QueueMessage], Awaitable[bool]]
    ) -> None:
        """Listen for queue messages with optimized polling."""
        
    async def delete_message(
        self, 
        message: QueueMessage
    ) -> bool:
        """Delete processed message from queue."""
```

#### Blob Storage Methods

```python
class BlobStorageService:
    async def store_results(
        self, 
        agent_id: str,
        eval_run_id: str,
        results: EvaluationSummary
    ) -> bool:
        """Store evaluation results with connection pooling."""
```

## 🔍 Error Response Format

### Standard Error Response

```json
{
  "error": {
    "code": "EVALUATION_FAILED",
    "message": "Evaluation processing failed",
    "details": {
      "eval_run_id": "eval-12345",
      "step": "run_evaluations",
      "error_type": "MetricExecutionError",
      "timestamp": "2025-10-31T11:30:00Z"
    }
  }
}
```

### Error Codes

| Code | Description | HTTP Status |
|------|-------------|-------------|
| `INVALID_MESSAGE_FORMAT` | Queue message format is invalid | 400 |
| `CONFIGURATION_NOT_FOUND` | Dataset or metrics config not found | 404 |
| `EVALUATION_FAILED` | Evaluation processing failed | 500 |
| `TIMEOUT_ERROR` | Operation timed out | 408 |
| `STORAGE_ERROR` | Azure Storage operation failed | 502 |

## 🚀 Performance Considerations

### Connection Pooling

- **HTTP Connections**: 20 total, 10 per host
- **Session Lifecycle**: 1-hour maximum
- **Timeouts**: 30s connect, 60s read

### Concurrent Processing

- **Dataset Items**: 3 concurrent items
- **Metrics**: 8 concurrent evaluations
- **Timeout Protection**: 30s per metric

### Rate Limiting

- **Queue Polling**: Exponential backoff (1s to 30s)
- **API Calls**: Built-in retry with backoff
- **Resource Limits**: Configurable semaphores

## 📝 Usage Examples

### Queue Message Processing

```python
# Example queue message handler
async def handle_message(message: dict) -> bool:
    try:
        queue_message = QueueMessage.from_dict(message)
        result = await evaluation_engine.process_queue_message(queue_message)
        return result
    except Exception as e:
        logger.error(f"Message processing failed: {e}")
        return False
```

### Configuration Fetching

```python
# Example configuration fetching
async def fetch_evaluation_config(eval_run_id: str):
    dataset = await api_client.fetch_enriched_dataset(eval_run_id)
    metrics = await api_client.fetch_metrics_configuration(metrics_config_id)
    return dataset, metrics
```

### Results Storage

```python
# Example results storage
async def store_evaluation_results(
    agent_id: str,
    eval_run_id: str, 
    summary: EvaluationSummary
):
    success = await blob_service.store_results(agent_id, eval_run_id, summary)
    if success:
        await api_client.update_evaluation_status(eval_run_id, "EvalRunCompleted")
```

## 🔗 Related Documentation

- [Performance Guide](PERFORMANCE_GUIDE.md) - Detailed performance optimization
- [Troubleshooting Guide](TROUBLESHOOTING_GUIDE.md) - Common issues and solutions
- [Deployment Guide](DEPLOYMENT.md) - Production deployment instructions
- [Evaluators Documentation](EVALUATORS_README.md) - Comprehensive metrics guide