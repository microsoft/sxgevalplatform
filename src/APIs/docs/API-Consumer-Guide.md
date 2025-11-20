# SXG Evaluation Platform API - Consumer Guide

**Version:** 1.0.0  
**Base URL:** `https://sxgevalapidev.azurewebsites.net` (Development)  
**Swagger UI:** `https://sxgevalapidev.azurewebsites.net/swagger`

---

## Table of Contents

1. [Introduction](#introduction)
2. [Getting Started](#getting-started)
3. [Authentication](#authentication)
4. [API Endpoints](#api-endpoints)
   - [Health Check](#health-check)
   - [Evaluation Configurations](#evaluation-configurations)
   - [Datasets](#datasets)
   - [Evaluation Runs](#evaluation-runs)
   - [Evaluation Results](#evaluation-results)
5. [Data Models](#data-models)
6. [Error Handling](#error-handling)
7. [Best Practices](#best-practices)
8. [Code Examples](#code-examples)
9. [Rate Limits and Quotas](#rate-limits-and-quotas)
10. [Support](#support)

---

## Introduction

The SXG Evaluation Platform API is a RESTful service that enables you to:

- **Manage Evaluation Configurations**: Create and configure metrics for evaluating AI agents
- **Store Datasets**: Upload and manage golden (reference) and synthetic (generated) datasets
- **Run Evaluations**: Execute evaluation runs against your AI agents
- **Track Results**: Store and retrieve evaluation results with comprehensive metrics
- **Monitor Health**: Check API and dependency health status

### Key Features

? **Multi-tenant Support**: Agent-based data isolation  
? **Flexible Metrics**: Customizable evaluation configurations  
? **Rich Telemetry**: Comprehensive logging and monitoring with Application Insights  
? **Azure Integration**: Built on Azure Storage (Tables, Blobs, Queues)  
? **Caching**: Redis-based caching for improved performance  
? **RESTful Design**: Clean, predictable API structure

---

## Getting Started

### Prerequisites

- Azure subscription with access to the SXG Evaluation Platform
- API access credentials (Azure AD authentication)
- HTTP client (cURL, Postman, or your preferred programming language)

### Quick Start

1. **Check API Health**
   ```bash
   curl -X GET https://sxgevalapidev.azurewebsites.net/api/v1/health
   ```

2. **Explore API Documentation**
   - Visit Swagger UI: `https://sxgevalapidev.azurewebsites.net/swagger`
   - All endpoints are documented with request/response examples

3. **Create Your First Evaluation Configuration**
   - See [Evaluation Configurations](#evaluation-configurations) section

4. **Upload a Dataset**
   - See [Datasets](#datasets) section

5. **Run an Evaluation**
   - See [Evaluation Runs](#evaluation-runs) section

---

## Authentication

### Azure Active Directory (OAuth 2.0)

The API uses Azure AD for authentication. All requests must include a valid bearer token.

#### Obtaining a Token

```bash
# Get token using Azure CLI
az account get-access-token --resource https://your-api.azurewebsites.net
```

#### Using the Token

Include the token in the `Authorization` header:

```http
Authorization: Bearer eyJ0eXAiOiJKV1QiLCJhbGc...
```

#### Example with cURL

```bash
curl -X GET \
  https://sxgevalapidev.azurewebsites.net/api/v1/eval/configurations/defaultconfiguration \
  -H 'Authorization: Bearer YOUR_TOKEN_HERE'
```

---

## API Endpoints

### Health Check

Monitor API and dependency health status.

#### Basic Health Check

**Endpoint:** `GET /api/v1/health`

**Response:**
```json
{
  "status": "Healthy",
  "timestamp": "2024-01-15T10:30:00.000Z",
  "version": "1.0.0",
  "environment": "Development"
}
```

#### Detailed Health Check

**Endpoint:** `GET /api/v1/health/detailed`

**Response:**
```json
{
  "status": "Healthy",
  "timestamp": "2024-01-15T10:30:00.000Z",
  "version": "1.0.0",
  "environment": "Development",
  "machineName": "prod-vm-01",
  "processId": 12345,
  "dependencies": [
    {
  "name": "AzureBlobStorage",
      "status": "Healthy",
      "responseTime": "00:00:00.045",
      "errorMessage": null
    },
    {
    "name": "AzureTableStorage",
      "status": "Healthy",
      "responseTime": "00:00:00.034",
    "errorMessage": null
    },
    {
      "name": "Cache (Redis)",
   "status": "Healthy",
  "responseTime": "00:00:00.023",
      "errorMessage": null,
      "additionalInfo": "Version: 7.2.4, Clients: 5, Memory: 2.5MB"
    },
    {
      "name": "ApplicationInsights",
   "status": "Healthy",
      "responseTime": "00:00:00.012",
      "errorMessage": null
    }
  ],
  "openTelemetry": {
    "enabled": true,
    "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
    "spanId": "00f067aa0ba902b7",
    "activityId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
  }
}
```

---

### Evaluation Configurations

Manage metrics configurations for your evaluations.

#### Get Default Configuration

**Endpoint:** `GET /api/v1/eval/configurations/defaultconfiguration`

**Description:** Retrieve the platform's default metrics configuration.

**Response:**
```json
{
  "metrics": [
    {
      "name": "Accuracy",
      "weight": 0.4,
      "threshold": 0.85
    },
    {
 "name": "Precision",
      "weight": 0.3,
      "threshold": 0.80
  },
  {
   "name": "Recall",
      "weight": 0.3,
    "threshold": 0.75
    }
  ],
  "evaluationType": "Standard",
  "version": "1.0"
}
```

#### Get Configuration by ID

**Endpoint:** `GET /api/v1/eval/configurations/{configurationId}`

**Parameters:**
- `configurationId` (path, required): GUID of the configuration

**Example:**
```bash
GET /api/v1/eval/configurations/550e8400-e29b-41d4-a716-446655440000
```

**Response:**
```json
[
  {
    "configurationId": "550e8400-e29b-41d4-a716-446655440000",
 "agentId": "agent-001",
    "configurationName": "High Accuracy Config",
    "environmentName": "Production",
    "metrics": [...],
    "createdDateTime": "2024-01-10T08:00:00Z",
    "modifiedDateTime": "2024-01-15T10:30:00Z"
  }
]
```

#### Get Configurations by Agent

**Endpoint:** `GET /api/v1/eval/configurations`

**Query Parameters:**
- `agentId` (required): Agent identifier
- `environmentName` (optional): Filter by environment (Development, PPE, Production)

**Example:**
```bash
GET /api/v1/eval/configurations?agentId=agent-001&environmentName=Production
```

**Response:**
```json
[
  {
    "configurationId": "550e8400-e29b-41d4-a716-446655440000",
    "configurationName": "High Accuracy Config",
    "agentId": "agent-001",
    "environmentName": "Production",
    "createdDateTime": "2024-01-10T08:00:00Z",
    "modifiedDateTime": "2024-01-15T10:30:00Z"
  },
  {
    "configurationId": "660e8400-e29b-41d4-a716-446655440001",
    "configurationName": "Balanced Config",
    "agentId": "agent-001",
    "environmentName": "Production",
    "createdDateTime": "2024-01-12T14:00:00Z",
    "modifiedDateTime": "2024-01-12T14:00:00Z"
  }
]
```

#### Create Configuration

**Endpoint:** `POST /api/v1/eval/configurations`

**Request Body:**
```json
{
  "agentId": "agent-001",
  "configurationName": "My Custom Config",
  "environmentName": "Development",
  "selectedMetrics": [
    {
    "name": "Accuracy",
   "displayName": "Accuracy Score",
      "description": "Measures correctness of predictions",
    "weight": 0.5,
      "threshold": 0.90,
      "formula": "correct_predictions / total_predictions"
    },
    {
      "name": "F1Score",
      "displayName": "F1 Score",
      "description": "Harmonic mean of precision and recall",
      "weight": 0.5,
   "threshold": 0.85,
      "formula": "2 * (precision * recall) / (precision + recall)"
    }
  ]
}
```

**Response (201 Created):**
```json
{
"configurationId": "770e8400-e29b-41d4-a716-446655440002",
  "status": "created",
  "message": "Configuration created successfully"
}
```

**Response (409 Conflict - if configuration with same name exists):**
```json
{
  "configurationId": "770e8400-e29b-41d4-a716-446655440002",
  "status": "updated",
  "message": "Configuration with the same name already exists and has been updated"
}
```

#### Update Configuration

**Endpoint:** `PUT /api/v1/eval/configurations/{configurationId}`

**Request Body:**
```json
{
  "agentId": "agent-001",
  "configurationName": "Updated Config Name",
"environmentName": "Development",
  "selectedMetrics": [...]
}
```

**Response (200 OK):**
```json
{
"configurationId": "770e8400-e29b-41d4-a716-446655440002",
  "status": "updated",
  "message": "Configuration updated successfully"
}
```

#### Delete Configuration

**Endpoint:** `DELETE /api/v1/eval/configurations/{configurationId}`

**Response (200 OK):**
```json
{
  "message": "Configuration '770e8400-e29b-41d4-a716-446655440002' deleted successfully"
}
```

**Response (404 Not Found):**
```json
{
  "title": "Configuration Not Found",
  "status": 404,
  "detail": "Configuration with ID '770e8400-e29b-41d4-a716-446655440002' not found"
}
```

---

### Datasets

Manage evaluation datasets (golden and synthetic).

#### Get Datasets by Agent

**Endpoint:** `GET /api/v1/eval/datasets`

**Query Parameters:**
- `agentId` (required): Agent identifier

**Example:**
```bash
GET /api/v1/eval/datasets?agentId=agent-001
```

**Response:**
```json
[
  {
    "datasetId": "880e8400-e29b-41d4-a716-446655440003",
 "datasetName": "Customer Support Q&A",
    "agentId": "agent-001",
    "datasetType": "Golden",
    "recordCount": 500,
    "createdDateTime": "2024-01-05T12:00:00Z",
    "modifiedDateTime": "2024-01-05T12:00:00Z"
  },
  {
    "datasetId": "990e8400-e29b-41d4-a716-446655440004",
    "datasetName": "Synthetic Test Cases",
    "agentId": "agent-001",
    "datasetType": "Synthetic",
    "recordCount": 1000,
    "createdDateTime": "2024-01-08T09:00:00Z",
    "modifiedDateTime": "2024-01-08T09:00:00Z"
  }
]
```

#### Get Dataset by ID

**Endpoint:** `GET /api/v1/eval/datasets/{datasetId}`

**Response:**
```json
[
  {
    "question": "How do I reset my password?",
    "expectedAnswer": "Go to Settings > Security > Reset Password",
    "metadata": {
  "category": "authentication",
   "difficulty": "easy"
    }
  },
  {
    "question": "What are your business hours?",
    "expectedAnswer": "We are open Monday-Friday, 9 AM to 5 PM EST",
    "metadata": {
      "category": "general",
      "difficulty": "easy"
    }
  }
]
```

#### Create/Update Dataset

**Endpoint:** `POST /api/v1/eval/datasets`

**Request Body:**
```json
{
  "agentId": "agent-001",
  "datasetName": "Product FAQ Dataset",
  "datasetType": "Golden",
  "datasetRecords": [
    {
      "question": "What is your return policy?",
      "expectedAnswer": "We accept returns within 30 days of purchase",
      "metadata": {
"category": "returns",
        "priority": "high"
      }
    },
    {
      "question": "Do you ship internationally?",
      "expectedAnswer": "Yes, we ship to over 100 countries worldwide",
 "metadata": {
        "category": "shipping",
        "priority": "medium"
      }
    }
  ]
}
```

**Response (201 Created):**
```json
{
  "datasetId": "aa0e8400-e29b-41d4-a716-446655440005",
  "status": "created",
  "message": "Dataset created successfully"
}
```

**Response (200 OK - if dataset already exists):**
```json
{
  "datasetId": "aa0e8400-e29b-41d4-a716-446655440005",
  "status": "updated",
  "message": "Dataset updated successfully"
}
```

#### Update Existing Dataset

**Endpoint:** `PUT /api/v1/eval/datasets/{datasetId}`

**Request Body:**
```json
{
  "datasetRecords": [
    {
      "question": "Updated question",
      "expectedAnswer": "Updated answer"
    }
  ]
}
```

**Response (200 OK):**
```json
{
  "datasetId": "aa0e8400-e29b-41d4-a716-446655440005",
  "status": "updated",
  "message": "Dataset updated successfully"
}
```

#### Delete Dataset

**Endpoint:** `DELETE /api/v1/eval/datasets/{datasetId}`

**Response (200 OK):**
```json
{
  "message": "Dataset 'aa0e8400-e29b-41d4-a716-446655440005' deleted successfully"
}
```

---

### Evaluation Runs

Execute and manage evaluation runs.

#### Create Evaluation Run

**Endpoint:** `POST /api/v1/eval/runs`

**Request Body:**
```json
{
  "agentId": "agent-001",
  "evalRunName": "Production Accuracy Test",
  "dataSetId": "aa0e8400-e29b-41d4-a716-446655440005",
  "metricsConfigurationId": "770e8400-e29b-41d4-a716-446655440002",
  "type": "Automated",
  "environmentId": "Production",
  "agentSchemaName": "CustomerSupportAgent"
}
```

**Response (201 Created):**
```json
{
  "evalRunId": "bb0e8400-e29b-41d4-a716-446655440006",
  "agentId": "agent-001",
  "evalRunName": "Production Accuracy Test",
  "dataSetId": "aa0e8400-e29b-41d4-a716-446655440005",
  "metricsConfigurationId": "770e8400-e29b-41d4-a716-446655440002",
  "type": "Automated",
  "status": "Queued",
  "environmentId": "Production",
  "agentSchemaName": "CustomerSupportAgent",
  "createdDateTime": "2024-01-15T10:45:00Z",
  "modifiedDateTime": "2024-01-15T10:45:00Z"
}
```

#### Get Evaluation Run by ID

**Endpoint:** `GET /api/v1/eval/runs/{evalRunId}`

**Response:**
```json
{
  "evalRunId": "bb0e8400-e29b-41d4-a716-446655440006",
  "agentId": "agent-001",
  "evalRunName": "Production Accuracy Test",
  "dataSetId": "aa0e8400-e29b-41d4-a716-446655440005",
  "metricsConfigurationId": "770e8400-e29b-41d4-a716-446655440002",
  "type": "Automated",
  "status": "Running",
  "environmentId": "Production",
  "agentSchemaName": "CustomerSupportAgent",
  "createdDateTime": "2024-01-15T10:45:00Z",
  "modifiedDateTime": "2024-01-15T10:50:00Z",
  "startDateTime": "2024-01-15T10:46:00Z"
}
```

#### Get Evaluation Runs by Agent

**Endpoint:** `GET /api/v1/eval/runs`

**Query Parameters:**
- `agentId` (required): Agent identifier
- `startDateTime` (optional): Filter runs after this date (ISO 8601)
- `endDateTime` (optional): Filter runs before this date (ISO 8601)

**Example:**
```bash
GET /api/v1/eval/runs?agentId=agent-001&startDateTime=2024-01-01T00:00:00Z&endDateTime=2024-01-31T23:59:59Z
```

**Response:**
```json
[
  {
    "evalRunId": "bb0e8400-e29b-41d4-a716-446655440006",
    "agentId": "agent-001",
    "evalRunName": "Production Accuracy Test",
    "status": "Completed",
    "createdDateTime": "2024-01-15T10:45:00Z"
  },
  {
    "evalRunId": "cc0e8400-e29b-41d4-a716-446655440007",
    "agentId": "agent-001",
    "evalRunName": "Regression Test",
 "status": "Running",
    "createdDateTime": "2024-01-16T08:30:00Z"
  }
]
```

#### Update Evaluation Run Status

**Endpoint:** `PUT /api/v1/eval/runs/{evalRunId}/status`

**Request Body:**
```json
{
  "status": "Completed"
}
```

**Valid Status Values:**
- `Queued` - Initial state
- `Running` - Evaluation in progress
- `Completed` - Evaluation finished successfully
- `Failed` - Evaluation encountered an error

**Response (200 OK):**
```json
{
  "evalRunId": "bb0e8400-e29b-41d4-a716-446655440006",
  "status": "Completed",
  "modifiedDateTime": "2024-01-15T11:00:00Z",
  "endDateTime": "2024-01-15T11:00:00Z"
}
```

**Note:** Status values are **case-insensitive**. "completed", "COMPLETED", and "Completed" are all valid.

**Terminal State Protection:** Once a run reaches `Completed` or `Failed` status, it cannot be updated.

#### Get Enriched Dataset

**Endpoint:** `GET /api/v1/eval/runs/{evalRunId}/enriched-dataset`

**Description:** Retrieve the enriched dataset with agent responses.

**Response:**
```json
[
  {
    "question": "How do I reset my password?",
    "expectedAnswer": "Go to Settings > Security > Reset Password",
    "agentResponse": "Navigate to Settings, then Security, and click Reset Password",
    "timestamp": "2024-01-15T10:47:00Z"
  },
  {
  "question": "What are your business hours?",
    "expectedAnswer": "We are open Monday-Friday, 9 AM to 5 PM EST",
    "agentResponse": "Our business hours are Monday through Friday, 9:00 AM - 5:00 PM Eastern Time",
  "timestamp": "2024-01-15T10:47:05Z"
  }
]
```

#### Save Enriched Dataset

**Endpoint:** `POST /api/v1/eval/runs/{evalRunId}/enriched-dataset`

**Request Body:**
```json
{
  "enrichedDataset": [
    {
      "question": "How do I reset my password?",
      "expectedAnswer": "Go to Settings > Security > Reset Password",
    "agentResponse": "Navigate to Settings, then Security, and click Reset Password"
    }
  ]
}
```

**Response (201 Created):**
```json
{
  "evalRunId": "bb0e8400-e29b-41d4-a716-446655440006",
  "status": "saved",
  "message": "Enriched dataset saved successfully",
  "recordCount": 500
}
```

---

### Evaluation Results

Store and retrieve evaluation results.

#### Save Evaluation Results

**Endpoint:** `POST /api/v1/eval/runs/{evalRunId}/results`

**Request Body:**
```json
{
  "results": {
    "overallScore": 0.92,
  "metrics": {
      "accuracy": 0.94,
      "precision": 0.91,
      "recall": 0.90,
      "f1Score": 0.905
    },
    "detailedResults": [
      {
        "questionId": "q1",
      "score": 1.0,
      "category": "authentication"
      },
  {
        "questionId": "q2",
      "score": 0.85,
        "category": "general"
    }
    ],
    "executionTime": "00:15:32",
    "totalQuestions": 500,
    "passedQuestions": 470
  },
  "fileName": "evaluation_results.json"
}
```

**Response (201 Created):**
```json
{
  "evalRunId": "bb0e8400-e29b-41d4-a716-446655440006",
  "fileName": "evaluation_results.json",
  "status": "saved",
  "message": "Evaluation results saved successfully",
  "savedAt": "2024-01-15T11:00:00Z"
}
```

**Note:** Multiple result files can be saved for the same evaluation run.

#### Get Evaluation Results

**Endpoint:** `GET /api/v1/eval/runs/{evalRunId}/results/{fileName}`

**Example:**
```bash
GET /api/v1/eval/runs/bb0e8400-e29b-41d4-a716-446655440006/results/evaluation_results.json
```

**Response:**
```json
{
  "overallScore": 0.92,
  "metrics": {
    "accuracy": 0.94,
    "precision": 0.91,
    "recall": 0.90,
    "f1Score": 0.905
  },
  "detailedResults": [...],
  "executionTime": "00:15:32",
  "totalQuestions": 500,
  "passedQuestions": 470
}
```

#### List Evaluation Results

**Endpoint:** `GET /api/v1/eval/runs/{evalRunId}/results`

**Response:**
```json
{
  "evalRunId": "bb0e8400-e29b-41d4-a716-446655440006",
  "resultFiles": [
    {
      "fileName": "evaluation_results.json",
      "uploadedAt": "2024-01-15T11:00:00Z",
      "size": 45678
  },
    {
 "fileName": "detailed_metrics.json",
      "uploadedAt": "2024-01-15T11:01:00Z",
      "size": 123456
    }
  ]
}
```

---

## Data Models

### Configuration Models

#### CreateConfigurationRequestDto
```typescript
{
  agentId: string,  // Required: Agent identifier
  configurationName: string,  // Required: Configuration name
environmentName?: string,         // Optional: Environment (Development, PPE, Production)
  selectedMetrics: [         // Required: Array of metrics
    {
      name: string,         // Required: Metric name (e.g., "Accuracy")
      displayName: string,            // Required: Display name
 description: string,            // Required: Metric description
      weight: number,       // Required: Metric weight (0-1)
      threshold: number,    // Required: Pass threshold (0-1)
      formula?: string        // Optional: Calculation formula
    }
  ]
}
```

### Dataset Models

#### SaveDatasetDto
```typescript
{
  agentId: string,     // Required: Agent identifier
  datasetName: string,                // Required: Dataset name
  datasetType: "Golden" | "Synthetic", // Required: Dataset type
  datasetRecords: Array<object>       // Required: Array of dataset records (flexible schema)
}
```

#### DatasetMetadataDto
```typescript
{
  datasetId: string,   // GUID
  datasetName: string,
  agentId: string,
  datasetType: "Golden" | "Synthetic",
  recordCount: number,
  createdDateTime: string,      // ISO 8601 format
  modifiedDateTime: string// ISO 8601 format
}
```

### Evaluation Run Models

#### CreateEvalRunDto
```typescript
{
  agentId: string,         // Required: Agent identifier
  evalRunName: string,                // Required: Evaluation run name
  dataSetId: string,         // Required: Dataset GUID
  metricsConfigurationId: string,     // Required: Configuration GUID
  type: string,                // Required: Type (e.g., "Automated", "Manual")
  environmentId: string,              // Required: Environment
  agentSchemaName: string       // Required: Agent schema name
}
```

#### EvalRunDto
```typescript
{
  evalRunId: string,    // GUID
  agentId: string,
  evalRunName: string,
  dataSetId: string,
  metricsConfigurationId: string,
  type: string,
  status: "Queued" | "Running" | "Completed" | "Failed",
  environmentId: string,
  agentSchemaName: string,
  createdDateTime: string,   // ISO 8601
  modifiedDateTime: string,      // ISO 8601
  startDateTime?: string,       // ISO 8601 (when status changed to Running)
  endDateTime?: string  // ISO 8601 (when status changed to Completed/Failed)
}
```

### Error Models

#### ErrorResponseDto
```typescript
{
  title: string,       // Error title
  status: number,         // HTTP status code
  detail: string,               // Detailed error message
  instance?: string,       // Request path
traceId?: string,  // Telemetry trace ID
  errors?: {     // Validation errors
    [field: string]: string[]
  }
}
```

---

## Error Handling

### HTTP Status Codes

| Code | Description | Common Scenarios |
|------|-------------|------------------|
| 200 | OK | Successful GET, PUT, DELETE |
| 201 | Created | Successful POST (resource created) |
| 400 | Bad Request | Invalid input, validation failure |
| 401 | Unauthorized | Missing or invalid authentication token |
| 403 | Forbidden | Insufficient permissions |
| 404 | Not Found | Resource not found |
| 409 | Conflict | Duplicate resource (configuration already exists) |
| 500 | Internal Server Error | Server-side error |

### Error Response Format

```json
{
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more validation errors occurred",
  "instance": "/api/v1/eval/configurations",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "errors": {
    "agentId": ["AgentId is required"],
    "selectedMetrics": ["At least one metric is required"]
  }
}
```

### Common Error Scenarios

#### 1. Validation Errors (400)

**Scenario:** Missing required fields
```json
{
  "title": "Validation Error",
  "status": 400,
  "detail": "AgentId is required",
  "errors": {
    "agentId": ["The AgentId field is required."]
  }
}
```

#### 2. Not Found (404)

**Scenario:** Resource doesn't exist
```json
{
  "title": "Resource Not Found",
  "status": 404,
  "detail": "Configuration with ID '550e8400-e29b-41d4-a716-446655440000' not found"
}
```

#### 3. Conflict (409)

**Scenario:** Duplicate configuration
```json
{
  "configurationId": "770e8400-e29b-41d4-a716-446655440002",
  "status": "updated",
  "message": "Configuration with the same name already exists and has been updated"
}
```

#### 4. Terminal State Protection (400)

**Scenario:** Attempting to update completed evaluation
```json
{
  "title": "Invalid Operation",
  "status": 400,
  "detail": "Cannot update evaluation run in terminal state 'Completed'"
}
```

### Retry Strategy

For transient errors (500, 503), implement exponential backoff:

```python
import time

def retry_request(func, max_retries=3, base_delay=1):
    for attempt in range(max_retries):
  try:
          return func()
        except Exception as e:
            if attempt == max_retries - 1:
                raise
   delay = base_delay * (2 ** attempt)
            print(f"Retry {attempt + 1}/{max_retries} after {delay}s")
 time.sleep(delay)
```

---

## Best Practices

### 1. Use Idempotent Operations

When creating configurations or datasets, use unique names to enable idempotency:

```json
{
  "agentId": "agent-001",
  "configurationName": "prod-config-v1-2024-01-15",  // Include version/date
  "environmentName": "Production"
}
```

### 2. Implement Proper Error Handling

Always check HTTP status codes and parse error responses:

```python
response = requests.post(url, json=data, headers=headers)

if response.status_code == 201:
    result = response.json()
    print(f"Created: {result['configurationId']}")
elif response.status_code == 400:
    error = response.json()
    print(f"Validation error: {error['detail']}")
    if 'errors' in error:
        for field, messages in error['errors'].items():
     print(f"  {field}: {', '.join(messages)}")
else:
    print(f"Unexpected error: {response.status_code}")
```

### 3. Use Pagination for Large Datasets

When retrieving datasets or evaluation runs, filter by date range:

```bash
GET /api/v1/eval/runs?agentId=agent-001&startDateTime=2024-01-01T00:00:00Z&endDateTime=2024-01-07T23:59:59Z
```

### 4. Monitor Using Trace IDs

Every response includes OpenTelemetry trace IDs for debugging:

```json
{
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "spanId": "00f067aa0ba902b7"
}
```

Include these in support tickets for faster resolution.

### 5. Cache Configuration Data

Configurations and datasets change infrequently. Cache them locally:

```python
import time

class ConfigCache:
    def __init__(self, ttl=3600):  # 1 hour TTL
        self.cache = {}
      self.ttl = ttl
    
    def get(self, key):
    if key in self.cache:
            data, timestamp = self.cache[key]
            if time.time() - timestamp < self.ttl:
 return data
        return None
    
    def set(self, key, data):
        self.cache[key] = (data, time.time())
```

### 6. Validate Data Before Submission

Pre-validate data to avoid unnecessary API calls:

```python
def validate_create_config(data):
    errors = []
    
    if not data.get('agentId'):
        errors.append("agentId is required")
    
    if not data.get('configurationName'):
 errors.append("configurationName is required")
    
  metrics = data.get('selectedMetrics', [])
    if not metrics:
  errors.append("At least one metric is required")
    
    for metric in metrics:
        if not 0 <= metric.get('weight', -1) <= 1:
         errors.append(f"Metric '{metric.get('name')}' weight must be between 0 and 1")
    
    return errors
```

### 7. Use Bulk Operations

When creating multiple datasets, batch them:

```python
datasets = [
    {"datasetName": "Dataset 1", ...},
    {"datasetName": "Dataset 2", ...},
    # ... more datasets
]

for dataset in datasets:
  try:
  response = create_dataset(dataset)
        print(f"Created: {response['datasetId']}")
    except Exception as e:
 print(f"Failed to create {dataset['datasetName']}: {e}")
        # Continue with next dataset
```

---

## Code Examples

### Python

#### Complete Evaluation Workflow

```python
import requests
import json
from typing import Dict, Any

class EvalPlatformClient:
    def __init__(self, base_url: str, token: str):
        self.base_url = base_url
        self.headers = {
     "Authorization": f"Bearer {token}",
   "Content-Type": "application/json"
   }
    
    def create_configuration(self, config_data: Dict[str, Any]) -> str:
        """Create an evaluation configuration"""
url = f"{self.base_url}/api/v1/eval/configurations"
   response = requests.post(url, json=config_data, headers=self.headers)
        response.raise_for_status()
        return response.json()['configurationId']
    
    def upload_dataset(self, dataset_data: Dict[str, Any]) -> str:
        """Upload a dataset"""
        url = f"{self.base_url}/api/v1/eval/datasets"
        response = requests.post(url, json=dataset_data, headers=self.headers)
  response.raise_for_status()
        return response.json()['datasetId']
    
    def create_eval_run(self, run_data: Dict[str, Any]) -> str:
        """Create an evaluation run"""
  url = f"{self.base_url}/api/v1/eval/runs"
        response = requests.post(url, json=run_data, headers=self.headers)
        response.raise_for_status()
    return response.json()['evalRunId']
    
    def update_status(self, eval_run_id: str, status: str):
     """Update evaluation run status"""
        url = f"{self.base_url}/api/v1/eval/runs/{eval_run_id}/status"
        response = requests.put(url, json={"status": status}, headers=self.headers)
   response.raise_for_status()
    
    def save_results(self, eval_run_id: str, results: Dict[str, Any], filename: str):
        """Save evaluation results"""
        url = f"{self.base_url}/api/v1/eval/runs/{eval_run_id}/results"
        payload = {
       "results": results,
            "fileName": filename
        }
        response = requests.post(url, json=payload, headers=self.headers)
        response.raise_for_status()

# Usage Example
client = EvalPlatformClient(
    base_url="https://sxgevalapidev.azurewebsites.net",
    token="your-bearer-token"
)

# 1. Create configuration
config_id = client.create_configuration({
    "agentId": "agent-001",
    "configurationName": "Accuracy Test Config",
    "environmentName": "Development",
    "selectedMetrics": [
        {
      "name": "Accuracy",
   "displayName": "Accuracy Score",
    "description": "Overall accuracy",
"weight": 1.0,
      "threshold": 0.85
        }
    ]
})
print(f"Config created: {config_id}")

# 2. Upload dataset
dataset_id = client.upload_dataset({
    "agentId": "agent-001",
"datasetName": "Test Questions",
    "datasetType": "Golden",
    "datasetRecords": [
        {
      "question": "What is 2+2?",
            "expectedAnswer": "4"
        }
    ]
})
print(f"Dataset uploaded: {dataset_id}")

# 3. Create evaluation run
eval_run_id = client.create_eval_run({
    "agentId": "agent-001",
    "evalRunName": "First Test Run",
    "dataSetId": dataset_id,
    "metricsConfigurationId": config_id,
    "type": "Automated",
    "environmentId": "Development",
    "agentSchemaName": "TestAgent"
})
print(f"Eval run created: {eval_run_id}")

# 4. Update status to Running
client.update_status(eval_run_id, "Running")
print("Status updated to Running")

# 5. Process evaluation (your logic here)
# ... run your agent and collect results ...

# 6. Save results
client.save_results(eval_run_id, {
    "overallScore": 0.95,
    "totalQuestions": 100,
    "passedQuestions": 95
}, "results.json")
print("Results saved")

# 7. Mark as completed
client.update_status(eval_run_id, "Completed")
print("Evaluation completed!")
```

### C# / .NET

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class EvalPlatformClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public EvalPlatformClient(string baseUrl, string bearerToken)
    {
        _baseUrl = baseUrl;
   _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", bearerToken);
    }

    public async Task<string> CreateConfigurationAsync(object configData)
    {
 var url = $"{_baseUrl}/api/v1/eval/configurations";
var json = JsonSerializer.Serialize(configData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
    var response = await _httpClient.PostAsync(url, content);
  response.EnsureSuccessStatusCode();
        
      var result = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(result);
        return doc.RootElement.GetProperty("configurationId").GetString();
    }

    public async Task<string> CreateEvalRunAsync(object runData)
    {
     var url = $"{_baseUrl}/api/v1/eval/runs";
   var json = JsonSerializer.Serialize(runData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync(url, content);
   response.EnsureSuccessStatusCode();
    
        var result = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(result);
        return doc.RootElement.GetProperty("evalRunId").GetString();
    }

    public async Task UpdateStatusAsync(string evalRunId, string status)
    {
        var url = $"{_baseUrl}/api/v1/eval/runs/{evalRunId}/status";
        var json = JsonSerializer.Serialize(new { status });
        var content = new StringContent(json, Encoding.UTF8, "application/json");
      
        var response = await _httpClient.PutAsync(url, content);
        response.EnsureSuccessStatusCode();
    }
}

// Usage
var client = new EvalPlatformClient(
    "https://sxgevalapidev.azurewebsites.net",
    "your-bearer-token"
);

var configId = await client.CreateConfigurationAsync(new
{
    agentId = "agent-001",
    configurationName = "Test Config",
    environmentName = "Development",
    selectedMetrics = new[]
    {
        new
        {
     name = "Accuracy",
displayName = "Accuracy Score",
   description = "Overall accuracy",
    weight = 1.0,
          threshold = 0.85
        }
    }
});

Console.WriteLine($"Created configuration: {configId}");
```

### JavaScript / TypeScript

```typescript
class EvalPlatformClient {
    private baseUrl: string;
    private headers: HeadersInit;

    constructor(baseUrl: string, token: string) {
        this.baseUrl = baseUrl;
      this.headers = {
'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
        };
    }

    async createConfiguration(configData: any): Promise<string> {
     const response = await fetch(`${this.baseUrl}/api/v1/eval/configurations`, {
    method: 'POST',
     headers: this.headers,
    body: JSON.stringify(configData)
        });

  if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${await response.text()}`);
        }

        const result = await response.json();
   return result.configurationId;
    }

    async createEvalRun(runData: any): Promise<string> {
  const response = await fetch(`${this.baseUrl}/api/v1/eval/runs`, {
            method: 'POST',
  headers: this.headers,
         body: JSON.stringify(runData)
    });

        if (!response.ok) {
  throw new Error(`HTTP ${response.status}: ${await response.text()}`);
        }

     const result = await response.json();
    return result.evalRunId;
    }

    async updateStatus(evalRunId: string, status: string): Promise<void> {
        const response = await fetch(
            `${this.baseUrl}/api/v1/eval/runs/${evalRunId}/status`,
            {
            method: 'PUT',
         headers: this.headers,
                body: JSON.stringify({ status })
            }
        );

   if (!response.ok) {
         throw new Error(`HTTP ${response.status}: ${await response.text()}`);
        }
    }

    async getResults(evalRunId: string, fileName: string): Promise<any> {
     const response = await fetch(
            `${this.baseUrl}/api/v1/eval/runs/${evalRunId}/results/${fileName}`,
         {
       method: 'GET',
      headers: this.headers
            }
        );

        if (!response.ok) {
       throw new Error(`HTTP ${response.status}: ${await response.text()}`);
        }

        return await response.json();
    }
}

// Usage
const client = new EvalPlatformClient(
    'https://sxgevalapidev.azurewebsites.net',
    'your-bearer-token'
);

const configId = await client.createConfiguration({
    agentId: 'agent-001',
    configurationName: 'Test Config',
    environmentName: 'Development',
    selectedMetrics: [
        {
     name: 'Accuracy',
            displayName: 'Accuracy Score',
   description: 'Overall accuracy',
            weight: 1.0,
       threshold: 0.85
        }
    ]
});

console.log(`Created configuration: ${configId}`);
```

---

## Rate Limits and Quotas

### Current Limits

| Operation | Limit | Window |
|-----------|-------|--------|
| API Requests | 1000 requests | per minute |
| Concurrent Connections | 100 connections | - |
| Dataset Size | 100 MB | per upload |
| Result File Size | 50 MB | per file |

### Rate Limit Headers

Responses include rate limit information:

```http
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 995
X-RateLimit-Reset: 1705320000
```

### Handling Rate Limits

```python
def handle_rate_limit(response):
    if response.status_code == 429:
        reset_time = int(response.headers.get('X-RateLimit-Reset', 0))
        wait_time = reset_time - time.time()
        if wait_time > 0:
            print(f"Rate limited. Waiting {wait_time}s...")
      time.sleep(wait_time)
        return True
    return False
```

---

## Support

### Resources

- **Swagger UI**: `https://sxgevalapidev.azurewebsites.net/swagger`
- **Health Status**: `https://sxgevalapidev.azurewebsites.net/api/v1/health/detailed`
- **GitHub Issues**: [Report bugs or request features](https://github.com/microsoft/sxgevalplatform/issues)

### Contact

- **Email**: sxg-eval-support@microsoft.com
- **Teams**: SXG Evaluation Platform Channel

### Getting Help

When reporting issues, include:

1. **Trace ID** from error response
2. **Request/Response** examples
3. **Timestamp** (UTC)
4. **Environment** (Development, PPE, Production)
5. **Agent ID** if applicable

### Service Status

Check current service status:
- **Health Endpoint**: `GET /api/v1/health/detailed`
- **Azure Portal**: Monitor Application Insights dashboard

---

## Appendix

### Glossary

- **Agent**: An AI system being evaluated
- **Golden Dataset**: Reference dataset with known correct answers
- **Synthetic Dataset**: Generated test data for evaluation
- **Metrics Configuration**: Set of metrics and thresholds for evaluation
- **Evaluation Run**: Single execution of an evaluation against a dataset
- **Enriched Dataset**: Dataset augmented with agent responses

### Environment URLs

| Environment | Base URL |
|-------------|----------|
| Development | `https://sxgevalapidev.azurewebsites.net` |
| PPE | `https://sxgevalapippe.azurewebsites.net` |
| Production | `https://sxgevalapiproduction.azurewebsites.net` |

### Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2024-01-15 | Initial release |

---

**Last Updated**: January 15, 2024  
**Document Version**: 1.0.0  
**API Version**: v1

---

© 2024 Microsoft Corporation. All rights reserved.
