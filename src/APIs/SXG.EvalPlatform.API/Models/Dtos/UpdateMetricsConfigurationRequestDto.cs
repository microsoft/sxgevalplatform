using System.ComponentModel.DataAnnotations;

namespace SxgEvalPlatformApi.Models.Dtos
{
    public class UpdateMetricsConfigurationRequestDto: CreateConfigurationRequestDto
    {
        [Required]
        public string ConfigurationId { get; set; } = null;
                
    }
}