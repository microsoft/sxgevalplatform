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
    /// Get detailed health status with OpenTelemetry information
    /// </summary>
    /// <returns>Detailed health information including telemetry data</returns>
    [HttpGet("detailed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetDetailedHealth()
    {
        using var activity = _telemetryService.StartActivity("Health.DetailedCheck");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            activity?.SetTag("operation", "DetailedHealthCheck");

            var detailedStatus = new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Version = "1.0.0",
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                MachineName = Environment.MachineName,
                ProcessId = Environment.ProcessId,
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

            _logger.LogInformation("Detailed health check completed successfully");

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
}