using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models.Dtos;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Health check controller for monitoring API status
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get API health status
    /// </summary>
    /// <returns>Health status information</returns>
    [HttpGet]
    [ProducesResponseType(typeof(HealthStatusDto), StatusCodes.Status200OK)]
    public ActionResult<HealthStatusDto> GetHealth()
    {
        _logger.LogInformation("Health check requested");
        
        var healthStatus = new HealthStatusDto
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        };

        return Ok(healthStatus);
    }

   
}