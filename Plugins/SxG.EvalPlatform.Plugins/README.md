# SxG EvalPlatform Plugins

This project contains Dataverse plugins that expose Custom APIs for managing evaluation jobs in the SxG EvalPlatform using an Elastic table.

## Overview

The plugin provides two Custom APIs for evaluation job management:
1. **PostEvalRun** - Creates new evaluation job records (POST operation)
2. **GetEvalRun** - Retrieves a specific evaluation job record by Id (GET operation)

## Architecture

### Project Structure
```
SxG.EvalPlatform.Plugins/
??? Plugins/
?   ??? PostEvalRun.cs          # Plugin for creating eval jobs
?   ??? GetEvalRun.cs           # Plugin for retrieving eval jobs
??? Models/
?   ??? EvalJobEntity.cs        # Entity model for cr890_evaljob Elastic table
?   ??? Requests/
?   ?   ??? EvalRunRequests.cs  # Request models with validation
?   ??? Responses/
?       ??? EvalRunResponses.cs # Response models with status codes
??? CustomApis/
?   ??? CustomApiConfig.cs      # Configuration constants for Custom APIs
??? Framework/                  # Base plugin framework (inherited)
```

### Database Schema (Elastic Table)

#### cr890_evaljob Table
| Column | Type | Description | Required |
|--------|------|-------------|----------|
| cr890_evaljobid | Unique Identifier | Primary key (GUID) | Yes |
| cr890_id | Single Line of Text | Primary Name Column (GUID as string, same as evaljobid) | Yes |
| cr890_agentid | Single Line of Text | Agent identifier (GUID as string) | Yes |
| cr890_environmentid | Single Line of Text | Environment identifier (GUID as string) | Yes |
| cr890_schemaname | Single Line of Text | Schema name | Yes |
| cr890_status | Choice | Status: New (0), Active (1), Success (2), Failed (3). Default: New | Yes |
| cr890_input | Multi Line of Text | Input JSON data | No |
| cr890_output | Multi Line of Text | Output JSON data | No |
| createdon | DateTime | Record creation timestamp | Auto |
| modifiedon | DateTime | Record modification timestamp | Auto |

#### Status Values
- **New (0)** - Default status for new eval jobs
- **Active (1)** - Eval job is currently being processed
- **Success (2)** - Eval job completed successfully
- **Failed (3)** - Eval job failed during processing

## Custom APIs

### PostEvalRun API

**Purpose**: Creates a new evaluation job record in the `cr890_evaljob` Elastic table.

**HTTP Method**: POST  
**API Name**: `cr890_PostEvalRun`  
**Endpoint**: `/api/data/v9.2/cr890_PostEvalRun`

#### Request Parameters
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| AgentId | String | Yes | Agent identifier (GUID as string) |
| EnvironmentId | String | Yes | Environment identifier (GUID as string) |
| SchemaName | String | Yes | Name of the schema |
| Input | String | Yes | Input JSON data (Multi Line of Text) |

#### Request Example
```json
{
    "AgentId": "12345678-1234-1234-1234-123456789012",
    "EnvironmentId": "87654321-4321-4321-4321-210987654321", 
    "SchemaName": "evaluation-schema",
    "Input": "{\"data\":\"sample input data\",\"parameters\":{\"param1\":\"value1\"}}"
}
```

#### Response Properties
| Property | Type | Description |
|----------|------|-------------|
| Success | Boolean | Indicates if the operation was successful |
| Id | String | Unique identifier of the created evaluation job (GUID as string) |
| Message | String | Descriptive message about the operation result |
| StatusCode | Integer | HTTP status code (202 for success) |
| Timestamp | String | ISO 8601 timestamp of the operation |
| **EvalJobId** | String | **Eval Job ID (Primary Key as GUID string)** |
| **AgentId** | String | **Agent identifier** |
| **EnvironmentId** | String | **Environment identifier** |
| **SchemaName** | String | **Schema name** |
| **Status** | Integer | **Status value (0=New, 1=Active, 2=Success, 3=Failed)** |

