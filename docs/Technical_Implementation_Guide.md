# SXG Evaluation Platform - Technical Implementation Guide

## Service Architecture Overview

The SXG Evaluation Platform uses a layered architecture with specialized services for different data management concerns:

1. **DataSetTableService** - Dataset metadata management
2. **MetricsConfigTableService** - Metrics configuration management  
3. **EvalRunService** - Evaluation run lifecycle management
4. **AzureBlobStorageService** - File storage operations

---

## Azure Table Storage Design

### Partitioning Strategy

All services use **Agent-based partitioning** for optimal performance:

- **Partition Key**: `AgentId` 
- **Row Key**: Unique identifier (UUID)
- **Benefits**: 
  - Efficient agent-specific queries
  - Load distribution across partitions
  - Scalable multi-tenant architecture

### Entity Schemas

#### DataSetEntity
```csharp
public class DataSetEntity : ITableEntity
{
    public string AgentId { get; set; }           // Partition Key
    public string DatasetId { get; set; }         // Row Key (UUID)
    public string BlobFilePath { get; set; }      // Blob storage path
    public string ContainerName { get; set; }     // Container name
    public string DatasetType { get; set; }       // "Synthetic" or "Golden"
    public string FileName { get; set; }          // Original filename
    public DateTime LastUpdatedOn { get; set; }   // Timestamp
    public string LastUpdatedBy { get; set; }     // User identifier
}
```

#### MetricsConfigurationTableEntity
```csharp
public class MetricsConfigurationTableEntity : ITableEntity
{
    public string AgentId { get; set; }                    // Partition Key
    public string MetricsConfigurationId { get; set; }     // Row Key (UUID)
    public string ConfigurationName { get; set; }          // Display name
    public string SelectedMetricsJson { get; set; }        // JSON metrics
    public DateTime LastUpdatedOn { get; set; }            // Timestamp
    public string LastUpdatedBy { get; set; }              // User identifier
}
```

#### EvalRunEntity
```csharp
public class EvalRunEntity : ITableEntity
{
    public string AgentId { get; set; }                    // Partition Key
    public string EvalRunId { get; set; }                  // Row Key (UUID)
    public string MetricsConfigurationId { get; set; }     // Config reference
    public string DataSetId { get; set; }                  // Dataset reference
    public string Status { get; set; }                     // Run status
    public DateTime? LastUpdatedOn { get; set; }           // Timestamp
    public DateTime? StartedDatetime { get; set; }         // Start time
    public DateTime? CompletedDatetime { get; set; }       // End time
    public string? BlobFilePath { get; set; }              // Results path
    public string? ContainerName { get; set; }             // Results container
}
```

---

## Service Implementation Patterns

### Base Service Architecture

All table services inherit from `BaseTableService` which provides:

- **Standardized error handling**
- **Consistent logging patterns**
- **Azure credential management**
- **Table client initialization**

### Common Operations

#### Authentication
```csharp
// Development: Uses DefaultAzureCredential with fallbacks
// Production: Uses ManagedIdentityCredential
TokenCredential credential = CommonUtils.GetTokenCredential(environment);
```

#### Table Client Creation
```csharp
protected TableClient CreateTableClient(string tableName)
{
    var tableUri = $"https://{accountName}.table.core.windows.net";
    var serviceClient = new TableServiceClient(new Uri(tableUri), credential);
    var tableClient = serviceClient.GetTableClient(tableName);
    tableClient.CreateIfNotExists();
    return tableClient;
}
```

---

## DataSetTableService Implementation

### Key Features
- **CRUD Operations**: Create, read, update, delete datasets
- **Agent-based Filtering**: Efficient queries by agent
- **Blob Integration**: Manages blob storage references
- **Validation**: Ensures data integrity

### Configuration
```json
{
  "AzureStorage": {
    "AccountName": "your-storage-account",
    "DataSetsTable": "DataSetsTable"
  }
}
```

### Usage Examples
```csharp
// Create dataset
var datasetEntity = new DataSetEntity
{
    AgentId = "A001",
    DatasetId = Guid.NewGuid().ToString(),
    DatasetType = "Synthetic",
    FileName = "test-data.json"
};
await service.CreateDatasetAsync(datasetEntity);

// Get datasets for agent
var datasets = await service.GetDatasetsForAgentAsync("A001");
```

