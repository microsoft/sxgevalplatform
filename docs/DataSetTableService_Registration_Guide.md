# DataSetTableService Registration Guide

## Overview

This guide shows how to register and use the `DataSetTableService` in your main application project.

## Registration in Main Project

### **Step 1: Add Project Reference**

In your main project (e.g., `SXG.EvalPlatform.API`), add a reference to the storage project:

```xml
<ProjectReference Include="..\Sxg.EvalPlatform.API.Storage\Sxg.EvalPlatform.API.Storage.csproj" />
```

### **Step 2: Register Service in Program.cs**

Add the service registration to your `Program.cs`:

```csharp
using Sxg.EvalPlatform.API.Storage.Services;

var builder = WebApplication.CreateBuilder(args);

// ... other service registrations ...

// Register the DataSet table service
builder.Services.AddScoped<IDataSetTableService, DataSetTableService>();

// ... rest of configuration ...
```

### **Step 3: Update appsettings.json**

Ensure your `appsettings.json` includes the DataSets table configuration:

```json
{
  "AzureStorage": {
    "AccountName": "your-storage-account-name",
    "DataSetsTable": "DataSets"
  }
}
```

## Usage in Controllers

### **Constructor Injection**

```csharp
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.Entities;
using Sxg.EvalPlatform.API.Storage.Helpers;

[ApiController]
[Route("api/v1/[controller]")]
public class DataSetsController : ControllerBase
{
    private readonly IDataSetTableService _dataSetService;
    private readonly ILogger<DataSetsController> _logger;

    public DataSetsController(
        IDataSetTableService dataSetService, 
        ILogger<DataSetsController> logger)
    {
        _dataSetService = dataSetService;
        _logger = logger;
    }

    // ... controller methods ...
}
```

### **Controller Examples**

#### **Create Dataset**
```csharp
[HttpPost("agents/{agentId}/datasets")]
public async Task<IActionResult> CreateDataSet(
    string agentId, 
    [FromBody] CreateDataSetRequest request)
{
    try
    {
        var entity = DataSetEntityHelper.CreateEntity(
            agentId: agentId,
            blobFilePath: request.BlobFilePath,
            containerName: request.ContainerName,
            datasetType: request.DatasetType,
            fileName: request.FileName,
            lastUpdatedBy: User.Identity?.Name ?? "system"
        );

        var savedEntity = await _dataSetService.SaveDataSetAsync(entity);

        return Ok(new { 
            DatasetId = savedEntity.DatasetId,
            Message = "Dataset created successfully"
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to create dataset for agent {AgentId}", agentId);
        return StatusCode(500, "Failed to create dataset");
    }
}
```

#### **Get Dataset by ID**
```csharp
[HttpGet("agents/{agentId}/datasets/{datasetId}")]
public async Task<IActionResult> GetDataSet(string agentId, string datasetId)
{
    try
    {
        var dataset = await _dataSetService.GetDataSetAsync(agentId, datasetId);
        
        if (dataset == null)
        {
            return NotFound($"Dataset {datasetId} not found for agent {agentId}");
        }

        return Ok(dataset);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to retrieve dataset {DatasetId} for agent {AgentId}", 
            datasetId, agentId);
        return StatusCode(500, "Failed to retrieve dataset");
    }
}
```

#### **Get All Datasets for Agent**
```csharp
[HttpGet("agents/{agentId}/datasets")]
public async Task<IActionResult> GetAllDataSets(
    string agentId, 
    [FromQuery] string? datasetType = null)
{
    try
    {
        List<DataSetEntity> datasets;
        
        if (!string.IsNullOrEmpty(datasetType))
        {
            datasets = await _dataSetService.GetAllDataSetsByAgentIdAndTypeAsync(agentId, datasetType);
        }
        else
        {
            datasets = await _dataSetService.GetAllDataSetsByAgentIdAsync(agentId);
        }

        return Ok(new {
            AgentId = agentId,
            DatasetType = datasetType,
            Count = datasets.Count,
            Datasets = datasets
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to retrieve datasets for agent {AgentId}", agentId);
        return StatusCode(500, "Failed to retrieve datasets");
    }
}
```

#### **Update Dataset Metadata**
```csharp
[HttpPut("agents/{agentId}/datasets/{datasetId}")]
public async Task<IActionResult> UpdateDataSet(
    string agentId, 
    string datasetId,
    [FromBody] UpdateDataSetRequest request)
{
    try
    {
        var updatedEntity = await _dataSetService.UpdateDataSetMetadataAsync(
            agentId, 
            datasetId, 
            entity =>
            {
                if (!string.IsNullOrEmpty(request.DatasetType))
                    entity.DatasetType = request.DatasetType;
                
                entity.LastUpdatedBy = User.Identity?.Name ?? "system";
            });

        if (updatedEntity == null)
        {
            return NotFound($"Dataset {datasetId} not found for agent {agentId}");
        }

        return Ok(updatedEntity);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to update dataset {DatasetId} for agent {AgentId}", 
            datasetId, agentId);
        return StatusCode(500, "Failed to update dataset");
    }
}
```

