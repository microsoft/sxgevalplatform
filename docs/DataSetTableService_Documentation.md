# DataSetTableService Documentation

## Overview

The `DataSetTableService` is a comprehensive service for managing dataset metadata in Azure Table Storage. It follows the same architectural patterns as `MetricsConfigTableService` and provides full CRUD operations for dataset entities.

## Architecture

### **Table Structure**
- **Table Name**: `DataSets` (configurable via `AzureStorage:DataSetsTable`)
- **PartitionKey**: `AgentId` (enables efficient agent-based queries)
- **RowKey**: `DatasetId` (UUID for guaranteed uniqueness)

### **Entity Schema**
```csharp
public class DataSetEntity : ITableEntity
{
    public string AgentId { get; set; }           // Partition Key
    public string DatasetId { get; set; }         // Row Key (UUID)
    public string BlobFilePath { get; set; }      // Path to blob in storage
    public string ContainerName { get; set; }     // Blob container name
    public string DatasetType { get; set; }       // e.g., "Synthetic", "Golden"
    public string FileName { get; set; }          // Original filename
    public DateTime LastUpdatedOn { get; set; }   // Last update timestamp
    public string LastUpdatedBy { get; set; }     // Who updated it
}
```

## Configuration

### **appsettings.json**
```json
{
  "AzureStorage": {
    "AccountName": "your-storage-account",
    "DataSetsTable": "DataSets"
  }
}
```

### **Service Registration**
```csharp
// In Program.cs or Startup.cs
builder.Services.AddScoped<IDataSetTableService, DataSetTableService>();
```

## Key Features

### ? **Automatic Key Management**
- **PartitionKey**: Automatically set from `AgentId`
- **RowKey**: Automatically set from `DatasetId` (UUID)
- **UUID Generation**: Automatic UUID generation in constructor

### ? **Lazy Initialization**
- TableClient is initialized on first use
- Reduces startup time and resource usage
- Handles connection errors gracefully

### ? **Comprehensive Logging**
- Detailed logging for all operations
- Performance tracking and error diagnostics
- Structured logging with correlation IDs

### ? **Error Handling**
- Graceful handling of 404 (not found) scenarios
- Proper exception wrapping and logging
- Retry logic through Azure SDK

## API Methods

### **Create/Update Operations**

#### `SaveDataSetAsync(DataSetEntity entity)`
Saves or updates a dataset entity.
```csharp
var entity = DataSetEntityHelper.CreateEntity(
    agentId: "agent-001",
    blobFilePath: "datasets/agent-001/data.json",
    containerName: "agent-001",
    datasetType: "Synthetic",
    fileName: "data.json",
    lastUpdatedBy: "user@example.com"
);

var savedEntity = await dataSetService.SaveDataSetAsync(entity);
```

### **Read Operations**

#### `GetDataSetAsync(string agentId, string datasetId)`
Direct lookup by Agent ID and Dataset ID (fastest).
```csharp
var dataset = await dataSetService.GetDataSetAsync("agent-001", "dataset-uuid");
```

#### `GetDataSetByIdAsync(string datasetId)`
Search by Dataset ID across all agents.
```csharp
var dataset = await dataSetService.GetDataSetByIdAsync("dataset-uuid");
```

#### `GetAllDataSetsByAgentIdAsync(string agentId)`
Get all datasets for an agent.
```csharp
var datasets = await dataSetService.GetAllDataSetsByAgentIdAsync("agent-001");
```

#### `GetAllDataSetsByAgentIdAndTypeAsync(string agentId, string datasetType)`
Get datasets by agent and type.
```csharp
var syntheticData = await dataSetService.GetAllDataSetsByAgentIdAndTypeAsync("agent-001", "Synthetic");
```

#### `GetDataSetsByFileNameAsync(string agentId, string fileName)`
Find datasets by filename.
```csharp
var datasets = await dataSetService.GetDataSetsByFileNameAsync("agent-001", "test-data.json");
```

