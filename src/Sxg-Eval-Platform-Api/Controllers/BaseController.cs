using Microsoft.AspNetCore.Mvc;
using Azure;
using System.Text.RegularExpressions;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Base controller for SXG Evaluation Platform API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class BaseController : ControllerBase
{
    protected readonly ILogger _logger;

    protected BaseController(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a standardized error response
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="statusCode">HTTP status code</param>
    /// <returns>Problem details response</returns>
    protected IActionResult CreateErrorResponse(string message, int statusCode = 500)
    {
        return Problem(
            title: "An error occurred",
            detail: message,
            statusCode: statusCode
        );
    }

    /// <summary>
    /// Creates a standardized error response for ActionResult&lt;T&gt;
    /// </summary>
    /// <typeparam name="T">The type of the ActionResult</typeparam>
    /// <param name="message">Error message</param>
    /// <param name="statusCode">HTTP status code</param>
    /// <returns>Problem details response</returns>
    protected ActionResult<T> CreateErrorResponse<T>(string message, int statusCode = 500)
    {
        return Problem(
            title: "An error occurred",
            detail: message,
            statusCode: statusCode
        );
    }

    /// <summary>
    /// Creates a standardized validation error response
    /// </summary>
    /// <typeparam name="T">The type of the ActionResult</typeparam>
    /// <returns>Validation error response</returns>
    protected ActionResult<T> CreateValidationErrorResponse<T>()
    {
        return BadRequest(new
        {
            title = "One or more validation errors occurred.",
            status = 400,
            errors = ModelState.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? new string[0]
            )
        });
    }

    /// <summary>
    /// Creates a simple validation error response for IActionResult
    /// </summary>
    /// <returns>Validation error response</returns>
    protected IActionResult CreateSimpleValidationErrorResponse()
    {
        return BadRequest(new
        {
            title = "One or more validation errors occurred.",
            status = 400,
            errors = ModelState.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? new string[0]
            )
        });
    }

    /// <summary>
    /// Creates a standardized bad request error response with consistent format
    /// </summary>
    /// <param name="fieldName">The field name that has the error</param>
    /// <param name="errorMessage">The error message</param>
    /// <returns>Standardized error response</returns>
    protected IActionResult CreateBadRequestResponse(string fieldName, string errorMessage)
    {
        return BadRequest(new
        {
            title = "One or more validation errors occurred.",
            status = 400,
            errors = new Dictionary<string, string[]>
            {
                { fieldName, new[] { errorMessage } }
            }
        });
    }

    /// <summary>
    /// Creates a standardized bad request error response with consistent format for ActionResult&lt;T&gt;
    /// </summary>
    /// <typeparam name="T">The type of the ActionResult</typeparam>
    /// <param name="fieldName">The field name that has the error</param>
    /// <param name="errorMessage">The error message</param>
    /// <returns>Standardized error response</returns>
    protected ActionResult<T> CreateBadRequestResponse<T>(string fieldName, string errorMessage)
    {
        return BadRequest(new
        {
            title = "One or more validation errors occurred.",
            status = 400,
            errors = new Dictionary<string, string[]>
            {
                { fieldName, new[] { errorMessage } }
            }
        });
    }

    /// <summary>
    /// Validates string input and adds to ModelState if invalid
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="fieldName">Field name for ModelState</param>
    /// <param name="validationType">Type of validation to perform (supports "agentid", "type", "agentschemaname")</param>
    protected void ValidateAndAddToModelState(string? value, string fieldName, string validationType)
    {
        var alphanumericPattern = new Regex(@"^[a-zA-Z0-9\-_\.]+$", RegexOptions.Compiled);
        
        switch (validationType.ToLower())
        {
            case "agentid":
                if (string.IsNullOrWhiteSpace(value))
                    ModelState.AddModelError(fieldName, "Agent ID is required");
                else if (value == "string" || value.Length < 3)
                    ModelState.AddModelError(fieldName, "Agent ID must be a valid identifier (minimum 3 characters, not 'string')");
                else if (value.Length > 100 || !alphanumericPattern.IsMatch(value))
                    ModelState.AddModelError(fieldName, "Invalid agent ID format");
                break;
                
            case "type":
                if (string.IsNullOrWhiteSpace(value))
                    ModelState.AddModelError(fieldName, "Type is required");
                else if (value == "string" || value.Length < 2)
                    ModelState.AddModelError(fieldName, "Type must be a valid type (e.g., MCS, AI Foundary, SK), not 'string'");
                else if (value.Length > 50)
                    ModelState.AddModelError(fieldName, "Type cannot exceed 50 characters");
                break;
                
            case "agentschemaname":
                if (string.IsNullOrWhiteSpace(value))
                    ModelState.AddModelError(fieldName, "Agent Schema Name is required");
                else if (value == "string" || value.Length < 3)
                    ModelState.AddModelError(fieldName, "Agent Schema Name must be a valid schema name (minimum 3 characters, not 'string')");
                else if (value.Length > 200)
                    ModelState.AddModelError(fieldName, "Agent Schema Name cannot exceed 200 characters");
                break;
                
            default:
                throw new ArgumentException($"Unsupported validation type: {validationType}", nameof(validationType));
        }
    }

    /// <summary>
    /// Handles Azure exceptions and returns appropriate error responses
    /// </summary>
    /// <typeparam name="T">The type of the ActionResult</typeparam>
    /// <param name="ex">The Azure exception</param>
    /// <param name="context">Additional context for the error</param>
    /// <returns>Error response</returns>
    protected ActionResult<T> HandleAzureException<T>(RequestFailedException ex, string context)
    {
        var statusCode = ex.Status switch
        {
            404 => StatusCodes.Status404NotFound,
            401 => StatusCodes.Status401Unauthorized,
            403 => StatusCodes.Status403Forbidden,
            409 => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return CreateErrorResponse<T>($"{context}: {ex.Message}", statusCode);
    }

    /// <summary>
    /// Checks if an exception is an authorization error
    /// </summary>
    /// <param name="ex">The exception to check</param>
    /// <returns>True if it's an authorization error</returns>
    protected bool IsAuthorizationError(Exception ex)
    {
        return ex is RequestFailedException azEx && (azEx.Status == 401 || azEx.Status == 403);
    }

    /// <summary>
    /// Validates evaluation run ID input
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID to validate</param>
    /// <returns>BadRequest if invalid, null if valid</returns>
    protected ActionResult? ValidateEvalRunId(Guid evalRunId)
    {
        if (evalRunId == Guid.Empty)
        {
            return BadRequest(new
            {
                title = "One or more validation errors occurred.",
                status = 400,
                errors = new Dictionary<string, string[]>
                {
                    { "evalRunId", new[] { "Evaluation run ID is required and must be a valid GUID" } }
                }
            });
        }

        return null;
    }

    /// <summary>
    /// Creates a standardized 404 Not Found response with proper JSON formatting
    /// </summary>
    /// <param name="message">The error message</param>
    /// <returns>NotFound ActionResult with structured JSON</returns>
    protected new ActionResult NotFound(string message)
    {
        return base.NotFound(new
        {
            title = "Not Found",
            status = 404,
            detail = message,
            type = "https://httpstatuses.com/404"
        });
    }

    /// <summary>
    /// Creates a standardized 404 Not Found response for generic types with proper JSON formatting
    /// </summary>
    /// <typeparam name="T">The generic type</typeparam>
    /// <param name="message">The error message</param>
    /// <returns>NotFound ActionResult with structured JSON</returns>
    protected ActionResult<T> NotFound<T>(string message)
    {
        return base.NotFound(new
        {
            title = "Not Found",
            status = 404,
            detail = message,
            type = "https://httpstatuses.com/404"
        });
    }

    /// <summary>
    /// Creates a standardized 404 Not Found response when passing an object with proper JSON formatting
    /// </summary>
    /// <param name="value">The response object (will be wrapped in standard error format)</param>
    /// <returns>NotFound ActionResult with structured JSON</returns>
    protected new ActionResult NotFound(object value)
    {
        // If the value is already a structured error response, return it as is
        if (value != null && value.GetType().GetProperty("status") != null)
        {
            return base.NotFound(value);
        }

        // Otherwise, wrap it in a standard error format
        return base.NotFound(new
        {
            title = "Not Found",
            status = 404,
            detail = value?.ToString() ?? "The requested resource was not found",
            type = "https://httpstatuses.com/404"
        });
    }
}