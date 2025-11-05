namespace SxgEvalPlatformApi.Models.Dtos;

/// <summary>
/// Data transfer object for configuration save response
/// </summary>
public class ConfigurationSaveResponseDto
{
    public string ConfigurationId { get; set; } = string.Empty;
    
    public string Status { get; set; } = string.Empty;
    
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedOn { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime LastUpdatedOn { get; set; }

    public string LastUpdatedBy { get; set; } = string.Empty;
}