#### Response Example (Success)
```json
{
    "Success": true,
    "Id": "98765432-8765-4321-8765-432187654321",
    "Message": "Eval job created successfully",
    "StatusCode": 202,
    "Timestamp": "2024-01-15T10:30:00.000Z",
    "EvalJobId": "98765432-8765-4321-8765-432187654321",
    "AgentId": "12345678-1234-1234-1234-123456789012",
    "EnvironmentId": "87654321-4321-4321-4321-210987654321",
    "SchemaName": "evaluation-schema",
    "Status": 0
}
```

### GetEvalRun API

**Purpose**: Retrieves a specific evaluation job record by Id (Primary Name Column).

**HTTP Method**: GET  
**API Name**: `cr890_GetEvalRun`  
**Endpoint**: `/api/data/v9.2/cr890_GetEvalRun(Id='your-eval-job-id')`

#### Request Parameters
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| Id | String | Yes | Eval job Id (Primary Name Column - GUID as string) |

#### Request Example
```
GET /api/data/v9.2/cr890_GetEvalRun(Id='98765432-8765-4321-8765-432187654321')
```

#### Response Properties
| Property | Type | Description |
|----------|------|-------------|
| Success | Boolean | Indicates if the operation was successful |
| EvalJob | String | JSON string containing the evaluation job record |
| Message | String | Descriptive message about the operation result |
| StatusCode | Integer | HTTP status code (200 for success, 404 if not found) |
| Timestamp | String | ISO 8601 timestamp of the operation |
| **EvalJobId** | String | **Eval Job ID (Primary Key as GUID string)** |
| **AgentId** | String | **Agent identifier** |
| **EnvironmentId** | String | **Environment identifier** |
| **SchemaName** | String | **Schema name** |
| **Status** | Integer | **Status value (0=New, 1=Active, 2=Success, 3=Failed)** |
| **Input** | String | **Input JSON data** |
| **Output** | String | **Output JSON data** |

#### Response Example (Success)
```json
{
    "Success": true,
    "EvalJob": "{\"EvalJobId\":\"98765432-8765-4321-8765-432187654321\",\"Id\":\"98765432-8765-4321-8765-432187654321\",\"AgentId\":\"12345678-1234-1234-1234-123456789012\",\"EnvironmentId\":\"87654321-4321-4321-4321-210987654321\",\"SchemaName\":\"evaluation-schema\",\"Status\":0,\"Input\":\"{\\\"data\\\":\\\"sample input data\\\"}\",\"Output\":\"\",\"CreatedOn\":\"2024-01-15T10:30:00.000Z\",\"ModifiedOn\":\"2024-01-15T10:30:00.000Z\"}",
    "Message": "Eval job retrieved successfully",
    "StatusCode": 200,
    "Timestamp": "2024-01-15T11:30:00.000Z",
    "EvalJobId": "98765432-8765-4321-8765-432187654321",
    "AgentId": "12345678-1234-1234-1234-123456789012",
    "EnvironmentId": "87654321-4321-4321-4321-210987654321",
    "SchemaName": "evaluation-schema",
    "Status": 0,
    "Input": "{\"data\":\"sample input data\",\"parameters\":{\"param1\":\"value1\"}}",
    "Output": null
}
```

#### Response Example (Not Found)
```json
{
    "Success": true,
    "EvalJob": "null",
    "Message": "Eval job not found",
    "StatusCode": 404,
    "Timestamp": "2024-01-15T11:30:00.000Z",
    "EvalJobId": null,
    "AgentId": null,
    "EnvironmentId": null,
    "SchemaName": null,
    "Status": 0,
    "Input": null,
    "Output": null
}
```

## Deployment Guide

### Prerequisites

1. **Plugin Registration Tool** installed and connected to your Dataverse environment
2. **System Administrator** or **System Customizer** role
3. **cr890_evaljob** Elastic table created in Dataverse

### Step 1: Create the Elastic Table (cr890_evaljob)

Before deploying the plugins, ensure the `cr890_evaljob` Elastic table exists with the following configuration:

#### Table Details
- **Logical Name**: `cr890_evaljob`
- **Display Name**: `Eval Job`
- **Table Type**: `Elastic`
- **Primary Column**: `cr890_id` (Single Line of Text)

