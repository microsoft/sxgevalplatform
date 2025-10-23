using System.ComponentModel.DataAnnotations;

namespace SxgEvalPlatformApi.Models.Dtos;

/// <summary>
/// Standard error response model for API endpoints
/// </summary>
public class ErrorResponseDto
{
    /// <summary>
    /// The error title
    /// </summary>
    /// <example>Not Found</example>
    [Required]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// HTTP status code
    /// </summary>
    /// <example>404</example>
    [Required]
    public int Status { get; set; }

    /// <summary>
    /// Detailed error message
    /// </summary>
    /// <example>Resource not found with ID: 3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
    [Required]
    public string Detail { get; set; } = string.Empty;

    /// <summary>
    /// URI reference for the error type
    /// </summary>
    /// <example>https://httpstatuses.com/404</example>
    public string? Type { get; set; }
}

/// <summary>
/// Validation error response model for API endpoints
/// </summary>
public class ValidationErrorResponseDto
{
    /// <summary>
    /// The error title
    /// </summary>
    /// <example>One or more validation errors occurred.</example>
    [Required]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// HTTP status code
    /// </summary>
    /// <example>400</example>
    [Required]
    public int Status { get; set; }

    /// <summary>
    /// Validation errors grouped by field
    /// </summary>
    /// <example>{"agentId": ["Agent ID is required"], "configurationName": ["Configuration name must be between 1 and 100 characters"]}</example>
    [Required]
    public Dictionary<string, string[]> Errors { get; set; } = new();
}