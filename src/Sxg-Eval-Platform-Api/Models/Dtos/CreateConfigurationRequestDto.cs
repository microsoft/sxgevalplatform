using System.ComponentModel.DataAnnotations;

namespace SxgEvalPlatformApi.Models.Dtos
{
    /// <summary>
    /// Data transfer object for creating new metrics configuration (without ConfigurationId)
    /// </summary>
    public class CreateConfigurationRequestDto
    {
        [Required]
        public string AgentId { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string ConfigurationName { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = "Production";

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public IList<SelectedMetricsConfigurationDto> MetricsConfiguration { get; set; } = new List<SelectedMetricsConfigurationDto>();

        [Required]
        public UserMetadataDto UserMetadata { get; set; } = new UserMetadataDto();
    }
}