using System.ComponentModel.DataAnnotations;

namespace SxgEvalPlatformApi.Models;

/// <summary>
/// Request model for dataset enrichment queue messages
/// </summary>
public class DatasetEnrichmentRequest
{
    /// <summary>
    /// Evaluation run ID that requested the enrichment
    /// </summary>
    [Required]
    public Guid EvalRunId { get; set; }

    /// <summary>
    /// Dataset ID to be enriched
    /// </summary>
    [Required]
    public Guid DatasetId { get; set; }

    /// <summary>
    /// Agent ID that owns the dataset
    /// </summary>
    [Required]
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the enrichment was requested
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// Priority of the enrichment request (e.g., "High", "Normal", "Low")
    /// </summary>
    public string Priority { get; set; } = "Normal";

    /// <summary>
    /// Optional metadata for the enrichment request
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Response model for dataset enrichment completion
/// </summary>
public class DatasetEnrichmentResponse
{
    /// <summary>
    /// Evaluation run ID that requested the enrichment
    /// </summary>
    public Guid EvalRunId { get; set; }

    /// <summary>
    /// Dataset ID that was enriched
    /// </summary>
    public Guid DatasetId { get; set; }

    /// <summary>
    /// Agent ID that owns the dataset
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Status of the enrichment process
    /// </summary>
    public string Status { get; set; } = string.Empty; // "Completed", "Failed", "InProgress"

    /// <summary>
    /// Timestamp when the enrichment was completed
    /// </summary>
    public DateTime CompletedAt { get; set; }

    /// <summary>
    /// Error message if enrichment failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Path to the enriched dataset blob
    /// </summary>
    public string? EnrichedDatasetPath { get; set; }

    /// <summary>
    /// Additional processing results or metadata
    /// </summary>
    public Dictionary<string, object>? Results { get; set; }
}