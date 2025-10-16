# SXG Evaluation Platform API Documentation

## 📚 Complete Documentation

For comprehensive API documentation, please refer to:

**[SXG Evaluation Platform API - Complete Guide](docs/SXG_Evaluation_Platform_API_Complete_Guide.md)**

This consolidated guide includes:
- Complete API endpoint reference
- Authentication and security
- Data models and schemas
- Business rules and validation
- Error handling
- Azure storage architecture
- Development setup instructions

## 🔧 Technical Implementation

For technical implementation details, see:

**[Technical Implementation Guide](docs/Technical_Implementation_Guide.md)**

This guide covers:
- Service architecture patterns
- Azure Table Storage design
- Blob storage integration
- Performance optimization
- Deployment considerations
- Development guidelines

## 🚀 Quick Start

### Base Information
- **Base URL**: `https://your-domain/api/v1`
- **Framework**: .NET 8
- **Authentication**: OAuth using Azure Active Directory

### Key Endpoints
- `POST /api/v1/eval/runs` - Create evaluation run
- `GET /api/v1/eval/runs/{id}` - Get evaluation run
- `PUT /api/v1/eval/runs/{id}` - Update evaluation run status
- `GET /api/v1/health` - Health check

### Running Locally
```bash
cd src/Sxg-Eval-Platform-Api
dotnet restore
dotnet run
```

Access Swagger UI at: `http://localhost:5000/swagger`

---

*This documentation has been consolidated for better maintainability. All detailed information is available in the linked comprehensive guides.*

1. [Health Check](#health-check)
2. [EVAL Configurations](#evaluation-configurations)
3. [Dataset Management](#dataset-management)
4. [Data Models](#data-models)
5. [Error Handling](#error-handling)
6. [Examples](#examples)

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

## EVAL Configurations

The Evaluation Configuration endpoints allow you to manage metric configurations for AI agents. This api provides methods to create, update, and retrieve metric configurations.

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

The Dataset Management endpoints allow you to manage EVAL Datasets for AI agents.

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

---

## Architecture Notes

- **Storage**: The API uses Azure Blob Storage for storing dataset content and configuration data, with Azure Table Storage for metadata management.
- **Authentication**: Currently not implemented. Consider implementing OAuth 2.0 or API key authentication for production use.
- **Logging**: Comprehensive logging is implemented throughout the application for monitoring and debugging.
- **Validation**: Input validation is performed using data annotations and custom validation logic.
- **Error Handling**: Consistent error handling with proper HTTP status codes and error messages.

---

## Version History

- **v1.0.0**: Initial release with basic configuration and dataset management functionality.

---

*For technical support or questions about this API, please contact the SXG development team.*