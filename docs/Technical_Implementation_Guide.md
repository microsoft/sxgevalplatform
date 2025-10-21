# SXG Evaluation Platform - Technical Implementation Guide

## Architecture Overview

The SXG Evaluation Platform uses a layered RequestHandlers architecture with clear separation of concerns:

### RequestHandlers Layer (Business Logic)
1. **DataSetRequestHandler** - Dataset operations and validation
2. **MetricsConfigurationRequestHandler** - Metrics configuration management
3. **EvalRunRequestHandler** - Evaluation run lifecycle management
4. **EvaluationResultRequestHandler** - Evaluation result operations

### Storage Services Layer (Data Access)
1. **DataSetTableService** - Dataset table operations
2. **MetricsConfigTableService** - Metrics configuration table operations
3. **EvalRunTableService** - Evaluation run table operations
4. **AzureBlobStorageService** - Blob storage operations

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

#### EvalRunTableEntity
```csharp
public class EvalRunTableEntity : ITableEntity
{
    public string AgentId { get; set; }                    // Partition Key (auto-set)
    public Guid EvalRunId { get; set; }                    // Business GUID
    public string PartitionKey { get; set; }               // Table partition key
    public string RowKey { get; set; }                     // Table row key (UUID string)
    public string MetricsConfigurationId { get; set; }     // Config reference
    public string DataSetId { get; set; }                  // Dataset reference
    public string Status { get; set; }                     // Run status
    public DateTime? LastUpdatedOn { get; set; }           // Timestamp
    public DateTime? StartedDatetime { get; set; }         // Start time
    public DateTime? CompletedDatetime { get; set; }       // End time
    public string? BlobFilePath { get; set; }              // Results path
    public string? ContainerName { get; set; }             // Results container
    public string Type { get; set; }                       // Evaluation type
    public string EnvironmentId { get; set; }              // Environment reference
    public string AgentSchemaName { get; set; }            // Schema identifier
}
```

---

## RequestHandler Architecture Pattern

### RequestHandler-Service Pattern

The application follows a **Controllers → RequestHandlers → Storage Services** architecture:

1. **Controllers**: Handle HTTP requests and responses
2. **RequestHandlers**: Contain business logic and orchestration
3. **Storage Services**: Abstract Azure storage operations

### Storage Service Base Architecture

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

## EvalRunTableService Implementation

### Key Features
- **CRUD Operations**: Create, read, update evaluation runs
- **Status Management**: Handle evaluation run lifecycle
- **Cross-Partition Queries**: Efficient ID-based lookups
- **Agent-based Filtering**: Secure multi-tenant access

### Status Management
Evaluation runs follow a strict state machine:

```
Pending → Running → Completed/Failed
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
var query = tableClient.QueryAsync<EvalRunTableEntity>(
    filter: $"EvalRunId eq guid'{evalRunId}'");
```

### RequestHandler Pattern Example
```csharp
public class EvalRunRequestHandler
{
    private readonly IEvalRunTableService _evalRunTableService;
    private readonly IAzureBlobStorageService _blobStorageService;
    
    public async Task<EvaluationRun> CreateEvalRunAsync(CreateEvalRunRequest request)
    {
        var tableEntity = new EvalRunTableEntity
        {
            AgentId = request.AgentId,
            EvalRunId = Guid.NewGuid(),
            Status = EvalRunStatus.Pending,
            StartedDatetime = DateTime.UtcNow
        };
        
        var createdEntity = await _evalRunTableService.CreateEvalRunAsync(tableEntity);
        return _mapper.Map<EvaluationRun>(createdEntity);
    }
}
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

## Dependency Injection Configuration

### Program.cs Registration
```csharp
// Storage Services
builder.Services.AddSingleton<IConfigHelper, ConfigHelper>();
builder.Services.AddSingleton<IDataSetTableService, DataSetTableService>();
builder.Services.AddSingleton<IMetricsConfigTableService, MetricsConfigTableService>();
builder.Services.AddSingleton<IEvalRunTableService, EvalRunTableService>();
builder.Services.AddSingleton<IAzureBlobStorageService, AzureBlobStorageService>();

// RequestHandlers
builder.Services.AddScoped<DatasetRequestHandler>();
builder.Services.AddScoped<MetricsConfigurationRequestHandler>();
builder.Services.AddScoped<EvalRunRequestHandler>();
builder.Services.AddScoped<EvaluationResultRequestHandler>();
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