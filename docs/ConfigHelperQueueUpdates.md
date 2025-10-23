# ConfigHelper Queue Configuration Updates

This document summarizes the updates made to the `ConfigHelper` class to support the new queue configuration settings in `appsettings.json`.

## Overview

The `ConfigHelper` class has been enhanced to support two new Azure Storage Queue configuration settings:
- `DatasetEnrichmentRequestsQueueName`
- `EvalProcessingRequestsQueueName`

## Configuration Settings

### appsettings.json Updates
```json
{
  "AzureStorage": {
    "DatasetEnrichmentRequestsQueueName": "dataset-enrichment-requests",
    "EvalProcessingRequestsQueueName": "eval-processing-requests"
  }
}
```

## Interface Changes

### IConfigHelper.cs
Added two new method signatures:

```csharp
/// <summary>
/// Get the dataset enrichment requests queue name
/// </summary>
/// <returns>Queue name for dataset enrichment requests</returns>
string GetDatasetEnrichmentRequestsQueueName();

/// <summary>
/// Get the evaluation processing requests queue name
/// </summary>
/// <returns>Queue name for evaluation processing requests</returns>
string GetEvalProcessingRequestsQueueName();
```

## Implementation Changes

### ConfigHelper.cs
Added implementations for the new queue configuration methods:

```csharp
public string GetDatasetEnrichmentRequestsQueueName()
{
    var queueName = _configuration["AzureStorage:DatasetEnrichmentRequestsQueueName"];
    if (string.IsNullOrEmpty(queueName))
    {
        throw new InvalidOperationException("Dataset Enrichment Requests queue name is not configured.");
    }
    return queueName;
}

public string GetEvalProcessingRequestsQueueName()
{
    var queueName = _configuration["AzureStorage:EvalProcessingRequestsQueueName"];
    if (string.IsNullOrEmpty(queueName))
    {
        throw new InvalidOperationException("Evaluation Processing Requests queue name is not configured.");
    }
    return queueName;
}
```

## Integration Updates

### EvalRunRequestHandler.cs
Updated to use configuration-based queue name:

**Before:**
```csharp
const string queueName = "enrich-dataset-request";
```

**After:**
```csharp
var queueName = _configHelper.GetDatasetEnrichmentRequestsQueueName();
```

### DatasetEnrichmentController.cs
Updated to use `IConfigHelper` for queue name resolution:

**Before:**
```csharp
private const string EnrichmentQueueName = "enrich-dataset-request";
```

**After:**
```csharp
private readonly IConfigHelper _configHelper;
// ...
var queueName = _configHelper.GetDatasetEnrichmentRequestsQueueName();
```

## New Features Added

### 1. ConfigHelper Extensions
**File:** `Sxg.EvalPlatform.API.Storage\Examples\ConfigHelperQueueExamples.cs`

Provides utility methods and extensions for queue configuration:
- `GetAllQueueNames()` - Get all queue names as dictionary
- `HasAllQueueConfigurations()` - Validate all queue configs exist
- `GetValidatedQueueNames()` - Get queue names with validation
- Azure queue naming convention validation

### 2. Configuration Validation Service
**File:** `Sxg.EvalPlatform.API.Storage\Services\ConfigurationValidationService.cs`

Comprehensive configuration validation service:
- Validates all configuration settings at startup
- Provides detailed validation results
- Supports queue name format validation
- Can be used for health checks and diagnostics

## Benefits

### 1. **Configuration Flexibility**
- Queue names can be different per environment (dev, staging, prod)
- Easy to change queue names without code changes
- Supports multiple deployment scenarios

### 2. **Error Prevention**
- Configuration validation at startup
- Clear error messages for missing configurations
- Type-safe configuration access

### 3. **Maintainability**
- Centralized configuration management
- No hardcoded queue names in business logic
- Consistent configuration access pattern

### 4. **Testing Support**
- Easy to mock different queue names for testing
- Configuration validation can be tested independently
- Support for integration testing with different configs

