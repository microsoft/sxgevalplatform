# SxG Eval Platform Plugins

## Overview

This project contains Dataverse plugins for the SxG Eval Platform that provide custom APIs for managing evaluation runs and datasets. The plugins include early-bound entity implementation, environment variable-based configuration, and unified logging to both Dataverse audit logs and Azure Application Insights.

## Environment Variables

Configure these environment variables in Dataverse:

| Schema Name | Type | Description | Default Value | Required |
|------------|------|-------------|---------------|----------|
| `cr890_EvalApiBaseUrl` | Text | Base URL for Eval API | `https://sxgevalapidev.azurewebsites.net` | Yes |
| `cr890_ApiTimeoutSeconds` | Decimal | API call timeout (seconds) | `30` | No |
| `cr890_ApiScope` | Text | OAuth scope for API authentication | `443bbe62-c474-49f7-884c-d1b5a23eb735/.default` | No |
| `cr890_EnableAppInsightsLogging` | Yes/No | Enable Application Insights | `No` | No |
| `cr890_EnableAuditLogging` | Yes/No | Enable Dataverse audit logs | `Yes` | No |
| `cr890_AppInsightsConnectionString` | Text | Azure App Insights connection string | (none) | Only if App Insights enabled |

### API URL Construction

The configuration service builds the following API endpoints:

- **Datasets API**: `{BaseUrl}/api/v1/eval/datasets/{datasetId}`
- **Eval Run Status API**: `{BaseUrl}/api/v1/eval/runs/{evalRunId}/status`
- **Enriched Dataset API**: `{BaseUrl}/api/v1/eval/runs/{evalRunId}/enriched-dataset`

**Configuration Methods:**
- `GetDatasetsApiUrl(string datasetId)` - Returns datasets endpoint
- `GetEvalRunsStatusApiUrl(string evalRunId)` - Returns status update endpoint
- `GetEnrichedDatasetApiUrl(string evalRunId)` - Returns enriched dataset endpoint

### API Endpoints

| Endpoint | Method | Used By | Purpose | Configuration Method |
|----------|--------|---------|---------|---------------------|
| `/api/v1/eval/datasets/{datasetId}` | GET | UpdateDatasetAsFile | Fetch dataset | `GetDatasetsApiUrl(datasetId)` |
| `/api/v1/eval/runs/{evalRunId}/status` | PUT | UpdateDatasetAsFile, UpdateFailedState | Update status | `GetEvalRunsStatusApiUrl(evalRunId)` |
| `/api/v1/eval/runs/{evalRunId}/enriched-dataset` | POST | PublishEnrichedDataset | Publish enriched data | `GetEnrichedDatasetApiUrl(evalRunId)` |

## Logging Layer

### Overview

The logging layer provides unified logging to both Dataverse audit logs and Azure Application Insights with feature flag control.

### Logging Destinations

1. **Dataverse Audit Logs** (Default: Enabled)
   - Traditional plugin trace logs
   - Available in Plugin Trace Log records
   - Includes correlation ID and user context
   - No additional setup required

2. **Application Insights** (Default: Disabled - WIP - Currently this is not working)
   - Rich telemetry and analytics
   - Distributed tracing
   - Custom dashboards and alerts
   - Requires Azure setup

### Feature Flag Control

Control logging destinations independently via environment variables:

```
cr890_EnableAuditLogging = Yes/No
cr890_EnableAppInsightsLogging = Yes/No
```

### Logging Capabilities

**Trace Messages:**
```csharp
log.Trace("Information message");
log.Trace("Warning message", TraceSeverity.Warning);
log.Trace("Error message", TraceSeverity.Error);
log.Trace("Critical message", TraceSeverity.Critical);
```

**Exception Logging:**
```csharp
try {
    // code
} catch (Exception ex) {
    log.LogException(ex, "Additional context");
}
```

**Custom Events:**
```csharp
log.LogEvent("GetEvalRunSuccess", new Dictionary<string, string> {
    { "EvalRunId", id.ToString() },
    { "AgentId", agentId },
    { "Status", status }
});
```

