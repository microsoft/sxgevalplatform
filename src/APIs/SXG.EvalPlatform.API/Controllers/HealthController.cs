using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.Services;
using System.Diagnostics;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Health check controller for monitoring API status with OpenTelemetry
/// </summary>
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
    /// Test Redis cache directly using IConnectionMultiplexer
    /// </summary>
    private async Task<DependencyHealthCheckDTO> CheckRedisCacheDirectlyAsync(Stopwatch stopwatch)
    {
        try
        {
            // Get Redis connection multiplexer directly
            var connectionMultiplexer = HttpContext.RequestServices.GetService<StackExchange.Redis.IConnectionMultiplexer>();

            if (connectionMultiplexer == null)
            {
                return new DependencyHealthCheckDTO
                {
                    Name = "Cache (Redis)",
                    IsHealthy = false,
                    ErrorMessage = "Redis connection multiplexer not registered or not connected",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            // Check if Redis is connected
            if (!connectionMultiplexer.IsConnected)
            {
                return new DependencyHealthCheckDTO
                {
                    Name = "Cache (Redis)",
                    IsHealthy = false,
                    ErrorMessage = "Redis connection is not established",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            var database = connectionMultiplexer.GetDatabase();
            var testKey = "health-check-" + Guid.NewGuid().ToString("N")[..8];
            var testValue = "health-test-" + DateTime.UtcNow.Ticks;

            // Test SET operation directly on Redis
            var setResult = await database.StringSetAsync(testKey, testValue, TimeSpan.FromMinutes(1));
            if (!setResult)
            {
                return new DependencyHealthCheckDTO
                {
                    Name = "Cache (Redis)",
                    IsHealthy = false,
                    ErrorMessage = "Redis SET operation failed",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            // Test GET operation directly on Redis
            var getValue = await database.StringGetAsync(testKey);
            if (!getValue.HasValue || getValue != testValue)
            {
                return new DependencyHealthCheckDTO
                {
                    Name = "Cache (Redis)",
                    IsHealthy = false,
                    ErrorMessage = "Redis GET operation failed or returned incorrect value",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            // Test DELETE operation
            await database.KeyDeleteAsync(testKey);

            // Get Redis server info for additional details
            var server = connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints().First());
            var info = await server.InfoAsync("server");

            // Parse Redis info - it's grouped data
            var redisVersion = "Unknown";
            var clients = "Unknown";
            var memory = "Unknown";

            try
            {
                // Flatten the grouped data and search for specific keys
                var flatInfo = info.SelectMany(group => group).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                redisVersion = flatInfo.ContainsKey("redis_version") ? flatInfo["redis_version"] : "Unknown";
                clients = flatInfo.ContainsKey("connected_clients") ? flatInfo["connected_clients"] : "Unknown";
                memory = flatInfo.ContainsKey("used_memory_human") ? flatInfo["used_memory_human"] : "Unknown";
            }
            catch (Exception infoEx)
            {
                _logger.LogWarning(infoEx, "Failed to parse Redis server info");
            }

            return new DependencyHealthCheckDTO
            {
                Name = "Cache (Redis)",
                IsHealthy = true,
                ResponseTime = stopwatch.Elapsed,
                AdditionalInfo = $"Version: {redisVersion}, Clients: {clients}, Memory: {memory}"
            };
        }
        catch (StackExchange.Redis.RedisTimeoutException ex)
        {
            return new DependencyHealthCheckDTO
            {
                Name = "Cache (Redis)",
                IsHealthy = false,
                ErrorMessage = $"Redis timeout: {ex.Message}",
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (StackExchange.Redis.RedisConnectionException ex)
        {
            return new DependencyHealthCheckDTO
            {
                Name = "Cache (Redis)",
                IsHealthy = false,
                ErrorMessage = $"Redis connection failed: {ex.Message}",
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            return new DependencyHealthCheckDTO
            {
                Name = "Cache (Redis)",
                IsHealthy = false,
                ErrorMessage = $"Redis error: {ex.Message}",
                ResponseTime = stopwatch.Elapsed
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