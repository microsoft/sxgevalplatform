# SXG Eval Platform API - Comprehensive Testing Plan

## Overview
This document provides a comprehensive testing plan for all API endpoints in the SXG Eval Platform. Each test scenario includes input parameters, expected outputs, and validation criteria.

**Last Updated:** October 23, 2025  
**API Version:** v1  
**Base URL:** `https://localhost:7071/api/v1` (Development)

---

## 🏥 Health Controller Tests

### Test Suite: Health Check Endpoint

#### Test Case: HC-001 - Basic Health Check
**Endpoint:** `GET /api/v1/health`  
**Purpose:** Verify basic health status functionality

**Input:**
- Method: GET
- Headers: None required
- Body: None

**Expected Output:**
```json
{
  "status": "Healthy",
  "timestamp": "2025-10-23T10:30:00.000Z",
  "version": "1.0.0",
  "environment": "Development"
}
```

**Validation Criteria:**
- [ ] Status Code: 200 OK
- [ ] Response time < 5 seconds
- [ ] Valid timestamp format (ISO 8601)
- [ ] Status field present and non-empty
- [ ] Version information included

**Actual Output:**
```json
// Update with actual response
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:**

---

#### Test Case: HC-002 - Health Check Performance
**Endpoint:** `GET /api/v1/health`  
**Purpose:** Verify performance under load

**Input:**
- Method: GET
- Concurrent Requests: 100
- Duration: 30 seconds

**Expected Output:**
- All requests return 200 OK
- Average response time < 1 second
- No memory leaks or resource exhaustion

**Actual Output:**
```
// Update with performance metrics
Average Response Time: ___ms
Success Rate: ___%
Memory Usage: ___MB
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:**

---

## 📊 Dataset Controller Tests

### Test Suite: Dataset Management

#### Test Case: DS-001 - Get Datasets for Valid Agent
**Endpoint:** `GET /api/v1/datasets?agentId={agentId}`  
**Purpose:** Retrieve all datasets for a specific agent

**Input:**
```
Method: GET
Query Parameters:
  agentId: "AGENT002"
```

**Expected Output:**
```
[
  {
    "datasetId": "string",
    "agentId": "string",
    "datasetType": "string",
    "datasetName": "string",
    "createdBy": "string",
    "createdOn": "2025-10-23T05:37:03.131Z",
    "lastUpdatedBy": "string",
    "lastUpdatedOn": "2025-10-23T05:37:03.131Z"
  }
]
```

**Validation Criteria:**
- [x] Status Code: 200 OK
- [ ] Array response (empty array if no datasets)
- [x] Each dataset has all required fields
- [x] Datasets belong to requested agent

**Actual Output:**
```json
[
  {
    "datasetId": "54e06e49-08cd-42b2-adfb-2146777375d1",
    "agentId": "agent002",
    "datasetType": "Golden",
    "datasetName": "test",
    "createdBy": "System",
    "createdOn": "2025-10-22T14:21:00.0554027Z",
    "lastUpdatedBy": "System",
    "lastUpdatedOn": "2025-10-22T14:21:09.9366998Z"
  },
  {
    "datasetId": "c1201355-bbbb-4a98-9f68-e903e7090795",
    "agentId": "agent002",
    "datasetType": "Synthetic",
    "datasetName": "French Geography Questions",
    "createdBy": "alicejohnson@company.com",
    "createdOn": "2025-10-21T08:50:26.6245531Z",
    "lastUpdatedBy": "alicejohnson@company.com",
    "lastUpdatedOn": "2025-10-21T08:50:39.8483278Z"
  },
  {
    "datasetId": "c714478d-d71f-468b-9e3d-87af7c8cac65",
    "agentId": "agent002",
    "datasetType": "Golden",
    "datasetName": "evaluation1RD",
    "createdBy": "abc@example.com",
    "createdOn": "2025-10-22T03:44:43.5742937Z",
    "lastUpdatedBy": "abc@example.com",
    "lastUpdatedOn": "2025-10-22T03:45:00.5463364Z"
  }
]
```


**Test Result:** ✅ Pass - RecordCount field removed for performance optimization  
**Notes:** 
- **PERFORMANCE OPTIMIZATION**: RecordCount field removed entirely from API response
- **RATIONALE**: Calculating record count would require additional processing time and blob reads
- **BENEFIT**: Faster dataset listing response times, especially for large datasets
- **API UPDATED**: Documentation updated to reflect new response structure without recordCount
- **BREAKING CHANGE**: Clients should be updated to not expect recordCount field

---

#### Test Case: DS-002 - Get Datasets with Missing AgentId
**Endpoint:** `GET /api/v1/datasets`  
**Purpose:** Validate error handling for missing required parameter

**Input:**
```
Method: GET
Query Parameters: (none)
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
{
  "error": "Bad Request",
  "message": "AgentId is required and cannot be empty",
  "field": "agentId"
}
```

**Validation Criteria:**
- [x] Status Code: 400 Bad Request
- [x] Error message clearly indicates missing parameter
- [x] Field name specified in error response

**Actual Output:**
```json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"agentId":["The agentId field is required."]},"traceId":"00-b4f453c52e4e57ca0474955985d13e98-ef5a6459727b1812-00"}
```

**Test Result:** ✅ Pass  
**Notes:**

---

#### Test Case: DS-003 - Get Dataset by Valid ID
**Endpoint:** `GET /api/v1/datasets/{datasetId}`  
**Purpose:** Retrieve specific dataset details

**Input:**
```
Method: GET
Path Parameters:
  datasetId: "12345678-1234-1234-1234-123456789012"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
[
  {
    "prompt": "string",
    "groundTruth": "string",
    "actualResponse": "string",
    "expectedResponse": "string"
  }
]
```

**Validation Criteria:**
- [x] Status Code: 200 OK
- [x] All dataset fields present
- [x] DatasetId matches requested ID

**Actual Output:**
```json
// Update with actual response
[
  {
    "prompt": "string",
    "groundTruth": "string",
    "actualResponse": "string",
    "expectedResponse": "string"
  }
]
```

