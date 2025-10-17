# SXG Evaluation Platform API Documentation

## Overview

The SXG Evaluation Platform API provides a comprehensive set of endpoints for managing evaluation runs, datasets, metrics configurations, and evaluation results. This API enables clients to create, monitor, and analyze AI model evaluations using a RESTful interface built with .NET 8.

**Environments**:
- **Development**: `https://sxgevalapidev.azurewebsites.net` (Storage: `sxgagentevaldev`)
- **PPE (Client Environment)**: `https://sxgevalapippe.azurewebsites.net` (Storage: `sxgagentevalppe`)

**API Version**: v1  
**Content Type**: `application/json`  
**Authentication**: OAuth using Azure Active Directory  

## Table of Contents

1. [Quick Start](#quick-start)
2. [Authentication](#authentication)
3. [API Endpoints](#api-endpoints)
   - [Health Check](#health-check)
   - [Evaluation Runs](#evaluation-runs)
   - [Evaluation Results](#evaluation-results)
   - [Datasets](#datasets)
   - [Metrics Configuration](#metrics-configuration)
4. [Data Models](#data-models)
5. [Error Handling](#error-handling)
6. [Best Practices](#best-practices)
7. [Integration Examples](#integration-examples)

---

## Quick Start

### 1. Check API Health

**Development Environment:**
```bash
curl -X GET https://sxgevalapidev.azurewebsites.net/api/v1/health
```

**PPE Environment (Client):**
```bash
curl -X GET https://sxgevalapippe.azurewebsites.net/api/v1/health
```

### 2. Create Evaluation Run

**Development Environment:**
```bash
curl -X POST https://sxgevalapidev.azurewebsites.net/api/v1/eval/runs \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "agentId": "my-ai-agent",
    "dataSetId": "golden-dataset-001", 
    "metricsConfigurationId": "standard-metrics"
  }'
```

**PPE Environment (Client):**
```bash
curl -X POST https://sxgevalapippe.azurewebsites.net/api/v1/eval/runs \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "agentId": "my-ai-agent",
    "dataSetId": "golden-dataset-001", 
    "metricsConfigurationId": "standard-metrics"
  }'
```

### 3. Update Status and Save Results
```bash
# Update to completed (replace BASE_URL with your environment)
curl -X PUT {BASE_URL}/api/v1/eval/runs/{evalRunId} \
  -H "Content-Type: application/json" \
  -d '{"status": "completed"}'

# Save results (replace BASE_URL with your environment)
curl -X POST {BASE_URL}/api/v1/eval/results \
  -H "Content-Type: application/json" \
  -d '{
    "evalRunId": "{evalRunId}",
    "fileName": "results.json",
    "evaluationRecords": [{"id": 1, "score": 0.95}]
  }'
```

---

## Authentication

The API uses OAuth authentication with Azure Active Directory.

**Requirements**:
- Applications must be registered in Azure AD
- Admin consent required for API access
- User tokens required (App-to-App authentication not supported for evaluation triggers)

**Authentication Flow**:
1. Obtain bearer token from Azure AD
2. Include token in Authorization header: `Authorization: Bearer {token}`
3. Tokens are validated against Azure AD for each request

---

## API Endpoints

### Health Check

#### GET /api/v1/health
**Description**: Check if the API is running and healthy.

**Authentication**: Not required

**Response** (200 OK):
```json
{
  "status": "Healthy",
  "timestamp": "2025-10-17T10:30:00.000Z",
  "version": "1.0.0",
  "environment": "Development"
}
```

**Use Cases**:
- Monitor API availability
- Service health checks
- Version verification

---

### Evaluation Runs

Evaluation runs represent the execution of an AI model evaluation using specific datasets and metrics configurations.

#### 1. Create Evaluation Run
**Endpoint**: `POST /api/v1/eval/runs`

**Description**: Create a new evaluation run with specified dataset and metrics configuration.

**Authentication**: Required

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
- Start new model evaluations
- Queue evaluations for batch processing
- Initiate automated testing workflows

#### 2. Update Evaluation Run Status
**Endpoint**: `PUT /api/v1/eval/runs/{evalRunId}`

**Description**: Update the status of an existing evaluation run. Status updates are case insensitive.

**Authentication**: Required

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

**Valid State Transitions**:
- `Queued` → `Running`, `Completed`, `Failed`
- `Running` → `Completed`, `Failed`
- `Completed` → ❌ (Terminal state - no further updates)
- `Failed` → ❌ (Terminal state - no further updates)

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Status updated successfully"
}
```

**Terminal State Error** (400 Bad Request):
```json
{
  "success": false,
  "message": "Cannot update status for evaluation run with ID {evalRunId}. The evaluation run is already in a terminal state '{currentStatus}' and cannot be modified."
}
```

**Important Notes**:
- Once an evaluation run reaches a terminal state (`Completed` or `Failed`), its status cannot be updated
- Status comparisons are case insensitive for flexibility
- Status values are normalized to Pascal case in storage

#### 3. Get Evaluation Run
**Endpoint**: `GET /api/v1/eval/runs/{evalRunId}`

**Description**: Retrieve details of a specific evaluation run.

**Authentication**: Required

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

---

### Evaluation Results

Evaluation results contain the detailed output and metrics from completed evaluation runs.

#### 1. Save Evaluation Results
**Endpoint**: `POST /api/v1/eval/results`

**Description**: Save evaluation results for a completed evaluation run. Results can only be saved for runs with status "Completed" or "Failed".

**Authentication**: Required

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

**Storage Structure**: Results are stored in Azure Blob Storage using the folder structure:
- Container: `{agentid}` (lowercase)
- Path: `evalresults/{evalrunid}/{filename}`

#### 2. Get Evaluation Results
**Endpoint**: `GET /api/v1/eval/results/{evalRunId}`

**Description**: Retrieve evaluation results for a specific evaluation run.

**Authentication**: Required

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

#### 3. Get Evaluation Runs by Agent
**Endpoint**: `GET /api/v1/eval/results/agent/{agentId}`

**Description**: Retrieve all evaluation runs for a specific agent.

**Authentication**: Required

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
  }
]
```

#### 4. Get Evaluation Results by Date Range
**Endpoint**: `GET /api/v1/eval/results/agent/{agentId}/daterange`

**Description**: Retrieve evaluation results for a specific agent within a date range.

**Authentication**: Required

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

---

### Datasets

Datasets contain the test data used for evaluations, including prompts, expected responses, and ground truth data.

#### 1. Get Datasets by Agent
**Endpoint**: `GET /api/v1/datasets`

**Description**: Retrieve all datasets associated with a specific agent.

**Authentication**: Required

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

#### 2. Get Dataset Content
**Endpoint**: `GET /api/v1/datasets/{datasetId}`

**Description**: Retrieve the full content of a specific dataset.

**Authentication**: Required

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
  }
]
```

#### 3. Save Dataset
**Endpoint**: `POST /api/v1/datasets`

**Description**: Create or update a dataset for a specific agent.

**Authentication**: Required

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

---

### Metrics Configuration

#### Get Default Metrics Configuration
**Endpoint**: `GET /api/v1/eval/defaultconfiguration`

**Description**: Retrieve the default metrics configuration template.

**Authentication**: Required

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

---

## Data Models

### Core Entities

#### EvalRunDto
```json
{
  "evalRunId": "uuid",
  "agentId": "string",
  "dataSetId": "string",
  "metricsConfigurationId": "string",
  "status": "string",
  "lastUpdatedBy": "string",
  "lastUpdatedOn": "datetime",
  "startedDatetime": "datetime",
  "completedDatetime": "datetime"
}
```

#### CreateEvalRunDto
```json
{
  "agentId": "string (required, 1-100 chars)",
  "dataSetId": "string (required, 1-100 chars)",
  "metricsConfigurationId": "string (required, 1-100 chars)"
}
```

#### UpdateStatusDto
```json
{
  "status": "string (required)"
}
```

#### SaveEvaluationResultDto
```json
{
  "evalRunId": "uuid (required)",
  "fileName": "string (required, 1-100 chars)",
  "evaluationRecords": "object (required, flexible JSON structure)"
}
```

### Status Constants
- `Queued` - Waiting to be processed
- `Running` - Currently being processed
- `Completed` - Successfully finished (terminal state)
- `Failed` - Encountered an error (terminal state)

---

## Error Handling

### HTTP Status Codes
- `200 OK` - Successful GET, PUT operations
- `201 Created` - Successful POST operations
- `400 Bad Request` - Invalid input data, validation errors, terminal state violations
- `401 Unauthorized` - Authentication failed
- `403 Forbidden` - Authorization failed
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Server-side errors

### Error Response Format
```json
{
  "success": false,
  "message": "Error description",
  "details": "Additional error details (optional)"
}
```

### Common Error Scenarios

**400 Bad Request** - Invalid Input:
```json
{
  "success": false,
  "message": "Invalid input data",
  "details": "AgentId is required and cannot be empty"
}
```

**400 Bad Request** - Terminal State Violation:
```json
{
  "success": false,
  "message": "Cannot update status for evaluation run with ID {evalRunId}. The evaluation run is already in a terminal state 'Completed' and cannot be modified."
}
```

**404 Not Found**:
```json
{
  "success": false,
  "message": "Evaluation run with ID 550e8400-e29b-41d4-a716-446655440000 not found"
}
```

---

## Azure Storage Architecture

### Table Storage Partitioning
- **Partition Key**: `AgentId` for optimal performance and multi-tenant support
- **Row Key**: `EvalRunId` for unique identification
- **Benefits**: Efficient agent-based queries and load distribution

### Blob Storage Organization
- **Container**: Agent-specific containers using lowercase agent IDs (e.g., `agent123`)
- **Folder Structure**: `evalresults/{evalRunId}/`
- **Multiple Files**: Supports multiple output files per evaluation run
  - `evalresults/{evalRunId}/results.json`
  - `evalresults/{evalRunId}/detailed-metrics.json`
  - `evalresults/{evalRunId}/summary-report.csv`

---

## Best Practices

### 1. Status Management
- Always check evaluation run status before saving results
- Use case-insensitive status values for flexibility
- Handle terminal state violations gracefully
- Remember that `Completed` and `Failed` states cannot be updated

### 2. Data Organization
- Use meaningful `agentId` values for easy identification
- Include descriptive dataset names
- Store comprehensive evaluation records for analysis
- Use consistent naming conventions for result files

### 3. Error Handling
- Always check response status codes
- Handle authentication and authorization errors appropriately
- Implement retry logic for transient failures
- Parse error messages for specific business rule violations

### 4. Performance
- Use date range queries to limit result sets
- Implement pagination for large datasets
- Cache frequently accessed configuration data
- Use agent-based filtering for better performance

### 5. Security
- Secure storage of authentication tokens
- Regular rotation of access keys
- Monitor for unusual access patterns
- Validate all input data

### 6. Monitoring
- Use the health endpoint for service monitoring
- Track evaluation run completion rates
- Monitor storage usage for large result sets
- Implement proper logging for debugging

---

## Integration Examples

### Complete Evaluation Workflow
```bash
#!/bin/bash

# Configuration - Choose your environment
# For Development:
BASE_URL="https://sxgevalapidev.azurewebsites.net/api/v1"
# For PPE (Client):
# BASE_URL="https://sxgevalapippe.azurewebsites.net/api/v1"

AUTH_TOKEN="your-bearer-token"
AGENT_ID="my-agent"
DATASET_ID="dataset-001"
METRICS_CONFIG_ID="metrics-001"

# 1. Create evaluation run
echo "Creating evaluation run..."
EVAL_RUN_RESPONSE=$(curl -s -X POST "$BASE_URL/eval/runs" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $AUTH_TOKEN" \
  -d "{
    \"agentId\": \"$AGENT_ID\",
    \"dataSetId\": \"$DATASET_ID\",
    \"metricsConfigurationId\": \"$METRICS_CONFIG_ID\"
  }")

EVAL_RUN_ID=$(echo $EVAL_RUN_RESPONSE | jq -r '.evalRunId')
echo "Created evaluation run: $EVAL_RUN_ID"

# 2. Update status to running
echo "Updating status to Running..."
curl -s -X PUT "$BASE_URL/eval/runs/$EVAL_RUN_ID" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $AUTH_TOKEN" \
  -d '{"status":"Running"}'

# 3. Simulate evaluation processing...
echo "Processing evaluation..."
sleep 5

# 4. Update status to completed
echo "Updating status to Completed..."
curl -s -X PUT "$BASE_URL/eval/runs/$EVAL_RUN_ID" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $AUTH_TOKEN" \
  -d '{"status":"Completed"}'

# 5. Save evaluation results
echo "Saving evaluation results..."
curl -s -X POST "$BASE_URL/eval/results" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $AUTH_TOKEN" \
  -d "{
    \"evalRunId\": \"$EVAL_RUN_ID\",
    \"fileName\": \"results.json\",
    \"evaluationRecords\": [
      {
        \"id\": 1,
        \"question\": \"Test question\",
        \"actualAnswer\": \"Test answer\",
        \"metrics\": {
          \"accuracy\": 0.95,
          \"confidence\": 0.87
        }
      }
    ]
  }"

# 6. Retrieve results
echo "Retrieving evaluation results..."
curl -s -X GET "$BASE_URL/eval/results/$EVAL_RUN_ID" \
  -H "Authorization: Bearer $AUTH_TOKEN"

echo "Evaluation workflow completed!"
```

### Error Handling Example
```javascript
async function updateEvaluationStatus(evalRunId, status) {
  try {
    const response = await fetch(`/api/v1/eval/runs/${evalRunId}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${authToken}`
      },
      body: JSON.stringify({ status })
    });

    const result = await response.json();

    if (!response.ok) {
      if (response.status === 400 && result.message.includes('terminal state')) {
        console.log('Cannot update: evaluation run is in terminal state');
        return { success: false, reason: 'terminal_state' };
      }
      throw new Error(result.message);
    }

    return { success: true, data: result };
  } catch (error) {
    console.error('Error updating evaluation status:', error);
    return { success: false, error: error.message };
  }
}

// Usage
const result = await updateEvaluationStatus('eval-run-id', 'completed');
if (result.success) {
  console.log('Status updated successfully');
} else if (result.reason === 'terminal_state') {
  console.log('Evaluation already completed - skipping status update');
} else {
  console.error('Failed to update status:', result.error);
}
```

---

## Support and Resources

### Development Resources
- **Swagger UI**: `https://sxgevalapidev.azurewebsites.net/swagger/index.html`
- **Source Code**: Available in the repository under `src/Sxg-Eval-Platform-Api/`
- **Azure Documentation**: [Azure Active Directory Authentication](https://docs.microsoft.com/en-us/azure/active-directory/)

### Getting Help
- Review this documentation for API usage patterns
- Check the Swagger UI for interactive API exploration
- Implement proper error handling based on the examples provided
- Monitor API health using the health check endpoint

This comprehensive documentation provides all the information needed to integrate with the SXG Evaluation Platform API effectively, including the latest enhancements for case insensitive status updates and terminal state protection.