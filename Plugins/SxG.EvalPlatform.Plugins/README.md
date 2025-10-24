# SxG Evaluation Platform Plugins

This project contains Dataverse plugins for the SxG Evaluation Platform that provide Custom APIs for managing evaluation runs and integrating with external evaluation services.

## Overview

The SxG Evaluation Platform Plugins enable evaluation workflow management through four main Custom APIs:

1. **PostEvalRun** - Creates new evaluation run records
2. **GetEvalRun** - Retrieves evaluation run records
3. **UpdateDataset** - Updates dataset from external APIs and populates evaluation runs
4. **PublishEnrichedDataset** - Publishes enriched dataset to external APIs

## Architecture

### Entity Structure

**EvalRun Entity (`cr890_evalrun`)**
- Primary Key: `cr890_evalrunid` (GUID)
- Primary Name: `cr890_id` (String - same value as Primary Key)
- Fields:
  - `cr890_agentid` - Agent identifier (string)
  - `cr890_environmentid` - Environment identifier (GUID as string)
  - `cr890_agentschemaname` - Agent schema name (string)
  - `cr890_status` - Status (Choice field with integer values, returned as strings in API responses)
  - `cr890_dataset` - Dataset JSON data (Multi Line Text)

### Status Field Details

The Status field is implemented as a **Choice** (OptionSet) field in Dataverse with the following integer values:

| Status Name | Dataverse Value | API Response |
|-------------|----------------|--------------|
| New | 0 | "New" |
| Started | 1 | "Started" |
| Updated | 2 | "Updated" |
| Completed | 3 | "Completed" |
| Failed | 4 | "Failed" |

**Note**: While the status is stored as integer values in Dataverse for proper Choice field handling, all API responses return the status as human-readable string values.

### Status Flow

```
New → Updated → Completed
  ↘            ↗
    Failed ←----
```

## Custom APIs

### 1. PostEvalRun API

**Method**: POST  
**Action**: `cr890_PostEvalRun`  
**API Endpoint**: `https://orgbae05e06.api.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun`
**Description**: Creates a new evaluation run record

**Request Body**:
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd",
    "agentId": "dri-agent",
    "environmentId": "948a58e0-a265e26e-bbd0-3d0cf7978511",
    "agentSchemaName": "crb32_sxGDriCopilot"
}
```

**Behavior**:
- Creates a new evaluation run record in Dataverse using the provided `evalRunId` as the Primary Key
- Sets the `cr890_id` field (Primary Name) to the same value as the Primary Key (as string)
- Populates all provided fields (`agentId`, `environmentId`, `agentSchemaName`) if included in request
- Sets initial status to "New" (integer value: 0)
- Validates that `evalRunId` is a valid GUID format before processing
- Uses optimized entity creation with direct Primary Key assignment
- Returns standardized success/error response format

**Response**:
```json
{
    "success": true,
    "message": "Eval run created successfully",
    "timestamp": "2024-01-15T10:30:00.000Z"
}
```

### 2. GetEvalRun API

**Method**: GET  
**Function**: `cr890_GetEvalRun`  
**API Endpoint**: `https://orgbae05e06.api.crm.dynamics.com/api/data/v9.2/cr890_GetEvalRun(evalRunId='6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd')`
**Description**: Retrieves an evaluation run record by ID

**Query Parameters**:
- `evalRunId` (required): GUID string of the evaluation run

**Behavior**:
- Uses optimized Primary Key lookup via `organizationService.Retrieve()` for fastest performance
- Retrieves all relevant fields from the evaluation run record
- Converts internal Choice field integer values to human-readable status strings
- **Parses dataset JSON string into structured objects** for easier consumption
- Returns detailed evaluation run information including current state and parsed dataset
- Handles cases where record is not found gracefully
- Validates `evalRunId` format before attempting database query
- Returns comprehensive record data suitable for status checking and workflow decisions
- **JSON Parsing**: Automatically parses stored dataset JSON string into array of objects

**Response**:
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd",
    "message": "Eval run retrieved successfully",
    "timestamp": "2024-01-15T10:30:00.000Z",
    "agentId": "dri-agent",
    "environmentId": "948a58e0-a265e26e-bbd0-3d0cf7978511",
    "agentSchemaName": "crb32_sxGDriCopilot",
    "status": "Completed",
    "dataset": [
        {
            "prompt": "What is the severity level of ICM 691776070?",
            "groundTruth": "",
            "actualResponse": "",
            "expectedResponse": "The severity level of ICM 691776070 is 3."
        },
        {
            "prompt": "Are there any past incidents similar to 691776070?",
            "groundTruth": "",
            "actualResponse": "",
            "expectedResponse": "Agent should identify similar past incidents to 691776070."
        }
    ]
}
```

### 3. UpdateDataset API

**Method**: POST  
**Action**: `cr890_UpdateDataset`  
**API Endpoint**: `https://orgbae05e06.api.crm.dynamics.com/api/data/v9.2/cr890_UpdateDataset`
**Description**: Updates dataset from external API and populates evaluation run with dataset content

