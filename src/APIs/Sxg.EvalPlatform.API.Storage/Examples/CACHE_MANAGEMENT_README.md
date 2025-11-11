# SXG Evaluation Platform Cache Management System

This document provides comprehensive guidance on using the cache management system in the SXG Evaluation Platform API. The system supports both in-memory caching and distributed Redis caching with Azure managed identity authentication.

## Overview

The cache management system provides:

- **Unified Interface**: Single `ICacheManager` interface for both memory and Redis cache
- **Flexible Configuration**: Switch between memory and Redis cache via configuration
- **Managed Identity Support**: Secure Redis authentication without storing secrets
- **Rich Operations**: Get, Set, Remove, Exists, GetOrCreate, Refresh, and Clear operations
- **Statistics**: Built-in cache performance monitoring
- **Type Safety**: Generic methods with type-safe operations
- **Async/Await**: Full async support with cancellation tokens

## Quick Start

### 1. Configuration

Add cache configuration to your `appsettings.json`:

#### Memory Cache Configuration
```json
{
  "Cache": {
    "Provider": "Memory",
    "DefaultExpirationMinutes": 30,
    "Memory": {
      "SizeLimitMB": 100,
      "CompactionPercentage": 0.25,
      "ExpirationScanFrequencySeconds": 60
    }
  }
}
```

#### Redis Cache Configuration
```json
{
  "Cache": {
    "Provider": "Redis",
    "DefaultExpirationMinutes": 60,
    "Redis": {
      "Endpoint": "your-redis-cache.redis.cache.windows.net:6380",
      "InstanceName": "SXG-EvalPlatform",
      "UseManagedIdentity": true,
      "ConnectTimeoutSeconds": 30,
      "CommandTimeoutSeconds": 30,
      "UseSsl": true,
      "Retry": {
        "Enabled": true,
        "MaxRetryAttempts": 3,
        "BaseDelayMs": 1000,
        "MaxDelayMs": 5000
      }
    }
  }
}
```

### 2. Service Registration

The cache services are automatically registered in `Program.cs` via the `AddBusinessServices` method:

```csharp
builder.Services.AddBusinessServices(builder.Configuration);
```

### 3. Using the Cache Manager

Inject `ICacheManager` into your service:

```csharp
public class YourService
{
    private readonly ICacheManager _cacheManager;
    
    public YourService(ICacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }
    
    public async Task<UserData> GetUserAsync(string userId)
    {
  var cacheKey = $"user:{userId}";
        
        return await _cacheManager.GetOrCreateAsync(
   cacheKey,
  () => FetchUserFromDatabase(userId),
        TimeSpan.FromMinutes(30)
    );
  }
}
```

## API Reference

### Basic Operations

#### GetAsync<T>
Retrieves a cached item by key.

```csharp
var user = await _cacheManager.GetAsync<UserData>("user:123");
if (user == null)
{
    // Item not in cache
}
```

#### SetAsync<T>
Stores an item in cache with optional expiration.

```csharp
// With relative expiration
await _cacheManager.SetAsync("user:123", userData, TimeSpan.FromMinutes(30));

// With absolute expiration
await _cacheManager.SetAsync("user:123", userData, DateTimeOffset.UtcNow.AddHours(1));

// No expiration (use default or never expire)
await _cacheManager.SetAsync("user:123", userData);
```

#### RemoveAsync
Removes an item from cache.

```csharp
await _cacheManager.RemoveAsync("user:123");
```

#### ExistsAsync
Checks if an item exists in cache.

```csharp
var exists = await _cacheManager.ExistsAsync("user:123");
```

### Advanced Operations

#### GetOrCreateAsync<T>
Gets an item from cache or creates it using a factory function if not found.

```csharp
var userData = await _cacheManager.GetOrCreateAsync(
    "user:123",
    async () => await FetchUserFromDatabase("123"),
    TimeSpan.FromMinutes(30)
);
```

#### RefreshAsync
Refreshes a cached item (useful for sliding expiration).

```csharp
await _cacheManager.RefreshAsync("user:123");
```

