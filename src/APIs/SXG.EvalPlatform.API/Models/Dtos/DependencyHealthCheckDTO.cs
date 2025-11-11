namespace SxgEvalPlatformApi.Models.Dtos
{
    public class DependencyHealthCheckDTO
    {
        public string Name { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public string? AdditionalInfo { get; set; }
    }
}