#### Required Columns Configuration
| Schema Name | Display Name | Type | Required | Additional Info |
|-------------|--------------|------|----------|-----------------|
| cr890_evaljobid | Eval Job ID | Unique Identifier | Yes | Primary key |
| cr890_id | ID | Single Line of Text | Yes | Primary Name Column |
| cr890_agentid | Agent ID | Single Line of Text | Yes | GUID stored as text |
| cr890_environmentid | Environment ID | Single Line of Text | Yes | GUID stored as text |
| cr890_schemaname | Schema Name | Single Line of Text | Yes | |
| cr890_status | Status | Choice | Yes | Options: New (0), Active (1), Success (2), Failed (3) |
| cr890_input | Input | Multi Line of Text | No | JSON data |
| cr890_output | Output | Multi Line of Text | No | JSON data |

### Step 2: Deploy Plugin Assembly

1. Build the `SxG.EvalPlatform.Plugins` project
2. Sign the assembly (recommended for production)
3. Register the assembly in Dataverse using Plugin Registration Tool

#### Using Plugin Registration Tool
1. Open Plugin Registration Tool
2. Connect to your Dataverse environment
3. Click **Register** ? **Register New Assembly**
4. Select the compiled `SxG.EvalPlatform.Plugins.dll` file
5. Choose **Sandbox** isolation mode (recommended)
6. Click **Register Selected Plugins**

### Step 3: Register Custom APIs

#### Create PostEvalRun Custom API

1. In Plugin Registration Tool, navigate to **Custom APIs** section
2. Right-click and select **Register New Custom API**
3. Fill in the following details:

| Field | Value |
|-------|--------|
| **Unique Name** | `cr890_PostEvalRun` |
| **Display Name** | `Post Eval Run` |
| **Description** | `Creates a new eval job record` |
| **Binding Type** | `Global` |
| **Is Function** | `No` *(POST operation)* |
| **Is Private** | `No` |
| **Execute Privilege Name** | `cr890_PostEvalRun` |
| **Plugin Type** | Select `SxG.EvalPlatform.Plugins.Plugins.PostEvalRun` |

#### Create Request Parameters for PostEvalRun

Create the following request parameters (right-click on the Custom API and select "Add Request Parameter"):

1. **AgentId** - String, Required, Sequence: 1
2. **EnvironmentId** - String, Required, Sequence: 2
3. **SchemaName** - String, Required, Sequence: 3
4. **Input** - String, Required, Sequence: 4

#### Create Response Properties for PostEvalRun

Create the following response properties (right-click on the Custom API and select "Add Response Property"):

1. **Success** (Boolean, Sequence: 1)
2. **Id** (String, Sequence: 2)
3. **Message** (String, Sequence: 3)
4. **StatusCode** (Integer, Sequence: 4)
5. **Timestamp** (String, Sequence: 5)
6. **EvalJobId** (String, Sequence: 6)
7. **AgentId** (String, Sequence: 7)
8. **EnvironmentId** (String, Sequence: 8)
9. **SchemaName** (String, Sequence: 9)
10. **Status** (Integer, Sequence: 10)

#### Create GetEvalRun Custom API

Follow similar steps for GetEvalRun with these details:

| Field | Value |
|-------|--------|
| **Unique Name** | `cr890_GetEvalRun` |
| **Display Name** | `Get Eval Run` |
| **Description** | `Retrieves eval job record by Id` |
| **Is Function** | `Yes` *(GET operation)* |
| **Plugin Type** | Select `SxG.EvalPlatform.Plugins.Plugins.GetEvalRun` |

**Request Parameters**: 
- Id (String, Required, Sequence: 1)

**Response Properties**: 
1. **Success** (Boolean, Sequence: 1)
2. **EvalJob** (String, Sequence: 2)
3. **Message** (String, Sequence: 3)
4. **StatusCode** (Integer, Sequence: 4)
5. **Timestamp** (String, Sequence: 5)
6. **EvalJobId** (String, Sequence: 6)
7. **AgentId** (String, Sequence: 7)
8. **EnvironmentId** (String, Sequence: 8)
9. **SchemaName** (String, Sequence: 9)
10. **Status** (Integer, Sequence: 10)
11. **Input** (String, Sequence: 11)
12. **Output** (String, Sequence: 12)

