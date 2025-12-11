using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;

namespace SxgEvalPlatformApi.Models;

/// <summary>
/// Data transfer object for evaluation configuration
/// </summary>
public class EvaluationConfigurationDto
{
    public string? ConfigId { get; set; }
    
    [Required]
    public string AgentId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string ConfigName { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    [Required]
    public DatasetDto Dataset { get; set; } = new();
    
    [Required]
    public List<EvaluatorDto> Evaluators { get; set; } = new();
    
    [Range(0.0, 1.0)]
    public double PassingThreshold { get; set; }
    
    public string? CreatedBy { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? LastUpdated { get; set; }
}

/// <summary>
/// Data transfer object for dataset information
/// </summary>
public class DatasetDto
{
    [Required]
    public string Type { get; set; } = string.Empty;
    
    [Required]
    public string Source { get; set; } = string.Empty;
    
    public int? Size { get; set; }
}

/// <summary>
/// Data transfer object for evaluator information
/// </summary>
public class EvaluatorDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string Type { get; set; } = string.Empty;
    
    [Range(0.0, 1.0)]
    public double Weight { get; set; }
}

/// <summary>
/// Data transfer object for configurations retrieval response
/// </summary>
public class ConfigurationsResponseDto
{
    public string AgentId { get; set; } = string.Empty;
    
    public List<ConfigurationSummaryDto> Configurations { get; set; } = new();
}

/// <summary>
/// Data transfer object for configuration summary
/// </summary>
public class ConfigurationSummaryDto
{
    public string ConfigId { get; set; } = string.Empty;
    
    public string ConfigName { get; set; } = string.Empty;
    
    public DatasetSummaryDto Dataset { get; set; } = new();
    
    public List<string> Evaluators { get; set; } = new();
    
    public double PassingThreshold { get; set; }
    
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Data transfer object for dataset summary
/// </summary>
public class DatasetSummaryDto
{
    public string Type { get; set; } = string.Empty;
    
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// Azure Table entity for configuration metadata
/// </summary>
public class ConfigurationMetadataEntity : ITableEntity
{
    /// <summary>
    /// Configuration ID - GUID generated automatically
    /// </summary>
    public string ConfigurationId { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of configuration: PlatformConfiguration or ApplicationConfiguration
    /// </summary>
    public string ConfigurationType { get; set; } = string.Empty;
    
    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime LastUpdatedOn { get; set; }
    
    /// <summary>
    /// Container name where blob is stored
    /// </summary>
    public string ContainerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Blob file path including folder and file name
    /// </summary>
    public string BlobFilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Agent ID
    /// </summary>
    public string AgentId { get; set; } = string.Empty;
    
    /// <summary>
    /// Configuration name
    /// </summary>
    public string ConfigurationName { get; set; } = string.Empty;
    
    // ITableEntity implementation
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

/// <summary>
/// Configuration types enum
/// </summary>
public static class ConfigurationTypes
{
    public const string PlatformConfiguration = "PlatformConfiguration";
    public const string ApplicationConfiguration = "ApplicationConfiguration";
}