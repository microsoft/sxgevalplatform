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
    protected readonly ICallerIdentificationService _callerService;

    // Pre-compiled regex for better performance
    private static readonly Regex AlphanumericPattern = new(@"^[a-zA-Z0-9\-_\.]+$",
     RegexOptions.Compiled | RegexOptions.CultureInvariant);

    protected BaseController(ILogger logger, ICallerIdentificationService callerService, IOpenTelemetryService? telemetryService = null)
    {
        _logger = logger;
        _callerService = callerService;
        _telemetryService = telemetryService;
    }

    #region Caller Identification (Delegated to Service)

    /// <summary>
    /// Gets the caller's user ID, checking delegated context first (from service principal headers)
    /// then falling back to direct user authentication
    /// </summary>
    protected string GetCurrentUserId() => _callerService.GetCurrentUserId();

    /// <summary>
    /// Gets the caller's email, checking delegated context first
    /// </summary>
    protected string GetCurrentUserEmail() => _callerService.GetCurrentUserEmail();

    /// <summary>
    /// Gets the caller's tenant ID
    /// </summary>
    protected string GetCurrentTenantId() => _callerService.GetCurrentTenantId();

    /// <summary>
    /// Gets the calling application's Client ID (for app-to-app scenarios)
    /// </summary>
    protected string? GetCallingApplicationId() => _callerService.GetCallingApplicationId();

    /// <summary>
    /// Gets the calling application's name
    /// </summary>
    protected string GetCallingApplicationName() => _callerService.GetCallingApplicationName();

    /// <summary>
    /// Checks if the current request is from a service principal (app-to-app)
    /// </summary>
    protected bool IsServicePrincipalCall() => _callerService.IsServicePrincipalCall();

    /// <summary>
    /// Checks if the current request has delegated user context
    /// (service principal acting on behalf of a user)
    /// </summary>
    protected bool HasDelegatedUserContext() => _callerService.HasDelegatedUserContext();

    /// <summary>
    /// Gets comprehensive caller information for logging and auditing
    /// </summary>
    protected CallerInfo GetCallerInfo() => _callerService.GetCallerInfo();

    /// <summary>
    /// Gets a formatted string describing the caller for logging
    /// </summary>
    protected string GetCallerDescription() => _callerService.GetCallerDescription();

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

    #region Error Responses

    /// <summary>
    /// Creates a standardized error response
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
    /// Creates a standardized error response for ActionResult&lt;T&gt;
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
    /// Creates a standardized validation error response from ModelState
    /// </summary>
    protected ActionResult<T> CreateValidationErrorResponse<T>()
    {
        var errors = ModelState.ToDictionary(
         kvp => kvp.Key,
                 kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
             );

        var response = new
        {
            title = "One or more validation errors occurred.",
            status = 400,
            type = "https://httpstatuses.com/400",
            errors,
            timestamp = DateTimeOffset.UtcNow
        };

        return BadRequest(response);
    }

    /// <summary>
    /// Creates a standardized validation error response from ModelState
    /// </summary>
    protected ActionResult CreateValidationErrorResponse()
    {
        var errors = ModelState.ToDictionary(
                   kvp => kvp.Key,
           kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
               );

        var response = new
        {
            title = "One or more validation errors occurred.",
            status = 400,
            type = "https://httpstatuses.com/400",
            errors,
            timestamp = DateTimeOffset.UtcNow
        };

        return BadRequest(response);
    }

    /// <summary>
    /// Creates a standardized validation error for a single field
    /// </summary>
    protected ActionResult<T> CreateFieldValidationError<T>(string fieldName, string errorMessage)
    {
        var response = new
        {
            title = "One or more validation errors occurred.",
            status = 400,
            type = "https://httpstatuses.com/400",
            errors = new Dictionary<string, string[]>
   {
          { fieldName, new[] { errorMessage } }
  },
            timestamp = DateTimeOffset.UtcNow
        };

        return BadRequest(response);
    }

    /// <summary>
    /// Creates a standardized validation error for a single field
    /// </summary>
    protected ActionResult CreateFieldValidationError(string fieldName, string errorMessage)
    {
        var response = new
        {
            title = "One or more validation errors occurred.",
            status = 400,
            type = "https://httpstatuses.com/400",
            errors = new Dictionary<string, string[]>
          {
    { fieldName, new[] { errorMessage } }
            },
            timestamp = DateTimeOffset.UtcNow
        };

        return BadRequest(response);
    }

    /// <summary>
    /// Creates a standardized 404 Not Found response
    /// </summary>
    protected ActionResult<T> CreateNotFoundResponse<T>(string message)
    {
        var response = new
        {
            title = "Not Found",
            status = 404,
            type = "https://httpstatuses.com/404",
            detail = message,
            timestamp = DateTimeOffset.UtcNow
        };

        return base.NotFound(response);
    }

    /// <summary>
    /// Creates a standardized 404 Not Found response
    /// </summary>
    protected ActionResult CreateNotFoundResponse(string message)
    {
        var response = new
        {
            title = "Not Found",
            status = 404,
            type = "https://httpstatuses.com/404",
            detail = message,
            timestamp = DateTimeOffset.UtcNow
        };

        return base.NotFound(response);
    }

    #endregion

    #region Validation

    /// <summary>
    /// High-performance validation with pre-compiled patterns
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

    #region Azure Exception Handling

    /// <summary>
    /// Enhanced Azure exception handling with better error mapping
    /// </summary>
    protected ActionResult<T> HandleAzureException<T>(RequestFailedException ex, string context)
    {
        var (statusCode, userMessage) = MapAzureException(ex);

        _logger.LogError(ex, "Azure operation failed: {Context}", context);

        return CreateErrorResponse<T>($"{context}: {userMessage}", statusCode);
    }

    /// <summary>
    /// Enhanced Azure exception handling with better error mapping
    /// </summary>
    protected ActionResult HandleAzureException(RequestFailedException ex, string context)
    {
        var (statusCode, userMessage) = MapAzureException(ex);

        _logger.LogError(ex, "Azure operation failed: {Context}", context);

        return (ActionResult)CreateErrorResponse($"{context}: {userMessage}", statusCode);
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

    #region Helper Methods

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