## Usage Examples

### Basic Usage
```csharp
// In service/controller
var datasetQueue = _configHelper.GetDatasetEnrichmentRequestsQueueName();
var evalQueue = _configHelper.GetEvalProcessingRequestsQueueName();
```

### With Validation
```csharp
// Using extensions
var (datasetQueue, evalQueue) = _configHelper.GetValidatedQueueNames();
```

### Startup Validation
```csharp
// In Program.cs or startup
builder.Services.AddScoped<IConfigurationValidationService, ConfigurationValidationService>();

// Later in application startup
var validationService = app.Services.GetRequiredService<IConfigurationValidationService>();
var isValid = await validationService.ValidateAllConfigurationsAsync();
if (!isValid)
{
    // Handle configuration errors
}
```

## Environment-Specific Configuration

### Development (appsettings.Development.json)
```json
{
  "AzureStorage": {
    "DatasetEnrichmentRequestsQueueName": "dev-dataset-enrichment-requests",
    "EvalProcessingRequestsQueueName": "dev-eval-processing-requests"
  }
}
```

### Production (appsettings.Production.json)
```json
{
  "AzureStorage": {
    "DatasetEnrichmentRequestsQueueName": "prod-dataset-enrichment-requests", 
    "EvalProcessingRequestsQueueName": "prod-eval-processing-requests"
  }
}
```

## Migration Guide

### For Existing Code
1. **Update appsettings.json** with new queue configuration keys
2. **Inject IConfigHelper** where queue names are needed
3. **Replace hardcoded queue names** with ConfigHelper method calls
4. **Add configuration validation** to startup process (optional but recommended)

### For New Development
- Always use `ConfigHelper` for configuration access
- Leverage the validation service for robust configuration management
- Use extension methods for common configuration patterns

## Error Handling

### Configuration Missing
```csharp
try
{
    var queueName = _configHelper.GetDatasetEnrichmentRequestsQueueName();
}
catch (InvalidOperationException ex)
{
    // Handle missing configuration
    _logger.LogError(ex, "Queue configuration is missing");
}
```

### Validation Failures
```csharp
var validationResult = await _validationService.GetValidationResultsAsync();
if (!validationResult.IsValid)
{
    foreach (var failure in validationResult.ValidationItems.Where(v => !v.IsValid))
    {
        _logger.LogError("Configuration validation failed: {Name} - {Message}", 
            failure.Name, failure.Message);
    }
}
```

## Future Enhancements

### Planned Features
1. **Dynamic Configuration Reload** - Support for configuration changes without restart
2. **Configuration Caching** - Performance optimization for frequently accessed configs
3. **Configuration Templates** - Standardized configuration patterns
4. **Environment Detection** - Automatic environment-specific defaults

### Integration Opportunities
- **Azure Key Vault** - Secure configuration storage
- **Azure App Configuration** - Centralized configuration management
- **Health Checks** - Built-in configuration health monitoring
- **Metrics** - Configuration usage and validation metrics

## Testing

### Unit Tests
```csharp
[Test]
public void GetDatasetEnrichmentRequestsQueueName_WhenConfigured_ReturnsQueueName()
{
    // Arrange
    var config = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            ["AzureStorage:DatasetEnrichmentRequestsQueueName"] = "test-queue"
        })
        .Build();
    var configHelper = new ConfigHelper(config);

    // Act
    var result = configHelper.GetDatasetEnrichmentRequestsQueueName();

    // Assert
    Assert.AreEqual("test-queue", result);
}
```

### Integration Tests
```csharp
[Test]
public async Task ConfigurationValidation_WithValidConfig_ReturnsTrue()
{
    // Arrange
    var validationService = _serviceProvider.GetRequiredService<IConfigurationValidationService>();

    // Act
    var isValid = await validationService.ValidateAllConfigurationsAsync();

    // Assert
    Assert.IsTrue(isValid);
}
```

This comprehensive update ensures robust, flexible, and maintainable queue configuration management across your evaluation platform.