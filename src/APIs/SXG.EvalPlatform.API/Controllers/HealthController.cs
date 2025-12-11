using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.Services;
using System.Diagnostics;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Health check controller for monitoring API status with OpenTelemetry
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly IOpenTelemetryService _telemetryService;

    public HealthController(ILogger<HealthController> logger, IOpenTelemetryService telemetryService)
    {
        _logger = logger;
        _telemetryService = telemetryService;
    }

    /// <summary>
    /// Get API health status with telemetry tracking
    /// </summary>
    /// <returns>Health status information</returns>
    [HttpGet]
    [ProducesResponseType(typeof(HealthStatusDto), StatusCodes.Status200OK)]
    public ActionResult<HealthStatusDto> GetHealth()
    {
        using var activity = _telemetryService.StartActivity("Health.Check");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Health check requested");

            activity?.SetTag("operation", "HealthCheck");
            activity?.SetTag("endpoint", "/api/v1/health");

            var healthStatus = new HealthStatusDto
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
            };

            // Record custom metric for health check
            _telemetryService.RecordMetric("health_check_duration", stopwatch.Elapsed.TotalSeconds,
                new Dictionary<string, object>
                {
                    ["status"] = "healthy",
                    ["endpoint"] = "/api/v1/health"
                });

            activity?.SetTag("success", true);
            activity?.SetTag("status", "healthy");
            activity?.SetTag("environment", healthStatus.Environment);

            _logger.LogInformation("Health check completed successfully in {Duration}ms", stopwatch.ElapsedMilliseconds);

            return Ok(healthStatus);
        }
        catch (Exception ex)
        {
            activity?.SetTag("success", false);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);

            _logger.LogError(ex, "Health check failed");

            return StatusCode(500, new HealthStatusDto
            {
                Status = "Unhealthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Environment = "Error"
            });
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Get detailed health status with dependency checks
    /// </summary>
    /// <returns>Detailed health information including dependency status</returns>
    [HttpGet("detailed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetDetailedHealth()
    {
        using var activity = _telemetryService.StartActivity("Health.DetailedCheck");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            activity?.SetTag("operation", "DetailedHealthCheck");

            var dependencyChecks = await CheckDependenciesAsync();
            var isHealthy = dependencyChecks.All(d => d.IsHealthy);

            var detailedStatus = new
            {
                Status = isHealthy ? "Healthy" : "Degraded",
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Version = "1.0.0",
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                MachineName = Environment.MachineName,
                ProcessId = Environment.ProcessId,
                Dependencies = dependencyChecks.Select(d => new
                {
                    Name = d.Name,
                    Status = d.IsHealthy ? "Healthy" : "Unhealthy",
                    ResponseTime = d.ResponseTime,
                    ErrorMessage = d.ErrorMessage
                }).ToArray(),
                OpenTelemetry = new
                {
                    Enabled = true,
                    TraceId = Activity.Current?.TraceId.ToString(),
                    SpanId = Activity.Current?.SpanId.ToString(),
                    ActivityId = Activity.Current?.Id
                },
                ApplicationInsights = new
                {
                    Enabled = true,
                    ServiceName = "SXG-EvalPlatform-API"
                }
            };

            activity?.SetTag("success", true);
            activity?.SetTag("machine_name", Environment.MachineName);
            activity?.SetTag("process_id", Environment.ProcessId);
            activity?.SetTag("overall_health", isHealthy ? "healthy" : "degraded");
            activity?.SetTag("dependency_count", dependencyChecks.Count);

            _logger.LogInformation("Detailed health check completed - Overall: {Status}, Dependencies: {HealthyCount}/{TotalCount}",
                isHealthy ? "Healthy" : "Degraded",
                dependencyChecks.Count(d => d.IsHealthy),
                dependencyChecks.Count);

            return Ok(detailedStatus);
        }
        catch (Exception ex)
        {
            activity?.SetTag("success", false);
            activity?.SetTag("error.message", ex.Message);

            _logger.LogError(ex, "Detailed health check failed");

            return StatusCode(500, new
            {
                Status = "Unhealthy",
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Error = ex.Message
            });
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Check all system dependencies
    /// </summary>
    private async Task<List<DependencyHealthCheckDTO>> CheckDependenciesAsync()
    {
        var checks = new List<DependencyHealthCheckDTO>();
        var checkTasks = new List<Task<DependencyHealthCheckDTO>>
        {
            CheckAzureBlobStorageAsync(),
            CheckAzureTableStorageAsync(),
            CheckCacheAsync(),
            CheckApplicationInsightsAsync()
        };

        try
        {
            var results = await Task.WhenAll(checkTasks);
            checks.AddRange(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during dependency health checks");
            // Add a failed check for the exception
            checks.Add(new DependencyHealthCheckDTO
            {
                Name = "DependencyChecks",
                IsHealthy = false,
                ErrorMessage = ex.Message,
                ResponseTime = TimeSpan.Zero
            });
        }

        return checks;
    }

    /// <summary>
    /// Check Azure Blob Storage connectivity
    /// </summary>
    private async Task<DependencyHealthCheckDTO> CheckAzureBlobStorageAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Get blob service from DI container if available
            var blobService = HttpContext.RequestServices.GetService<Sxg.EvalPlatform.API.Storage.Services.IAzureBlobStorageService>();

            if (blobService == null)
            {
                return new DependencyHealthCheckDTO
                {
                    Name = "AzureBlobStorage",
                    IsHealthy = false,
                    ErrorMessage = "Blob storage service not registered",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            // Try to access a known container (platform-configurations)
            var configHelper = HttpContext.RequestServices.GetService<Sxg.EvalPlatform.API.Storage.IConfigHelper>();
            if (configHelper != null)
            {
                var containerName = configHelper.GetPlatformConfigurationsContainer();
                // Simple check - just try to get container properties without reading actual data
                var exists = await blobService.BlobExistsAsync(containerName, "health-check-test.txt");
                // This will succeed or fail based on connectivity, not file existence
            }

            return new DependencyHealthCheckDTO
            {
                Name = "AzureBlobStorage",
                IsHealthy = true,
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Blob Storage health check failed");
            return new DependencyHealthCheckDTO
            {
                Name = "AzureBlobStorage",
                IsHealthy = false,
                ErrorMessage = ex.Message,
                ResponseTime = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Check Azure Table Storage connectivity
    /// </summary>
    private async Task<DependencyHealthCheckDTO> CheckAzureTableStorageAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Get table service from DI container if available
            var tableService = HttpContext.RequestServices.GetService<Sxg.EvalPlatform.API.Storage.Services.IMetricsConfigTableService>();

            if (tableService == null)
            {
                return new DependencyHealthCheckDTO
                {
                    Name = "AzureTableStorage",
                    IsHealthy = false,
                    ErrorMessage = "Table storage service not registered",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            // Simple connectivity test - this will verify table access
            // Just checking if we can access the table, not retrieving data
            await tableService.GetAllMetricsConfigurations("health-check", "test");

            return new DependencyHealthCheckDTO
            {
                Name = "AzureTableStorage",
                IsHealthy = true,
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Table Storage health check failed");
            return new DependencyHealthCheckDTO
            {
                Name = "AzureTableStorage",
                IsHealthy = false,
                ErrorMessage = ex.Message,
                ResponseTime = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Check Cache connectivity - tests Redis directly when configured
    /// </summary>
    private async Task<DependencyHealthCheckDTO> CheckCacheAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Get cache configuration to determine provider type
            var configuration = HttpContext.RequestServices.GetService<IConfiguration>();
            if (configuration == null)
            {
                return new DependencyHealthCheckDTO
                {
                    Name = "Cache",
                    IsHealthy = false,
                    ErrorMessage = "Configuration service not available",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            var cacheProvider = configuration.GetValue<string>("Cache:Provider")?.ToLowerInvariant() ?? "memory";

            if (cacheProvider == "redis" || cacheProvider == "distributed")
            {
                return await CheckRedisCacheDirectlyAsync(stopwatch);
            }
            else
            {
                return await CheckMemoryCacheAsync(stopwatch);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache health check failed");
            return new DependencyHealthCheckDTO
            {
                Name = "Cache",
                IsHealthy = false,
                ErrorMessage = ex.Message,
                ResponseTime = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Test Redis cache directly using IConnectionMultiplexer with comprehensive diagnostics
    /// </summary>
    private async Task<DependencyHealthCheckDTO> CheckRedisCacheDirectlyAsync(Stopwatch stopwatch)
    {
        var diagnostics = new List<string>();
        
        try
        {
            // Get configuration for diagnostics
            var configuration = HttpContext.RequestServices.GetService<IConfiguration>();
            if (configuration != null)
            {
                diagnostics.Add("=== Redis Configuration ===");
                diagnostics.Add($"Provider: {configuration.GetValue<string>("Cache:Provider")}");
                diagnostics.Add($"Endpoint: {configuration.GetValue<string>("Cache:Redis:Endpoint")}");
                diagnostics.Add($"InstanceName: {configuration.GetValue<string>("Cache:Redis:InstanceName")}");
                diagnostics.Add($"UseManagedIdentity: {configuration.GetValue<bool>("Cache:Redis:UseManagedIdentity")}");
                diagnostics.Add($"UseSsl: {configuration.GetValue<bool>("Cache:Redis:UseSsl")}");
                diagnostics.Add($"ConnectTimeout: {configuration.GetValue<int>("Cache:Redis:ConnectTimeoutSeconds")}s");
                diagnostics.Add($"CommandTimeout: {configuration.GetValue<int>("Cache:Redis:CommandTimeoutSeconds")}s");
                diagnostics.Add($"Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");
                diagnostics.Add($"MachineName: {Environment.MachineName}");
                diagnostics.Add("");
            }

            // Get Redis connection multiplexer directly
            var connectionMultiplexer = HttpContext.RequestServices.GetService<StackExchange.Redis.IConnectionMultiplexer>();

            if (connectionMultiplexer == null)
            {
                diagnostics.Add("ERROR: Redis IConnectionMultiplexer not registered in DI container");
                _logger.LogError("Redis health check failed: ConnectionMultiplexer not registered");
            
                return new DependencyHealthCheckDTO
                {
                    Name = "Cache (Redis)",
                    IsHealthy = false,
                    ErrorMessage = "Redis connection multiplexer not registered or not connected",
                    ResponseTime = stopwatch.Elapsed,
                    AdditionalInfo = string.Join(Environment.NewLine, diagnostics)
                };
            }

            diagnostics.Add("=== Connection Status ===");
            diagnostics.Add($"IsConnected: {connectionMultiplexer.IsConnected}");
            diagnostics.Add($"IsConnecting: {connectionMultiplexer.IsConnecting}");
            
         // Get endpoint information
            var endpoints = connectionMultiplexer.GetEndPoints();
        diagnostics.Add($"Endpoints Count: {endpoints.Length}");
            foreach (var endpoint in endpoints)
  {
     diagnostics.Add($"  - {endpoint}");
            }
         diagnostics.Add("");
        // Check if Redis is connected
   if (!connectionMultiplexer.IsConnected)
            {
   diagnostics.Add("WARNING: Redis connection is not established");
        
     // Try to get connection status from each endpoint
 diagnostics.Add("=== Endpoint Status ===");
           foreach (var endpoint in endpoints)
      {
         try
        {
           var server = connectionMultiplexer.GetServer(endpoint);
                diagnostics.Add($"Endpoint {endpoint}:");
     diagnostics.Add($"  IsConnected: {server.IsConnected}");
        diagnostics.Add($"  IsReplica: {server.IsReplica}");
    }
               catch (Exception endpointEx)
            {
  diagnostics.Add($"  Error checking endpoint: {endpointEx.GetType().Name} - {endpointEx.Message}");
     }
     }
     diagnostics.Add("");
                diagnostics.Add("Note: Connection will retry in background. Cache operations may timeout until connected.");
      }

 diagnostics.Add("=== Performing Operations ===");
            var database = connectionMultiplexer.GetDatabase();
   var testKey = "health-check-" + Guid.NewGuid().ToString("N")[..8];
            var testValue = "health-test-" + DateTime.UtcNow.Ticks;

            diagnostics.Add($"Test Key: {testKey}");
   diagnostics.Add($"Test Value: {testValue}");
        
         // Test SET operation with timeout
        var setStartTime = Stopwatch.GetTimestamp();
            var setResult = await database.StringSetAsync(testKey, testValue, TimeSpan.FromMinutes(1));
 var setDuration = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - setStartTime);
          
  diagnostics.Add($"SET Operation: {(setResult ? "Success" : "Failed")} (Duration: {setDuration.TotalMilliseconds:F2}ms)");
            
 if (!setResult)
    {
      diagnostics.Add("ERROR: Redis SET operation failed");
        _logger.LogError("Redis health check failed: SET operation failed");
         
 return new DependencyHealthCheckDTO
   {
       Name = "Cache (Redis)",
            IsHealthy = false,
      ErrorMessage = "Redis SET operation failed",
  ResponseTime = stopwatch.Elapsed,
           AdditionalInfo = string.Join(Environment.NewLine, diagnostics)
 };
       }

         // Test GET operation
 var getStartTime = Stopwatch.GetTimestamp();
          var getValue = await database.StringGetAsync(testKey);
     var getDuration = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - getStartTime);
            
     diagnostics.Add($"GET Operation: {(getValue.HasValue ? "Success" : "Failed")} (Duration: {getDuration.TotalMilliseconds:F2}ms)");
            diagnostics.Add($"Retrieved Value Match: {getValue == testValue}");
            
  if (!getValue.HasValue || getValue != testValue)
            {
         diagnostics.Add($"ERROR: Retrieved value mismatch. Expected: {testValue}, Got: {(getValue.HasValue ? getValue.ToString() : "null")}");
    _logger.LogError("Redis health check failed: GET operation failed or value mismatch");
            
             return new DependencyHealthCheckDTO
        {
          Name = "Cache (Redis)",
          IsHealthy = false,
  ErrorMessage = "Redis GET operation failed or returned incorrect value",
       ResponseTime = stopwatch.Elapsed,
        AdditionalInfo = string.Join(Environment.NewLine, diagnostics)
     };
 }

            // Test DELETE operation
       var deleteStartTime = Stopwatch.GetTimestamp();
  var deleteResult = await database.KeyDeleteAsync(testKey);
         var deleteDuration = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - deleteStartTime);

      diagnostics.Add($"DELETE Operation: {(deleteResult ? "Success" : "Failed")} (Duration: {deleteDuration.TotalMilliseconds:F2}ms)");
   diagnostics.Add("");
            // Get Redis server info
diagnostics.Add("=== Server Information ===");
            var redisVersion = "Unknown";
  var clients = "Unknown";
        var memory = "Unknown";
         var uptime = "Unknown";
            var role = "Unknown";
            
   try
            {
       var server = connectionMultiplexer.GetServer(endpoints.First());
  var info = await server.InfoAsync("server");
   var statsInfo = await server.InfoAsync("stats");
     var clientsInfo = await server.InfoAsync("clients");
   var memoryInfo = await server.InfoAsync("memory");
           var replicationInfo = await server.InfoAsync("replication");

                var allInfo = info.Concat(statsInfo).Concat(clientsInfo).Concat(memoryInfo).Concat(replicationInfo)
        .SelectMany(group => group)
           .GroupBy(kvp => kvp.Key)
   .Select(g => g.First())
             .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

     redisVersion = allInfo.ContainsKey("redis_version") ? allInfo["redis_version"] : "Unknown";
         clients = allInfo.ContainsKey("connected_clients") ? allInfo["connected_clients"] : "Unknown";
           memory = allInfo.ContainsKey("used_memory_human") ? allInfo["used_memory_human"] : "Unknown";
    uptime = allInfo.ContainsKey("uptime_in_days") ? $"{allInfo["uptime_in_days"]} days" : "Unknown";
            role = allInfo.ContainsKey("role") ? allInfo["role"] : "Unknown";

 diagnostics.Add($"Redis Version: {redisVersion}");
                diagnostics.Add($"Role: {role}");
   diagnostics.Add($"Connected Clients: {clients}");
     diagnostics.Add($"Used Memory: {memory}");
            diagnostics.Add($"Uptime: {uptime}");
         
     if (allInfo.ContainsKey("maxmemory_human"))
         diagnostics.Add($"Max Memory: {allInfo["maxmemory_human"]}");
            if (allInfo.ContainsKey("total_commands_processed"))
      diagnostics.Add($"Total Commands Processed: {allInfo["total_commands_processed"]}");
if (allInfo.ContainsKey("instantaneous_ops_per_sec"))
       diagnostics.Add($"Ops/Sec: {allInfo["instantaneous_ops_per_sec"]}");
   }
   catch (Exception infoEx)
            {
             diagnostics.Add($"Warning: Failed to retrieve server info: {infoEx.GetType().Name} - {infoEx.Message}");
      _logger.LogWarning(infoEx, "Failed to parse Redis server info");
 }

            diagnostics.Add("");
      diagnostics.Add("=== Performance Metrics ===");
            diagnostics.Add($"Total Health Check Duration: {stopwatch.Elapsed.TotalMilliseconds:F2}ms");
         diagnostics.Add($"SET Duration: {setDuration.TotalMilliseconds:F2}ms");
  diagnostics.Add($"GET Duration: {getDuration.TotalMilliseconds:F2}ms");
       diagnostics.Add($"DELETE Duration: {deleteDuration.TotalMilliseconds:F2}ms");

       _logger.LogInformation("Redis health check successful");

  return new DependencyHealthCheckDTO
       {
          Name = "Cache (Redis)",
  IsHealthy = true,
            ResponseTime = stopwatch.Elapsed,
          AdditionalInfo = $"Version: {redisVersion}, Clients: {clients}, Memory: {memory}, Role: {role}, SET: {setDuration.TotalMilliseconds:F0}ms, GET: {getDuration.TotalMilliseconds:F0}ms, DELETE: {deleteDuration.TotalMilliseconds:F0}ms"
            };
        }
        catch (StackExchange.Redis.RedisTimeoutException ex)
        {
          diagnostics.Add("");
diagnostics.Add("=== EXCEPTION: RedisTimeoutException ===");
            diagnostics.Add($"Type: {ex.GetType().FullName}");
            diagnostics.Add($"Message: {ex.Message}");
 diagnostics.Add($"Source: {ex.Source}");
            diagnostics.Add($"HResult: 0x{ex.HResult:X8}");
         diagnostics.Add("");
   diagnostics.Add("Stack Trace:");
            diagnostics.Add(ex.StackTrace ?? "(no stack trace)");
            
            if (ex.InnerException != null)
        {
       diagnostics.Add("");
           diagnostics.Add($"Inner Exception Type: {ex.InnerException.GetType().FullName}");
                diagnostics.Add($"Inner Exception Message: {ex.InnerException.Message}");
          diagnostics.Add("Inner Exception Stack Trace:");
diagnostics.Add(ex.InnerException.StackTrace ?? "(no stack trace)");
      }

      if (ex.Data.Count > 0)
{
     diagnostics.Add("");
      diagnostics.Add("Exception Data:");
       foreach (var key in ex.Data.Keys)
         diagnostics.Add($"  {key}: {ex.Data[key]}");
            }
            
            var errorDetails = string.Join(Environment.NewLine, diagnostics);
            _logger.LogError(ex, "RedisTimeoutException during health check. Diagnostics: {Diagnostics}", errorDetails);
            
            return new DependencyHealthCheckDTO
            {
         Name = "Cache (Redis)",
                IsHealthy = false,
          ErrorMessage = $"Redis timeout: {ex.Message}",
        ResponseTime = stopwatch.Elapsed,
  AdditionalInfo = errorDetails
          };
        }
        catch (StackExchange.Redis.RedisConnectionException ex)
        {
      diagnostics.Add("");
      diagnostics.Add("=== EXCEPTION: RedisConnectionException ===");
            diagnostics.Add($"Type: {ex.GetType().FullName}");
            diagnostics.Add($"Message: {ex.Message}");
    diagnostics.Add($"Source: {ex.Source}");
          diagnostics.Add($"FailureType: {ex.FailureType}");
            diagnostics.Add($"HResult: 0x{ex.HResult:X8}");
     diagnostics.Add("");
    diagnostics.Add("Stack Trace:");
      diagnostics.Add(ex.StackTrace ?? "(no stack trace)");
   
     if (ex.InnerException != null)
   {
    diagnostics.Add("");
   diagnostics.Add($"Inner Exception Type: {ex.InnerException.GetType().FullName}");
    diagnostics.Add($"Inner Exception Message: {ex.InnerException.Message}");
  diagnostics.Add("Inner Exception Stack Trace:");
     diagnostics.Add(ex.InnerException.StackTrace ?? "(no stack trace)");
            }

      if (ex.Data.Count > 0)
            {
       diagnostics.Add("");
     diagnostics.Add("Exception Data:");
  foreach (var key in ex.Data.Keys)
        diagnostics.Add($"  {key}: {ex.Data[key]}");
    }
    
      var errorDetails = string.Join(Environment.NewLine, diagnostics);
            _logger.LogError(ex, "RedisConnectionException during health check. Diagnostics: {Diagnostics}", errorDetails);
            
   return new DependencyHealthCheckDTO
       {
      Name = "Cache (Redis)",
        IsHealthy = false,
 ErrorMessage = $"Redis connection failed: {ex.Message}",
           ResponseTime = stopwatch.Elapsed,
        AdditionalInfo = errorDetails
  };
        }
        catch (StackExchange.Redis.RedisException ex)
        {
   diagnostics.Add("");
 diagnostics.Add("=== EXCEPTION: RedisException ===");
        diagnostics.Add($"Type: {ex.GetType().FullName}");
            diagnostics.Add($"Message: {ex.Message}");
            diagnostics.Add($"Source: {ex.Source}");
      diagnostics.Add($"HResult: 0x{ex.HResult:X8}");
   diagnostics.Add("");
            diagnostics.Add("Stack Trace:");
  diagnostics.Add(ex.StackTrace ?? "(no stack trace)");
            
    if (ex.InnerException != null)
   {
       diagnostics.Add("");
   diagnostics.Add($"Inner Exception Type: {ex.InnerException.GetType().FullName}");
    diagnostics.Add($"Inner Exception Message: {ex.InnerException.Message}");
     diagnostics.Add("Inner Exception Stack Trace:");
                diagnostics.Add(ex.InnerException.StackTrace ?? "(no stack trace)");
  }

            if (ex.Data.Count > 0)
            {
diagnostics.Add("");
       diagnostics.Add("Exception Data:");
       foreach (var key in ex.Data.Keys)
          diagnostics.Add($"  {key}: {ex.Data[key]}");
   }
         
            var errorDetails = string.Join(Environment.NewLine, diagnostics);
  _logger.LogError(ex, "RedisException during health check. Diagnostics: {Diagnostics}", errorDetails);
        
 return new DependencyHealthCheckDTO
  {
    Name = "Cache (Redis)",
        IsHealthy = false,
             ErrorMessage = $"Redis error: {ex.Message}",
 ResponseTime = stopwatch.Elapsed,
            AdditionalInfo = errorDetails
       };
        }
        catch (Exception ex)
        {
            diagnostics.Add("");
diagnostics.Add("=== EXCEPTION: Unexpected Error ===");
    diagnostics.Add($"Exception Type: {ex.GetType().FullName}");
       diagnostics.Add($"Message: {ex.Message}");
   diagnostics.Add($"Source: {ex.Source}");
   diagnostics.Add($"HResult: 0x{ex.HResult:X8}");
      diagnostics.Add("");
   diagnostics.Add("Stack Trace:");
     diagnostics.Add(ex.StackTrace ?? "(no stack trace)");
         
       if (ex.InnerException != null)
            {
              diagnostics.Add("");
         diagnostics.Add($"Inner Exception Type: {ex.InnerException.GetType().FullName}");
 diagnostics.Add($"Inner Exception Message: {ex.InnerException.Message}");
      diagnostics.Add("Inner Exception Stack Trace:");
    diagnostics.Add(ex.InnerException.StackTrace ?? "(no stack trace)");
          }
        
 if (ex.Data.Count > 0)
    {
    diagnostics.Add("");
                diagnostics.Add("Exception Data:");
 foreach (var key in ex.Data.Keys)
        diagnostics.Add($"  {key}: {ex.Data[key]}");
        }
    
            var errorDetails = string.Join(Environment.NewLine, diagnostics);
    _logger.LogError(ex, "Unexpected exception during Redis health check. Diagnostics: {Diagnostics}", errorDetails);
            
          return new DependencyHealthCheckDTO
            {
       Name = "Cache (Redis)",
    IsHealthy = false,
       ErrorMessage = $"Redis error: {ex.Message}",
                ResponseTime = stopwatch.Elapsed,
    AdditionalInfo = errorDetails
       };
      }
    }

    /// <summary>
    /// Test Memory cache using ICacheManager
    /// </summary>
    private async Task<DependencyHealthCheckDTO> CheckMemoryCacheAsync(Stopwatch stopwatch)
    {
        try
        {
            // For memory cache, we can safely use ICacheManager since it's local
            var cacheManager = HttpContext.RequestServices.GetService<Sxg.EvalPlatform.API.Storage.Services.ICacheManager>();

            if (cacheManager == null)
            {
                return new DependencyHealthCheckDTO
                {
                    Name = "Cache (Memory)",
                    IsHealthy = false,
                    ErrorMessage = "Memory cache manager not registered",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            // Test cache with a simple operation
            var testKey = "health-check-" + Guid.NewGuid().ToString("N")[..8];
            var testValue = new { timestamp = DateTime.UtcNow, check = "health" };

            // Test SET operation
            await cacheManager.SetAsync(testKey, testValue, TimeSpan.FromMinutes(1));

            // Test GET operation
            var retrieved = await cacheManager.GetAsync<object>(testKey);

            // Verify the value was retrieved correctly
            if (retrieved == null)
            {
                return new DependencyHealthCheckDTO
                {
                    Name = "Cache (Memory)",
                    IsHealthy = false,
                    ErrorMessage = "Memory cache GET operation returned null",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            // Clean up
            await cacheManager.RemoveAsync(testKey);

            // Get cache statistics
            var stats = await cacheManager.GetStatisticsAsync();

            return new DependencyHealthCheckDTO
            {
                Name = "Cache (Memory)",
                IsHealthy = true,
                ResponseTime = stopwatch.Elapsed,
                AdditionalInfo = $"HitRatio: {stats.HitRatio:P1}, Items: {stats.ItemCount}"
            };
        }
        catch (Exception ex)
        {
            return new DependencyHealthCheckDTO
            {
                Name = "Cache (Memory)",
                IsHealthy = false,
                ErrorMessage = ex.Message,
                ResponseTime = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Check Application Insights connectivity
    /// </summary>
    private async Task<DependencyHealthCheckDTO> CheckApplicationInsightsAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Test telemetry service
            var telemetryService = HttpContext.RequestServices.GetService<IOpenTelemetryService>();

            if (telemetryService == null)
            {
                return new DependencyHealthCheckDTO
                {
                    Name = "ApplicationInsights",
                    IsHealthy = false,
                    ErrorMessage = "Telemetry service not registered",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            // Test telemetry recording
            telemetryService.RecordMetric("health_check_test", 1.0, new Dictionary<string, object>
            {
                ["source"] = "health_check",
                ["timestamp"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            });

            return new DependencyHealthCheckDTO
            {
                Name = "ApplicationInsights",
                IsHealthy = true,
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Application Insights health check failed");
            return new DependencyHealthCheckDTO
            {
                Name = "ApplicationInsights",
                IsHealthy = false,
                ErrorMessage = ex.Message,
                ResponseTime = stopwatch.Elapsed
            };
        }
    }
}