#### ClearAsync
Clears all cached items (use with caution in distributed scenarios).

```csharp
await _cacheManager.ClearAsync();
```

#### GetStatisticsAsync
Gets cache performance statistics.

```csharp
var stats = await _cacheManager.GetStatisticsAsync();
Console.WriteLine($"Cache Type: {stats.CacheType}");
Console.WriteLine($"Hit Ratio: {stats.HitRatio:P2}");
Console.WriteLine($"Items: {stats.ItemCount}");
```

## Configuration Options

### CacheOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Provider` | string | "Memory" | Cache provider: "Memory" or "Redis" |
| `DefaultExpirationMinutes` | int | 30 | Default expiration time in minutes |
| `Memory` | MemoryCacheOptions | - | Memory cache specific options |
| `Redis` | RedisOptions | - | Redis cache specific options |

### MemoryCacheOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SizeLimitMB` | int | 100 | Maximum cache size in MB (0 = unlimited) |
| `CompactionPercentage` | double | 0.25 | Memory compaction threshold (0.0-1.0) |
| `ExpirationScanFrequencySeconds` | int | 60 | Cleanup scan frequency in seconds |

### RedisOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Endpoint` | string | null | Redis endpoint (required for Redis) |
| `InstanceName` | string | "SXG-EvalPlatform" | Redis instance name prefix |
| `UseManagedIdentity` | bool | true | Use Azure managed identity |
| `ConnectTimeoutSeconds` | int | 30 | Connection timeout |
| `CommandTimeoutSeconds` | int | 30 | Command timeout |
| `UseSsl` | bool | true | Enable SSL connection |
| `Retry` | RedisRetryOptions | - | Retry configuration |

## Cache Patterns

### 1. Cache-Aside Pattern
Most common pattern where you check cache first, then load from source if not found.

```csharp
public async Task<EvaluationResult> GetEvaluationResultAsync(string evalRunId)
{
    var cacheKey = $"eval_result:{evalRunId}";
    
    // Try cache first
  var cached = await _cacheManager.GetAsync<EvaluationResult>(cacheKey);
    if (cached != null)
     return cached;
    
    // Load from source
    var result = await LoadFromStorage(evalRunId);
    
    // Cache the result
 if (result != null)
  {
        await _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));
 }
    
    return result;
}
```

### 2. Write-Through Pattern
Update cache when data is modified.

```csharp
public async Task<bool> UpdateUserAsync(string userId, UserData userData)
{
    var success = await SaveToDatabase(userId, userData);
    
    if (success)
    {
        var cacheKey = $"user:{userId}";
      await _cacheManager.SetAsync(cacheKey, userData, TimeSpan.FromMinutes(30));
    }
    
    return success;
}
```

### 3. Write-Behind Pattern
Invalidate cache when data is modified.

```csharp
public async Task<bool> UpdateUserAsync(string userId, UserData userData)
{
    var success = await SaveToDatabase(userId, userData);
    
    if (success)
    {
   var cacheKey = $"user:{userId}";
        await _cacheManager.RemoveAsync(cacheKey);
    }
    
    return success;
}
```

### 4. Bulk Operations

```csharp
public async Task<Dictionary<string, MetricData>> GetMetricsAsync(IEnumerable<string> metricIds)
{
    var results = new Dictionary<string, MetricData>();
    var uncachedIds = new List<string>();
    
 // Check cache for each metric
    foreach (var id in metricIds)
    {
        var cached = await _cacheManager.GetAsync<MetricData>($"metric:{id}");
        if (cached != null)
      {
    results[id] = cached;
        }
        else
        {
     uncachedIds.Add(id);
        }
    }
    
    // Load uncached metrics
    if (uncachedIds.Any())
    {
 var freshMetrics = await LoadMetricsFromDatabase(uncachedIds);
  
        foreach (var (id, data) in freshMetrics)
        {
 await _cacheManager.SetAsync($"metric:{id}", data, TimeSpan.FromMinutes(15));
    results[id] = data;
        }
  }
    
    return results;
}
```

## Best Practices

### 1. Key Naming
Use consistent, hierarchical key naming:

```csharp
// Good
"user:{userId}"
"eval_result:{evalRunId}"
"metric:{metricId}:daily"
"session:{sessionId}:preferences"

// Avoid
"UserData123"
"result"
"temp_cache_key"
```

### 2. Expiration Strategy
Set appropriate expiration times based on data volatility:

```csharp
// Static reference data - longer expiration
await _cacheManager.SetAsync("config:metrics", config, TimeSpan.FromHours(12));

// User session data - medium expiration
await _cacheManager.SetAsync($"session:{sessionId}", session, TimeSpan.FromMinutes(30));

// Frequently changing data - short expiration
await _cacheManager.SetAsync($"stats:{userId}", stats, TimeSpan.FromMinutes(5));
```

### 3. Error Handling
Always handle cache failures gracefully:

```csharp
public async Task<UserData> GetUserAsync(string userId)
{
    try
 {
 var cached = await _cacheManager.GetAsync<UserData>($"user:{userId}");
        if (cached != null) return cached;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Cache get failed for user {UserId}, falling back to database", userId);
    }
    
    // Always have a fallback
    return await LoadUserFromDatabase(userId);
}
```

### 4. Monitoring
Regularly check cache performance:

```csharp
public async Task<object> GetCacheHealthAsync()
{
    var stats = await _cacheManager.GetStatisticsAsync();
    
    return new
    {
      CacheType = stats.CacheType,
      HitRatio = stats.HitRatio,
        ItemCount = stats.ItemCount,
        IsHealthy = stats.HitRatio > 0.7 // 70% hit rate threshold
    };
}
```

## Azure Redis Cache Setup

### 1. Create Azure Redis Cache
```bash
# Create resource group
az group create --name MyResourceGroup --location eastus

# Create Redis cache
az redis create --resource-group MyResourceGroup \
    --name MyRedisCache \
    --location eastus \
    --sku Premium \
    --vm-size P1
```

### 2. Configure Managed Identity
```bash
# Enable system-assigned managed identity for your App Service
az webapp identity assign --resource-group MyResourceGroup --name MyWebApp

# Grant Redis cache access to the managed identity
az role assignment create \
  --assignee $(az webapp identity show --resource-group MyResourceGroup --name MyWebApp --query principalId -o tsv) \
  --role "Redis Cache Contributor" \
    --scope $(az redis show --resource-group MyResourceGroup --name MyRedisCache --query id -o tsv)
```

### 3. Update Configuration
```json
{
  "Cache": {
  "Provider": "Redis",
    "Redis": {
      "Endpoint": "MyRedisCache.redis.cache.windows.net:6380",
      "UseManagedIdentity": true
    }
  }
}
```

## Troubleshooting

### Common Issues

#### 1. Redis Connection Failures
```
InvalidOperationException: Failed to connect to Redis cache
```
**Solution:** Verify endpoint, check firewall rules, ensure managed identity permissions.

#### 2. Memory Cache Size Limits
```
Memory pressure detected, compacting cache
```
**Solution:** Increase `SizeLimitMB` or reduce cache expiration times.

#### 3. Serialization Errors
```
JsonException: Unable to deserialize cached value
```
**Solution:** Ensure cached objects are serializable, check for circular references.

### Performance Tips

1. **Batch Operations**: Use bulk operations when possible
2. **Appropriate Expiration**: Don't cache data longer than necessary
3. **Key Patterns**: Use consistent key naming for easier management
4. **Monitor Hit Ratio**: Aim for >70% hit ratio for optimal performance
5. **Size Limits**: Set reasonable size limits for memory cache

## Security Considerations

### 1. Managed Identity Benefits
- No secrets in configuration
- Automatic token rotation
- Azure RBAC integration
- Audit trail

### 2. Data Sensitivity
- Don't cache sensitive data without encryption
- Consider data residency requirements
- Implement appropriate access controls
- Use cache expiration for sensitive data

### 3. Connection Security
- Always use SSL for Redis connections
- Implement proper timeout settings
- Use VNet integration when possible
- Monitor for unusual access patterns

## License

Microsoft Corporation. All rights reserved.