**Request Body**:
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd"
}
```

**External API Call**: 
- URL: `https://sxgevalapidev.azurewebsites.net/api/v1/eval/artifacts/dataset?evalRunId={evalRunId}`
- Method: GET
- Expected Response:
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd",
    "agentId": "dri-agent",
    "dataSetId": "35ea4b87-9dd2-4bda-a968-60bf4c5464c9",
    "datasetContent": [
        {
            "prompt": "What is the severity level of ICM 691776070?",
            "groundTruth": "",
            "actualResponse": "",
            "expectedResponse": "The severity level of ICM 691776070 is 3."
        },
        {
            "prompt": "Are there any past incidents similar to 691776070?",
            "groundTruth": "",
            "actualResponse": "",
            "expectedResponse": "Agent should identify similar past incidents to 691776070."
        }
    ]
}
```

**Behavior**:
- Calls external dataset API using the provided `evalRunId` as query parameter
- **Parses JSON response using Newtonsoft.Json** for reliable data extraction
- Extracts `datasetContent` from the external API response and converts it to JSON string
- Updates the evaluation run's `dataset` field with the retrieved dataset content (stored as JSON string)
- Sets status to "Updated" (integer value: 2)
- Uses optimized Primary Key-based entity updates for performance
- Handles external API failures and JSON parsing errors gracefully with detailed logging
- Validates `evalRunId` format before processing
- **Real JSON Processing**: No mock data - parses actual external API responses

**Response**:
```json
{
    "success": true,
    "message": "Dataset updated successfully",
    "timestamp": "2024-01-15T10:35:00.000Z"
}
```

### 4. PublishEnrichedDataset API

**Method**: POST  
**Action**: `cr890_PublishEnrichedDataset`  
**API Endpoint**: `https://orgbae05e06.api.crm.dynamics.com/api/data/v9.2/cr890_PublishEnrichedDataset`
**Description**: Publishes enriched dataset to external API using stored dataset from evaluation run

**Request Body**:
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd"
}
```

**External API Call**:
- URL: `https://sxgevalapidev.azurewebsites.net/api/v1/eval/artifacts/enriched-dataset?evalRunId={evalRunId}`
- Method: POST
- Request Body:
```json
{
    "enrichedDataset": [
        {
            "prompt": "What is the severity level of ICM 691776070?",
            "groundTruth": "",
            "actualResponse": "",
            "expectedResponse": "The severity level of ICM 691776070 is 3."
        },
        {
            "prompt": "Are there any past incidents similar to 691776070?",
            "groundTruth": "",
            "actualResponse": "",
            "expectedResponse": "Agent should identify similar past incidents to 691776070."
        }
    ]
}
```

**Behavior**:
- Retrieves the stored dataset from the evaluation run record using `evalRunId`
- Calls external enriched dataset API to publish the dataset content
- Sets status to "Completed" (integer value: 3) after successful external API call
- Uses optimized Primary Key-based entity queries and updates
- Handles external API failures gracefully - status remains unchanged if external call fails
- Validates `evalRunId` format and ensures dataset exists in the record
- Publishes dataset as `enrichedDataset` property in the external API request body
- Uses two-phase operation: retrieve dataset, then publish to external API and update status

**Response**:
```json
{
    "success": true,
    "message": "Enriched dataset published successfully",
    "timestamp": "2024-01-15T10:40:00.000Z"
}
```

## Testing

1. **API Endpoint Verification**:
   - **POST APIs**: `https://orgbae05e06.api.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun`
   - **GET API**: `https://orgbae05e06.api.crm.dynamics.com/api/data/v9.2/cr890_GetEvalRun(evalRunId='guid')`
   - **POST APIs**: Similar pattern for UpdateDataset and PublishEnrichedDataset

2. **Test Each API**:
   - Use Insomnia, Power Platform CLI, or custom client
   - Verify request/response formats match documentation
   - Check plugin traces for debugging

3. **Authentication Token**
   - Login with your credentials using Az CLI and provide the Org URL as the scope
```
az login --scope "https://orgbae05e06.crm.dynamics.com/.default"
az account get-access-token --resource "https://orgbae05e06.crm.dynamics.com"
```

## Typical Workflow

