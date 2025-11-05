namespace SxgEvalPlatformApi.Models.Dtos;

/// <summary>
/// Health status response data transfer object
/// </summary>
public class HealthStatusDto
{
    /// <summary>
    /// API health status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when health check was performed
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// API version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Current environment (Development, Staging, Production)
    /// </summary>
    public string Environment { get; set; } = string.Empty;
}