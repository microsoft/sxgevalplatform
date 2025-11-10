using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SxgEvalPlatformApi.Models;

/// <summary>
/// Response model containing both metrics configuration and dataset content for an evaluation run
/// </summary>
public class EvalArtifactsDto
{
    public Guid EvalRunId { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public JsonElement? MetricsConfiguration { get; set; }
    public JsonElement? DatasetContent { get; set; }
    public DateTime? LastUpdated { get; set; }
}

/// <summary>
/// Response model containing only metrics configuration for an evaluation run
/// </summary>
public class MetricsConfigurationArtifactDto
{
    public Guid EvalRunId { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public string MetricsConfigurationId { get; set; } = string.Empty;
    public JsonElement? MetricsConfiguration { get; set; }
    public DateTime? LastUpdated { get; set; }
}

/// <summary>
/// Response model containing only dataset content for an evaluation run
/// </summary>
public class DatasetArtifactDto
{
    public Guid EvalRunId { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public string DataSetId { get; set; } = string.Empty;
    public JsonElement? DatasetContent { get; set; }
    public DateTime? LastUpdated { get; set; }
}

/// <summary>
/// Request model for storing enriched dataset
/// </summary>
public class CreateEnrichedDatasetDto
{
    [Required]
    public JsonElement EnrichedDataset { get; set; }
}

/// <summary>
/// Response model for enriched dataset content
/// </summary>
public class EnrichedDatasetArtifactDto
{
    public Guid EvalRunId { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public JsonElement? EnrichedDataset { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastUpdated { get; set; }
}

/// <summary>
/// Response model for successful enriched dataset creation
/// </summary>
public class EnrichedDatasetResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid EvalRunId { get; set; }
    public string DataSetPath { get; set; } = string.Empty;
}