1. **Create Evaluation Run**: Use PostEvalRun to create a new evaluation run with agent information (Status: "New")
2. **Update Dataset**: Use UpdateDataset with `evalRunId` to fetch dataset content from the external dataset API and populate the evaluation run (Status: "Updated")
3. **Execute Evaluation**: External system processes the evaluation (outside plugin scope)
4. **Publish Enriched Dataset**: Use PublishEnrichedDataset with `evalRunId` to publish the stored dataset to the external API and mark as completed (Status: "Completed")

## External Integrations

### Dataset API
- **URL**: `https://sxgevalapidev.azurewebsites.net/api/v1/eval/artifacts/dataset`
- **Purpose**: Retrieve dataset content for evaluation runs
- **Authentication**: None (currently)

### Enriched Dataset API
- **URL**: `https://sxgevalapidev.azurewebsites.net/api/v1/eval/artifacts/enriched-dataset`
- **Purpose**: Publish enriched evaluation datasets
- **Authentication**: None (currently)

## Development

### Prerequisites
- .NET Framework 4.6.2
- Microsoft Dataverse SDK
- Visual Studio 2019 or later

### Building
```bash
dotnet build
```

### Deployment
1. Build the project
2. Register plugins in Dataverse
3. Configure Custom APIs

### Configuration
Values are currently hardcoded with future work involving moving to environment variables and usage of constants file.

## Error Handling

All APIs return standardized error responses:
```json
{
    "success": false,
    "message": "Error description",
    "timestamp": "2024-01-15T10:30:00.000Z"
}
```

## Logging

Plugins use Dataverse tracing service for detailed logging. Trace logs are available in plugin execution context and can be viewed in Dataverse logs.

## API Reference

### Custom API Names and Plugin Classes

| API Name | Plugin Class | Description |
|----------|-------------|-------------|
| `cr890_PostEvalRun` | `PostEvalRun` | Creates evaluation run records |
| `cr890_GetEvalRun` | `GetEvalRun` | Retrieves evaluation run records with parsed dataset |
| `cr890_UpdateDataset` | `UpdateDataset` | Updates dataset from external API with real JSON parsing |
| `cr890_PublishEnrichedDataset` | `PublishEnrichedDataset` | Publishes enriched dataset to external API |

### Plugin Type Names for Registration

- `SxG.EvalPlatform.Plugins.Plugins.PostEvalRun`
- `SxG.EvalPlatform.Plugins.Plugins.GetEvalRun` 
- `SxG.EvalPlatform.Plugins.Plugins.UpdateDataset`
- `SxG.EvalPlatform.Plugins.Plugins.PublishEnrichedDataset`

## Custom API Registration with Plugin Registration Tool

This section provides step-by-step instructions for registering Custom APIs using the Plugin Registration Tool (PRT).