#### `GetDataSetByFileNameAndTypeAsync(string agentId, string fileName, string datasetType)`
Find specific dataset by filename and type.
```csharp
var dataset = await dataSetService.GetDataSetByFileNameAndTypeAsync("agent-001", "data.json", "Golden");
```

### **Update Operations**

#### `UpdateDataSetMetadataAsync(string agentId, string datasetId, Action<DataSetEntity> updateAction)`
Update metadata without changing blob data.
```csharp
var updated = await dataSetService.UpdateDataSetMetadataAsync("agent-001", "dataset-id", entity =>
{
    entity.LastUpdatedBy = "new-user@example.com";
    entity.DatasetType = "Updated-Type";
});
```

### **Delete Operations**

#### `DeleteDataSetAsync(string agentId, string datasetId)`
Delete a specific dataset.
```csharp
var deleted = await dataSetService.DeleteDataSetAsync("agent-001", "dataset-id");
```

#### `DeleteAllDataSetsByAgentIdAsync(string agentId)`
Delete all datasets for an agent (use with caution).
```csharp
var deletedCount = await dataSetService.DeleteAllDataSetsByAgentIdAsync("agent-001");
```

### **Utility Operations**

#### `DataSetExistsAsync(string agentId, string datasetId)`
Check if a dataset exists.
```csharp
var exists = await dataSetService.DataSetExistsAsync("agent-001", "dataset-id");
```

## Helper Utilities

### **DataSetEntityHelper**
Provides utility methods for entity creation and validation.

#### **Entity Creation**
```csharp
// Create with auto-generated ID
var entity = DataSetEntityHelper.CreateEntity(
    agentId: "agent-001",
    blobFilePath: "path/to/blob",
    containerName: "container",
    datasetType: "Synthetic",
    fileName: "data.json",
    lastUpdatedBy: "user"
);

// Create with specific ID
var entity = DataSetEntityHelper.CreateEntity(
    agentId: "agent-001",
    datasetId: "specific-uuid",
    blobFilePath: "path/to/blob",
    containerName: "container",
    datasetType: "Golden",
    fileName: "data.json",
    lastUpdatedBy: "user"
);
```

#### **Path Generation**
```csharp
// Generate blob file path
var blobPath = DataSetEntityHelper.CreateBlobFilePath("agent-001", "dataset-id", "data.json");
// Result: "datasets/agent-001/dataset-id.json"

// Generate container name (lowercase)
var containerName = DataSetEntityHelper.CreateContainerName("Agent-001");
// Result: "agent-001"
```

#### **Validation**
```csharp
// Validate entity keys
var isValid = DataSetEntityHelper.ValidateKeys(entity);

// Validate entire entity
var errors = DataSetEntityHelper.ValidateEntity(entity);
if (errors.Any())
{
    foreach (var error in errors)
        Console.WriteLine($"Error: {error}");
}
```

#### **Query Filters**
```csharp
// Build filter strings
var filter = DataSetEntityHelper.BuildFilterString(
    agentId: "agent-001",
    datasetType: "Synthetic",
    fileName: "data.json"
);
// Result: "PartitionKey eq 'agent-001' and DatasetType eq 'Synthetic' and FileName eq 'data.json'"
```

## Query Performance

### **Excellent Performance** ?
- Direct lookups by `AgentId` + `DatasetId`
- All queries within a single agent partition
- Agent-specific dataset listings

### **Good Performance** ?
- Filtered queries within agent partition
- Type-based filtering for agent datasets
- Filename searches within agent scope

### **Moderate Performance** ??
- Cross-agent searches by Dataset ID
- Global dataset searches (avoid when possible)

## Best Practices

### ? **Do**
1. **Always include AgentId** in queries when possible
2. **Use direct lookups** with AgentId + DatasetId for best performance
3. **Validate entities** before saving using helper methods
4. **Handle null returns** gracefully for not-found scenarios
5. **Use bulk operations** for multiple dataset operations

