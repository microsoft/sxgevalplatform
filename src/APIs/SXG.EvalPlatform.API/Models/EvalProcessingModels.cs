using System.ComponentModel.DataAnnotations;

namespace SxgEvalPlatformApi.Models;

/// <summary>
/// Request model for evaluation processing queue messages
/// </summary>
public class EvalProcessingRequest
{
    /// <summary>
    /// Evaluation run ID that needs to be processed
    /// </summary>
    [Required]
    public Guid EvalRunId { get; set; }

    /// <summary>
    /// Metrics configuration ID for the evaluation
    /// </summary>
    [Required]
    public string MetricsConfigurationId { get; set; } = string.Empty;

    /// <summary>
    /// Enriched dataset ID (blob path identifier)
    /// </summary>
    [Required]
    public string EnrichedDatasetId { get; set; } = string.Empty;

    /// <summary>
    /// Original dataset ID used for the evaluation
    /// </summary>
    [Required]
    public string DatasetId { get; set; } = string.Empty;

    /// <summary>
    /// Agent ID that owns the evaluation run
    /// </summary>
    [Required]
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the processing was requested
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// Priority of the processing request (e.g., "High", "Normal", "Low")
    /// </summary>
    public string Priority { get; set; } = "Normal";

    /// <summary>
    /// Path to the enriched dataset
    /// </summary>
    public string? EnrichedDatasetPath { get; set; }

    /// <summary>
    /// Optional metadata for the processing request
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Response model for evaluation processing completion
/// </summary>
public class EvalProcessingResponse
{
    /// <summary>
    /// Evaluation run ID that was processed
    /// </summary>
    public Guid EvalRunId { get; set; }

    /// <summary>
    /// Agent ID that owns the evaluation run
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Status of the processing
    /// </summary>
    public string Status { get; set; } = string.Empty; // "Completed", "Failed", "InProgress"

    /// <summary>
    /// Timestamp when the processing was completed
    /// </summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Path to the evaluation results
    /// </summary>
    public string? ResultsPath { get; set; }

    /// <summary>
    /// Processing results or metrics data
    /// </summary>
    public Dictionary<string, object>? Results { get; set; }
}