### Step 4: Configure Security

#### Create Security Privileges
1. Navigate to **Settings** ? **Security** ? **Privileges**
2. Create privileges: `cr890_PostEvalRun` and `cr890_GetEvalRun`
3. Assign to appropriate security roles
4. Ensure users have permissions on the `cr890_evaljob` table

## Usage Examples

### PowerShell (using Dataverse Web API)
```powershell
# POST Example - Create Eval Job
$postBody = @{
    AgentId = "12345678-1234-1234-1234-123456789012"
    EnvironmentId = "87654321-4321-4321-4321-210987654321"
    SchemaName = "evaluation-schema"
    Input = '{"data":"sample input","params":{"p1":"v1"}}'
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "$dataverseUrl/api/data/v9.2/cr890_PostEvalRun" -Method POST -Body $postBody -ContentType "application/json" -Headers $headers

# Access additional response properties
Write-Host "Created Eval Job ID: $($response.EvalJobId)"
Write-Host "Agent ID: $($response.AgentId)"
Write-Host "Status: $($response.Status)"

# GET Example - Retrieve Eval Job
$getResponse = Invoke-RestMethod -Uri "$dataverseUrl/api/data/v9.2/cr890_GetEvalRun(Id='98765432-8765-4321-8765-432187654321')" -Method GET -Headers $headers

# Access individual response properties
Write-Host "Retrieved Eval Job ID: $($getResponse.EvalJobId)"
Write-Host "Agent ID: $($getResponse.AgentId)"
Write-Host "Status: $($getResponse.Status)"
Write-Host "Input: $($getResponse.Input)"
Write-Host "Output: $($getResponse.Output)"
```

### JavaScript (using Dataverse Web API)
```javascript
// POST Example - Create Eval Job
const postData = {
    AgentId: "12345678-1234-1234-1234-123456789012",
    EnvironmentId: "87654321-4321-4321-4321-210987654321", 
    SchemaName: "evaluation-schema",
    Input: JSON.stringify({data: "sample input", params: {p1: "v1"}})
};

fetch(`${dataverseUrl}/api/data/v9.2/cr890_PostEvalRun`, {
    method: 'POST',
    headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`
    },
    body: JSON.stringify(postData)
})
.then(response => response.json())
.then(data => {
    console.log('Created Eval Job ID:', data.EvalJobId);
    console.log('Agent ID:', data.AgentId);
    console.log('Status:', data.Status);
});

// GET Example - Retrieve Eval Job
fetch(`${dataverseUrl}/api/data/v9.2/cr890_GetEvalRun(Id='98765432-8765-4321-8765-432187654321')`, {
    method: 'GET',
    headers: {
        'Authorization': `Bearer ${token}`
    }
})
.then(response => response.json())
.then(data => {
    console.log('Retrieved Eval Job ID:', data.EvalJobId);
    console.log('Agent ID:', data.AgentId);
    console.log('Status:', data.Status);
    console.log('Input:', data.Input);
    console.log('Output:', data.Output);
});
```

### C# (using Dataverse SDK)
```csharp
// POST Example - Create Eval Job
var request = new OrganizationRequest("cr890_PostEvalRun");
request["AgentId"] = "12345678-1234-1234-1234-123456789012";
request["EnvironmentId"] = "87654321-4321-4321-4321-210987654321";
request["SchemaName"] = "evaluation-schema";
request["Input"] = "{\"data\":\"sample input\"}";

var response = organizationService.Execute(request);
var success = (bool)response["Success"];
var evalJobId = response["EvalJobId"].ToString();
var agentId = response["AgentId"].ToString();
var status = (int)response["Status"];

// GET Example - Retrieve Eval Job
var getRequest = new OrganizationRequest("cr890_GetEvalRun");
getRequest["Id"] = "98765432-8765-4321-8765-432187654321";