**Dependency Tracking:**
```csharp
var startTime = DateTimeOffset.UtcNow;
// Make HTTP call
var duration = DateTimeOffset.UtcNow - startTime;
log.LogDependency("EvalAPI", url, startTime, duration, success);
```

### Automatic Context Enrichment

All telemetry automatically includes:
- Correlation ID
- Initiating User ID
- Message Name
- Organization Name
- Execution Stage
- Execution Mode

### Runtime Configuration

The logging service automatically detects the configuration at runtime:

- **Audit Logging**: Controlled by `cr890_EnableAuditLogging` (default: Yes)
- **Application Insights**: Controlled by `cr890_EnableAppInsightsLogging` (default: No)
- **No recompilation needed**: Toggle logging by changing environment variables

Both logging destinations can run simultaneously or independently based on your configuration.

## Helper Classes

### AuthTokenHelper

### Overview

The `AuthTokenHelper` utility provides a centralized way to acquire authentication tokens using Managed Identity Service for external API calls.

### Usage

```csharp
// Get API scope from configuration
string apiScope = configService.GetApiScope();

// Acquire token using Managed Identity
string authToken = AuthTokenHelper.AcquireToken(
    managedIdentityService, 
    apiScope, 
    loggingService, 
  nameof(YourPlugin));

// Add authorization header to HTTP request
AuthTokenHelper.AddAuthorizationHeader(
    httpWebRequest, 
    authToken, 
 loggingService, 
    nameof(YourPlugin));
```

### Features

- **Centralized token acquisition** - Single reusable utility across all plugins
- **Environment variable configuration** - OAuth scope configurable via `cr890_ApiScope`
- **Comprehensive logging** - All operations logged for troubleshooting
- **Graceful error handling** - Returns null if token acquisition fails
- **Automatic header formatting** - Adds "Bearer {token}" to Authorization header

### Configuration

The OAuth scope is configured via the `cr890_ApiScope` environment variable:
- **Default**: `443bbe62-c474-49f7-884c-d1b5a23eb735/.default`
- **Format**: `{ResourceId}/.default` or specific scopes

---

### EvalRunHelper

### Overview

The `EvalRunHelper` utility provides reusable methods for common eval run operations across plugins, promoting code reuse and consistency.

### Usage

**Update External Eval Run Status:**
```csharp
// Update external API status
bool success = EvalRunHelper.UpdateExternalEvalRunStatus(
    evalRunId, 
    "EnrichingDataset", 
    authToken, 
    loggingService, 
    configService,
    nameof(YourPlugin));
```

**Update Eval Run Status in Dataverse:**
```csharp
// Update status to Completed (3)
bool success = EvalRunHelper.UpdateEvalRunStatus(
    evalRunId, 
    3, 
    organizationService, 
    loggingService,
    nameof(YourPlugin));
```

### Features

- **Centralized status updates** - Single reusable method for updating status in Dataverse and external API
- **Comprehensive logging** - All operations logged with caller context
- **Graceful error handling** - Returns boolean success indicators
- **Consistent implementation** - Ensures all plugins use the same approach

### Methods

| Method | Purpose | Returns |
|--------|---------|---------|
| `UpdateExternalEvalRunStatus` | Updates eval run status in external API via PUT call | `bool` - Success indicator |
| `UpdateEvalRunStatus` | Updates eval run status field in Dataverse | `bool` - Success indicator |

### Status Values

Common status values used in external API calls:
- `EnrichingDataset` - Dataset enrichment in progress
- `DatasetEnrichmentFailed` - Dataset enrichment failed
- `Failed` - General failure status

Common status integer values for Dataverse:
- `0` - New
- `1` - Started
- `2` - Updated
- `3` - Completed
- `4` - Failed

---

## Entity Structure

**EvalRun Entity (`cr890_evalrun`)**
- Primary Key: `cr890_evalrunid` (GUID)
- Primary Name: `cr890_id` (String - same value as Primary Key)
- Fields:
  - `cr890_datasetid` - Dataset identifier (GUID stored as string)
  - `cr890_agentid` - Agent identifier (string)
  - `cr890_environmentid` - Environment identifier (GUID as string)
  - `cr890_agentschemaname` - Agent schema name (string)
  - `cr890_status` - Status (Choice field with integer values)
  - `cr890_dataset` - Dataset JSON data (Multi Line Text)

