# SxG Evaluation Platform Plugins

This project contains Dataverse plugins for the SxG Evaluation Platform that provide Custom APIs for managing evaluation runs and integrating with external evaluation services.

## Overview

The SxG Evaluation Platform Plugins enable evaluation workflow management through four main Custom APIs:

1. **PostEvalRun** - Creates new evaluation run records with dataset tracking
2. **GetEvalRun** - Retrieves evaluation run records with parsed datasets
3. **UpdateDataset** - Fetches datasets from external API and updates evaluation runs with status tracking
4. **PublishEnrichedDataset** - Publishes enriched dataset to external APIs

## Architecture

### Entity Structure

**EvalRun Entity (`cr890_evalrun`)**
- Primary Key: `cr890_evalrunid` (GUID)
- Primary Name: `cr890_id` (String - same value as Primary Key)
- Fields:
  - `cr890_datasetid` - Dataset identifier (GUID stored as string) - **NEW**
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
  ↘       ↗
    Failed ←----
```

## Custom APIs

### 1. PostEvalRun API

**Method**: POST  
**Action**: `cr890_PostEvalRun`  
**Description**: Creates a new evaluation run record with optional dataset tracking

**Request Body**:
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd",
    "datasetId": "35ea4b87-9dd2-4bda-a968-60bf4c5464c9",
    "agentId": "dri-agent",
    "environmentId": "948a58e0-a265e26e-bbd0-3d0cf7978511",
    "agentSchemaName": "crb32_sxGDriCopilot"
}
```

**Request Parameters**:
- `evalRunId` (required): Evaluation run identifier (GUID)
- `datasetId` (optional): Dataset identifier (GUID) - **NEW**
- `agentId` (optional): Agent identifier
- `environmentId` (optional): Environment identifier (GUID)
- `agentSchemaName` (optional): Agent schema name

**Behavior**:
- Creates a new evaluation run record in Dataverse using the provided `evalRunId` as the Primary Key
- **Stores datasetId for dataset tracking** - **NEW**
- Sets the `cr890_id` field (Primary Name) to the same value as the Primary Key (as string)
- Populates all provided fields if included in request
- Sets initial status to "New" (integer value: 0)
- Validates that `evalRunId` and `datasetId` (if provided) are valid GUID formats
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
**Description**: Retrieves an evaluation run record by ID with parsed dataset

**Query Parameters**:
- `evalRunId` (required): GUID string of the evaluation run

**Behavior**:
- Uses optimized Primary Key lookup via `organizationService.Retrieve()` for fastest performance
- Retrieves all relevant fields from the evaluation run record including `datasetId`
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

### 3. UpdateDataset API

**Method**: POST  
**Action**: `cr890_UpdateDataset`  
**Description**: Updates external eval run status to "EnrichingDataset", fetches dataset from external API, and updates evaluation run record

**Request Body**:
```json
{
    "evalRunId": "6cb6deb9-4b5b-4a93-987b-fdaccc5e79dd",
    "datasetId": "35ea4b87-9dd2-4bda-a968-60bf4c5464c9"
}
```

**Request Parameters**:
- `evalRunId` (required): Evaluation run identifier to update
- `datasetId` (required): Dataset identifier to fetch - **NEW**

**External API Calls**:

1. **Status Update** (Pre-flight):
   - URL: `PUT https://sxgevalapidev.azurewebsites.net/api/v1/eval/runs/{evalRunId}/status`
- Body: `{"status":"EnrichingDataset"}`
   - Purpose: Signal that dataset enrichment is starting

2. **Dataset Fetch**:
   - URL: `GET https://sxgevalapidev.azurewebsites.net/api/v1/eval/datasets/{datasetId}`
   - Expected Response:
```json
[
  {
    "query": "What is the severity level of ICM 691776070?",
    "groundTruth": "The severity level of ICM 691776070 is 3.",
    "actualResponse": "",
    "context": ""
  },
  {
    "query": "Are there any past incidents similar to 691776070?",
    "groundTruth": "Agent should identify similar past incidents to 691776070.",
    "actualResponse": "",
    "context": ""
  }
]
```

**Behavior**:
1. **Validates** both `evalRunId` and `datasetId` are provided and valid GUIDs
2. **Updates External Status** - Calls external API to set status to "EnrichingDataset" (non-blocking if fails)
3. **Fetches Dataset** - Calls external datasets API using `datasetId` as path parameter
4. **Stores Raw JSON** - Saves the complete JSON array response in the `dataset` field
5. **Updates Dataverse Status** - Sets internal status to "Updated" (integer value: 2)
6. **Returns Response** - Success or detailed error message

**Key Features**:
- **Two-Parameter Design**: Uses `datasetId` for data retrieval, `evalRunId` for record identification
- **Pre-flight Status Update**: Signals enrichment start to external system
- **Non-Blocking Status**: Continues even if external status update fails
- **Raw JSON Storage**: No parsing or transformation, stores API response as-is
- **Comprehensive Logging**: All steps traced for debugging
- **Error Resilience**: Graceful handling of external API failures

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
   "query": "What is the severity level of ICM 691776070?",
            "groundTruth": "The severity level of ICM 691776070 is 3.",
    "actualResponse": "",
            "context": ""
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

### Status Update API
- **URL**: `https://sxgevalapidev.azurewebsites.net/api/v1/eval/runs/{evalRunId}/status`
- **Method**: PUT
- **Purpose**: Update external eval run status
- **Request Body**: `{"status":"EnrichingDataset"}`
- **Authentication**: None (currently)

### Dataset API
- **URL**: `https://sxgevalapidev.azurewebsites.net/api/v1/eval/datasets/{datasetId}`
- **Method**: GET
- **Purpose**: Retrieve dataset content by dataset ID
- **Authentication**: None (currently)

