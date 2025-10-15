namespace SxgEvalPlatformApi.Models.Dtos;

/// <summary>
/// Data transfer object for configuration save response
/// </summary>
public class ConfigurationSaveResponseDto
{
    public string ConfigId { get; set; } = string.Empty;
    
    public string Status { get; set; } = string.Empty;
    
    public string Message { get; set; } = string.Empty;
}