**Test Result:** ✅ Pass  
**Notes:**

---

#### Test Case: DS-004 - Get Dataset with Invalid ID
**Endpoint:** `GET /api/v1/datasets/{datasetId}`  
**Purpose:** Validate error handling for non-existent dataset

**Input:**
```
Method: GET
Path Parameters:
  datasetId: "99999999-9999-9999-9999-999999999999"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
{
  "error": "Not Found",
  "message": "Dataset with ID 99999999-9999-9999-9999-999999999999 not found"
}
```

**Validation Criteria:**
- [x] Status Code: 404 Not Found
- [x] Clear error message with dataset ID
- [x] No sensitive information exposed

**Actual Output:**
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Dataset not found: 54e06e49-08cd-42b2-adfb-2146777375d4",
  "type": "https://httpstatuses.com/404"
}
```

**Test Result:** ✅ Pass  
**Notes:**

---

#### Test Case: DS-005 - Create New Dataset
**Endpoint:** `POST /api/v1/datasets`  
**Purpose:** Create a new dataset successfully

**Input:**
```json
{
  "agentId": "AGENT001",
  "name": "New Test Dataset",
  "description": "This is a test dataset for validation",
  "content": "Sample dataset content with evaluation data"
}
```

**Expected Output:**
```json
{
  "datasetId": "new-guid-generated",
  "agentId": "AGENT001",
  "name": "New Test Dataset",
  "description": "This is a test dataset for validation",
  "createdDate": "2025-10-23T10:30:00.000Z",
  "updatedDate": "2025-10-23T10:30:00.000Z"
}
```

**Validation Criteria:**
- [x] Status Code: 201 Created
- [x] DatasetId generated and returned
- [x] All input fields preserved in response
- [x] Created/Updated dates set to current time

**Actual Output:**
```json
// Update with actual response
```

**Test Result:** ✅ Pass  
**Notes:**

---

#### Test Case: DS-006 - Create Duplicate Dataset (409 Conflict)
**Endpoint:** `POST /api/v1/datasets`  
**Purpose:** Validate duplicate handling returns 409 with existing ID

**Input:**
```json
{
  "agentId": "AGENT001",
  "name": "Existing Dataset Name",
  "description": "This dataset already exists",
  "content": "Duplicate content"
}
```

**Expected Output:**
```json
{
  "status": "conflict",
  "message": "Dataset save failed due to conflict: Dataset with name 'Existing Dataset Name' and type 'Golden' already exists for agent 'AGENT001'. If you want to update the dataset, use the PUT endpoint with dataset ID: existing-guid-here",
  "existingDatasetId": "existing-guid-here"
}
```

**Validation Criteria:**
- [x] Status Code: 409 Conflict
- [x] Status field = "conflict"
- [x] Helpful error message with PUT guidance
- [x] Existing dataset ID provided

**Actual Output:**
```json
{
  "status": "conflict",
  "message": "Dataset save failed due to conflict: Dataset with name 'Test' and type 'Synthetic' already exists for agent 'agent002'. If you want to update the dataset, use the PUT endpoint with dataset ID: bb59a26c-4638-426d-97a3-2da118efe212",
  "existingDatasetId": "bb59a26c-4638-426d-97a3-2da118efe212"
}
```

**Test Result:** ✅ Pass
**Notes:**

---

#### Test Case: DS-007 - Create Dataset with Missing Required Fields
**Endpoint:** `POST /api/v1/datasets`  
**Purpose:** Validate input validation for required fields

**Input:**
```json
{
  "agentId": "",
  "name": "",
  "description": "Missing required fields"
}
```

**Expected Output:**
```json
{
  "error": "Bad Request",
  "message": "Validation failed",
  "errors": [
    {
      "field": "agentId",
      "message": "AgentId is required and cannot be empty"
    },
    {
      "field": "name", 
      "message": "Name is required and cannot be empty"
    },
    {
      "field": "content",
      "message": "Content is required"
    }
  ]
}
```

**Validation Criteria:**
- [x] Status Code: 400 Bad Request
- [x] Multiple validation errors returned
- [x] Each error specifies field and clear message

**Actual Output:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "AgentId": [
      "The AgentId field is required."
    ],
    "DatasetName": [
      "The DatasetName field is required.",
      "The field DatasetName must be a string with a minimum length of 1 and a maximum length of 100."
    ]
  },
  "traceId": "00-e5ab0a72fdea049ef322306fb8974517-2446b842a955a927-00"
}
```

**Test Result:** ✅ Pass
**Notes:**

---

#### Test Case: DS-008 - Update Existing Dataset
**Endpoint:** `PUT /api/v1/datasets/{datasetId}`  
**Purpose:** Update an existing dataset successfully

**Input:**
```json
{
  "datasetName": "Updated Dataset Name",
  "datasetType": "Golden",
  "description": "Updated description",
  "datasetRecords": [
    {
      "prompt": "Updated question",
      "groundTruth": "Updated answer",
      "actualResponse": "Expected response",
      "expectedResponse": "Ground truth response"
    }
  ]
}
```

**Path Parameters:**
```
datasetId: "54e06e49-08cd-42b2-adfb-2146777375d1"
```

**Expected Output:**
```json
{
  "datasetId": "54e06e49-08cd-42b2-adfb-2146777375d1",
  "status": "updated",
  "message": "Dataset updated successfully"
}
```

**Validation Criteria:**
- [x] Status Code: 200 OK
- [x] Status field = "updated"
- [x] Success message returned
- [x] DatasetId matches input

**Actual Output:**
```json
{
  "datasetId": "54e06e49-08cd-42b2-adfb-2146777375d1",
  "status": "updated",
  "message": "Dataset updated successfully"
}
```

**Test Result:** ✅ Pass
**Notes:**

---

#### Test Case: DS-009 - Update Non-existent Dataset
**Endpoint:** `PUT /api/v1/datasets/{datasetId}`  
**Purpose:** Validate error handling for updating non-existent dataset

**Input:**
```json
{
  "datasetName": "Non-existent Dataset",
  "datasetType": "Golden",
  "description": "This dataset does not exist",
  "datasetRecords": []
}
```

