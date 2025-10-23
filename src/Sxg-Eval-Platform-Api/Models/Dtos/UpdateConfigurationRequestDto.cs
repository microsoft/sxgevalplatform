using System.ComponentModel.DataAnnotations;

namespace SxgEvalPlatformApi.Models.Dtos
{
    /// <summary>
    /// Data transfer object for updating existing metrics configuration via PUT
    /// </summary>
    public class UpdateConfigurationRequestDto
    {
        [Required]
        [MinLength(1)]
        public IList<SelectedMetricsConfigurationDto> MetricsConfiguration { get; set; } = new List<SelectedMetricsConfigurationDto>();
    }
}