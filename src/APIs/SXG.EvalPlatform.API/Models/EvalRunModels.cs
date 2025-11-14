using System.ComponentModel.DataAnnotations;
using Azure;
using Azure.Data.Tables;
using SXG.EvalPlatform.Common;

namespace SxgEvalPlatformApi.Models;


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
    public string Status { get; set; } = CommonConstants.EvalRunStatus.RequestSubmitted;
    public string? LastUpdatedBy { get; set; }
    public DateTime? LastUpdatedOn { get; set; }
    public DateTime? StartedDatetime { get; set; }
    public DateTime? CompletedDatetime { get; set; }
    public string? BlobFilePath { get; set; }
    public string? ContainerName { get; set; } // Add container name property
    public string Type { get; set; } = string.Empty;
    public string EnvironmentId { get; set; } = string.Empty;
    public string AgentSchemaName { get; set; } = string.Empty;
}


public class CreateEvalRunResponseDto: EvalRunDto
{
    public string DatasetEnrichementRequestStatus { get; set; }
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
    public string Status { get; set; } = CommonConstants.EvalRunStatus.RequestSubmitted;
    public string? LastUpdatedBy { get; set; }
    public DateTime? LastUpdatedOn { get; set; }
    public DateTime? StartedDatetime { get; set; }
    public DateTime? CompletedDatetime { get; set; }
    
    /// <summary>
    /// Name of the evaluation run
    /// </summary>
    public string EvalRunName { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the dataset used in this evaluation run
    /// </summary>
    public string DataSetName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the metrics configuration used in this evaluation run
    /// </summary>
    public string MetricsConfigurationName { get; set; } = string.Empty;

    // Note: BlobFilePath and ContainerName are internal details and not exposed to API consumers
}

/// <summary>
/// DTO for evaluation run status information
/// </summary>
public class EvalRunStatusDto
{
    public Guid EvalRunId { get; set; } = Guid.Empty;
    public string Status { get; set; } = CommonConstants.EvalRunStatus.RequestSubmitted;
    public string? LastUpdatedBy { get; set; }
    public DateTime? StartedDatetime { get; set; }
    public DateTime? CompletedDatetime { get; set; }
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
    public Guid DataSetId { get; set; }
    
    [Required]
    public Guid MetricsConfigurationId { get; set; }
    
    /// <summary>
    /// Type of agent/evaluation system (e.g., MCS, AI Foundery, SK)
    /// </summary>
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Type { get; set; } = string.Empty;

    
    /// <summary>
    /// Environment identifier where the agent is deployed
    /// </summary>
    [Required]
    public string EnvironmentId { get; set; }
    
    /// <summary>
    /// Schema name of the agent in the target environment
    /// </summary>
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string AgentSchemaName { get; set; } = string.Empty;

    [StringLength(200)]
    public string EvalRunName { get; set; } = string.Empty;

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