### ? **Don't**
1. **Avoid cross-partition queries** when possible
2. **Don't change keys** after entity creation
3. **Don't skip error handling** for Azure operations
4. **Don't delete without confirmation** (especially bulk deletes)

## Common Patterns

### **Check-Update-Create Pattern**
```csharp
// Check if dataset exists
var existing = await dataSetService.GetDataSetByFileNameAndTypeAsync(agentId, fileName, datasetType);

if (existing != null)
{
    // Update existing
    await dataSetService.UpdateDataSetMetadataAsync(agentId, existing.DatasetId, entity =>
    {
        entity.LastUpdatedBy = currentUser;
        // Update other metadata as needed
    });
}
else
{
    // Create new
    var newDataset = DataSetEntityHelper.CreateEntity(agentId, blobPath, container, datasetType, fileName, currentUser);
    await dataSetService.SaveDataSetAsync(newDataset);
}
```

### **Bulk Processing Pattern**
```csharp
var agentDatasets = await dataSetService.GetAllDataSetsByAgentIdAsync(agentId);

var tasks = agentDatasets.Select(async dataset =>
{
    // Process each dataset
    return await ProcessDatasetAsync(dataset);
});

var results = await Task.WhenAll(tasks);
```

### **Error-Safe Deletion Pattern**
```csharp
try
{
    // Verify dataset exists before deletion
    var dataset = await dataSetService.GetDataSetAsync(agentId, datasetId);
    if (dataset != null)
    {
        var deleted = await dataSetService.DeleteDataSetAsync(agentId, datasetId);
        if (deleted)
        {
            // Also delete blob if needed
            await blobService.DeleteBlobAsync(dataset.ContainerName, dataset.BlobFilePath);
        }
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to delete dataset {DatasetId}", datasetId);
    throw;
}
```

## Error Handling

### **Common Scenarios**
- **404 Not Found**: Returns `null` instead of throwing
- **Connection Issues**: Throws with detailed error information
- **Validation Errors**: Throws `ArgumentException` with validation details

### **Exception Types**
- `ArgumentException`: Configuration or validation issues
- `Azure.RequestFailedException`: Azure-specific errors
- `InvalidOperationException`: Invalid state or operation

## Integration Examples

### **With Blob Storage**
```csharp
// Save blob first, then metadata
await blobService.UploadBlobAsync(containerName, blobPath, dataStream);

var dataset = DataSetEntityHelper.CreateEntity(agentId, blobPath, containerName, datasetType, fileName, user);
await dataSetService.SaveDataSetAsync(dataset);
```

### **With API Controllers**
```csharp
[HttpPost("datasets")]
public async Task<IActionResult> CreateDataset([FromBody] CreateDatasetRequest request)
{
    try
    {
        var entity = DataSetEntityHelper.CreateEntity(
            request.AgentId, request.BlobPath, request.Container, 
            request.Type, request.FileName, User.Identity.Name);
        
        var saved = await _dataSetService.SaveDataSetAsync(entity);
        return Ok(new { DatasetId = saved.DatasetId });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create dataset");
        return StatusCode(500, "Failed to create dataset");
    }
}
```

## Monitoring and Troubleshooting

### **Key Metrics**
- Query response times by operation type
- Error rates for CRUD operations
- Partition hot spots (agent-based)

### **Logging Categories**
- `DataSetTableService.Save`: Save/update operations
- `DataSetTableService.Retrieve`: Get operations  
- `DataSetTableService.Delete`: Delete operations
- `DataSetTableService.Initialize`: Service initialization

### **Common Issues**
1. **Slow queries**: Check if AgentId is included in filters
2. **404 errors**: Verify entity keys and existence
3. **Connection timeouts**: Check Azure Storage account configuration

The DataSetTableService provides a robust, scalable solution for dataset metadata management with excellent performance characteristics and comprehensive functionality! ??