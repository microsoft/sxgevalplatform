# SXG Evaluation Platform API - Complete Documentation

## Overview

The SXG Evaluation Platform API provides a comprehensive integration point for systems to interact with the SXG evaluation platform. Built with .NET 8 and following RESTful principles, this API enables full lifecycle management of AI agent evaluations.

### Key Capabilities

1. **Metrics Configuration Management** - Create, update, and read metric configurations for agents
2. **Dataset Management** - Manage evaluation datasets (Synthetic or Golden) for agents  
3. **Evaluation Run Lifecycle** - Trigger, monitor, and manage evaluation runs
4. **Results Management** - Store, retrieve, and manage evaluation results
5. **Health Monitoring** - API health status and diagnostics

### Base Information

- **Base URL**: `https://your-domain/api/v1`
- **API Version**: 1.0.0
- **Framework**: .NET 8
- **Content Type**: `application/json`
- **Authentication**: OAuth using Azure Active Directory

---

## Authentication

This API supports OAuth authentication using Azure Active Directory. All endpoints except health checks require a valid bearer token.

**Authentication Flow**:
1. Applications must be registered in Azure AD
2. Admin consent is required for API access
3. User tokens are required (App-to-App authentication not supported for evaluation triggers)

---

## Core API Endpoints

### Health Check

#### GET /api/v1/health
Get API health status and basic information.

**Response**: `200 OK`
```json
{
  "status": "Healthy",
  "timestamp": "2024-10-16T10:30:00Z",
  "version": "1.0.0"
}
```

---

### Evaluation Configurations

#### GET /api/v1/eval/configurations/{agentId}
Get all metric configurations for an agent.

**Parameters**:
- `agentId` (path) - Agent identifier

**Response**: `200 OK`
```json
[
  {
    "id": "config-123",
    "agentId": "A001",
    "name": "Default Configuration",
    "metrics": [...]
  }
]
```

#### POST /api/v1/eval/configurations
Create or update evaluation configuration.

**Request Body**:
```json
{
  "agentId": "A001",
  "name": "Default Configuration",
  "metrics": [...]
}
```

---

### Dataset Management

#### GET /api/v1/eval/datasets/{agentId}
Get all datasets for an agent.

**Parameters**:
- `agentId` (path) - Agent identifier

#### POST /api/v1/eval/datasets
Create or update evaluation dataset.

**Request Body**:
```json
{
  "agentId": "A001",
  "name": "Test Dataset",
  "type": "Synthetic",
  "data": [...]
}
```

---

### Evaluation Runs

#### POST /api/v1/eval/runs
Create a new evaluation run.

**Request Body**:
```json
{
  "agentId": "A001",
  "dataSetId": "DS001",
  "metricsConfigurationId": "MC001"
}
```

**Response**: `201 Created`
```json
{
  "evalRunId": "12345-guid",
  "agentId": "A001",
  "status": "Queued",
  "startedDatetime": "2024-10-16T10:30:00Z"
}
```

#### GET /api/v1/eval/runs/{evalRunId}
Get evaluation run details.

**Parameters**:
- `evalRunId` (path) - Evaluation run GUID

**Response**: `200 OK`
```json
{
  "evalRunId": "12345-guid",
  "agentId": "A001",
  "status": "Running",
  "startedDatetime": "2024-10-16T10:30:00Z"
}
```

#### PUT /api/v1/eval/runs/{evalRunId}
Update evaluation run status.

**Parameters**:
- `evalRunId` (path) - Evaluation run GUID

**Request Body**:
```json
{
  "status": "Completed"
}
```

**Important**: Once an evaluation run reaches terminal state (`Completed` or `Failed`), it cannot be updated.

**Valid State Transitions**:
- `Queued` → `Running`, `Completed`, `Failed`
- `Running` → `Completed`, `Failed`
- `Completed` → ❌ (No further updates)
- `Failed` → ❌ (No further updates)

**Response**: `200 OK`
```json
{
  "success": true,
  "message": "Evaluation run status updated successfully"
}
```

**Terminal State Error**: `400 Bad Request`
```json
{
  "success": false,
  "message": "Cannot update status - evaluation run is in terminal state"
}
```

---

### Evaluation Results

#### POST /api/v1/eval/results
Save evaluation results.

**Request Body**:
```json
{
  "evalRunId": "12345-guid",
  "fileName": "results.json",
  "evaluationRecords": [...]
}
```

#### GET /api/v1/eval/results/{evalRunId}
Get evaluation results by run ID.

**Parameters**:
- `evalRunId` (path) - Evaluation run GUID

---

## Data Models

### Evaluation Run Status Constants
- `Queued` - Waiting to be processed
- `Running` - Currently being processed
- `Completed` - Successfully finished
- `Failed` - Encountered an error

### Core Entities

#### EvalRunDto
```json
{
  "evalRunId": "guid",
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

---

## Azure Storage Architecture

### Table Storage Partitioning
- **Partition Key**: `AgentId` for optimal performance
- **Row Key**: `EvalRunId` for unique identification
- **Benefits**: Efficient agent-based queries and load distribution

### Blob Storage Organization
- **Container**: Agent-specific containers (e.g., `a001`)
- **Path Structure**: `evaluations/{evalRunId}.json`
- **Content**: JSON-formatted evaluation results

---

## Error Handling

### HTTP Status Codes
- `200 OK` - Successful operation
- `201 Created` - Resource created successfully
- `400 Bad Request` - Invalid input or business rule violation
- `401 Unauthorized` - Authentication required
- `403 Forbidden` - Access denied
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Server error

### Error Response Format
```json
{
  "success": false,
  "message": "Error description",
  "details": "Additional error information"
}
```

---

## Business Rules

### Terminal State Protection
Once an evaluation run reaches `Completed` or `Failed` status, it becomes immutable to maintain data integrity and audit trails.

### Authentication Requirements
- User tokens required for evaluation triggers
- Service-to-service calls supported for data retrieval
- Azure AD integration mandatory

### Partitioning Strategy
Agent-based partitioning ensures optimal performance for multi-tenant scenarios and efficient agent-specific queries.

---

## Development Setup

### Prerequisites
- .NET 8.0 SDK
- Azure Storage Account
- Azure Active Directory tenant

### Configuration
```json
{
  "AzureStorage": {
    "AccountName": "your-storage-account"
  },
  "Authentication": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id"
  }
}
```

### Running Locally
```bash
cd src/Sxg-Eval-Platform-Api
dotnet restore
dotnet run
```

Access Swagger UI at: `http://localhost:5000/swagger`

---

## Monitoring and Observability

### Health Endpoints
- `/api/v1/health` - Basic health check
- `/api/v1/health/detailed` - Detailed system information

### Logging
Structured logging is implemented throughout the API for monitoring and debugging.

### Key Metrics
- Evaluation run completion rates
- API response times
- Authentication success rates
- Storage operation performance

---

## Best Practices

### API Usage
1. Always check evaluation run status before attempting updates
2. Handle terminal state errors gracefully
3. Use agent-specific queries for better performance
4. Implement proper retry logic for transient failures

### Security
1. Secure storage of authentication tokens
2. Regular rotation of access keys
3. Monitor for unusual access patterns
4. Implement proper CORS policies

### Performance
1. Use agent-based filtering for large datasets
2. Implement pagination for large result sets
3. Cache frequently accessed configurations
4. Monitor storage costs and optimize blob organization

---

This documentation covers the complete SXG Evaluation Platform API functionality, architecture, and usage guidelines.