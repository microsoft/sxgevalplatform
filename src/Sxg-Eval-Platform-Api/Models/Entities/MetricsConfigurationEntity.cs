using Azure;
using Azure.Data.Tables;
using System.Text.Json;

namespace SxgEvalPlatformApi.Models.Entities
{
    /// <summary>
    /// Entity class for storing metrics configuration in Azure Table Storage
    /// </summary>
    //public class MetricsConfigurationEntity : ITableEntity
    //{
    //    public string AgentId { get; set; } = string.Empty;
              
    //    public string ConfigurationName { get; set; } = string.Empty;
                
    //    public string EnvironmentName { get; set; } = string.Empty;

    //    public string? Description { get; set; }
                
    //    public IList<MetricsConfiguration> MetricsConfiguration { get; set; } = new List<MetricsConfiguration>();

    //    public string LastUpdatedBy { get; set; } = string.Empty;

    //    public DateTime LastUpdatedOn { get; set; } = DateTime.UtcNow;

    //    // Since IList<MetricsConfiguration> cannot be directly stored in Azure Table Storage,
    //    // we need to serialize it to JSON string
    //    public string MetricsConfigurationJson 
    //    { 
    //        get => JsonSerializer.Serialize(MetricsConfiguration, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    //        set => MetricsConfiguration = string.IsNullOrEmpty(value) 
    //            ? new List<MetricsConfiguration>() 
    //            : JsonSerializer.Deserialize<List<MetricsConfiguration>>(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) ?? new List<MetricsConfiguration>();
    //    }

    //    // ITableEntity implementation
    //    public string PartitionKey { get; set; } = string.Empty;
    //    public string RowKey { get; set; } = string.Empty;
    //    public DateTimeOffset? Timestamp { get; set; }
    //    public ETag ETag { get; set; }
    //}
}