### Status Values

```csharp
// Status enum (from generated early-bound entity)
cr890_evalrun_cr890_status.New        // 0
cr890_evalrun_cr890_status.Started    // 1
cr890_evalrun_cr890_status.Updated    // 2
cr890_evalrun_cr890_status.Completed  // 3
cr890_evalrun_cr890_status.Failed     // 4
```

| Status Name | Dataverse Value | API Response |
|-------------|----------------|--------------|
| New | 0 | "New" |
| Started | 1 | "Started" |
| Updated | 2 | "Updated" |
| Completed | 3 | "Completed" |
| Failed | 4 | "Failed" |

### Binding Strategy

| Operation | Binding Type | Reason |
|-----------|-------------|---------|
| **Create** | Late-Bound | Avoids serialization issues in Custom API plugins |
| **Read** | Early-Bound | Type-safe access with IntelliSense |
| **Update** | Late-Bound | Avoids serialization issues in Custom API plugins |

## Custom APIs

### Overview

The SxG Eval Platform exposes Custom APIs for managing evaluation runs and datasets:

1. **PostEvalRun** - Create new evaluation run
2. **GetEvalRun** - Retrieve evaluation run details
3. **UpdateDatasetAsFile** - Fetch dataset from external API and store as file
4. **UpdateEnrichedDatasetFile** - Update enriched dataset file after enrichment
5. **PublishEnrichedDataset** - Publish enriched dataset to external API
6. **UpdateFailedState** - Mark evaluation run as failed

---

### 1. PostEvalRun

**Purpose:** Creates a new evaluation run record in Dataverse.

**API Details:**
- **API Name:** `cr890_PostEvalRun`
- **Display Name:** Post Eval Run
- **HTTP Method:** POST (Action)
- **Is Function:** No
- **Execute Privilege:** `cr890_PostEvalRun`

**Request Parameters:**

| Parameter Name | Type | Required | Description |
|----------------|------|----------|-------------|
| `evalRunId` | String (GUID) | Yes | Unique identifier for the evaluation run |
| `datasetId` | String (GUID) | No | Dataset identifier |
| `agentId` | String | No | Agent identifier |
| `environmentId` | String (GUID) | No | Environment identifier |
| `agentSchemaName` | String | No | Agent schema name |

**Request Example:**
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd",
  "datasetId": "35ea4b87-9dd2-4bda-a968-60bf4c5464c9",
    "agentId": "dri-agent",
    "environmentId": "948a58e0-a265-26e-bbd0-3d0cf7978511",
    "agentSchemaName": "crb32_sxGDriCopilot"
}
```

**Response Properties:**

| Property Name | Type | Description |
|--------------|------|-------------|
| `success` | Boolean | Indicates if operation was successful |
| `message` | String | Success or error message |
| `timestamp` | String (ISO 8601) | Timestamp of operation |

**Success Response Example:**
```json
{
    "success": true,
    "message": "Eval run created successfully",
    "timestamp": "2024-01-15T10:30:00.000Z"
}
```

**Error Response Example:**
```json
{
    "success": false,
    "message": "EvalRunId is required",
    "timestamp": "2024-01-15T10:30:00.000Z"
}
```

**Operations Performed:**
1. Validates request parameters
2. Creates `cr890_evalrun` record with status = 0 (New)
3. Sets owner to initiating user
4. Sets all provided fields

**External API Calls:** None

---

### 2. GetEvalRun

**Purpose:** Retrieves an evaluation run by ID with parsed dataset.

**API Details:**
- **API Name:** `cr890_GetEvalRun`
- **Display Name:** Get Eval Run
- **HTTP Method:** GET (Function)
- **Is Function:** Yes
- **Execute Privilege:** `cr890_GetEvalRun`

**Request Parameters:**

| Parameter Name | Type | Required | Description |
|----------------|------|----------|-------------|
| `evalRunId` | String (GUID) | Yes | Unique identifier for the evaluation run to retrieve |

**Request Example:**
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd"
}
```

**Response Properties:**