var getResponse = organizationService.Execute(getRequest);
var retrievedEvalJobId = getResponse["EvalJobId"]?.ToString();
var retrievedAgentId = getResponse["AgentId"]?.ToString();
var retrievedStatus = (int)getResponse["Status"];
var input = getResponse["Input"]?.ToString();
var output = getResponse["Output"]?.ToString();
```

## Data Flow

1. **Create Eval Job (PostEvalRun)**:
   - Client provides: AgentId, EnvironmentId, SchemaName, Input
   - Plugin generates new GUID for both `evaljobid` and `id` (same value)
   - Status is set to "New" (0) by default
   - Output is initially empty
   - Returns: Success, Id, Message, StatusCode, Timestamp, **EvalJobId, AgentId, EnvironmentId, SchemaName, Status**

2. **Retrieve Eval Job (GetEvalRun)**:
   - Client provides: Id (Primary Name Column)
   - Plugin queries by `cr890_id` field
   - Returns: Success, EvalJob (JSON), Message, StatusCode, Timestamp, **EvalJobId, AgentId, EnvironmentId, SchemaName, Status, Input, Output**
   - Returns 404 if record not found

## Response Property Benefits

### PostEvalRun Additional Properties
- **EvalJobId**: Direct access to the primary key for database operations
- **AgentId, EnvironmentId, SchemaName**: Echo back input parameters for confirmation
- **Status**: Current status value for immediate client-side processing

### GetEvalRun Additional Properties  
- **Individual Field Access**: Direct access to each field without parsing the EvalJob JSON
- **Type Safety**: Proper data types (Integer for Status) instead of string parsing
- **Convenience**: Both structured (individual properties) and serialized (EvalJob JSON) formats available

## Error Handling

Both APIs implement comprehensive error handling:

- **400 Bad Request**: Invalid or missing required parameters
- **404 Not Found**: Record not found (GetEvalRun only)
- **500 Internal Server Error**: Unexpected server errors
- All errors include descriptive messages in the response

## Troubleshooting

### Common Issues

1. **"Plugin Type not found"**
   - Ensure plugin assembly is registered and plugin types are created
   - Verify plugin type names match exactly

2. **"Access Denied"**
   - Check security privileges are created and assigned to appropriate roles
   - Verify users have permissions on the `cr890_evaljob` table

3. **"Parameter validation failed"**
   - Ensure all required parameters are marked as "Is Optional = No"
   - Verify parameter names match the code exactly (case-sensitive)

4. **"Invalid JSON in Input"**
   - Validate JSON format in Input parameter
   - Ensure proper escaping of special characters

5. **"Missing Response Properties"**
   - Ensure all response properties are created in the Custom API definition
   - Verify sequence numbers are correct and unique

### Debugging

1. **Enable Plugin Trace Logs**: Go to **Settings** ? **System** ? **Administration** ? **System Settings**
2. **View Execution Logs**: Use Plugin Registration Tool to view plugin execution logs
3. **Performance Monitoring**: Monitor execution times and optimize queries

## Best Practices

1. **Error Handling**: Always implement comprehensive error handling
2. **Logging**: Use tracing service for debugging and monitoring
3. **Security**: Follow principle of least privilege
4. **Performance**: Optimize queries and minimize API calls
5. **Testing**: Thoroughly test in development environment before production deployment
6. **JSON Validation**: Validate JSON format for Input/Output fields
7. **GUID Format**: Ensure AgentId and EnvironmentId are valid GUIDs
8. **Response Properties**: Use individual response properties for type safety and convenience

## Technical Requirements

- **.NET Framework**: 4.6.2
- **C# Version**: 7.3
- **Dataverse**: Compatible with Power Platform environments (Elastic tables required)
- **Dependencies**: Microsoft.Xrm.Sdk

## Support

For issues related to:
- **Plugin functionality**: Check plugin trace logs
- **Custom API configuration**: Review Dataverse customization logs
- **Performance**: Monitor execution times and optimize queries
- **Security**: Review security roles and privileges
- **Elastic Table**: Ensure proper Elastic table configuration in Dataverse
- **Response Properties**: Verify all response properties are properly configured in Custom API definition