### Enriched Dataset API
- **URL**: `https://sxgevalapidev.azurewebsites.net/api/v1/eval/artifacts/enriched-dataset`
- **Method**: POST
- **Purpose**: Publish enriched evaluation datasets
- **Authentication**: None (currently)

## Development

### Prerequisites
- .NET Framework 4.6.2
- Microsoft Dataverse SDK
- Visual Studio 2019 or later
- Newtonsoft.Json 13.0.4

### Building
```bash
dotnet build
```

### Deployment
1. Build the project
2. Register plugins in Dataverse using Plugin Registration Tool
3. Configure Custom APIs with proper parameters
4. Set appropriate security roles and privileges

### Configuration
Plugins support both secure and unsecure configuration strings for environment-specific settings.


### Query Optimization
- **Performance**: All plugins query using `EvalRunId` (Primary Key) directly for faster database operations
- **GetEvalRun**: Uses `organizationService.Retrieve()` with Primary Key
- **UpdateDataset/PublishEnrichedDataset**: Uses direct entity updates with Primary Key instead of QueryExpression

### Error Handling
- All APIs return standardized error responses with `success`, `message`, and `timestamp`
- Comprehensive logging at each step for troubleshooting
- Graceful handling of external API failures
- Non-blocking status updates to ensure data flow continues

## Security

- All APIs require appropriate Dataverse privileges
- Custom API privileges: `cr890_PostEvalRun`, `cr890_GetEvalRun`, `cr890_UpdateDataset`, `cr890_PublishEnrichedDataset`
- Entity privileges: Create, Read, Update on `cr890_evalrun` entity
- Global binding allows flexibility for future environment variable access

### Dataset JSON Processing
- **Storage**: Dataset is stored as JSON string in Dataverse Multi Line Text field
- **Format**: Raw JSON array from external API (no transformation)
- **GetEvalRun Parsing**: Automatically parses JSON string into structured objects using Newtonsoft.Json
- **UpdateDataset**: Stores raw JSON array response as-is
- **Response Format**: Returns parsed dataset as array of objects
- **Error Handling**: Returns empty array if JSON parsing fails
- **Dependencies**: Uses Newtonsoft.Json 13.0.4 for JSON serialization/deserialization

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
| `cr890_PostEvalRun` | `PostEvalRun` | Creates evaluation run records with dataset tracking |
| `cr890_GetEvalRun` | `GetEvalRun` | Retrieves evaluation run records with parsed dataset |
| `cr890_UpdateDataset` | `UpdateDataset` | Updates external status and fetches dataset from external API |
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
   - **Description**: `Creates a new evaluation run record with dataset tracking`
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
 | `datasetId` | Dataset Id | Dataset identifier | String | Yes |
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

4. **Register Plugin Step**:
   - Right-click on the Custom API → "Register New Step"
 - **Message**: `cr890_PostEvalRun`
   - **Primary Entity**: `none`
   - **Plugin Type**: `SxG.EvalPlatform.Plugins.Plugins.PostEvalRun`
   - **Event Pipeline Stage**: `Main Operation`
   - **Execution Mode**: `Synchronous`
   - **Deployment**: `Server`

---

### 2.2 GetEvalRun Custom API

1. **Create Custom API**:
   - **Unique Name**: `cr890_GetEvalRun`
   - **Display Name**: `Get Eval Run`
   - **Description**: `Retrieves eval run record by EvalRunId with parsed dataset`
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
   | `datasetId` | Dataset Id | Dataset identifier | String |
   | `message` | Message | Response message | String |
   | `timestamp` | Timestamp | Operation timestamp | String |
   | `agentId` | Agent Id | Agent identifier | String |
   | `environmentId` | Environment Id | Environment identifier | String |
   | `agentSchemaName` | Agent Schema Name | Agent schema name | String |
   | `status` | Status | Evaluation run status | String |
   | `dataset` | Dataset | Dataset JSON array | String |

4. **Register Plugin Step**:
   - **Message**: `cr890_GetEvalRun`
   - **Plugin Type**: `SxG.EvalPlatform.Plugins.Plugins.GetEvalRun`
   - *Other settings same as PostEvalRun*

---

### 2.3 UpdateDataset Custom API

1. **Create Custom API**:
   - **Unique Name**: `cr890_UpdateDataset`
   - **Display Name**: `Update Dataset`
   - **Description**: `Updates external status and fetches dataset from external API`
   - **Binding Type**: `None` (Global)
   - **Is Function**: `No` (POST operation)
   - **Execute Privilege Name**: `cr890_UpdateDataset`

2. **Add Request Parameters**:
   
   | Name | Display Name | Description | Type | Optional |
   |------|--------------|-------------|------|----------|
   | `evalRunId` | Eval Run Id | Evaluation run identifier | String | No |
   | `datasetId` | Dataset Id | Dataset identifier | String | No |

3. **Add Response Properties**:
   
   | Name | Display Name | Description | Type |
   |------|--------------|-------------|------|
   | `success` | Success | Operation success indicator | Boolean |
   | `message` | Message | Response message | String |
   | `timestamp` | Timestamp | Operation timestamp | String |

4. **Register Plugin Step**:
   - **Message**: `cr890_UpdateDataset`
   - **Plugin Type**: `SxG.EvalPlatform.Plugins.Plugins.UpdateDataset`
   - *Other settings same as PostEvalRun*

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

4. **Register Plugin Step**:
   - **Message**: `cr890_PublishEnrichedDataset`
   - **Plugin Type**: `SxG.EvalPlatform.Plugins.Plugins.PublishEnrichedDataset`
   - *Other settings same as PostEvalRun*