### Prerequisites for Registration
1. **Plugin Assembly**: Build and have the `SxG.EvalPlatform.Plugins.dll` ready
2. **Plugin Registration Tool**: Download from [Microsoft PowerApps CLI](https://aka.ms/PowerAppsCLI) or use standalone PRT
3. **System Administrator**: Ensure you have System Administrator privileges in the target Dataverse environment
4. **Assembly Registration**: Plugin assembly must be registered first before creating Custom APIs

### Binding Recommendation: **Global (None)**

**Recommended Binding**: **None (Global)**

**Rationale**:
- **Environment Variables Access**: Future requirement to query environment variables requires global access to system tables (`environmentvariablevalue`, `environmentvariabledefinition`)
- **Cross-Entity Operations**: Plugins may need to interact with multiple entities beyond `cr890_evalrun`
- **Flexibility**: Global binding provides maximum flexibility for future enhancements
- **Performance**: No performance penalty as operations are already optimized with Primary Key queries
- **Security**: Security is handled through Custom API privileges rather than entity-level binding

### Step-by-Step Registration Process

#### Step 1: Register Plugin Assembly (One Time)

1. **Connect to Environment**:
   - Open Plugin Registration Tool
   - Click "Create New Connection"
   - Select authentication method (Office 365, IFD, etc.)
   - Enter server URL and credentials
   - Click "Login"

2. **Register Assembly**:
   - Click "Register" → "Register New Assembly"
   - Browse and select `SxG.EvalPlatform.Plugins.dll`
   - **Isolation Mode**: Sandbox (Recommended for Dataverse)
   - **Assembly Location**: Database (Required for Custom APIs)
   - Click "Register Selected Plugins"
   - Verify all 4 plugin classes are registered

#### Step 2: Create Custom APIs

**For each Custom API, follow these steps:**

---

### 2.1 PostEvalRun Custom API

1. **Create Custom API**:
   - Right-click on the registered assembly
   - Select "Create Custom API"
   - **Unique Name**: `cr890_PostEvalRun`
   - **Display Name**: `Post Eval Run`
   - **Description**: `Creates a new evaluation run record`
   - **Binding Type**: `None` (Global)
   - **Bound Entity Logical Name**: *(Leave empty)*
   - **Is Function**: `No` (POST operation)
   - **Is Private**: `No`
   - **Allowed Custom Processing Step Type**: `AsyncOnly` or `SyncAndAsync`
   - **Execute Privilege Name**: `cr890_PostEvalRun`

2. **Add Request Parameters**:
   - Click "Add Request Parameter" for each:
   
   | Name | Display Name | Description | Type | Optional |
   |------|--------------|-------------|------|----------|
   | `evalRunId` | Eval Run Id | Evaluation run identifier | String | No |
   | `agentId` | Agent Id | Agent identifier | String | Yes |
   | `environmentId` | Environment Id | Environment identifier | String | Yes |
   | `agentSchemaName` | Agent Schema Name | Agent schema name | String | Yes |

3. **Add Response Properties**:
   - Click "Add Response Property" for each:
   
   | Name | Display Name | Description | Type |
   |------|--------------|-------------|------|
   | `success` | Success | Operation success indicator | Boolean |
   | `message` | Message | Response message | String |
   | `timestamp` | Timestamp | Operation timestamp | String |

---

### 2.2 GetEvalRun Custom API

1. **Create Custom API**:
   - **Unique Name**: `cr890_GetEvalRun`
   - **Display Name**: `Get Eval Run`
   - **Description**: `Retrieves eval run record by EvalRunId`
   - **Binding Type**: `None` (Global)
   - **Is Function**: `Yes` (GET operation)
   - **Is Private**: `No`
   - **Execute Privilege Name**: `cr890_GetEvalRun`

2. **Add Request Parameters**:
   
   | Name | Display Name | Description | Type | Optional |
   |------|--------------|-------------|------|----------|
   | `evalRunId` | Eval Run Id | Evaluation run identifier | String | No |

3. **Add Response Properties**:
   
   | Name | Display Name | Description | Type |
   |------|--------------|-------------|------|
   | `evalRunId` | Eval Run Id | Evaluation run identifier | String |
   | `message` | Message | Response message | String |
   | `timestamp` | Timestamp | Operation timestamp | String |
   | `agentId` | Agent Id | Agent identifier | String |
   | `environmentId` | Environment Id | Environment identifier | String |
   | `agentSchemaName` | Agent Schema Name | Agent schema name | String |
   | `status` | Status | Evaluation run status | String |
   | `dataset` | Dataset | Dataset JSON array | String |

---

### 2.3 UpdateDataset Custom API

1. **Create Custom API**:
   - **Unique Name**: `cr890_UpdateDataset`
   - **Display Name**: `Update Dataset`
   - **Description**: `Updates dataset from external datasets API and updates eval run`
   - **Binding Type**: `None` (Global)
   - **Is Function**: `No` (POST operation)
   - **Execute Privilege Name**: `cr890_UpdateDataset`

2. **Add Request Parameters**:
   
   | Name | Display Name | Description | Type | Optional |
   |------|--------------|-------------|------|----------|
   | `evalRunId` | Eval Run Id | Evaluation run identifier | String | No |

3. **Add Response Properties**:
   
   | Name | Display Name | Description | Type |
   |------|--------------|-------------|------|
   | `success` | Success | Operation success indicator | Boolean |
   | `message` | Message | Response message | String |
   | `timestamp` | Timestamp | Operation timestamp | String |

---

### 2.4 PublishEnrichedDataset Custom API

1. **Create Custom API**:
   - **Unique Name**: `cr890_PublishEnrichedDataset`
   - **Display Name**: `Publish Enriched Dataset`
   - **Description**: `Publishes enriched dataset to external API from stored dataset`
   - **Binding Type**: `None` (Global)
   - **Is Function**: `No` (POST operation)
   - **Execute Privilege Name**: `cr890_PublishEnrichedDataset`

2. **Add Request Parameters**:
   
   | Name | Display Name | Description | Type | Optional |
   |------|--------------|-------------|------|----------|
   | `evalRunId` | Eval Run Id | Evaluation run identifier | String | No |

3. **Add Response Properties**:
   
   | Name | Display Name | Description | Type |
   |------|--------------|-------------|------|
   | `success` | Success | Operation success indicator | Boolean |
   | `message` | Message | Response message | String |
   | `timestamp` | Timestamp | Operation timestamp | String |
