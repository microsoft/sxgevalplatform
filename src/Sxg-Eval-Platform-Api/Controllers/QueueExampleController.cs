using Microsoft.AspNetCore.Mvc;
using Sxg.EvalPlatform.API.Storage.Services;
using SxgEvalPlatformApi.Controllers;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Example controller demonstrating Azure Queue Storage usage
/// This is for demonstration purposes and can be removed or modified as needed
/// </summary>
[ApiController]
[Route("api/v1/queue")]
public class QueueExampleController : BaseController
{
    private readonly IAzureQueueStorageService _queueService;

    public QueueExampleController(
        IAzureQueueStorageService queueService,
        ILogger<QueueExampleController> logger)
        : base(logger)
    {
        _queueService = queueService;
    }

    /// <summary>
    /// Send a message to a queue
    /// </summary>
    /// <param name="queueName">Name of the queue</param>
    /// <param name="message">Message content</param>
    /// <returns>Success response</returns>
    /// <response code="200">Message sent successfully</response>
    /// <response code="400">Invalid input</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("send")]
    [ProducesResponseType(typeof(QueueOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<QueueOperationResult>> SendMessage(
        [FromQuery, Required] string queueName,
        [FromBody, Required] SendMessageRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(queueName))
            {
                return CreateBadRequestResponse<QueueOperationResult>("queueName", "Queue name is required");
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return CreateBadRequestResponse<QueueOperationResult>("message", "Message content is required");
            }

            _logger.LogInformation("Sending message to queue: {QueueName}", queueName);

            var success = await _queueService.SendMessageAsync(
                queueName, 
                request.Message, 
                request.VisibilityTimeoutMinutes.HasValue ? TimeSpan.FromMinutes(request.VisibilityTimeoutMinutes.Value) : null,
                request.TimeToLiveHours.HasValue ? TimeSpan.FromHours(request.TimeToLiveHours.Value) : null);

            if (success)
            {
                return Ok(new QueueOperationResult
                {
                    Success = true,
                    Message = $"Message sent successfully to queue: {queueName}"
                });
            }

            return CreateErrorResponse<QueueOperationResult>("Failed to send message to queue", StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to queue: {QueueName}", queueName);
            return CreateErrorResponse<QueueOperationResult>("Failed to send message to queue", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Get queue information including message count
    /// </summary>
    /// <param name="queueName">Name of the queue</param>
    /// <returns>Queue information</returns>
    /// <response code="200">Queue information retrieved successfully</response>
    /// <response code="400">Invalid input</response>
    /// <response code="404">Queue not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("info")]
    [ProducesResponseType(typeof(QueueInfoResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<QueueInfoResult>> GetQueueInfo([FromQuery, Required] string queueName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(queueName))
            {
                return CreateBadRequestResponse<QueueInfoResult>("queueName", "Queue name is required");
            }

            _logger.LogInformation("Getting queue information for: {QueueName}", queueName);

            var exists = await _queueService.QueueExistsAsync(queueName);
            if (!exists)
            {
                return NotFound<QueueInfoResult>($"Queue '{queueName}' not found");
            }

            var messageCount = await _queueService.GetApproximateMessageCountAsync(queueName);

            return Ok(new QueueInfoResult
            {
                QueueName = queueName,
                Exists = exists,
                ApproximateMessageCount = messageCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue information for: {QueueName}", queueName);
            return CreateErrorResponse<QueueInfoResult>("Failed to get queue information", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Send an evaluation run notification to the queue
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <param name="notification">Notification details</param>
    /// <returns>Success response</returns>
    /// <response code="200">Notification sent successfully</response>
    /// <response code="400">Invalid input</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("notify/evalrun/{evalRunId}")]
    [ProducesResponseType(typeof(QueueOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<QueueOperationResult>> SendEvalRunNotification(
        Guid evalRunId,
        [FromBody] EvalRunNotificationRequest notification)
    {
        try
        {
            var evalRunIdValidation = ValidateEvalRunId(evalRunId);
            if (evalRunIdValidation != null)
            {
                return evalRunIdValidation;
            }

            if (!ModelState.IsValid)
            {
                return CreateValidationErrorResponse<QueueOperationResult>();
            }

            _logger.LogInformation("Sending evaluation run notification for: {EvalRunId}", evalRunId);

            var queueName = "evaluation-notifications";
            var notificationData = new
            {
                Type = "EvaluationRunStatusUpdate",
                EvalRunId = evalRunId,
                AgentId = notification.AgentId,
                Status = notification.Status,
                Timestamp = DateTime.UtcNow,
                AdditionalData = notification.AdditionalData
            };

            var jsonMessage = JsonSerializer.Serialize(notificationData);
            var success = await _queueService.SendMessageAsync(queueName, jsonMessage);

            if (success)
            {
                return Ok(new QueueOperationResult
                {
                    Success = true,
                    Message = $"Evaluation run notification sent successfully for: {evalRunId}"
                });
            }

            return CreateErrorResponse<QueueOperationResult>("Failed to send notification", StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending evaluation run notification for: {EvalRunId}", evalRunId);
            return CreateErrorResponse<QueueOperationResult>("Failed to send notification", StatusCodes.Status500InternalServerError);
        }
    }
}

/// <summary>
/// Request model for sending messages
/// </summary>
public class SendMessageRequest
{
    [Required]
    public string Message { get; set; } = string.Empty;

    public int? VisibilityTimeoutMinutes { get; set; }

    public int? TimeToLiveHours { get; set; }
}

/// <summary>
/// Request model for evaluation run notifications
/// </summary>
public class EvalRunNotificationRequest
{
    [Required]
    public string AgentId { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = string.Empty;

    public Dictionary<string, object>? AdditionalData { get; set; }
}

/// <summary>
/// Result model for queue operations
/// </summary>
public class QueueOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result model for queue information
/// </summary>
public class QueueInfoResult
{
    public string QueueName { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public int ApproximateMessageCount { get; set; }
}