#### **Delete Dataset**
```csharp
[HttpDelete("agents/{agentId}/datasets/{datasetId}")]
public async Task<IActionResult> DeleteDataSet(string agentId, string datasetId)
{
    try
    {
        var deleted = await _dataSetService.DeleteDataSetAsync(agentId, datasetId);
        
        if (!deleted)
        {
            return NotFound($"Dataset {datasetId} not found for agent {agentId}");
        }

        return Ok(new { Message = "Dataset deleted successfully" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to delete dataset {DatasetId} for agent {AgentId}", 
            datasetId, agentId);
        return StatusCode(500, "Failed to delete dataset");
    }
}
```

## Request/Response Models

### **CreateDataSetRequest**
```csharp
public class CreateDataSetRequest
{
    public string BlobFilePath { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string DatasetType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
```

### **UpdateDataSetRequest**
```csharp
public class UpdateDataSetRequest
{
    public string? DatasetType { get; set; }
    // Add other updatable fields as needed
}
```

## Usage in Services

### **Business Logic Service**
```csharp
public class DatasetManagementService
{
    private readonly IDataSetTableService _dataSetService;
    private readonly IBlobStorageService _blobService;

    public DatasetManagementService(
        IDataSetTableService dataSetService,
        IBlobStorageService blobService)
    {
        _dataSetService = dataSetService;
        _blobService = blobService;
    }

    public async Task<string> CreateDatasetAsync(
        string agentId, 
        Stream dataStream, 
        string fileName,
        string datasetType)
    {
        // Generate unique dataset ID
        var datasetId = DataSetEntityHelper.GenerateNewDatasetId();
        
        // Create blob path
        var blobPath = DataSetEntityHelper.CreateBlobFilePath(agentId, datasetId, fileName);
        var containerName = DataSetEntityHelper.CreateContainerName(agentId);

        try
        {
            // Upload blob first
            await _blobService.UploadBlobAsync(containerName, blobPath, dataStream);

            // Create metadata entity
            var entity = DataSetEntityHelper.CreateEntity(
                agentId, datasetId, blobPath, containerName, 
                datasetType, fileName, "system");

            // Save metadata
            await _dataSetService.SaveDataSetAsync(entity);

            return datasetId;
        }
        catch
        {
            // Cleanup blob if metadata save fails
            try
            {
                await _blobService.DeleteBlobAsync(containerName, blobPath);
            }
            catch { /* Ignore cleanup errors */ }
            
            throw;
        }
    }
}
```

## Configuration Options

### **Environment-Specific Settings**

#### **appsettings.Development.json**
```json
{
  "AzureStorage": {
    "AccountName": "devstorageaccount",
    "DataSetsTable": "DataSetsDev"
  }
}
```

#### **appsettings.Production.json**
```json
{
  "AzureStorage": {
    "AccountName": "prodstorageaccount", 
    "DataSetsTable": "DataSets"
  }
}
```

### **Connection String Alternative**
If using connection strings instead of managed identity:

```json
{
  "ConnectionStrings": {
    "AzureStorage": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;"
  },
  "AzureStorage": {
    "DataSetsTable": "DataSets"
  }
}
```

## Health Checks

Add health checks for the DataSet service:

```csharp
// In Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<DataSetTableServiceHealthCheck>("dataset-table-service");

// Custom health check implementation
public class DataSetTableServiceHealthCheck : IHealthCheck
{
    private readonly IDataSetTableService _dataSetService;

    public DataSetTableServiceHealthCheck(IDataSetTableService dataSetService)
    {
        _dataSetService = dataSetService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to query an agent (this will initialize the connection)
            await _dataSetService.GetAllDataSetsByAgentIdAsync("health-check");
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"DataSet service unavailable: {ex.Message}");
        }
    }
}
```

## Testing Setup

### **Unit Test Registration**
```csharp
public class DataSetControllerTests
{
    private readonly Mock<IDataSetTableService> _mockDataSetService;
    private readonly DataSetsController _controller;

    public DataSetControllerTests()
    {
        _mockDataSetService = new Mock<IDataSetTableService>();
        var mockLogger = new Mock<ILogger<DataSetsController>>();
        
        _controller = new DataSetsController(_mockDataSetService.Object, mockLogger.Object);
    }

    // ... test methods ...
}
```

This integration guide provides everything needed to successfully use the DataSetTableService in your main application! ??