| Property Name | Type | Description |
|--------------|------|-------------|
| `evalRunId` | String (GUID) | Evaluation run identifier |
| `message` | String | Success or error message |
| `timestamp` | String (ISO 8601) | Timestamp of operation |
| `agentId` | String | Agent identifier |
| `environmentId` | String (GUID) | Environment identifier |
| `agentSchemaName` | String | Agent schema name |
| `status` | String | Current status (New/Started/Updated/Completed/Failed) |
| `dataset` | String (JSON Array) | Parsed dataset as JSON string |

**Success Response Example:**
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd",
    "message": "Eval run retrieved successfully",
    "timestamp": "2024-01-15T10:30:00.000Z",
    "agentId": "dri-agent",
    "environmentId": "948a58e0-a265-26e-bbd0-3d0cf7978511",
    "agentSchemaName": "crb32_sxGDriCopilot",
    "status": "Completed",
    "dataset": "[{\"prompt\":\"What is the severity?\",\"groundTruth\":\"Severity 3\",\"actualResponse\":\"\",\"expectedResponse\":\"Severity 3\"}]"
}
```

**Error Response Example:**
```json
{
    "evalRunId": null,
    "message": "Eval run not found",
    "timestamp": "2024-01-15T10:30:00.000Z",
    "agentId": null,
    "environmentId": null,
  "agentSchemaName": null,
    "status": null,
    "dataset": null
}
```

**Operations Performed:**
1. Validates eval run ID
2. Retrieves `cr890_evalrun` record by primary key
3. Converts status from integer to string
4. Returns all fields including JSON dataset

**External API Calls:** None

---

### 3. UpdateDatasetAsFile

**Purpose:** Fetches dataset from external API and stores it as a file in Dataverse file column (DLP-compliant).

**API Details:**
- **API Name:** `cr890_UpdateDatasetAsFile`
- **Display Name:** Update Dataset As File
- **HTTP Method:** POST (Action)
- **Is Function:** No
- **Execute Privilege:** `cr890_UpdateDatasetAsFile`

**Request Parameters:**

| Parameter Name | Type | Required | Description |
|----------------|------|----------|-------------|
| `evalRunId` | String (GUID) | Yes | Unique identifier for the evaluation run |
| `datasetId` | String (GUID) | Yes | Dataset identifier to fetch from external API |

**Request Example:**
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd",
    "datasetId": "35ea4b87-9dd2-4bda-a968-60bf4c5464c9"
}
```

**Response Properties:**

| Property Name | Type | Description |
|--------------|------|-------------|
| `success` | Boolean | Indicates if operation was successful |
| `message` | String | Success or error message |
| `timestamp` | String (ISO 8601) | Timestamp of operation |

**Success Response Example:**
```json
{
    "success": true,
    "message": "Dataset file updated successfully",
    "timestamp": "2024-01-15T10:35:00.000Z"
}
```

**Error Response Example:**
```json
{
    "success": false,
    "message": "DatasetId is required",
    "timestamp": "2024-01-15T10:35:00.000Z"
}
```

**Operations Performed:**
1. Validates request parameters (both IDs required)
2. Updates external API status to "EnrichingDataset"
3. Fetches dataset from external datasets API
4. Stores dataset as file in `cr890_datasetfile` column using file blocks API
5. Sets `cr890_status` to 2 (Updated)

**External API Calls:**

**Call 1: Update External Status**
- **URL:** `{BaseUrl}/api/v1/eval/runs/{evalRunId}/status`
- **Method:** PUT
- **Headers:** `Content-Type: application/json`, `Authorization: Bearer {token}`
- **Body:** `{"status":"EnrichingDataset"}`

**Call 2: Get Dataset**
- **URL:** `{BaseUrl}/api/v1/eval/datasets/{datasetId}`
- **Method:** GET
- **Headers:** `Content-Type: application/json`, `Authorization: Bearer {token}`
- **Response:** Array of dataset items

---

### 4. UpdateEnrichedDatasetFile

**Purpose:** Updates enriched dataset file after Power Automate flow enrichment.

**API Details:**
- **API Name:** `cr890_UpdateEnrichedDatasetFile`
- **Display Name:** Update Enriched Dataset File
- **HTTP Method:** POST (Action)
- **Is Function:** No
- **Execute Privilege:** `cr890_UpdateEnrichedDatasetFile`

