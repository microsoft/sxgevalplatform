# SXG Evaluation Platform API Documentation

## Overview

The SXG Evaluation Platform API provides a comprehensive set of endpoints for managing evaluation runs, datasets, metrics configurations, and evaluation results. This API enables clients to create, monitor, and analyze AI model evaluations.

**Base URL**: `https://your-api-domain.com/api/v1`

**API Version**: v1

## Table of Contents

1. [Authentication](#authentication)
2. [Health Check](#health-check)
3. [Evaluation Runs](#evaluation-runs)
4. [Evaluation Results](#evaluation-results)
5. [Datasets](#datasets)
6. [Metrics Configuration](#metrics-configuration)
7. [Error Handling](#error-handling)
8. [Status Codes](#status-codes)

---

## Authentication

The API currently uses Azure-based authentication. Ensure your requests include appropriate authentication headers as configured in your Azure environment.

---

## Health Check

### Get API Health Status
**Endpoint**: `GET /health`

**Description**: Check if the API is running and healthy.

**Request**: No parameters required.

**Response**:
```json
{
  "status": "Healthy",
  "timestamp": "2025-10-17T10:30:00.000Z",
  "version": "1.0.0",
  "environment": "Development"
}
```

**Use Case**: Monitor API availability and version information.

**Sample Usage**:
```bash
curl -X GET https://your-api-domain.com/api/v1/health
```

---

## Evaluation Runs

Evaluation runs represent the execution of an AI model evaluation using specific datasets and metrics configurations.

### 1. Create Evaluation Run
**Endpoint**: `POST /eval/runs`

**Description**: Create a new evaluation run with specified dataset and metrics configuration.

**Request Body**:
```json
{
  "agentId": "agent-123",
  "dataSetId": "dataset-456", 
  "metricsConfigurationId": "metrics-789"
}
```

**Response** (201 Created):
```json
{
  "evalRunId": "550e8400-e29b-41d4-a716-446655440000",
  "metricsConfigurationId": "metrics-789",
  "dataSetId": "dataset-456",
  "agentId": "agent-123",
  "status": "Queued",
  "lastUpdatedBy": "System",
  "lastUpdatedOn": "2025-10-17T10:30:00.000Z",
  "startedDatetime": "2025-10-17T10:30:00.000Z",
  "completedDatetime": null
}
```

**Use Cases**: 
- Start a new model evaluation
- Queue evaluations for batch processing
- Initiate automated testing workflows

**Sample Usage**:
```bash
curl -X POST https://your-api-domain.com/api/v1/eval/runs \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "my-ai-agent",
    "dataSetId": "golden-dataset-001", 
    "metricsConfigurationId": "standard-metrics"
  }'
```

### 2. Update Evaluation Run Status
**Endpoint**: `PUT /eval/runs/{evalRunId}`

**Description**: Update the status of an existing evaluation run. Status updates are case insensitive.

**Path Parameters**:
- `evalRunId` (UUID): The unique identifier of the evaluation run

**Request Body**:
```json
{
  "status": "Running"
}
```

**Valid Status Values** (case insensitive):
- `Queued` / `queued` / `QUEUED`
- `Running` / `running` / `RUNNING`
- `Completed` / `completed` / `COMPLETED`
- `Failed` / `failed` / `FAILED`

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Status updated successfully"
}
```

**Important Notes**:
- Once an evaluation run reaches a terminal state (`Completed` or `Failed`), its status cannot be updated
- Status comparisons are case insensitive for flexibility

**Use Cases**:
- Update progress during evaluation execution
- Mark evaluations as completed or failed
- Track evaluation lifecycle stages

**Sample Usage**:
```bash
curl -X PUT https://your-api-domain.com/api/v1/eval/runs/550e8400-e29b-41d4-a716-446655440000 \
  -H "Content-Type: application/json" \
  -d '{"status": "completed"}'
```

### 3. Get Evaluation Run
**Endpoint**: `GET /eval/runs/{evalRunId}`

**Description**: Retrieve details of a specific evaluation run.

**Path Parameters**:
- `evalRunId` (UUID): The unique identifier of the evaluation run

**Response** (200 OK):
```json
{
  "evalRunId": "550e8400-e29b-41d4-a716-446655440000",
  "metricsConfigurationId": "metrics-789",
  "dataSetId": "dataset-456",
  "agentId": "agent-123",
  "status": "Completed",
  "lastUpdatedBy": "System",
  "lastUpdatedOn": "2025-10-17T11:45:00.000Z",
  "startedDatetime": "2025-10-17T10:30:00.000Z",
  "completedDatetime": "2025-10-17T11:45:00.000Z"
}
```

**Use Cases**:
- Monitor evaluation progress
- Retrieve evaluation metadata
- Check completion status and timestamps

**Sample Usage**:
```bash
curl -X GET https://your-api-domain.com/api/v1/eval/runs/550e8400-e29b-41d4-a716-446655440000
```

---

## Evaluation Results

Evaluation results contain the detailed output and metrics from completed evaluation runs.

### 1. Save Evaluation Results
**Endpoint**: `POST /eval/results`

**Description**: Save evaluation results for a completed evaluation run. Results can only be saved for runs with status "Completed" or "Failed".

**Request Body**:
```json
{
  "evalRunId": "550e8400-e29b-41d4-a716-446655440000",
  "fileName": "results.json",
  "evaluationRecords": [
    {
      "id": 1,
      "question": "What is machine learning?",
      "actualAnswer": "Machine learning is a subset of AI...",
      "expectedAnswer": "Machine learning is a method of data analysis...",
      "metrics": {
        "accuracy": 0.95,
        "bleuScore": 0.87,
        "semanticSimilarity": 0.92
      }
    },
    {
      "id": 2,
      "question": "Define neural networks",
      "actualAnswer": "Neural networks are computing systems...",
      "expectedAnswer": "Neural networks are a series of algorithms...",
      "metrics": {
        "accuracy": 0.88,
        "bleuScore": 0.82,
        "semanticSimilarity": 0.89
      }
    }
  ]
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Evaluation results saved successfully",
  "evalRunId": "550e8400-e29b-41d4-a716-446655440000",
  "blobPath": "evalresults/550e8400-e29b-41d4-a716-446655440000/results.json"
}
```

**Use Cases**:
- Store detailed evaluation results after completion
- Save metrics and performance data
- Archive evaluation outputs for analysis

**Sample Usage**:
```bash
curl -X POST https://your-api-domain.com/api/v1/eval/results \
  -H "Content-Type: application/json" \
  -d '{
    "evalRunId": "550e8400-e29b-41d4-a716-446655440000",
    "fileName": "detailed-results.json",
    "evaluationRecords": [{"id": 1, "score": 0.95, "details": "..."}]
  }'
```

### 2. Get Evaluation Results
**Endpoint**: `GET /eval/results/{evalRunId}`

**Description**: Retrieve evaluation results for a specific evaluation run.

**Path Parameters**:
- `evalRunId` (UUID): The unique identifier of the evaluation run

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Evaluation results retrieved successfully",
  "evalRunId": "550e8400-e29b-41d4-a716-446655440000",
  "fileName": "results.json",
  "evaluationRecords": [
    {
      "id": 1,
      "question": "What is machine learning?",
      "actualAnswer": "Machine learning is a subset of AI...",
      "metrics": {
        "accuracy": 0.95,
        "bleuScore": 0.87
      }
    }
  ]
}
```

**Use Cases**:
- Retrieve stored evaluation results
- Analyze evaluation performance
- Export results for reporting

**Sample Usage**:
```bash
curl -X GET https://your-api-domain.com/api/v1/eval/results/550e8400-e29b-41d4-a716-446655440000
```

### 3. Get Evaluation Runs by Agent
**Endpoint**: `GET /eval/results/agent/{agentId}`

**Description**: Retrieve all evaluation runs for a specific agent.

**Path Parameters**:
- `agentId` (string): The unique identifier of the agent

**Response** (200 OK):
```json
[
  {
    "evalRunId": "550e8400-e29b-41d4-a716-446655440000",
    "metricsConfigurationId": "metrics-789",
    "dataSetId": "dataset-456",
    "agentId": "agent-123",
    "status": "Completed",
    "startedDatetime": "2025-10-17T10:30:00.000Z",
    "completedDatetime": "2025-10-17T11:45:00.000Z"
  },
  {
    "evalRunId": "660f9511-f30c-52e5-b827-557766551111",
    "metricsConfigurationId": "metrics-456",
    "dataSetId": "dataset-789",
    "agentId": "agent-123",
    "status": "Running",
    "startedDatetime": "2025-10-17T12:00:00.000Z",
    "completedDatetime": null
  }
]
```

**Use Cases**:
- View all evaluations for a specific AI agent
- Monitor agent performance over time
- Track evaluation history

**Sample Usage**:
```bash
curl -X GET https://your-api-domain.com/api/v1/eval/results/agent/my-ai-agent
```

### 4. Get Evaluation Results by Date Range
**Endpoint**: `GET /eval/results/agent/{agentId}/daterange`

**Description**: Retrieve evaluation results for a specific agent within a date range.

**Path Parameters**:
- `agentId` (string): The unique identifier of the agent

**Query Parameters**:
- `startDateTime` (ISO 8601): Start date and time
- `endDateTime` (ISO 8601): End date and time

**Response** (200 OK):
```json
[
  {
    "success": true,
    "message": "Evaluation results retrieved successfully",
    "evalRunId": "550e8400-e29b-41d4-a716-446655440000",
    "fileName": "results.json",
    "evaluationRecords": [...]
  }
]
```

**Use Cases**:
- Generate reports for specific time periods
- Analyze performance trends
- Export data for specific date ranges

**Sample Usage**:
```bash
curl -X GET "https://your-api-domain.com/api/v1/eval/results/agent/my-ai-agent/daterange?startDateTime=2025-10-01T00:00:00Z&endDateTime=2025-10-17T23:59:59Z"
```

---

## Datasets

Datasets contain the test data used for evaluations, including prompts, expected responses, and ground truth data.

### 1. Get Datasets by Agent
**Endpoint**: `GET /datasets`

**Description**: Retrieve all datasets associated with a specific agent.

**Query Parameters**:
- `agentId` (string): The unique identifier of the agent

**Response** (200 OK):
```json
[
  {
    "datasetId": "dataset-456",
    "agentId": "agent-123",
    "datasetName": "Golden Dataset v1.0",
    "datasetType": "Golden",
    "recordCount": 150,
    "lastUpdatedOn": "2025-10-15T14:30:00.000Z"
  },
  {
    "datasetId": "dataset-789",
    "agentId": "agent-123", 
    "datasetName": "Synthetic Test Data",
    "datasetType": "Synthetic",
    "recordCount": 500,
    "lastUpdatedOn": "2025-10-16T09:15:00.000Z"
  }
]
```

**Use Cases**:
- List available datasets for evaluation
- Browse dataset metadata
- Select datasets for new evaluation runs

**Sample Usage**:
```bash
curl -X GET "https://your-api-domain.com/api/v1/datasets?agentId=my-ai-agent"
```

### 2. Get Dataset Content
**Endpoint**: `GET /datasets/{datasetId}`

**Description**: Retrieve the full content of a specific dataset.

**Path Parameters**:
- `datasetId` (string): The unique identifier of the dataset

**Response** (200 OK):
```json
[
  {
    "prompt": "What is machine learning?",
    "groundTruth": "Machine learning is a method of data analysis that automates analytical model building.",
    "actualResponse": "",
    "expectedResponse": "Machine learning is a subset of artificial intelligence focused on algorithms that learn from data."
  },
  {
    "prompt": "Define neural networks",
    "groundTruth": "Neural networks are computing systems vaguely inspired by biological neural networks.",
    "actualResponse": "",
    "expectedResponse": "Neural networks are a series of algorithms that mimic the operations of a human brain."
  }
]
```

**Use Cases**:
- View dataset content before evaluation
- Validate dataset quality
- Export dataset for external analysis

**Sample Usage**:
```bash
curl -X GET https://your-api-domain.com/api/v1/datasets/golden-dataset-001
```

### 3. Save Dataset
**Endpoint**: `POST /datasets`

**Description**: Create or update a dataset for a specific agent.

**Request Body**:
```json
{
  "agentId": "agent-123",
  "datasetType": "Golden",
  "datasetName": "Updated Golden Dataset",
  "datasetRecords": [
    {
      "prompt": "What is artificial intelligence?",
      "groundTruth": "Artificial intelligence is intelligence demonstrated by machines.",
      "actualResponse": "",
      "expectedResponse": "AI is the simulation of human intelligence in machines."
    },
    {
      "prompt": "What is deep learning?",
      "groundTruth": "Deep learning is part of machine learning based on artificial neural networks.",
      "actualResponse": "",
      "expectedResponse": "Deep learning uses neural networks with multiple layers to model data."
    }
  ]
}
```

**Valid Dataset Types**:
- `Golden`: High-quality, manually curated test data
- `Synthetic`: Automatically generated test data

**Response** (201 Created):
```json
{
  "datasetId": "dataset-new-001",
  "status": "created",
  "message": "Dataset created successfully"
}
```

**Response** (200 OK - for updates):
```json
{
  "datasetId": "dataset-456",
  "status": "updated", 
  "message": "Dataset updated successfully"
}
```

**Use Cases**:
- Upload new test datasets
- Update existing datasets
- Create synthetic datasets for testing

**Sample Usage**:
```bash
curl -X POST https://your-api-domain.com/api/v1/datasets \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "my-ai-agent",
    "datasetType": "Golden",
    "datasetName": "Production Test Set v2.0",
    "datasetRecords": [{"prompt": "test", "groundTruth": "answer"}]
  }'
```

---

## Metrics Configuration

Metrics configurations define how evaluations are measured and what metrics are calculated.

### 1. Get Default Metrics Configuration
**Endpoint**: `GET /eval/defaultconfiguration`

**Description**: Retrieve the default metrics configuration template.

**Response** (200 OK):
```json
{
  "configurationId": "default-metrics",
  "name": "Default Evaluation Metrics",
  "metrics": [
    {
      "name": "Accuracy",
      "description": "Measures correctness of responses",
      "category": "Performance",
      "enabled": true
    },
    {
      "name": "BLEU Score",
      "description": "Measures translation/generation quality",
      "category": "Language Quality", 
      "enabled": true
    },
    {
      "name": "Semantic Similarity",
      "description": "Measures semantic closeness to expected output",
      "category": "Semantic",
      "enabled": true
    }
  ]
}
```

**Use Cases**:
- Get standard metrics configuration
- Use as template for custom configurations
- Understand available metrics

**Sample Usage**:
```bash
curl -X GET https://your-api-domain.com/api/v1/eval/defaultconfiguration
```

---

## Error Handling

The API uses standard HTTP status codes and returns consistent error response formats.

### Error Response Format
```json
{
  "success": false,
  "message": "Error description",
  "details": "Additional error details (optional)"
}
```

### Common Error Scenarios

**400 Bad Request**:
```json
{
  "success": false,
  "message": "Invalid input data",
  "details": "AgentId is required and cannot be empty"
}
```

**404 Not Found**:
```json
{
  "success": false,
  "message": "Evaluation run with ID 550e8400-e29b-41d4-a716-446655440000 not found"
}
```

**500 Internal Server Error**:
```json
{
  "success": false,
  "message": "Failed to create evaluation run",
  "details": "Internal server error occurred"
}
```

---

## Status Codes

| Code | Description | When It Occurs |
|------|-------------|----------------|
| 200 | OK | Successful GET, PUT operations |
| 201 | Created | Successful POST operations |
| 400 | Bad Request | Invalid input data, validation errors |
| 401 | Unauthorized | Authentication failed |
| 403 | Forbidden | Authorization failed |
| 404 | Not Found | Resource not found |
| 500 | Internal Server Error | Server-side errors |

---

## Best Practices

### 1. Status Management
- Always check evaluation run status before saving results
- Use case-insensitive status values for flexibility
- Remember that terminal states (`Completed`, `Failed`) cannot be updated

### 2. Data Organization
- Use meaningful `agentId` values for easy identification
- Include descriptive dataset names
- Store comprehensive evaluation records for analysis

### 3. Error Handling
- Always check response status codes
- Handle authentication and authorization errors appropriately
- Implement retry logic for transient failures

### 4. Performance
- Use date range queries to limit result sets
- Implement pagination for large datasets
- Cache frequently accessed configuration data

### 5. Monitoring
- Use the health endpoint for service monitoring
- Track evaluation run completion rates
- Monitor storage usage for large result sets

---

## Integration Examples

### Complete Evaluation Workflow
```bash
# 1. Create evaluation run
EVAL_RUN_ID=$(curl -X POST https://your-api-domain.com/api/v1/eval/runs \
  -H "Content-Type: application/json" \
  -d '{"agentId":"my-agent","dataSetId":"dataset-001","metricsConfigurationId":"metrics-001"}' \
  | jq -r '.evalRunId')

# 2. Update status to running
curl -X PUT https://your-api-domain.com/api/v1/eval/runs/$EVAL_RUN_ID \
  -H "Content-Type: application/json" \
  -d '{"status":"Running"}'

# 3. After evaluation completion, update status
curl -X PUT https://your-api-domain.com/api/v1/eval/runs/$EVAL_RUN_ID \
  -H "Content-Type: application/json" \
  -d '{"status":"Completed"}'

# 4. Save evaluation results
curl -X POST https://your-api-domain.com/api/v1/eval/results \
  -H "Content-Type: application/json" \
  -d "{\"evalRunId\":\"$EVAL_RUN_ID\",\"fileName\":\"results.json\",\"evaluationRecords\":[{\"id\":1,\"score\":0.95}]}"

# 5. Retrieve results
curl -X GET https://your-api-domain.com/api/v1/eval/results/$EVAL_RUN_ID
```

This comprehensive API documentation provides all the information needed to integrate with the SXG Evaluation Platform API effectively.