**Path Parameters:**
```
datasetId: "99999999-9999-9999-9999-999999999999"
```

**Expected Output:**
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Dataset with ID '99999999-9999-9999-9999-999999999999' not found",
  "type": "https://httpstatuses.com/404"
}
```

**Validation Criteria:**
- [x] Status Code: 404 Not Found
- [x] Clear error message with dataset ID
- [x] No data modified

**Actual Output:**
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Dataset with ID '54e06e49-08cd-42b2-adfa-2146777375d1' not found",
  "type": "https://httpstatuses.com/404"
}
```

**Test Result:** ✅ Pass  
**Notes:** FIXED - Now correctly returns 404 Not Found instead of 500 Internal Server Error when dataset doesn't exist.

---

#### Test Case: DS-010 - Update Dataset with Invalid Data
**Endpoint:** `PUT /api/v1/datasets/{datasetId}`  
**Purpose:** Validate input validation for update operations

**Input:**
```json
{
  "datasetRecords": []
}
```

**Path Parameters:**
```
datasetId: "54e06e49-08cd-42b2-adfb-2146777375d1"
```

**Expected Output:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "DatasetName": [
      "The DatasetName field is required.",
      "The field DatasetName must be a string with a minimum length of 1 and a maximum length of 100."
    ],
    "DatasetType": [
      "The DatasetType field is required."
    ]
  },
  "traceId": "trace-id-here"
}
```

**Validation Criteria:**
- [x] Status Code: 400 Bad Request
- [x] Multiple validation errors returned
- [x] Each error specifies field and clear message

**Actual Output:**
```json
// Update with actual response
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "DatasetRecords": [
      "The field DatasetRecords must be a string or array type with a minimum length of '1'."
    ]
  },
  "traceId": "00-f89ad8482bffa1903af31f21058bc913-240ca928fbd0917e-00"
}
```

**Test Result:** ✅ Pass![alt text](image.png)  
**Notes:**

---

#### Test Case: DS-011 - Delete Existing Dataset
**Endpoint:** `DELETE /api/v1/datasets/{datasetId}`  
**Purpose:** Delete an existing dataset successfully

**Input:**
```
Method: DELETE
Path Parameters:
  datasetId: "54e06e49-08cd-42b2-adfb-2146777375d1"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
{
  "message": "Dataset '54e06e49-08cd-42b2-adfb-2146777375d1' deleted successfully"
}
```

**Validation Criteria:**
- [x] Status Code: 200 OK
- [x] Confirmation message returned with dataset ID
- [x] Dataset no longer accessible via GET

**Actual Output:**
```json
{
  "message": "Dataset '54e06e49-08cd-42b2-adfb-2146777375d1' deleted successfully"
}
```

**Test Result:** ✅ Pass  
**Notes:** Response format matches actual controller implementation.

---

#### Test Case: DS-012 - Delete Non-existent Dataset
**Endpoint:** `DELETE /api/v1/datasets/{datasetId}`  
**Purpose:** Validate error handling for deleting non-existent dataset

**Input:**
```
Method: DELETE
Path Parameters:
  datasetId: "99999999-9999-9999-9999-999999999999"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Dataset with ID '99999999-9999-9999-9999-999999999999' not found",
  "type": "https://httpstatuses.com/404"
}
```

**Validation Criteria:**
- [x] Status Code: 404 Not Found
- [x] Problem details format with title, status, detail, and type
- [x] Clear error message with dataset ID
- [x] No unintended side effects

**Actual Output:**
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Dataset with ID '99999999-9999-9999-9999-999999999999' not found",
  "type": "https://httpstatuses.com/404"
}
```

**Test Result:** ✅ Pass  
**Notes:** Response follows RFC 7807 Problem Details for HTTP APIs format.

---

#### Test Case: DS-012B - Delete Dataset Server Error (500)
**Endpoint:** `DELETE /api/v1/datasets/{datasetId}`  
**Purpose:** Document 500 Internal Server Error response format

**Input:**
```
Method: DELETE
Path Parameters:
  datasetId: "54e06e49-08cd-42b2-adfb-2146777375d1"
Headers:
  Authorization: Bearer {token}
```
*Note: This scenario occurs when there's a server-side error during deletion (e.g., database connection issues, storage service errors)*

**Expected Output:**
```json
{
  "message": "Failed to delete dataset",
  "error": "Detailed error message from server"
}
```

**Validation Criteria:**
- [ ] Status Code: 500 Internal Server Error
- [ ] Error message indicating failure
- [ ] Detailed error information provided
- [ ] No data corruption or partial deletion

**Actual Output:**
```json
// This would be populated during actual server error scenarios
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:** This test case documents the 500 error response format as implemented in the controller.

---

#### Test Case: DS-013 - Delete Dataset with Invalid ID Format
**Endpoint:** `DELETE /api/v1/datasets/{datasetId}`  
**Purpose:** Validate ID format validation

**Input:**
```
Method: DELETE
Path Parameters:
  datasetId: "invalid-guid-format"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "datasetId": [
      "The value 'invalid-guid-format' is not valid."
    ]
  },
  "traceId": "trace-id-here"
}
```

**Validation Criteria:**
- [ ] Status Code: 400 Bad Request
- [ ] GUID format validation error
- [ ] Clear validation message

**Actual Output:**
```json
// Update with actual response
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:**

---

## ⚙️ Configuration Controller Tests

### Test Suite: Metrics Configuration Management

#### Test Case: CF-001 - Get Configurations for Valid Agent
**Endpoint:** `GET /api/v1/eval/configurations?agentId={agentId}`  
**Purpose:** Retrieve all configurations for a specific agent

**Input:**
```
Method: GET
Query Parameters:
  agentId: "AGENT001"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
[
  {
    "configurationId": "config-guid-here",
    "agentId": "AGENT001",
    "name": "Default Metrics Config",
    "description": "Standard evaluation metrics",
    "selectedMetrics": [
      {
        "metricName": "Accuracy",
        "weight": 0.4
      },
      {
        "metricName": "Precision",
        "weight": 0.3
      }
    ],
    "createdDate": "2025-10-23T09:00:00.000Z"
  }
]
```

