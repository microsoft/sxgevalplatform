using Microsoft.AspNetCore.Mvc;
using Azure;
using System.Text.RegularExpressions;
using SxgEvalPlatformApi.Common;
using SxgEvalPlatformApi.Services;
using System.Diagnostics;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Enhanced base controller for SXG Evaluation Platform API with Result pattern integration
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class BaseController : ControllerBase
{
    protected readonly ILogger _logger;
    protected readonly IOpenTelemetryService? _telemetryService;

    // Pre-compiled regex for better performance
    private static readonly Regex AlphanumericPattern = new(@"^[a-zA-Z0-9\-_\.]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Standard error response structure
    private static readonly object NotFoundResponseTemplate = new
    {
        title = "Not Found",
        status = 404,
        type = "https://httpstatuses.com/404"
    };

    private static readonly object ValidationErrorResponseTemplate = new
    {
        title = "One or more validation errors occurred.",
        status = 400,
        type = "https://httpstatuses.com/400"
    };

    protected BaseController(ILogger logger, IOpenTelemetryService? telemetryService = null)
    {
        _logger = logger;
        _telemetryService = telemetryService;
    }

    #region Legacy Methods for Backward Compatibility

    /// <summary>
    /// Legacy validation method for backward compatibility
    /// </summary>
    protected void ValidateAndAddToModelState(string? value, string fieldName, string validationType)
    {
        var type = validationType.ToLower() switch
        {
            "agentid" => ValidationType.AgentId,
            "type" => ValidationType.Type,
            "agentschemaname" => ValidationType.AgentSchemaName,
            _ => throw new ArgumentException($"Unsupported validation type: {validationType}", nameof(validationType))
        };

        ValidateInput(value, fieldName, type);
    }

    /// <summary>
    /// Legacy method for checking authorization errors
    /// </summary>
    protected bool IsAuthorizationError(Exception ex)
    {
        return ex is RequestFailedException azEx && (azEx.Status == 401 || azEx.Status == 403);
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

    #endregion

    #region Result Pattern Integration

    /// <summary>
    /// Converts a Result to an ActionResult
    /// </summary>
    protected ActionResult<T> ToActionResult<T>(Result<T> result)
    {
        return result.IsSuccess ? Ok(result.Value) : CreateErrorResponse<T>(result.Error, 500);
    }

    /// <summary>
    /// Converts a Result to an IActionResult
    /// </summary>
    protected IActionResult ToActionResult(Result result)
    {
        return result.IsSuccess ? Ok() : CreateErrorResponse(result.Error, 500);
    }

    /// <summary>
    /// Safely executes an async operation and wraps it in a Result
    /// </summary>
    protected async Task<Result<T>> SafeExecuteAsync<T>(Func<Task<T>> operation, string operationName)
    {
        using var activity = _telemetryService?.StartActivity($"BaseController.{operationName}");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            activity?.SetTag("operation", operationName);
            var result = await operation();
            activity?.SetTag("success", true);
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            activity?.SetTag("success", false);
            activity?.SetTag("error.message", ex.Message);
            activity?.SetTag("error.type", ex.GetType().Name);

            _logger.LogError(ex, "Operation {OperationName} failed", operationName);
            return Result<T>.Failure(ex.Message);
        }
        finally
        {
            stopwatch.Stop();
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
        }
    }

    #endregion

    #region Enhanced Error Responses

    /// <summary>
    /// Creates a standardized error response with caching
    /// </summary>
    protected IActionResult CreateErrorResponse(string message, int statusCode = 500)
    {
        var errorResponse = new
        {
            title = GetErrorTitle(statusCode),
            detail = message,
            status = statusCode,
            type = $"https://httpstatuses.com/{statusCode}",
            timestamp = DateTimeOffset.UtcNow
        };

        return StatusCode(statusCode, errorResponse);
    }

    /// <summary>
    /// Creates a standardized error response for ActionResult&lt;T&gt; with caching
    /// </summary>
    protected ActionResult<T> CreateErrorResponse<T>(string message, int statusCode = 500)
    {
        var errorResponse = new
        {
            title = GetErrorTitle(statusCode),
            detail = message,
            status = statusCode,
            type = $"https://httpstatuses.com/{statusCode}",
            timestamp = DateTimeOffset.UtcNow
        };

        return StatusCode(statusCode, errorResponse);
    }

    /// <summary>
    /// Creates optimized validation error response
    /// </summary>
    protected ActionResult<T> CreateValidationErrorResponse<T>()
    {
        var errors = ModelState.ToDictionary(
       kvp => kvp.Key,
 kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
   );

        var response = new
        {
            title = ValidationErrorResponseTemplate.GetType().GetProperty("title")?.GetValue(ValidationErrorResponseTemplate),
            status = ValidationErrorResponseTemplate.GetType().GetProperty("status")?.GetValue(ValidationErrorResponseTemplate),
            type = ValidationErrorResponseTemplate.GetType().GetProperty("type")?.GetValue(ValidationErrorResponseTemplate),
            errors,
            timestamp = DateTimeOffset.UtcNow
        };

        return BadRequest(response);
    }

    protected ActionResult CreateValidationErrorResponse()
    {
        var errors = ModelState.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>());

        var response = new
        {
            title = ValidationErrorResponseTemplate.GetType().GetProperty("title")?.GetValue(ValidationErrorResponseTemplate),
            status = ValidationErrorResponseTemplate.GetType().GetProperty("status")?.GetValue(ValidationErrorResponseTemplate),
            type = ValidationErrorResponseTemplate.GetType().GetProperty("type")?.GetValue(ValidationErrorResponseTemplate),
            errors,
            timestamp = DateTimeOffset.UtcNow
        };

        return BadRequest(response);
    }

    /// <summary>
    /// Creates a simple validation error response for IActionResult
    /// </summary>
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

    #endregion

    #region Enhanced Validation

    /// <summary>
    /// High-performance validation with caching and pre-compiled patterns
    /// </summary>
    protected bool ValidateInput(string? value, string fieldName, ValidationType validationType)
    {
        if (IsValidInputBasic(value, fieldName, validationType, out var errorMessage))
            return true;

        ModelState.AddModelError(fieldName, errorMessage!);
        return false;
    }

    /// <summary>
    /// Batch validation for multiple inputs
    /// </summary>
    protected bool ValidateInputs(params (string? value, string fieldName, ValidationType type)[] validations)
    {
        bool isValid = true;
        foreach (var (value, fieldName, type) in validations)
        {
            if (!ValidateInput(value, fieldName, type))
                isValid = false;
        }
        return isValid;
    }

    private static bool IsValidInputBasic(string? value, string fieldName, ValidationType validationType, out string? errorMessage)
    {
        errorMessage = null;

        return validationType switch
        {
            ValidationType.AgentId => ValidateAgentId(value, out errorMessage),
            ValidationType.Type => ValidateType(value, out errorMessage),
            ValidationType.AgentSchemaName => ValidateAgentSchemaName(value, out errorMessage),
            ValidationType.ConfigurationId => ValidateConfigurationId(value, out errorMessage),
            _ => throw new ArgumentException($"Unsupported validation type: {validationType}", nameof(validationType))
        };
    }

    private static bool ValidateAgentId(string? value, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = "Agent ID is required";
            return false;
        }

        if (value == "string" || value.Length < 3)
        {
            errorMessage = "Agent ID must be a valid identifier (minimum 3 characters, not 'string')";
            return false;
        }

        if (value.Length > 100 || !AlphanumericPattern.IsMatch(value))
        {
            errorMessage = "Invalid agent ID format";
            return false;
        }

        return true;
    }

    private static bool ValidateType(string? value, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = "Type is required";
            return false;
        }

        if (value == "string" || value.Length < 2)
        {
            errorMessage = "Type must be a valid type (e.g., MCS, AI Foundary, SK), not 'string'";
            return false;
        }

        if (value.Length > 50)
        {
            errorMessage = "Type cannot exceed 50 characters";
            return false;
        }

        return true;
    }

    private static bool ValidateAgentSchemaName(string? value, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = "Agent Schema Name is required";
            return false;
        }

        if (value == "string" || value.Length < 3)
        {
            errorMessage = "Agent Schema Name must be a valid schema name (minimum 3 characters, not 'string')";
            return false;
        }

        if (value.Length > 200)
        {
            errorMessage = "Agent Schema Name cannot exceed 200 characters";
            return false;
        }

        return true;
    }

    private static bool ValidateConfigurationId(string? value, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = "Configuration ID is required";
            return false;
        }

        if (!Guid.TryParse(value, out _))
        {
            errorMessage = "Configuration ID must be a valid GUID";
            return false;
        }

        return true;
    }

    #endregion

    #region Enhanced Azure Exception Handling

    /// <summary>
    /// Enhanced Azure exception handling with better error mapping
    /// </summary>
    protected ActionResult<T> HandleAzureException<T>(RequestFailedException ex, string context)
    {
        var (statusCode, userMessage) = MapAzureException(ex);

        _logger.LogError(ex, "Azure operation failed: {Context}", context);

        return CreateErrorResponse<T>($"{context}: {userMessage}", statusCode);
    }

    private static (int statusCode, string userMessage) MapAzureException(RequestFailedException ex)
    {
        return ex.Status switch
        {
            404 => (StatusCodes.Status404NotFound, "The requested resource was not found"),
            401 => (StatusCodes.Status401Unauthorized, "Authentication is required"),
            403 => (StatusCodes.Status403Forbidden, "Access to the resource is forbidden"),
            409 => (StatusCodes.Status409Conflict, "The resource already exists or there's a conflict"),
            429 => (StatusCodes.Status429TooManyRequests, "Too many requests. Please try again later"),
            500 => (StatusCodes.Status500InternalServerError, "An internal server error occurred"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };
    }

    #endregion

    #region Optimized Helper Methods

    /// <summary>
    /// Enhanced evaluation run ID validation
    /// </summary>
    protected ActionResult? ValidateEvalRunId(Guid evalRunId)
    {
        if (evalRunId == Guid.Empty)
        {
            return (ActionResult)CreateBadRequestResponse("evalRunId", "Evaluation run ID is required and must be a valid GUID");
        }
        return null;
    }

    /// <summary>
    /// Creates optimized 404 responses
    /// </summary>
    protected new ActionResult<T> NotFound<T>(string message)
    {
        var response = new
        {
            title = NotFoundResponseTemplate.GetType().GetProperty("title")?.GetValue(NotFoundResponseTemplate),
            status = NotFoundResponseTemplate.GetType().GetProperty("status")?.GetValue(NotFoundResponseTemplate),
            type = NotFoundResponseTemplate.GetType().GetProperty("type")?.GetValue(NotFoundResponseTemplate),
            detail = message,
            timestamp = DateTimeOffset.UtcNow
        };

        return base.NotFound(response);
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

    private static string GetErrorTitle(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        _ => "Error"
    };

    #endregion
}

/// <summary>
/// Validation types for input validation
/// </summary>
public enum ValidationType
{
    AgentId,
    Type,
    AgentSchemaName,
    ConfigurationId
}