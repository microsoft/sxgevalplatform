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
    /// Validates agent ID input and adds to ModelState if invalid
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <param name="fieldName">Field name for ModelState</param>
    /// <param name="validationType">Type of validation to perform (currently only supports "agentid")</param>
    protected void ValidateAndAddToModelState(string? value, string fieldName, string validationType)
    {
        var alphanumericPattern = new Regex(@"^[a-zA-Z0-9\-_\.]+$", RegexOptions.Compiled);
        
        switch (validationType.ToLower())
        {
            case "agentid":
                if (string.IsNullOrWhiteSpace(value))
                    ModelState.AddModelError(fieldName, "Agent ID is required");
                else if (value.Length > 100 || !alphanumericPattern.IsMatch(value))
                    ModelState.AddModelError(fieldName, "Invalid agent ID format");
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
}