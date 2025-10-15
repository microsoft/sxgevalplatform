using System.ComponentModel.DataAnnotations;

namespace SxgEvalPlatformApi.Models.Dtos
{
    public class CreateMetricsConfigurationDto
    {
        public string? ConfigurationId { get; set; } = null;

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
    }
}