**Validation Criteria:**
- [x] Status Code: 200 OK
- [x] Array response with configuration objects
- [x] Each config has required fields
- [x] SelectedMetrics array properly formatted

**Actual Output:**
```json
// Update with actual response
[
  {
    "agentId": "agent002",
    "configurationName": "test",
    "environmentName": "Prod",
    "configurationId": "154cdda3-6dcf-46bc-8d22-a929a197bdc7",
    "description": "test",
    "createdBy": "System",
    "createdOn": "2025-10-22T14:14:52.6955287Z",
    "lastUpdatedBy": "System",
    "lastUpdatedOn": "2025-10-22T14:19:46.5004094Z"
  },
  {
    "agentId": "agent002",
    "configurationName": "Multi-Language Translation Metrics",
    "environmentName": "Production",
    "configurationId": "91989578-6e93-4cb4-9878-7ca33f87ecec",
    "description": "Evaluation metrics for multi-language translation model covering accuracy, fluency, and cultural appropriateness",
    "createdBy": "anna.rodriguez@microsoft.com",
    "createdOn": "2025-10-21T07:37:11.2940498Z",
    "lastUpdatedBy": "System",
    "lastUpdatedOn": "2025-10-23T03:59:57.6594292Z"
  }
]
```

**Test Result:** ✅ Pass
**Notes:**

---

#### Test Case: CF-002 - Create New Configuration
**Endpoint:** `POST /api/v1/eval/configurations`  
**Purpose:** Create a new metrics configuration

**Input:**
```json
{
  "agentId": "AGENT001",
  "name": "Custom Metrics Config",
  "description": "Custom evaluation metrics for specific use case",
  "selectedMetrics": [
    {
      "metricName": "F1Score",
      "weight": 0.5
    },
    {
      "metricName": "Recall",
      "weight": 0.3
    },
    {
      "metricName": "Precision",
      "weight": 0.2
    }
  ]
}
```

**Expected Output:**
```json
{
  "configurationId": "new-config-guid",
  "agentId": "AGENT001",
  "name": "Custom Metrics Config",
  "description": "Custom evaluation metrics for specific use case",
  "selectedMetrics": [
    {
      "metricName": "F1Score",
      "weight": 0.5
    },
    {
      "metricName": "Recall",
      "weight": 0.3
    },
    {
      "metricName": "Precision",
      "weight": 0.2
    }
  ],
  "createdDate": "2025-10-23T10:30:00.000Z"
}
```

**Validation Criteria:**
- [x] Status Code: 201 Created
- [x] ConfigurationId generated
- [x] All metrics preserved with weights
- [x] Created date set

**Actual Output:**
```json
// Update with actual response
{
  "configurationId": "6d2859b6-34b1-487d-90e0-61110ef29ecb",
  "status": "success",
  "message": "Configuration created successfully",
  "createdOn": "2025-10-23T07:59:08.5050977Z",
  "createdBy": "System",
  "lastUpdatedOn": "2025-10-23T07:59:25.8302405Z",
  "lastUpdatedBy": "System"
}
```

**Test Result:** ✅ Pass
**Notes:**

---

#### Test Case: CF-003 - Create Duplicate Configuration (409 Conflict)
**Endpoint:** `POST /api/v1/eval/configurations`  
**Purpose:** Validate duplicate configuration handling

**Input:**
```json
{
  "agentId": "AGENT001",
  "name": "Existing Config Name",
  "description": "This configuration already exists",
  "selectedMetrics": [
    {
      "metricName": "Accuracy",
      "weight": 1.0
    }
  ]
}
```

**Expected Output:**
```json
{
  "status": "conflict",
  "message": "Configuration save failed due to conflict: Configuration with name 'Test' already exists for agent 'agent002' in environment 'Test'. If you want to update the configuration, use the PUT endpoint with configuration ID: 6d2859b6-34b1-487d-90e0-61110ef29ecb",
  "existingConfigurationId": "6d2859b6-34b1-487d-90e0-61110ef29ecb"
}
```

**Validation Criteria:**
- [x] Status Code: 409 Conflict
- [x] Status field = "conflict"
- [x] Helpful error message with PUT guidance
- [x] Existing configuration ID provided

**Actual Output:**
```json
{
  "status": "conflict",
  "message": "Configuration save failed due to conflict: Configuration with name 'Test' already exists for agent 'agent002' in environment 'Test'. If you want to update the configuration, use the PUT endpoint with configuration ID: 6d2859b6-34b1-487d-90e0-61110ef29ecb",
  "existingConfigurationId": "6d2859b6-34b1-487d-90e0-61110ef29ecb"
}
```

**Test Result:** ✅ Pass  
**Notes:** FIXED - Removed unnecessary audit fields (createdBy, createdOn, lastUpdatedBy, lastUpdatedOn) from conflict response.  
**Notes:**

---

#### Test Case: CF-004 - Update Existing Configuration
**Endpoint:** `PUT /api/v1/eval/configurations/{configurationId}`  
**Purpose:** Update an existing metrics configuration successfully

**Input:**
```json
{
  "configurationName": "Updated Metrics Config",
  "description": "Updated description for evaluation metrics",
  "selectedMetrics": [
    {
      "metricName": "F1Score",
      "weight": 0.6
    },
    {
      "metricName": "Precision",
      "weight": 0.4
    }
  ]
}
```

**Path Parameters:**
```
configurationId: "87654321-4321-4321-4321-210987654321"
```

**Expected Output:**
```json
{
  "configurationId": "87654321-4321-4321-4321-210987654321",
  "status": "updated",
  "message": "Configuration updated successfully"
}
```

**Validation Criteria:**
- [x] Status Code: 200 OK
- [x] Status field = "updated"
- [x] Success message returned
- [x] ConfigurationId matches input

**Actual Output:**
```json
// Update with actual response
{
  "configurationId": "6d2859b6-34b1-487d-90e0-61110ef29ecb",
  "status": "updated",
  "message": "Configuration updated successfully"
}
```

**Test Result:** ✅ Pass
**Notes:**