---

## MetricsConfigTableService Implementation

### Key Features
- **Configuration Management**: Store/retrieve metrics configurations
- **JSON Serialization**: Handle complex metrics structures
- **Validation**: Ensure configuration integrity
- **Agent Isolation**: Secure multi-tenant access

### Error Handling
The service implements comprehensive error handling for:
- **404 Not Found**: Configuration doesn't exist
- **400 Bad Request**: Invalid configuration data
- **500 Internal Server Error**: Azure storage issues

### Common Issues & Resolutions

#### Issue: Configuration Not Found
```csharp
// Resolution: Check AgentId and ConfigurationId
var config = await service.GetMetricsConfigurationAsync(agentId, configId);
if (config == null)
{
    // Handle not found scenario
}
```

#### Issue: JSON Serialization Errors
```csharp
// Resolution: Validate JSON structure
try
{
    var metrics = JsonSerializer.Deserialize<SelectedMetricsConfiguration[]>(jsonString);
}
catch (JsonException ex)
{
    // Handle invalid JSON
}
```

---

## EvalRunService Implementation

### Status Management
Evaluation runs follow a strict state machine:

```
Queued → Running → Completed/Failed
```

### Terminal State Protection
Once `Completed` or `Failed`, runs become immutable:

```csharp
var terminalStatuses = new[] { "Completed", "Failed" };
if (terminalStatuses.Contains(currentStatus))
{
    throw new InvalidOperationException("Cannot modify terminal state");
}
```

### Cross-Partition Queries
For ID-based lookups without AgentId:

```csharp
var query = tableClient.QueryAsync<EvalRunEntity>(
    filter: $"EvalRunId eq guid'{evalRunId}'");
```

---

## Azure Blob Storage Integration

### Container Strategy
- **Agent-specific containers**: Each agent gets dedicated storage
- **Lowercase naming**: All container names normalized to lowercase
- **Path structure**: `evaluations/{evalRunId}.json`

### Implementation
```csharp
public class AzureBlobStorageService
{
    public async Task WriteBlobContentAsync(string containerName, string blobPath, string content)
    {
        containerName = containerName.ToLower(); // Ensure lowercase
        var blobClient = GetBlobClient(containerName, blobPath);
        await blobClient.UploadAsync(BinaryData.FromString(content), overwrite: true);
    }
}
```

---

## Development Guidelines

### Unit Testing
All services should have comprehensive unit tests covering:
- **Happy path scenarios**
- **Error conditions**
- **Edge cases**
- **Azure service failures**

### Logging Standards
Use structured logging with consistent patterns:
```csharp
_logger.LogInformation("Operation: {OperationName} - AgentId: {AgentId}, Success: {Success}", 
    operationName, agentId, success);
```

### Error Handling
Implement consistent error handling:
```csharp
try
{
    // Operation
}
catch (RequestFailedException ex) when (ex.Status == 404)
{
    // Handle not found
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error in {OperationName}", operationName);
    throw;
}
```

---

## Deployment Considerations

### Azure Resources Required
- **Storage Account**: For tables and blobs
- **Azure AD App Registration**: For authentication
- **App Service**: For API hosting
- **Application Insights**: For monitoring

### Configuration Management
Use Azure Key Vault for sensitive settings:
- Storage account connection strings
- Authentication secrets
- API keys

### Monitoring
Implement monitoring for:
- **Storage operations latency**
- **Authentication failures**
- **Error rates by service**
- **Resource utilization**

---

## Performance Optimization

### Query Optimization
1. **Use partition keys**: Always include AgentId when possible
2. **Limit result sets**: Implement pagination for large queries
3. **Index appropriately**: Ensure proper table design

### Caching Strategy
- **Configuration data**: Cache frequently accessed configs
- **Agent metadata**: Cache agent information
- **Avoid blob caching**: Large files shouldn't be cached

### Connection Pooling
Azure SDK handles connection pooling automatically, but monitor:
- Connection timeouts
- Retry policies
- Circuit breaker patterns

---

This technical guide provides the foundation for understanding and extending the SXG Evaluation Platform services.