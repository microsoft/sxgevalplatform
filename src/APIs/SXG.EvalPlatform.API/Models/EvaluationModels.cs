using System.ComponentModel.DataAnnotations;

namespace SxgEvalPlatformApi.Models;

/// <summary>
/// Data transfer object for evaluation information
/// </summary>
public class EvaluationDto
{
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    public string Status { get; set; } = "Pending";
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? UpdatedAt { get; set; }
    
    public string? CreatedBy { get; set; }
    
    public double? Score { get; set; }
    
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Data transfer object for creating a new evaluation
/// </summary>
public class CreateEvaluationDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    public string? CreatedBy { get; set; }
    
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Data transfer object for updating an existing evaluation
/// </summary>
public class UpdateEvaluationDto
{
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; set; }
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    public string? Status { get; set; }
    
    public double? Score { get; set; }
    
    public Dictionary<string, object>? Metadata { get; set; }
}