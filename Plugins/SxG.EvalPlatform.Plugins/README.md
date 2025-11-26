# SxG Eval Platform Plugins

## Overview

This project contains Dataverse plugins for the SxG Eval Platform that provide custom APIs for managing evaluation runs and datasets. The plugins include early-bound entity implementation, environment variable-based configuration, and unified logging to both Dataverse audit logs and Azure Application Insights.

## Environment Variables

Configure these environment variables in Dataverse:

| Schema Name | Type | Description | Default Value | Required |
|------------|------|-------------|---------------|----------|
| `cr890_EvalApiBaseUrl` | Text | Base URL for Eval API | `https://sxgevalapidev.azurewebsites.net` | Yes |
| `cr890_ApiTimeoutSeconds` | Decimal | API call timeout (seconds) | `30` | No |
| `cr890_EnableAppInsightsLogging` | Yes/No | Enable Application Insights (WIP - Currently this is not working) | `No` | No |
| `cr890_EnableAuditLogging` | Yes/No | Enable Dataverse audit logs | `Yes` | No |
| `cr890_AppInsightsConnectionString` | Text | Azure App Insights connection string | (none) | Only if App Insights enabled |

### API URL Construction

The configuration service builds the following API endpoints:

- **Datasets API**: `{BaseUrl}/api/v1/eval/datasets/{datasetId}`
- **Eval Runs API**: `{BaseUrl}/api/v1/eval/runs/{evalRunId}`
- **Enriched Dataset API**: `{BaseUrl}/api/v1/eval/runs/{evalRunId}/enriched-dataset`
- **Status Update API**: `{BaseUrl}/api/v1/eval/runs/{evalRunId}/status`

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
  - `cr890_datasetfile` - Dataset File (File column - **required for UpdateDatasetAsFile plugin**)

### Status Values

```csharp
EvalRun.cr890_status_OptionSet.New      // 0
EvalRun.cr890_status_OptionSet.Started     // 1
EvalRun.cr890_status_OptionSet.Updated     // 2
EvalRun.cr890_status_OptionSet.Completed   // 3
EvalRun.cr890_status_OptionSet.Failed// 4
```

| Status Name | Dataverse Value | API Response |
|-------------|----------------|--------------|
| New | 0 | "New" |
| Started | 1 | "Started" |
| Updated | 2 | "Updated" |
| Completed | 3 | "Completed" |
| Failed | 4 | "Failed" |

## Custom APIs

### 1. PostEvalRun

Creates a new evaluation run record.

**Request:**
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd",
    "datasetId": "35ea4b87-9dd2-4bda-a968-60bf4c5464c9",
    "agentId": "dri-agent",
    "environmentId": "948a58e0-a265e26e-bbd0-3d0cf7978511",
    "agentSchemaName": "crb32_sxGDriCopilot"
}
```

**Response:**
```json
{
    "success": true,
    "message": "Eval run created successfully",
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

### 2. GetEvalRun

Retrieves an evaluation run by ID.

**Request:**
- `evalRunId` (string) - GUID of the eval run

**Response:**
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd",
    "datasetId": "35ea4b87-9dd2-4bda-a968-60bf4c5464c9",
    "message": "Eval run retrieved successfully",
    "timestamp": "2024-01-15T10:30:00.000Z",
    "agentId": "dri-agent",
    "environmentId": "948a58e0-a265e26e-bbd0-3d0cf7978511",
    "agentSchemaName": "crb32_sxGDriCopilot",
    "status": "Completed",
    "dataset": [
        {
            "query": "What is the severity level of ICM 691776070?",
            "groundTruth": "The severity level of ICM 691776070 is 3.",
            "actualResponse": "",
            "context": ""
        }
    ]
}
```

### 3. UpdateDataset

Updates evaluation run with dataset from external API.

**Request:**
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd",
    "datasetId": "35ea4b87-9dd2-4bda-a968-60bf4c5464c9"
}
```

**Process:**
1. Updates external status to "EnrichingDataset"
2. Fetches dataset from external API
3. Updates eval run record with dataset
4. Sets status to "Updated"

**Response:**
```json
{
    "success": true,
    "message": "Dataset updated successfully",
    "timestamp": "2024-01-15T10:35:00.000Z"
}
```

### 4. PublishEnrichedDataset

Publishes enriched dataset to external API.

**Request:**
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd"
}
```

**Process:**
1. Retrieves dataset from eval run record
2. Publishes to external API
3. Updates status to "Completed"

**Response:**
```json
{
    "success": true,
    "message": "Enriched dataset published successfully",
    "timestamp": "2024-01-15T10:40:00.000Z"
}
```

### 5. UpdateDatasetAsFile (NEW - DLP-Compliant File Storage)

Updates evaluation run with dataset from external API and stores as file in Dataverse file column.

**Request:**
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd",
    "datasetId": "35ea4b87-9dd2-4bda-a968-60bf4c5464c9"
}
```

**Process:**
1. Updates external status to "EnrichingDataset"
2. Fetches dataset from external API
3. Uploads dataset as file to `cr890_datasetfile` column using Dataverse File Blocks API
4. Sets status to "Updated"

**Key Features:**
- **DLP-Compliant**: Uses only 1st party Dataverse SDK connections
- **No Direct Uploads**: Files are not uploaded directly from external sources
- **File Storage**: Stores JSON as downloadable `.json` file
- **Large File Support**: Handles files up to 128MB using block upload
- **Automatic Naming**: Files named as `dataset_{datasetId}_{timestamp}.json`

**Schema Requirement:**
You must add a **File** column named `cr890_datasetfile` to the `cr890_evalrun` table.

**Response:**
```json
{
    "success": true,
    "message": "Dataset updated successfully",
    "timestamp": "2024-01-15T10:35:00.000Z"
}
```

**Comparison with UpdateDataset:**

| Feature | UpdateDataset | UpdateDatasetAsFile |
|---------|--------------|---------------------|
| Storage | Text column | File column |
| Size Limit | ~1 MB | 128 MB (configurable) |
| Retrieval | Direct field access | File download API |
| DLP Compliance | Yes | Yes |

**For detailed documentation, see:** [UpdateDatasetAsFile_README.md](Plugins/UpdateDatasetAsFile_README.md)

## Development

### Building the Project

```powershell
# Restore packages
dotnet restore

# Build Debug
dotnet build --configuration Debug

# Build Release
dotnet build --configuration Release
```

## License

Copyright (c) 2025 Microsoft. All rights reserved.
