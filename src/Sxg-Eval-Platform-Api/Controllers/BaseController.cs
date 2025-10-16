using Microsoft.AspNetCore.Mvc;
using Azure;

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
        return BadRequest(new { 
            title = "Validation failed",
            detail = "One or more validation errors occurred.",
            errors = ModelState
        });
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
}