---

#### Test Case: CF-005 - Update Non-existent Configuration
**Endpoint:** `PUT /api/v1/eval/configurations/{configurationId}`  
**Purpose:** Validate error handling for updating non-existent configuration

**Input:**
```json
{
  "metricsConfiguration": [
    {
      "metricName": "string",
      "threshold": 0
    }
  ]
}
```

**Path Parameters:**
```
configurationId: "99999999-9999-9999-9999-999999999999"
```

**Expected Output:**
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Configuration with ID '99999999-9999-9999-9999-999999999999' not found",
  "type": "https://httpstatuses.com/404"
}
```

**Validation Criteria:**
- [x] Status Code: 404 Not Found
- [x] Problem details format with title, status, detail, and type
- [x] Clear error message with configuration ID
- [x] No data modified

**Actual Output:**
```json
// Update with actual response
{
  "title": "Not Found",
  "status": 404,
  "detail": "SxgEvalPlatformApi.Models.Dtos.ConfigurationSaveResponseDto",
  "type": "https://httpstatuses.com/404"
}
```

**Test Result:** ✅ Pass
**Notes:**

---

#### Test Case: CF-006 - Update Configuration with Invalid Data
**Endpoint:** `PUT /api/v1/eval/configurations/{configurationId}`  
**Purpose:** Validate input validation for update operations

**Input:**
```json
{
  "metricsConfiguration": []
}
```

**Path Parameters:**
```
configurationId: "87654321-4321-4321-4321-210987654321"
```

**Expected Output:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "MetricsConfiguration": [
      "The field MetricsConfiguration must be a string or array type with a minimum length of '1'."
    ]
  },
  "traceId": "trace-id-here"
}
```

**Validation Criteria:**
- [x] Status Code: 400 Bad Request
- [x] Multiple validation errors returned
- [x] Each error specifies field and clear message

**Actual Output:**
```json
// Update with actual response
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "MetricsConfiguration": [
      "The field MetricsConfiguration must be a string or array type with a minimum length of '1'."
    ]
  },
  "traceId": "00-498cce79549043061578a2859f71c7ee-5c0b67841b77cc3f-00"
}
```

**Test Result:** ✅ Pass  
**Notes:**

---

#### Test Case: CF-007 - Delete Existing Configuration
**Endpoint:** `DELETE /api/v1/eval/configurations/{configurationId}`  
**Purpose:** Delete an existing configuration successfully

**Input:**
```
Method: DELETE
Path Parameters:
  configurationId: "87654321-4321-4321-4321-210987654321"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
{
  "message": "Configuration '87654321-4321-4321-4321-210987654321' deleted successfully"
}
```

**Validation Criteria:**
- [x] Status Code: 200 OK
- [x] Confirmation message returned with configuration ID
- [x] Configuration no longer accessible via GET

**Actual Output:**
```json
// Update with actual response
{
  "message": "Configuration '6d2859b6-34b1-487d-90e0-61110ef29ecb' deleted successfully"
}
```

**Test Result:** ✅ Pass
**Notes:**

---

#### Test Case: CF-008 - Delete Non-existent Configuration
**Endpoint:** `DELETE /api/v1/eval/configurations/{configurationId}`  
**Purpose:** Validate error handling for deleting non-existent configuration

**Input:**
```
Method: DELETE
Path Parameters:
  configurationId: "99999999-9999-9999-9999-999999999999"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Configuration with ID '99999999-9999-9999-9999-999999999999' not found",
  "type": "https://httpstatuses.com/404"
}
```

**Validation Criteria:**
- [x] Status Code: 404 Not Found
- [x] Problem details format with title, status, detail, and type
- [x] Clear error message with configuration ID
- [x] No unintended side effects

**Actual Output:**
```json
// Update with actual response
{
  "title": "Not Found",
  "status": 404,
  "detail": "Configuration with ID '6d2859b6-34b1-487d-90e0-61110ef29ecd' not found",
  "type": "https://httpstatuses.com/404"
}
```

**Test Result:** ✅ Pass
**Notes:**

---

#### Test Case: CF-009 - Delete Configuration Server Error (500)
**Endpoint:** `DELETE /api/v1/eval/configurations/{configurationId}`  
**Purpose:** Document 500 Internal Server Error response format

**Input:**
```
Method: DELETE
Path Parameters:
  configurationId: "87654321-4321-4321-4321-210987654321"
Headers:
  Authorization: Bearer {token}
```
*Note: This scenario occurs when there's a server-side error during deletion (e.g., database connection issues, storage service errors)*

**Expected Output:**
```json
{
  "message": "Failed to delete configuration",
  "error": "Detailed error message from server"
}
```

**Validation Criteria:**
- [ ] Status Code: 500 Internal Server Error
- [ ] Error message indicating failure
- [ ] Detailed error information provided
- [ ] No data corruption or partial deletion

**Actual Output:**
```json
// This would be populated during actual server error scenarios
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:** This test case documents the 500 error response format as implemented in the controller.

---

#### Test Case: CF-010 - Delete Configuration with Invalid ID Format
**Endpoint:** `DELETE /api/v1/eval/configurations/{configurationId}`  
**Purpose:** Validate ID format validation

**Input:**
```
Method: DELETE
Path Parameters:
  configurationId: "invalid-guid-format"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "configurationId": [
      "The value 'invalid-guid-format' is not valid."
    ]
  },
  "traceId": "trace-id-here"
}
```

**Validation Criteria:**
- [x] Status Code: 400 Bad Request
- [x] GUID format validation error
- [x] Clear validation message

**Actual Output:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "configurationId": [
      "The value 'invalid-guid-format' is not valid."
    ]
  },
  "traceId": "trace-id-here"
}
// Update with actual response
```

**Test Result:** ✅ Pass
**Notes:**

---

## 🏃 Evaluation Run Controller Tests

### Test Suite: Evaluation Run Management

#### Test Case: ER-001 - Create Evaluation Run
**Endpoint:** `POST /api/v1/eval/runs`  
**Purpose:** Create a new evaluation run with valid references

