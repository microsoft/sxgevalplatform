namespace SxgEvalPlatformApi.Models.Dtos;

/// <summary>
/// Data transfer object for configuration save response
/// </summary>
public class ConfigurationSaveResponseDto
{
    public string ConfigurationId { get; set; } = string.Empty;
    
    public string Status { get; set; } = string.Empty;
    
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Configuration conflict response for 409 errors
/// </summary>
public class ConfigurationConflictResponseDto
{
    public string Status { get; set; } = "conflict";
    public string Message { get; set; } = string.Empty;
    public string ExistingConfigurationId { get; set; } = string.Empty;
}
