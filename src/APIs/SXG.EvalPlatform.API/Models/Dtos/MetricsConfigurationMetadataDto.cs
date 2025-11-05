using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SxgEvalPlatformApi.Models.Dtos;

/// <summary>
/// Data transfer object for creating evaluation configuration
/// </summary>
//[JsonConverter(typeof(Converters.CreateMetricsConfigurationDtoConverter))]
public class MetricsConfigurationMetadataDto
{
    [Required]
    public string AgentId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string ConfigurationName { get; set; } = string.Empty;

    [Required]
    public string EnvironmentName { get; set; } = string.Empty; 

    //public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Alias for ConfigurationName to maintain backward compatibility
    /// </summary>
    //[JsonIgnore]
    //public string ConfigName => ConfigurationName;

    public string ConfigurationId { get; set; } = string.Empty;


    [StringLength(500)]
    public string? Description { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    public string LastUpdatedBy { get; set; } = string.Empty;

    public DateTime LastUpdatedOn { get; set; } = DateTime.UtcNow;


}