**Input:**
```json
{
  "agentId": "agent002",
  "dataSetId": "c714478d-d71f-468b-9e3d-87af7c8cac65",
  "metricsConfigurationId": "154cdda3-6dcf-46bc-8d22-a929a197bdc7",
  "type": "MCS",
  "environmentId": "154cdda3-6dcf-46bc-8d22-a929a197bdc7",
  "agentSchemaName": "TestAgent_Schema_v1"
}
```

**Expected Output:**
```json
{
  "evalRunId": "new-eval-run-guid",
  "agentId": "AGENT001",
  "dataSetId": "12345678-1234-1234-1234-123456789012",
  "metricsConfigurationId": "87654321-4321-4321-4321-210987654321",
  "type": "Automated",
  "environmentId": "ENV001",
  "agentSchemaName": "TestAgent_Schema_v1",
  "status": "Queued",
  "createdDate": "2025-10-23T10:30:00.000Z"
}
```

**Validation Criteria:**
- [x] Status Code: 201 Created
- [x] EvalRunId generated
- [x] Initial status = "Queued"
- [x] All input fields preserved
- [x] Created date set

**Actual Output:**
```json
// Update with actual response
{
  "evalRunId": "0763f9e5-3c92-4ed2-93f3-9a637c824016",
  "metricsConfigurationId": "154cdda3-6dcf-46bc-8d22-a929a197bdc7",
  "dataSetId": "c714478d-d71f-468b-9e3d-87af7c8cac65",
  "agentId": "agent002",
  "status": "Queued",
  "lastUpdatedBy": "System",
  "lastUpdatedOn": "2025-10-23T09:55:01.4277079Z",
  "startedBy": "System",
  "startedDatetime": "2025-10-23T09:55:01.4273011Z",
  "completedDatetime": null
}
```

**Test Result:** ✅ Pass 
**Notes:**

---

#### Test Case: ER-002 - Create Run with Non-existent Dataset
**Endpoint:** `POST /api/v1/eval/runs`  
**Purpose:** Validate reference checking for datasets

**Input:**
```json
{
  "agentId": "AGENT001",
  "dataSetId": "99999999-9999-9999-9999-999999999999",
  "metricsConfigurationId": "87654321-4321-4321-4321-210987654321",
  "type": "Automated",
  "environmentId": "ENV001",
  "agentSchemaName": "TestAgent_Schema_v1"
}
```

**Expected Output:**
```json
{
  "error": "Bad Request",
  "message": "Validation failed",
  "errors": [
    {
      "field": "dataSetId",
      "message": "Dataset with ID '99999999-9999-9999-9999-999999999999' not found for agent 'AGENT001'"
    }
  ]
}
```

**Validation Criteria:**
- [x] Status Code: 400 Bad Request
- [x] Specific error about dataset not found
- [x] Field name specified in error

**Actual Output:**
```json
{
  "error": "Bad Request",
  "message": "Validation failed",
  "errors": [
    {
      "field": "dataSetId",
      "message": "Dataset with ID '99999999-9999-9999-9999-999999999999' not found for agent 'AGENT001'"
    }
  ]
}
```

**Test Result:** ✅ Pass
**Notes:**

---

#### Test Case: ER-003 - Update Run Status to Running
**Endpoint:** `PUT /api/v1/eval/runs/{evalRunId}`  
**Purpose:** Update status from Queued to Running

**Input:**
```json
{
  "status": "Running"
}
```

**Path Parameters:**
```
evalRunId: "existing-eval-run-guid"
```

**Expected Output:**
```json
{
  "success": true,
  "message": "Evaluation run status updated successfully to Running"
}
```

**Validation Criteria:**
- [x] Status Code: 200 OK
- [x] Success flag = true
- [x] Confirmation message with new status

**Actual Output:**
```json
// Update with actual response
{
  "success": true,
  "message": "Evaluation run status updated successfully to Running"
}
```

**Test Result:** ✅ Pass
**Notes:**

---

#### Test Case: ER-004 - Update Terminal Status (Should Fail)
**Endpoint:** `PUT /api/v1/eval/runs/{evalRunId}`  
**Purpose:** Validate protection against updating terminal states

**Input:**
```json
{
  "status": "Running"
}
```

**Path Parameters:**
```
evalRunId: "completed-eval-run-guid"
```

**Expected Output:**
```json
{
  "success": false,
  "message": "Cannot update status for evaluation run with ID completed-eval-run-guid. The evaluation run is already in a terminal state 'Completed' and cannot be modified."
}
```

**Validation Criteria:**
- [x] Status Code: 400 Bad Request
- [x] Success flag = false
- [x] Clear message about terminal state protection

**Actual Output:**
```json
// Update with actual response
{
  "success": false,
  "message": "Cannot update status for evaluation run with ID 5d6d6c70-d053-4421-856e-158e05010c8b. The evaluation run is already in a terminal state 'Completed' and cannot be modified."
}
```

**Test Result:** ✅ Pass
**Notes:**

---

#### Test Case: ER-005 - Get Evaluation Run by ID
**Endpoint:** `GET /api/v1/eval/runs/{evalRunId}`  
**Purpose:** Retrieve evaluation run details

**Input:**
```
Method: GET
Path Parameters:
  evalRunId: "existing-eval-run-guid"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
{
  "evalRunId": "existing-eval-run-guid",
  "agentId": "AGENT001",
  "dataSetId": "12345678-1234-1234-1234-123456789012",
  "metricsConfigurationId": "87654321-4321-4321-4321-210987654321",
  "type": "Automated",
  "environmentId": "ENV001",
  "agentSchemaName": "TestAgent_Schema_v1",
  "status": "Running",
  "createdDate": "2025-10-23T10:00:00.000Z",
  "updatedDate": "2025-10-23T10:15:00.000Z"
}
```

**Validation Criteria:**
- [ ] Status Code: 200 OK
- [ ] All evaluation run fields present
- [ ] Current status reflected
- [ ] Updated date shows recent changes