**Request Parameters:**

| Parameter Name | Type | Required | Description |
|----------------|------|----------|-------------|
| `evalRunId` | String (GUID) | Yes | Unique identifier for the evaluation run |
| `enrichedDatasetJson` | String (JSON) | Yes | Enriched dataset JSON content |

**Request Example:**
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd",
    "enrichedDatasetJson": "[{\"prompt\":\"Test\",\"actualResponse\":\"Response\"}]"
}
```

**Response Properties:**

| Property Name | Type | Description |
|--------------|------|-------------|
| `success` | Boolean | Indicates if operation was successful |
| `message` | String | Success or error message |
| `timestamp` | String (ISO 8601) | Timestamp of operation |

**Success Response Example:**
```json
{
    "success": true,
    "message": "Enriched dataset file updated successfully",
    "timestamp": "2024-01-15T10:37:00.000Z"
}
```

**Error Response Example:**
```json
{
    "success": false,
    "message": "EnrichedDatasetJson is required",
    "timestamp": "2024-01-15T10:37:00.000Z"
}
```

**Operations Performed:**
1. Validates request parameters
2. Stores enriched dataset as file in `cr890_datasetfile` column using file blocks API
3. Sets `cr890_status` to 2 (Updated)

**External API Calls:** None

---

### 5. PublishEnrichedDataset

**Purpose:** Publishes enriched dataset to external API.

**API Details:**
- **API Name:** `cr890_PublishEnrichedDataset`
- **Display Name:** Publish Enriched Dataset
- **HTTP Method:** POST (Action)
- **Is Function:** No
- **Execute Privilege:** `cr890_PublishEnrichedDataset`

**Request Parameters:**

| Parameter Name | Type | Required | Description |
|----------------|------|----------|-------------|
| `evalRunId` | String (GUID) | Yes | Unique identifier for the evaluation run |

**Request Example:**
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd"
}
```

**Response Properties:**

| Property Name | Type | Description |
|--------------|------|-------------|
| `success` | Boolean | Indicates if operation was successful |
| `message` | String | Success or error message |
| `timestamp` | String (ISO 8601) | Timestamp of operation |

**Success Response Example:**
```json
{
    "success": true,
    "message": "Enriched dataset published successfully",
    "timestamp": "2024-01-15T10:40:00.000Z"
}
```

**Error Response Example:**
```json
{
    "success": false,
    "message": "No dataset found in eval run record or failed to retrieve dataset",
    "timestamp": "2024-01-15T10:40:00.000Z"
}
```

**Operations Performed:**
1. Validates eval run ID
2. Retrieves `cr890_dataset` from eval run record
3. Publishes enriched dataset to external API
4. Updates `cr890_status` to 3 (Completed)

**External API Calls:**

**Call: Publish Enriched Dataset**
- **URL:** `{BaseUrl}/api/v1/eval/runs/{evalRunId}/enriched-dataset`
- **Method:** POST
- **Headers:** `Content-Type: application/json`, `Authorization: Bearer {token}`
- **Body:** `{"enrichedDataset":[...]}`
- **Expected Response:** 200 OK or 201 Created

---

### 6. UpdateFailedState

**Purpose:** Marks evaluation run as failed in both Dataverse and external API.

**API Details:**
- **API Name:** `cr890_UpdateFailedState`
- **Display Name:** Update Failed State
- **HTTP Method:** POST (Action)
- **Is Function:** No
- **Execute Privilege:** `cr890_UpdateFailedState`

**Request Parameters:**

| Parameter Name | Type | Required | Description |
|----------------|------|----------|-------------|
| `evalRunId` | String (GUID) | Yes | Unique identifier for the evaluation run |

