using System.ComponentModel.DataAnnotations;
using Azure;
using Azure.Data.Tables;

namespace SxgEvalPlatformApi.Models;

/// <summary>
/// Enumeration for evaluation run status
/// </summary>
public enum EvalRunStatus
{
    Queued,
    Running,
    Completed,
    Failed
}

/// <summary>
/// Constants for evaluation run status values
/// </summary>
public static class EvalRunStatusConstants
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

/// <summary>
/// Azure Table entity for evaluation run data
/// </summary>
public class EvalRunEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "EvalRun";
    public string RowKey { get; set; } = string.Empty; // Will be the EvalRunId.ToString()
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    public Guid EvalRunId { get; set; } = Guid.Empty;
    public string MetricsConfigurationId { get; set; } = string.Empty;
    public string DataSetId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string Status { get; set; } = EvalRunStatusConstants.Queued;
    public string? LastUpdatedBy { get; set; }
    public DateTime? LastUpdatedOn { get; set; }
    public DateTime? StartedDatetime { get; set; }
    public DateTime? CompletedDatetime { get; set; }
    public string? BlobFilePath { get; set; }
    public string? ContainerName { get; set; } // Add container name property
}

/// <summary>
/// DTO for evaluation run data
/// </summary>
public class EvalRunDto
{
    public Guid EvalRunId { get; set; } = Guid.Empty;
    public string MetricsConfigurationId { get; set; } = string.Empty;
    public string DataSetId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string Status { get; set; } = EvalRunStatusConstants.Queued;
    public string? LastUpdatedBy { get; set; }
    public DateTime? LastUpdatedOn { get; set; }
    public DateTime? StartedDatetime { get; set; }
    public DateTime? CompletedDatetime { get; set; }
    // Note: BlobFilePath and ContainerName are internal details and not exposed to API consumers
}

/// <summary>
/// Data transfer object for creating a new evaluation run
/// </summary>
public class CreateEvalRunDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string AgentId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string DataSetId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string MetricsConfigurationId { get; set; } = string.Empty;
}

/// <summary>
/// Data transfer object for updating evaluation run status
/// </summary>
public class UpdateEvalRunStatusDto
{
    // EvalRunId will be set from route parameter, not required in request body
    public Guid EvalRunId { get; set; } = Guid.Empty;
    
    [Required]
    public string Status { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string AgentId { get; set; } = string.Empty;
}

/// <summary>
/// Simplified DTO for updating evaluation run status via PUT endpoint
/// Only requires the new status - evalRunId comes from route parameter
/// </summary>
public class UpdateStatusDto
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Simple response for update operations
/// </summary>
public class UpdateResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}