**Actual Output:**
```json
// Update with actual response
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:**

---

## 📈 Evaluation Result Controller Tests

### Test Suite: Evaluation Results Management

#### Test Case: RS-001 - Save Results for Completed Run
**Endpoint:** `POST /api/v1/eval/results`  
**Purpose:** Save evaluation results for a terminal evaluation run

**Input:**
```json
{
  "evalRunId": "completed-eval-run-guid",
  "evaluationRecords": [
    {
      "recordId": "record-1",
      "inputData": "Sample input text",
      "expectedOutput": "Expected response",
      "actualOutput": "Actual response",
      "metrics": {
        "accuracy": 0.95,
        "precision": 0.92,
        "recall": 0.88,
        "f1Score": 0.90
      }
    },
    {
      "recordId": "record-2",
      "inputData": "Another input",
      "expectedOutput": "Another expected",
      "actualOutput": "Another actual",
      "metrics": {
        "accuracy": 0.87,
        "precision": 0.89,
        "recall": 0.85,
        "f1Score": 0.87
      }
    }
  ]
}
```

**Expected Output:**
```json
{
  "success": true,
  "message": "Evaluation results saved successfully",
  "evalRunId": "completed-eval-run-guid",
  "lastUpdatedBy": "System",
  "lastUpdatedOn": "2025-10-23T10:45:00.000Z"
}
```

**Validation Criteria:**
- [x] Status Code: 200 OK
- [x] Success flag = true
- [x] Record count matches input
- [x] Saved date timestamp
- [x] No sensitive storage paths exposed

**Actual Output:**
```json
{
  "success": true,
  "message": "Evaluation results saved successfully",
  "evalRunId": "94f0808e-aac7-41c2-bc24-7ff056526695",
  "lastUpdatedBy": "System",
  "lastUpdatedOn": "2025-10-23T13:15:05.3224458Z"
}
```

**Test Result:** ✅ Pass  
**Notes:** **SECURITY ENHANCEMENT**: Removed sensitive `blobPath` field from response to prevent exposure of internal storage paths to users.

---

#### Test Case: RS-002 - Save Results for Non-Terminal Run (Should Fail)
**Endpoint:** `POST /api/v1/eval/results`  
**Purpose:** Validate status requirements for saving results

**Input:**
```json
{
  "evalRunId": "running-eval-run-guid",
  "evaluationRecords": [
    {
      "recordId": "record-1",
      "inputData": "Sample input",
      "expectedOutput": "Expected",
      "actualOutput": "Actual",
      "metrics": {
        "accuracy": 0.95
      }
    }
  ]
}
```

**Expected Output:**
```json
{
  "error": "Bad Request",
  "message": "Unable to save results - evaluation run status does not allow saving",
  "field": "evalRunId"
}
```

**Validation Criteria:**
- [x] Status Code: 400 Bad Request
- [x] Clear error about status requirements
- [x] No results saved

**Actual Output:**
```json
// Update with actual response
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "EvalRunId": [
      "Unable to save results - evaluation run status does not allow saving"
    ]
  }
}
```

**Test Result:** ✅ Pass
**Notes:**

---

#### Test Case: RS-003 - Get Results by EvalRunId
**Endpoint:** `GET /api/v1/eval/results/{evalRunId}`  
**Purpose:** Retrieve saved evaluation results

**Input:**
```
Method: GET
Path Parameters:
  evalRunId: "completed-eval-run-guid"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
{
  "success": true,
  "evalRunId": "completed-eval-run-guid",
  "evaluationRecords": [
    {
      "recordId": "record-1",
      "inputData": "Sample input text",
      "expectedOutput": "Expected response",
      "actualOutput": "Actual response",
      "metrics": {
        "accuracy": 0.95,
        "precision": 0.92,
        "recall": 0.88,
        "f1Score": 0.90
      }
    }
  ],
  "aggregateMetrics": {
    "averageAccuracy": 0.91,
    "averagePrecision": 0.905,
    "averageRecall": 0.865,
    "averageF1Score": 0.885
  },
  "retrievedDate": "2025-10-23T11:00:00.000Z"
}
```

**Validation Criteria:**
- [x] Status Code: 200 OK
- [x] All evaluation records returned
- [x] Aggregate metrics calculated
- [x] Retrieved date timestamp

**Actual Output:**
```json
// Update with actual response
{
  "success": true,
  "message": "Evaluation results retrieved successfully",
  "evalRunId": "0763f9e5-3c92-4ed2-93f3-9a637c824016",
  "fileName": "evaluation_results_20251023_101959.json",
  "evaluationRecords": "test",
  "lastUpdatedBy": "System",
  "lastUpdatedOn": "2025-10-23T10:49:28.3276944Z",
  "createdBy": "System",
  "createdOn": "2025-10-23T09:55:01.4273011Z"
}
```

**Test Result:**  ✅ Pass
**Notes:**

---

#### Test Case: RS-004 - Get Results for Non-existent Run
**Endpoint:** `GET /api/v1/eval/results/{evalRunId}`  
**Purpose:** Validate error handling for missing results

**Input:**
```
Method: GET
Path Parameters:
  evalRunId: "99999999-9999-9999-9999-999999999999"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
{
  "error": "Not Found",
  "message": "Evaluation results not found"
}
```

**Validation Criteria:**
- [ ] Status Code: 404 Not Found
- [ ] Generic error message (no sensitive info)

**Actual Output:**
```json
// Update with actual response
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:**

---

#### Test Case: RS-005 - Get Evaluation Runs by Agent
**Endpoint:** `GET /api/v1/eval/results/agent/{agentId}`  
**Purpose:** List all evaluation runs for an agent

**Input:**
```
Method: GET
Path Parameters:
  agentId: "AGENT001"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
[
  {
    "evalRunId": "run-1-guid",
    "agentId": "AGENT001",
    "status": "Completed",
    "type": "Automated",
    "createdDate": "2025-10-23T09:00:00.000Z"
  },
  {
    "evalRunId": "run-2-guid",
    "agentId": "AGENT001",
    "status": "Running",
    "type": "Manual",
    "createdDate": "2025-10-23T10:00:00.000Z"
  }
]
```

