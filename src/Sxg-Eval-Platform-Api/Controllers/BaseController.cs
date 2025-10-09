using Microsoft.AspNetCore.Mvc;

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
    /// Creates a standardized error response for ActionResult<T>
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
}