**Request Example:**
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd"
}
```

**Response Properties:**

| Property Name | Type | Description |
|--------------|------|-------------|
| `success` | Boolean | Indicates if operation was successful |
| `message` | String | Success or error message |
| `timestamp` | String (ISO 8601) | Timestamp of operation |

**Success Response Example:**
```json
{
    "success": true,
    "message": "Eval run status updated to Failed successfully",
    "timestamp": "2024-01-15T10:45:00.000Z"
}
```

**Error Response Example:**
```json
{
    "success": false,
    "message": "Invalid EvalRunId format",
    "timestamp": "2024-01-15T10:45:00.000Z"
}
```

**Operations Performed:**
1. Validates eval run ID
2. Updates `cr890_status` to 4 (Failed) in Dataverse
3. Attempts to update external API status to "Failed"
4. Returns success even if external API fails (Dataverse is prioritized)

**External API Calls:**

**Call: Update External Status**
- **URL:** `{BaseUrl}/api/v1/eval/runs/{evalRunId}/status`
- **Method:** PUT
- **Headers:** `Content-Type: application/json`, `Authorization: Bearer {token}`
- **Body:** `{"status":"Failed"}`
- **Expected Response:** 200 OK or 204 No Content

**Notes:**
- Dataverse update is prioritized
- External API failure is logged but doesn't fail the operation
- Useful for error handling and recovery scenarios

---

## Custom API Summary Table

| API Name | Method | Function | Request Params | Response Params | External Calls | Status Update |
|----------|--------|----------|----------------|-----------------|----------------|---------------|
| `cr890_PostEvalRun` | POST | No | 5 params | 3 params | 0 | Creates (New) |
| `cr890_GetEvalRun` | GET | Yes | 1 param | 8 params | 0 | None |
| `cr890_UpdateDatasetAsFile` | POST | No | 2 params | 3 params | 2 | Sets Updated |
| `cr890_UpdateEnrichedDatasetFile` | POST | No | 2 params | 3 params | 0 | Sets Updated |
| `cr890_PublishEnrichedDataset` | POST | No | 1 param | 3 params | 1 | Sets Completed |
| `cr890_UpdateFailedState` | POST | No | 1 param | 3 params | 1 | Sets Failed |

---

## Request/Response Patterns

### Standard Success Response
All action-based Custom APIs (POST methods) return this structure:
```json
{
    "success": true,
 "message": "{Operation} successful",
    "timestamp": "2024-01-15T10:30:00.000Z"
}
```

### Standard Error Response
All Custom APIs return errors in this structure:
```json
{
    "success": false,
    "message": "{Error description}",
    "timestamp": "2024-01-15T10:30:00.000Z"
}
```

### GetEvalRun Extended Response
Only `GetEvalRun` returns extended data:
```json
{
    "evalRunId": "guid",
    "message": "string",
    "timestamp": "iso8601",
 "agentId": "string",
    "environmentId": "guid",
    "agentSchemaName": "string",
    "status": "string",
    "dataset": "json-array-string"
}
```

---

## Validation Rules

### Common Validations (All APIs)
- `evalRunId` must be a valid GUID format
- `evalRunId` cannot be null or empty

### UpdateDatasetAsFile Specific
- `datasetId` must be a valid GUID format
- `datasetId` cannot be null or empty
- Both `evalRunId` and `datasetId` are required

### UpdateEnrichedDatasetFile Specific
- `enrichedDatasetJson` cannot be null or empty
- `enrichedDatasetJson` must be valid JSON format
- Both `evalRunId` and `enrichedDatasetJson` are required

### PostEvalRun Specific
- Only `evalRunId` is required
- Other parameters are optional but recommended

---

## External API Summary

### Authentication

All external API calls use OAuth 2.0 Bearer token authentication:
- Token acquired via Managed Identity Service
- Scope: Configurable via `cr890_ApiScope` environment variable
- Default: `443bbe62-c474-49f7-884c-d1b5a23eb735/.default`
- Header Format: `Authorization: Bearer {token}`

### Status Values

| Status | Used In External API | Description |
|--------|---------------------|-------------|
| `EnrichingDataset` | UpdateDataset | Indicates dataset enrichment in progress |
| `Failed` | UpdateFailedState | Indicates evaluation run failed |

### Error Handling

- **Connection failures**: Logged and returned as error response
- **Timeout**: Configurable via `cr890_ApiTimeoutSeconds` (default: 30s)
- **Authentication failures**: Logged with warning, operation continues
- **Non-success status codes**: Logged and treated as failure

---

## Development
