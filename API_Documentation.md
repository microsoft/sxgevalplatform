# SXG Evaluation Platform API Documentation

## Overview

The SXG Evaluation Platform API is a RESTful web service built with .NET 8 that provides comprehensive functionality for managing evaluation configurations and datasets for AI agents. The API enables users to create, manage, and retrieve evaluation configurations and datasets, supporting both synthetic and golden standard data types.

## Base Information

- **Base URL**: `https://your-domain/api/v1`
- **API Version**: 1.0.0
- **Framework**: .NET 8
- **Content Type**: `application/json`
- **Authentication**: Not implemented in current version

## Table of Contents

1. [Health Check](#health-check)
2. [Evaluation Configurations](#evaluation-configurations)
3. [Dataset Management](#dataset-management)
4. [Evaluation Runs](#evaluation-runs)
5. [Evaluation Results](#evaluation-results)
6. [Data Models](#data-models)
7. [Error Handling](#error-handling)
8. [Examples](#examples)

---

## Health Check

### GET /api/v1/health

**Description**: Get API health status and basic information.

**Parameters**: None

**Response**:
- **200 OK**: Health information
```json
{
  "status": "Healthy",
  "timestamp": "2024-01-01T12:00:00.000Z",
  "version": "1.0.0",
  "environment": "Production"
}
```

---

## Evaluation Configurations

The Evaluation Configuration endpoints allow you to manage metric configurations for AI agents.

### GET /api/v1/eval/configurations/{agentId}

**Description**: Retrieve all configurations for a specific agent.

**Parameters**:
- `agentId` (path, required): Unique identifier of the agent

**Response**:
- **200 OK**: List of configurations
- **404 Not Found**: No configurations found for the agent
- **400 Bad Request**: Invalid agent ID
- **500 Internal Server Error**: Server error

**Response Body** (200 OK):
```json
[
  {
    "agentId": "agent-001",
    "configurationName": "default-config",
    "description": "Default evaluation configuration",
    "metricsConfiguration": [
      {
        "metricName": "accuracy",
        "threshold": 0.85
      }
    ]
  }
]
```

### POST /api/v1/eval/configurations

**Description**: Create a new evaluation configuration or update an existing one.

**Request Body**:
```json
{
  "agentId": "agent-001",
  "configurationName": "my-config",
  "description": "Custom evaluation configuration",
  "metricsConfiguration": [
    {
      "metricName": "accuracy",
      "threshold": 0.9
    },
    {
      "metricName": "precision",
      "threshold": 0.85
    }
  ]
}
```

**Response**:
- **201 Created**: Configuration created successfully
- **200 OK**: Configuration updated successfully
- **400 Bad Request**: Invalid input data
- **500 Internal Server Error**: Server error

**Response Body**:
```json
{
  "configId": "12345678-1234-1234-1234-123456789012",
  "status": "created",
  "message": "Configuration created successfully"
}
```

---

## Dataset Management

The Dataset Management endpoints allow you to manage evaluation datasets for AI agents.

### POST /api/v1/datasets

**Description**: Save evaluation dataset (creates new or updates existing based on AgentId, DatasetType, and FileName).

**Request Body**:
```json
{
  "agentId": "agent-001",
  "datasetType": "Synthetic",
  "fileName": "evaluation-dataset-v1.json",
  "datasetRecords": [
    {
      "prompt": "What is the capital of France?",
      "groundTruth": "Paris",
      "actualResponse": "Paris is the capital of France.",
      "estimatedResponse": "The capital of France is Paris."
    },
    {
      "prompt": "Explain machine learning",
      "groundTruth": "Machine learning is a method of data analysis that automates analytical model building.",
      "actualResponse": "Machine learning allows computers to learn from data without explicit programming.",
      "estimatedResponse": "ML is when computers learn patterns from data automatically."
    }
  ]
}
```

**Parameters** (Request Body):
- `agentId` (string, required): Unique identifier of the agent
- `datasetType` (string, required): Must be either "Synthetic" or "Golden"
- `fileName` (string, required): Name of the dataset file (1-100 characters)
- `datasetRecords` (array, required): Array of dataset records (minimum 1 record)

**DatasetRecord Properties**:
- `prompt` (string, required): The input prompt/question
- `groundTruth` (string, required): The expected/correct answer
- `actualResponse` (string, optional): The actual response from the system
- `estimatedResponse` (string, optional): The estimated response

**Response**:
- **201 Created**: Dataset created successfully
- **200 OK**: Dataset updated successfully
- **400 Bad Request**: Invalid input or validation failed
- **500 Internal Server Error**: Server error

**Response Body**:
```json
{
  "datasetId": "87654321-4321-4321-4321-210987654321",
  "status": "created",
  "message": "Dataset saved successfully"
}
```

### GET /api/v1/datasets/agent/{agentId}

**Description**: Get dataset list by agent ID.

**Parameters**:
- `agentId` (path, required): Unique identifier of the agent

**Response**:
- **200 OK**: Dataset list retrieved successfully
- **404 Not Found**: No datasets found for the agent
- **400 Bad Request**: Invalid agent ID
- **500 Internal Server Error**: Server error

**Response Body** (200 OK):
```json
{
  "agentId": "agent-001",
  "datasets": [
    {
      "datasetId": "87654321-4321-4321-4321-210987654321",
      "lastUpdatedOn": "2024-01-01T12:00:00.000Z",
      "agentId": "agent-001",
      "datasetType": "Synthetic",
      "fileName": "evaluation-dataset-v1.json",
      "recordCount": 50
    },
    {
      "datasetId": "11111111-2222-3333-4444-555555555555",
      "lastUpdatedOn": "2024-01-02T10:30:00.000Z",
      "agentId": "agent-001",
      "datasetType": "Golden",
      "fileName": "golden-standard.json",
      "recordCount": 25
    }
  ]
}
```

### GET /api/v1/datasets/{datasetId}

**Description**: Get dataset content by dataset ID.

**Parameters**:
- `datasetId` (path, required): Unique identifier of the dataset

**Response**:
- **200 OK**: Dataset content retrieved successfully
- **404 Not Found**: Dataset not found
- **400 Bad Request**: Invalid dataset ID
- **500 Internal Server Error**: Server error

**Response Body** (200 OK):
```json
[
  {
    "prompt": "What is the capital of France?",
    "groundTruth": "Paris",
    "actualResponse": "Paris is the capital of France.",
    "estimatedResponse": "The capital of France is Paris."
  },
  {
    "prompt": "Explain machine learning",
    "groundTruth": "Machine learning is a method of data analysis that automates analytical model building.",
    "actualResponse": "Machine learning allows computers to learn from data without explicit programming.",
    "estimatedResponse": "ML is when computers learn patterns from data automatically."
  }
]
```

### GET /api/v1/datasets/{datasetId}/metadata

**Description**: Get dataset metadata by dataset ID.

**Parameters**:
- `datasetId` (path, required): Unique identifier of the dataset

**Response**:
- **200 OK**: Dataset metadata retrieved successfully
- **404 Not Found**: Dataset not found
- **400 Bad Request**: Invalid dataset ID
- **500 Internal Server Error**: Server error

**Response Body** (200 OK):
```json
{
  "datasetId": "87654321-4321-4321-4321-210987654321",
  "lastUpdatedOn": "2024-01-01T12:00:00.000Z",
  "agentId": "agent-001",
  "datasetType": "Synthetic",
  "fileName": "evaluation-dataset-v1.json",
  "recordCount": 50
}
```

---

## Evaluation Runs

The Evaluation Run endpoints allow you to manage evaluation run lifecycles for AI agents.

### POST /api/v1/eval/runs

**Description**: Create a new evaluation run.

**Request Body**:
```json
{
  "agentId": "agent-001",
  "dataSetId": "87654321-4321-4321-4321-210987654321",
  "metricsConfigurationId": "12345678-1234-1234-1234-123456789012"
}
```

**Parameters** (Request Body):
- `agentId` (string, required): Unique identifier of the agent
- `datasetId` (string, required): GUID of the dataset to evaluate
- `metricsConfigurationId` (string, required): GUID of the metrics configuration

**Response**:
- **201 Created**: Evaluation run created successfully
- **400 Bad Request**: Invalid input data or validation failed
- **500 Internal Server Error**: Server error

**Response Body**:
```json
{
  "evalRunId": "98765432-8765-4321-1234-567890123456",
  "status": "Queued",
  "agentId": "agent-001",
  "datasetId": "87654321-4321-4321-4321-210987654321",
  "metricsConfigurationId": "12345678-1234-1234-1234-123456789012",
  "createdOn": "2024-01-01T12:00:00.000Z"
}
```

### PUT /api/v1/eval/runs/updatestatus

**Description**: Update the status of an evaluation run.

**Parameters**:
- `evalRunId` (path, required): Unique identifier of the evaluation run

**Request Body**:
```json
{
  "status": "Completed"
}
```

**Parameters** (Request Body):
- `status` (string, required): New status. Valid values: "", "Failed"

**Response**:
- **200 OK**: Status updated successfully
- **400 Bad Request**: Invalid input or EvalRunId not found
- **500 Internal Server Error**: Server error

**Response Body**:
```json
{
  "success": true,
  "message": "Evaluation run status updated successfully"
}
```

### GET /api/v1/eval/runs/{evalRunId}

**Description**: Get evaluation run details by ID.

**Parameters**:
- `evalRunId` (path, required): Unique identifier of the evaluation run

**Response**:
- **200 OK**: Evaluation run retrieved successfully
- **400 Bad Request**: Invalid EvalRunId
- **404 Not Found**: Evaluation run not found
- **500 Internal Server Error**: Server error

**Response Body** (200 OK):
```json
{
  "evalRunId": "98765432-8765-4321-1234-567890123456",
  "status": "Completed",
  "agentId": "agent-001",
  "datasetId": "87654321-4321-4321-4321-210987654321",
  "metricsConfigurationId": "12345678-1234-1234-1234-123456789012",
  "createdOn": "2024-01-01T12:00:00.000Z",
  "lastUpdatedOn": "2024-01-01T12:30:00.000Z",
  "blobFilePath": "evaluations/98765432-8765-4321-1234-567890123456.json"
}
```

---

## Evaluation Results

The Evaluation Result endpoints allow you to save and retrieve evaluation results for completed runs.

### POST /api/v1/eval/results

**Description**: Save evaluation results for a specific evaluation run. Results can only be saved for evaluations with status 'Completed' or 'Failed'.

**Request Body**:
```json
{
  "evalRunId": "98765432-8765-4321-1234-567890123456",
  "fileName": "evaluation-results.json",
  "evaluationRecords": [
    {
      "id": 1,
      "question": "What is the release year of Inception?",
      "actualAnswer": "The release year of the movie Inception is 2010.",
      "expectedAnswer": "The release year of the movie Inception is 2010.",
      "metrics": {
        "similarity": {
          "similarity": 5.0,
          "gpt_similarity": 5.0,
          "similarity_result": "pass",
          "similarity_threshold": 3,
          "reason": ""
        },
        "f1": {
          "f1_score": 1.0,
          "f1_result": "pass",
          "f1_threshold": 0.5,
          "reason": ""
        }
      }
    }
  ]
}
```

**Parameters** (Request Body):
- `evalRunId` (string, required): GUID of the evaluation run (must have status 'Completed' or 'Failed')
- `fileName` (string, required): Name of the results file
- `evaluationRecords` (any JSON structure, required): Flexible JSON structure containing evaluation results

**Response**:
- **200 OK**: Evaluation results saved successfully
- **400 Bad Request**: Invalid input data, EvalRunId not found, or evaluation run status is not terminal (must be 'Completed' or 'Failed')
- **403 Forbidden**: Access denied - authorization failed
- **500 Internal Server Error**: Server error

**Response Body**:
```json
{
  "success": true,
  "message": "Evaluation results saved successfully",
  "evalRunId": "98765432-8765-4321-1234-567890123456",
  "blobPath": "evaluations/98765432-8765-4321-1234-567890123456.json"
}
```

**Note**: The `evaluationRecords` field accepts any JSON structure and stores it exactly as provided. This allows for maximum flexibility in the format of evaluation results while maintaining the core functionality of associating results with evaluation runs.

### GET /api/v1/eval/results/{evalRunId}

**Description**: Get evaluation results by evaluation run ID.

**Parameters**:
- `evalRunId` (path, required): Unique identifier of the evaluation run

**Response**:
- **200 OK**: Evaluation results retrieved successfully
- **400 Bad Request**: Invalid EvalRunId
- **404 Not Found**: Evaluation results not found or run hasn't completed
- **500 Internal Server Error**: Server error

**Response Body** (200 OK):
```json
{
  "success": true,
  "evalRunId": "98765432-8765-4321-1234-567890123456",
  "fileName": "evaluation-results.json",
  "evaluationRecords": [
    {
      "metricName": "accuracy",
      "threshold": 0.85,
      "result": "pass",
      "tests_ran": 100,
      "percentageTestsPassed": 85.5
    }
  ]
}
```

### GET /api/v1/eval/results/agent/{agentId}/daterange

**Description**: Get evaluation results for a specific agent within a date range.

**Parameters**:
- `agentId` (path, required): Unique identifier of the agent
- `startDateTime` (query, required): Start date and time (ISO 8601 format)
- `endDateTime` (query, required): End date and time (ISO 8601 format)

**Response**:
- **200 OK**: Evaluation results retrieved successfully
- **400 Bad Request**: Invalid parameters
- **403 Forbidden**: Access denied - authorization failed
- **500 Internal Server Error**: Server error

**Response Body** (200 OK):
```json
[
  {
    "success": true,
    "evalRunId": "98765432-8765-4321-1234-567890123456",
    "fileName": "evaluation-results.json",
    "evaluationRecords": [
      {
        "metricName": "accuracy",
        "threshold": 0.85,
        "result": "pass",
        "tests_ran": 50,
        "percentageTestsPassed": 92.5
      }
    ]
  }
]
```

### GET /api/v1/eval/results/agent/{agentId}

**Description**: Get all evaluation runs for a specific agent.

**Parameters**:
- `agentId` (path, required): Unique identifier of the agent

**Response**:
- **200 OK**: Evaluation runs retrieved successfully
- **400 Bad Request**: Invalid AgentId
- **403 Forbidden**: Access denied - authorization failed
- **500 Internal Server Error**: Server error

**Response Body** (200 OK):
```json
[
  {
    "evalRunId": "98765432-8765-4321-1234-567890123456",
    "status": "Completed",
    "agentId": "agent-001",
    "datasetId": "87654321-4321-4321-4321-210987654321",
    "metricsConfigurationId": "12345678-1234-1234-1234-123456789012",
    "createdOn": "2024-01-01T12:00:00.000Z",
    "lastUpdatedOn": "2024-01-01T12:30:00.000Z",
    "blobFilePath": "evaluations/98765432-8765-4321-1234-567890123456.json"
  },
  {
    "evalRunId": "11111111-2222-3333-4444-555555555555",
    "status": "Running",
    "agentId": "agent-001",
    "datasetId": "22222222-3333-4444-5555-666666666666",
    "metricsConfigurationId": "33333333-4444-5555-6666-777777777777",
    "createdOn": "2024-01-02T10:00:00.000Z",
    "lastUpdatedOn": "2024-01-02T10:15:00.000Z",
    "blobFilePath": null
  }
]
```

---

## Data Models

### MetricsConfiguration

```json
{
  "metricName": "string",
  "threshold": "number (double)"
}
```

### CreateMetricsConfigurationDto

```json
{
  "agentId": "string (required)",
  "configurationName": "string (required, 1-100 chars)",
  "description": "string (optional, max 500 chars)",
  "metricsConfiguration": "array of MetricsConfiguration (required)"
}
```

### EvalDataset

```json
{
  "prompt": "string",
  "groundTruth": "string",
  "actualResponse": "string",
  "estimatedResponse": "string"
}
```

### SaveDatasetDto

```json
{
  "agentId": "string (required)",
  "datasetType": "string (required, 'Synthetic' or 'Golden')",
  "fileName": "string (required, 1-100 chars)",
  "datasetRecords": "array of EvalDataset (required, min 1 item)"
}
```

### DatasetMetadataDto

```json
{
  "datasetId": "string",
  "lastUpdatedOn": "datetime",
  "agentId": "string",
  "datasetType": "string",
  "fileName": "string",
  "recordCount": "number (integer)"
}
```

### CreateEvalRunDto

```json
{
  "agentId": "string (required)",
  "datasetId": "string (required, GUID)",
  "metricsConfigurationId": "string (required, GUID)"
}
```

### EvalRunDto

```json
{
  "evalRunId": "string (GUID)",
  "status": "string ('Queued', 'Running', 'Completed', 'Failed')",
  "agentId": "string",
  "datasetId": "string (GUID)",
  "metricsConfigurationId": "string (GUID)",
  "createdOn": "datetime",
  "lastUpdatedOn": "datetime",
  "blobFilePath": "string (optional)"
}
```

### UpdateEvalRunStatusDto

```json
{
  "status": "string (required, 'Queued', 'Running', 'Completed', 'Failed')"
}
```

### SaveEvaluationResultDto

```json
{
  "evalRunId": "string (required, GUID)",
  "fileName": "string (required)",
  "evaluationRecords": "any JSON structure (required)"
}
```

**Note**: The `evaluationRecords` field accepts any JSON structure. It can be an array of objects, a single object, or any valid JSON format. The structure will be stored exactly as provided without any modification or validation of the internal structure.

**Example with flexible structure**:
```json
{
  "evalRunId": "12345678-1234-1234-1234-123456789012",
  "fileName": "test-results.json",
  "evaluationRecords": [
    {
      "id": 1,
      "question": "What is the release year of Inception?",
      "actualAnswer": "The release year of the movie Inception is 2010.",
      "expectedAnswer": "The release year of the movie Inception is 2010.",
      "metrics": {
        "similarity": {
          "similarity": 5.0,
          "gpt_similarity": 5.0,
          "similarity_result": "pass",
          "similarity_threshold": 3,
          "reason": ""
        },
        "f1": {
          "f1_score": 1.0,
          "f1_result": "pass",
          "f1_threshold": 0.5,
          "reason": ""
        }
      }
    }
  ]
}
```

### EvaluationResultSaveResponseDto

```json
{
  "success": "boolean",
  "message": "string",
  "evalRunId": "string (GUID)",
  "blobPath": "string (optional)"
}
```

### EvaluationResultResponseDto

```json
{
  "success": "boolean",
  "evalRunId": "string (GUID)",
  "fileName": "string (optional)",
  "evaluationRecords": "any JSON structure (optional)"
}
```

**Note**: The `evaluationRecords` field returns the exact JSON structure that was originally submitted, without any modification.

---

## Error Handling

The API uses standard HTTP status codes and returns error information in a consistent format.

### Standard Error Response

```json
{
  "error": {
    "message": "Error description",
    "details": "Additional error details (optional)"
  }
}
```

### Common HTTP Status Codes

- **200 OK**: Request successful
- **201 Created**: Resource created successfully
- **400 Bad Request**: Invalid request data or parameters
- **404 Not Found**: Requested resource not found
- **500 Internal Server Error**: Unexpected server error

### Validation Errors

For validation errors (400 Bad Request), the response includes detailed field-level errors:

```json
{
  "errors": {
    "AgentId": ["The AgentId field is required."],
    "DatasetType": ["DatasetType must be either 'Synthetic' or 'Golden'"],
    "DatasetRecords": ["The DatasetRecords field must contain at least 1 item."]
  }
}
```

---

## Examples

### Creating a New Configuration

```bash
curl -X POST "https://your-domain/api/v1/eval/configurations" \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "agent-001",
    "configurationName": "accuracy-config",
    "description": "High accuracy configuration",
    "metricsConfiguration": [
      {
        "metricName": "accuracy",
        "threshold": 0.95
      }
    ]
  }'
```

### Saving a Dataset

```bash
curl -X POST "https://your-domain/api/v1/datasets" \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "agent-001",
    "datasetType": "Synthetic",
    "fileName": "test-data.json",
    "datasetRecords": [
      {
        "prompt": "What is 2+2?",
        "groundTruth": "4",
        "actualResponse": "The answer is 4",
        "estimatedResponse": "2+2=4"
      }
    ]
  }'
```

### Retrieving Agent Configurations

```bash
curl -X GET "https://your-domain/api/v1/eval/configurations/agent-001"
```

### Getting Dataset List

```bash
curl -X GET "https://your-domain/api/v1/datasets/agent/agent-001"
```

### Creating an Evaluation Run

```bash
curl -X POST "https://your-domain/api/v1/evalrun" \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "agent-001",
    "datasetId": "87654321-4321-4321-4321-210987654321",
    "metricsConfigurationId": "12345678-1234-1234-1234-123456789012"
  }'
```

### Updating Evaluation Run Status

```bash
curl -X PUT "https://your-domain/api/v1/evalrun/98765432-8765-4321-1234-567890123456" \
  -H "Content-Type: application/json" \
  -d '{
    "status": "Completed"
  }'
```

### Saving Evaluation Results

```bash
curl -X POST "https://your-domain/api/v1/evalresult" \
  -H "Content-Type: application/json" \
  -d '{
    "evalRunId": "98765432-8765-4321-1234-567890123456",
    "fileName": "results-batch-1.json",
    "evaluationRecords": [
      {
        "prompt": "What is machine learning?",
        "groundTruth": "A method of data analysis that automates model building",
        "actualResponse": "Machine learning is AI that learns from data",
        "isCorrect": true,
        "confidence": 0.87,
        "responseTime": 250,
        "percentageTestsPassed": 92.5,
        "numberOfTestsRan": 50
      }
    ]
  }'
```

### Getting Evaluation Results

```bash
curl -X GET "https://your-domain/api/v1/evalresult/98765432-8765-4321-1234-567890123456"
```

### Getting Agent Evaluation History

```bash
curl -X GET "https://your-domain/api/v1/evalresult/agent/agent-001"
```

---

## Architecture Notes

- **Storage**: The API uses Azure Blob Storage for storing dataset content, evaluation results, and configuration data, with Azure Table Storage for metadata management and evaluation run tracking.
- **Evaluation Workflow**: The evaluation process follows a lifecycle: Create evaluation run → Update status to "Running" → Save results → Update status to "Completed"/"Failed".
- **Agent Organization**: Evaluation results are organized by agent ID in blob storage containers, with each evaluation run stored as a separate JSON file.
- **Authentication**: Currently not implemented. Consider implementing OAuth 2.0 or API key authentication for production use.
- **Logging**: Comprehensive logging is implemented throughout the application for monitoring and debugging.
- **Validation**: Input validation is performed using data annotations and custom validation logic.
- **Error Handling**: Consistent error handling with proper HTTP status codes and error messages.

---

## Version History

- **v1.0.0**: Initial release with basic configuration and dataset management functionality.

---

*For technical support or questions about this API, please contact the SXG development team.*