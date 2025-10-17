namespace SxgEvalPlatformApi.Models.Dtos
{
    public class SelectedMetricsConfigurationDto
    {
        public required string MetricName { get; set; }
        public required string CategoryName { get; set; }
        public double Threshold { get; set; }
    }

    
}
