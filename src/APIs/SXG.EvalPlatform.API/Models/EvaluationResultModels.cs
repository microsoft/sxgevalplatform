using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Net;

namespace SxgEvalPlatformApi.Models;

/// <summary>
/// DTO for saving evaluation results with flexible JSON structure
/// </summary>
public class SaveEvaluationResultDto
{
    
    /// <summary>
    /// Flexible JSON structure containing evaluation results. 
    /// Can be an array of objects, a single object, or any valid JSON format.
    /// Example: [{"id": 1, "question": "...", "actualAnswer": "...", "Metrics": {...}}]
    /// </summary>
    [Required(ErrorMessage = "EvaluationResultSummary is required")]
    public JsonElement EvaluationResultSummary { get; set; }

    [Required(ErrorMessage = "EvaluationResultDataset is required")]
    public JsonElement EvaluationResultDataset { get; set; }
}

/// <summary>
/// Model for stored evaluation results (used for blob storage and retrieval)
/// </summary>
public class StoredEvaluationResultDto
{
    [Required]
    public Guid EvalRunId { get; set; } = Guid.Empty;
    
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string FileName { get; set; } = string.Empty;
    
    public string AgentId { get; set; } = string.Empty;
    public string DataSetId { get; set; } = string.Empty;
    public string MetricsConfigurationId { get; set; } = string.Empty;
    public string SavedAt { get; set; } = string.Empty;
    
    [Required]
    public JsonElement EvaluationResults { get; set; }
}

/// <summary>
/// Response DTO for evaluation result save operation
/// </summary>
public class EvaluationResultSaveResponseDto
{
    public Guid EvalRunId { get; set; }
    
}

/// <summary>
/// Response DTO for evaluation result retrieval
/// </summary>
public class EvaluationResultResponseDto
{
    [JsonPropertyName("evalRunId")]
    public string EvalRunId { get; set; }

    [JsonPropertyName("agentId")]
    public string AgentId { get; set; }

    [JsonPropertyName("dataSetId")]
    public string DataSetId { get; set; }

    [JsonPropertyName("dataSetName")]
    public string DataSetName { get; set; }

    [JsonPropertyName("metricsConfigurationId")]
    public string MetricsConfigurationId { get; set; }

    [JsonPropertyName("metricsConfigurationName")]
    public string MetricsConfigurationName { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }

    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }
    public JsonElement? EvaluationRecords { get; set; }
    
}

public class APIRequestProcessingResultDto
{
    public bool IsSuccessful { get; set; }
    public string Message { get; set; } = string.Empty;
    public HttpStatusCode StatusCode { get; set; }

}

