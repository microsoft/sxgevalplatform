using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace SxgEvalPlatformApi.Models;

/// <summary>
/// DTO for saving evaluation results with flexible JSON structure
/// </summary>
public class SaveEvaluationResultDto
{
    //[Required(ErrorMessage = "EvalRunId is required")]
    //public Guid EvalRunId { get; set; } = Guid.Empty;
    
    /// <summary>
    /// Flexible JSON structure containing evaluation results. 
    /// Can be an array of objects, a single object, or any valid JSON format.
    /// Example: [{"id": 1, "question": "...", "actualAnswer": "...", "metrics": {...}}]
    /// </summary>
    [Required(ErrorMessage = "EvaluationResults is required")]
    public JsonElement EvaluationResults { get; set; }
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
    public bool Success { get; set; }
    
    public string Message { get; set; } = string.Empty;
    
    public Guid EvalRunId { get; set; }
    
    public string BlobPath { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for evaluation result retrieval
/// </summary>
public class EvaluationResultResponseDto
{
    public bool Success { get; set; }
    
    public string Message { get; set; } = string.Empty;
    
    public Guid EvalRunId { get; set; }
    
    public string FileName { get; set; } = string.Empty;
    
    public JsonElement? EvaluationRecords { get; set; }
}