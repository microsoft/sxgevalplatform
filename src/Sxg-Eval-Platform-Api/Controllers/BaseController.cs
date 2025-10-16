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
    /// Handles Azure RequestFailedException and returns appropriate HTTP status code
    /// </summary>
    /// <param name="ex">The Azure RequestFailedException</param>
    /// <param name="defaultMessage">Default error message if none can be determined</param>
    /// <returns>Appropriate ActionResult with correct status code</returns>
    protected IActionResult HandleAzureException(RequestFailedException ex, string defaultMessage = "An error occurred")
    {
        return ex.Status switch
        {
            403 => CreateErrorResponse("Access denied. Authorization failed.", StatusCodes.Status403Forbidden),
            404 => CreateErrorResponse("Resource not found.", StatusCodes.Status404NotFound),
            401 => CreateErrorResponse("Authentication failed.", StatusCodes.Status401Unauthorized),
            409 => CreateErrorResponse("Resource conflict.", StatusCodes.Status409Conflict),
            _ => CreateErrorResponse(defaultMessage, StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// Handles Azure RequestFailedException and returns appropriate HTTP status code for ActionResult&lt;T&gt;
    /// </summary>
    /// <param name="ex">The Azure RequestFailedException</param>
    /// <param name="defaultMessage">Default error message if none can be determined</param>
    /// <returns>Appropriate ActionResult with correct status code</returns>
    protected ActionResult<T> HandleAzureException<T>(RequestFailedException ex, string defaultMessage = "An error occurred")
    {
        return ex.Status switch
        {
            403 => CreateErrorResponse<T>("Access denied. Authorization failed.", StatusCodes.Status403Forbidden),
            404 => CreateErrorResponse<T>("Resource not found.", StatusCodes.Status404NotFound),
            401 => CreateErrorResponse<T>("Authentication failed.", StatusCodes.Status401Unauthorized),
            409 => CreateErrorResponse<T>("Resource conflict.", StatusCodes.Status409Conflict),
            _ => CreateErrorResponse<T>(defaultMessage, StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// Checks if an exception is an Azure authorization error
    /// </summary>
    /// <param name="ex">The exception to check</param>
    /// <returns>True if it's an authorization error</returns>
    protected static bool IsAuthorizationError(Exception ex)
    {
        if (ex is RequestFailedException azureEx)
        {
            return azureEx.Status == 403 || azureEx.Status == 401 || 
                   azureEx.ErrorCode == "AuthorizationPermissionMismatch" ||
                   azureEx.Message.Contains("not authorized", StringComparison.OrdinalIgnoreCase);
        }
        
        return ex.Message.Contains("not authorized", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a clean validation error response from ModelState
    /// </summary>
    /// <returns>BadRequest with simplified error messages</returns>
    protected IActionResult CreateValidationErrorResponse()
    {
        var errors = ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors.Select(e => new { Field = x.Key, Message = e.ErrorMessage }))
            .ToList();

        if (errors.Count == 1)
        {
            var error = errors.First();
            var fieldName = error.Field.Split('.').Last(); // Get just the property name, not the full path
            return BadRequest($"{fieldName} is required and cannot be empty.");
        }

        var errorMessages = errors.Select(e => 
        {
            var fieldName = e.Field.Split('.').Last();
            return $"{fieldName}: {e.Message}";
        });

        return BadRequest($"Validation failed: {string.Join("; ", errorMessages)}");
    }

    /// <summary>
    /// Creates a clean validation error response from ModelState for ActionResult&lt;T&gt;
    /// </summary>
    /// <returns>BadRequest with simplified error messages</returns>
    protected ActionResult<T> CreateValidationErrorResponse<T>()
    {
        var errors = ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors.Select(e => new { Field = x.Key, Message = e.ErrorMessage }))
            .ToList();

        if (errors.Count == 1)
        {
            var error = errors.First();
            var fieldName = error.Field.Split('.').Last(); // Get just the property name, not the full path
            return BadRequest($"{fieldName} is required and cannot be empty.");
        }

        var errorMessages = errors.Select(e => 
        {
            var fieldName = e.Field.Split('.').Last();
            return $"{fieldName}: {e.Message}";
        });

        return BadRequest($"Validation failed: {string.Join("; ", errorMessages)}");
    }
}