# DataSet Save Operation Refactoring Summary

## Overview
Refactored the `SaveDatasetAsync` method in `DataSetRequestHandler` and the `SaveDataset` controller method in `EvalDatasetsController` to implement proper upsert behavior (create or update based on business key).

## Key Changes

### 1. DataSetRequestHandler - Refactored Methods

#### `SaveDatasetAsync(SaveDatasetDto saveDatasetDto)` - UPSERT Behavior
**Before**:
- Would return an error if a dataset already existed with the same AgentId, DatasetName, and DatasetType
- Had inline logic that was calling `UpdateDatasetAsync` but wasn't working correctly

**After**:
- **Upsert Logic**: Checks if dataset exists with same `AgentId + DatasetName + DatasetType`
  - If EXISTS: Updates the existing dataset (blob and table)
  - If NOT EXISTS: Creates a new dataset
- **Clean Delegation**: Uses private helper methods for better code organization
- **Returns Proper Status**: 
  - Status "created" when new dataset is created
  - Status "updated" when existing dataset is updated
  - Status "error" on failure

#### New Private Helper Methods

1. **`FindExistingDatasetAsync(agentId, datasetName, datasetType)`**
   - Finds existing dataset by business key (AgentId + DatasetName + DatasetType)
   - Returns `DataSetTableEntity?` or null if not found

2. **`CreateNewDatasetAsync(SaveDatasetDto saveDatasetDto)`**
   - Handles complete new dataset creation flow
   - Creates entity, blob paths, saves to storage, updates caches
   - Returns success response with status "created"

3. **`UpdateExistingDatasetAsync(DataSetTableEntity existingEntity, List<EvalDataset> datasetRecords)`**
   - Handles complete dataset update flow
   - Updates blob storage and table metadata
   - Updates caches
   - Returns success response with status "updated"

4. **`CreateBlobPaths(SaveDatasetDto saveDatasetDto, string datasetId)`**
   - Centralizes blob container and file path creation logic
   - Returns tuple (container, filePath)

5. **`SerializeDatasetRecords(List<EvalDataset> datasetRecords)`**
   - Centralizes JSON serialization with consistent formatting
   - Uses camelCase naming policy

6. **`UpdateCachesAfterSave(...)`**
   - Centralizes cache update logic after save operations
   - Updates content cache, metadata cache
   - Invalidates agent-level list cache

7. **`RemoveDatasetFromCacheAsync(datasetId, agentId)`**
   - Removes dataset from all relevant caches
   - Used during delete operations

8. **`TryDeleteBlobAsync(containerName, blobFilePath, datasetId)`**
   - Safely attempts to delete blob file with error handling
   - Non-critical failure (logs warning but doesn't throw)

9. **`InvalidateAgentDatasetCaches(agentId)`**
   - Invalidates agent-level dataset list cache
   - Used after create, update, or delete operations

10. **`CreateSuccessResponse(savedEntity, status, message)`**
    - Creates consistent success response DTOs
    - Populates all metadata fields

11. **`CreateErrorResponse(datasetId, errorMessage)`**
    - Creates consistent error response DTOs

### 2. Cache Duration Constants

Added constants for better maintainability:
```csharp
private static readonly TimeSpan DatasetContentCacheDuration = TimeSpan.FromHours(2);
private static readonly TimeSpan DatasetMetadataCacheDuration = TimeSpan.FromHours(2);
private static readonly TimeSpan DatasetListCacheDuration = TimeSpan.FromMinutes(30);
```

### 3. EvalDatasetsController - Already Optimized

The controller was already well-structured to handle both create and update scenarios:
- Checks `result.Status` to determine response
- Returns `201 Created` for new datasets
- Returns `200 OK` for updated datasets
- No changes needed!

## Behavior Changes

### POST `/api/v1/eval/datasets` - SaveDataset

**Before**:
- Would fail with error if dataset already exists

**After**:
- **Creates** new dataset if no existing dataset found with same AgentId + DatasetName + DatasetType
- **Updates** existing dataset if found with same AgentId + DatasetName + DatasetType
- Returns appropriate status code:
  - `201 Created` - New dataset created
  - `200 OK` - Existing dataset updated
  - `400 Bad Request` - Validation error
  - `500 Internal Server Error` - Server error

### Business Key for Upsert
The unique combination that determines if a dataset already exists:
- **AgentId** + **DatasetName** + **DatasetType**

If all three match an existing dataset, it will be updated. Otherwise, a new dataset is created.

## Benefits

### 1. Single Responsibility Principle
- Each method has one clear purpose
- Helper methods are focused and reusable
- Better separation of concerns

### 2. Improved Code Reusability
- `UpdateExistingDatasetAsync` is used by both:
  - `SaveDatasetAsync` (for upsert updates)
  - `UpdateDatasetAsync` (for explicit updates by ID)
- Cache management methods are centralized

### 3. Better Error Handling
- Consistent error responses
- Non-critical blob deletion failures don't break the flow
- Clear logging at each step

### 4. Enhanced Maintainability
- Easy to understand the flow
- Constants for cache durations
- Helper methods make testing easier

### 5. Consistent Caching Strategy
- Content cache: 2 hours
- Metadata cache: 2 hours
- List cache: 30 minutes
- Automatic cache invalidation on changes

### 6. RESTful API Behavior
- POST endpoint now properly implements upsert semantics
- Appropriate HTTP status codes (201 vs 200)
- Clear distinction between create and update in responses

## Response Status Values

| Status | Meaning | HTTP Code |
|--------|---------|-----------|
| "created" | New dataset was created | 201 Created |
| "updated" | Existing dataset was updated | 200 OK |
| "error" | Operation failed | 500 Internal Server Error |

## Example Flow

### Scenario 1: Creating New Dataset
1. Client POSTs dataset with AgentId="agent1", DatasetName="test", DatasetType="Golden"
2. `FindExistingDatasetAsync` returns null (no match found)
3. `CreateNewDatasetAsync` is called
4. New dataset created with new GUID
5. Response: `{ status: "created", ... }` with HTTP 201

### Scenario 2: Updating Existing Dataset
1. Client POSTs dataset with AgentId="agent1", DatasetName="test", DatasetType="Golden"
2. `FindExistingDatasetAsync` returns existing dataset entity
3. `UpdateExistingDatasetAsync` is called
4. Existing dataset blob and metadata updated
5. Response: `{ status: "updated", ... }` with HTTP 200

## Files Modified
1. `SXG.EvalPlatform.API/RequestHandlers/DataSetRequetHandler.cs`

## Build Status
? All compilation errors resolved
? Build successful
? No breaking changes
? Controller already handles both "created" and "updated" responses properly

## Testing Recommendations

### Test Cases to Verify:
1. ? Create new dataset with unique combination of AgentId + DatasetName + DatasetType
2. ? POST same dataset again (should update, not create new)
3. ? Verify blob content is updated on upsert
4. ? Verify metadata timestamps are updated correctly
5. ? Verify cache is invalidated and updated
6. ? Verify different DatasetType with same AgentId + DatasetName creates new dataset
7. ? Verify HTTP status codes (201 for create, 200 for update)
8. ? Verify explicit PUT /datasets/{id} still works