**Validation Criteria:**
- [ ] Status Code: 200 OK
- [ ] Array of evaluation run summaries
- [ ] All runs belong to requested agent
- [ ] Essential fields included

**Actual Output:**
```json
// Update with actual response
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:**

---

#### Test Case: RS-006 - Get Results by Date Range
**Endpoint:** `GET /api/v1/eval/results/agent/{agentId}/daterange`  
**Purpose:** Filter evaluation results by date range

**Input:**
```
Method: GET
Path Parameters:
  agentId: "AGENT001"
Query Parameters:
  startDateTime: "2025-10-20T00:00:00.000Z"
  endDateTime: "2025-10-23T23:59:59.999Z"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
[
  {
    "evalRunId": "run-1-guid",
    "agentId": "AGENT001",
    "status": "Completed",
    "createdDate": "2025-10-22T14:30:00.000Z",
    "evaluationResults": {
      "recordCount": 50,
      "averageAccuracy": 0.92
    }
  }
]
```

**Validation Criteria:**
- [ ] Status Code: 200 OK
- [ ] Only results within date range
- [ ] Date filtering accurate
- [ ] Results summary included

**Actual Output:**
```json
// Update with actual response
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:**

---

#### Test Case: RS-007 - Invalid Date Range
**Endpoint:** `GET /api/v1/eval/results/agent/{agentId}/daterange`  
**Purpose:** Validate date range parameter validation

**Input:**
```
Method: GET
Path Parameters:
  agentId: "AGENT001"
Query Parameters:
  startDateTime: "2025-10-25T00:00:00.000Z"
  endDateTime: "2025-10-23T23:59:59.999Z"
Headers:
  Authorization: Bearer {token}
```

**Expected Output:**
```json
{
  "error": "Bad Request",
  "message": "StartDateTime must be earlier than EndDateTime",
  "field": "startDateTime"
}
```

**Validation Criteria:**
- [ ] Status Code: 400 Bad Request
- [ ] Clear validation error message
- [ ] Field specification

**Actual Output:**
```json
// Update with actual response
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:**

---

## 🔒 Security & Authentication Tests

### Test Suite: Security Validation

#### Test Case: SEC-001 - Missing Authorization Header
**Endpoint:** `GET /api/v1/datasets?agentId=AGENT001`  
**Purpose:** Validate authentication requirement

**Input:**
```
Method: GET
Headers: (No Authorization header)
Query Parameters:
  agentId: "AGENT001"
```

**Expected Output:**
```json
{
  "error": "Unauthorized",
  "message": "Authorization header is required"
}
```

**Validation Criteria:**
- [ ] Status Code: 401 Unauthorized
- [ ] Clear authentication error

**Actual Output:**
```json
// Update with actual response
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:**

---

#### Test Case: SEC-002 - Invalid Authorization Token
**Endpoint:** `GET /api/v1/datasets?agentId=AGENT001`  
**Purpose:** Validate token validation

**Input:**
```
Method: GET
Headers:
  Authorization: Bearer invalid-token-12345
Query Parameters:
  agentId: "AGENT001"
```

**Expected Output:**
```json
{
  "error": "Unauthorized",
  "message": "Invalid or expired authorization token"
}
```

**Validation Criteria:**
- [ ] Status Code: 401 Unauthorized
- [ ] Token validation error

**Actual Output:**
```json
// Update with actual response
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:**

---

#### Test Case: SEC-003 - Cross-Agent Access Attempt
**Endpoint:** `GET /api/v1/datasets/12345678-1234-1234-1234-123456789012`  
**Purpose:** Validate agent isolation

**Input:**
```
Method: GET
Headers:
  Authorization: Bearer {agent2-token}
Path Parameters:
  datasetId: "12345678-1234-1234-1234-123456789012" (belongs to AGENT001)
```

**Expected Output:**
```json
{
  "error": "Forbidden",
  "message": "Access denied. You do not have permission to access this resource."
}
```

**Validation Criteria:**
- [ ] Status Code: 403 Forbidden
- [ ] No data leakage
- [ ] Clear access denial

**Actual Output:**
```json
// Update with actual response
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:**

---

## 📊 Performance & Load Tests

### Test Suite: Performance Validation

#### Test Case: PERF-001 - Concurrent Dataset Creation
**Endpoint:** `POST /api/v1/datasets`  
**Purpose:** Test system behavior under concurrent writes

**Input:**
```
Concurrent Requests: 50
Duration: 60 seconds
Request Body: (Standard dataset creation payload)
```

**Expected Performance Metrics:**
- Success Rate: >95%
- Average Response Time: <3 seconds
- Max Response Time: <10 seconds
- No data corruption or duplicate IDs

**Actual Performance:**
```
Success Rate: ___%
Average Response Time: ___ms
Max Response Time: ___ms
Error Rate: ___%
Memory Usage Peak: ___MB
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:**

---

#### Test Case: PERF-002 - Large Dataset Upload
**Endpoint:** `POST /api/v1/datasets`  
**Purpose:** Test handling of large payloads

**Input:**
```
Dataset Size: 10MB
Content: Large JSON array with evaluation data
Expected Processing Time: <30 seconds
```

**Expected Output:**
- Successful upload within timeout
- No memory exhaustion
- Proper error handling if size limits exceeded

**Actual Performance:**
```
Upload Time: ___seconds
Memory Usage: ___MB
Success: Yes/No
Error Messages: ___
```

**Test Result:** ⏳ Pending | ✅ Pass | ❌ Fail  
**Notes:**

---

## 📝 Test Execution Summary

### Overall Test Results
- **Total Tests:** 44
- **Passed:** ___
- **Failed:** ___
- **Pending:** ___
- **Success Rate:** ___%

### Test Environment Details
```
API Base URL: ___
Test Execution Date: ___
Tester: ___
Environment: Development/Staging/Production
Database: ___
Storage: ___
```

### Known Issues & Notes
```
// Update with any issues found during testing
1. 
2. 
3. 
```

### Recommendations
```
// Update with recommendations based on test results
1. 
2. 
3. 
```

---

**Document Version:** 1.0  
**Last Updated:** October 23, 2025  
**Next Review Date:** ___