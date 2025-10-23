using Microsoft.AspNetCore.Mvc;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage;
using SxgEvalPlatformApi.Controllers;
using SxgEvalPlatformApi.Models;
using System.Text.Json;

namespace SxgEvalPlatformApi.Controllers;

/// <summary>
/// Controller for monitoring and managing evaluation processing queue
/// </summary>
[ApiController]
[Route("api/v1/eval-processing")]
public class EvalProcessingController : BaseController
{
    private readonly IAzureQueueStorageService _queueService;
    private readonly IConfigHelper _configHelper;

    public EvalProcessingController(
        IAzureQueueStorageService queueService,
        IConfigHelper configHelper,
        ILogger<EvalProcessingController> logger)
        : base(logger)
    {
        _queueService = queueService;
        _configHelper = configHelper;
    }

    /// <summary>
    /// Get status of the evaluation processing queue
    /// </summary>
    /// <returns>Queue status information</returns>
    /// <response code="200">Queue status retrieved successfully</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("queue/status")]
    [ProducesResponseType(typeof(EvalProcessingQueueStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EvalProcessingQueueStatusDto>> GetQueueStatus()
    {
        try
        {
            _logger.LogInformation("Getting evaluation processing queue status");

            var queueName = _configHelper.GetEvalProcessingRequestsQueueName();
            var exists = await _queueService.QueueExistsAsync(queueName);
            var messageCount = exists ? await _queueService.GetApproximateMessageCountAsync(queueName) : 0;

            var status = new EvalProcessingQueueStatusDto
            {
                QueueName = queueName,
                Exists = exists,
                ApproximateMessageCount = messageCount,
                CheckedAt = DateTime.UtcNow
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting evaluation processing queue status");
            return CreateErrorResponse<EvalProcessingQueueStatusDto>("Failed to get queue status", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Peek at pending evaluation processing requests in the queue
    /// </summary>
    /// <param name="maxMessages">Maximum number of messages to peek (1-32)</param>
    /// <returns>List of pending evaluation processing requests</returns>
    /// <response code="200">Messages retrieved successfully</response>
    /// <response code="400">Invalid parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("queue/peek")]
    [ProducesResponseType(typeof(List<EvalProcessingRequestPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<EvalProcessingRequestPreviewDto>>> PeekQueueMessages([FromQuery] int maxMessages = 5)
    {
        try
        {
            if (maxMessages < 1 || maxMessages > 32)
            {
                return CreateBadRequestResponse<List<EvalProcessingRequestPreviewDto>>("maxMessages", "maxMessages must be between 1 and 32");
            }

            _logger.LogInformation("Peeking at evaluation processing queue messages, MaxMessages: {MaxMessages}", maxMessages);

            var queueName = _configHelper.GetEvalProcessingRequestsQueueName();
            var messages = await _queueService.PeekMessagesAsync(queueName, maxMessages);
            var previews = new List<EvalProcessingRequestPreviewDto>();

            foreach (var message in messages)
            {
                try
                {
                    var processingRequest = JsonSerializer.Deserialize<EvalProcessingRequest>(message.MessageText, 
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                    if (processingRequest != null)
                    {
                        previews.Add(new EvalProcessingRequestPreviewDto
                        {
                            MessageId = message.MessageId,
                            EvalRunId = processingRequest.EvalRunId,
                            MetricsConfigurationId = processingRequest.MetricsConfigurationId,
                            EnrichedDatasetId = processingRequest.EnrichedDatasetId,
                            DatasetId = processingRequest.DatasetId,
                            AgentId = processingRequest.AgentId,
                            Priority = processingRequest.Priority,
                            RequestedAt = processingRequest.RequestedAt,
                            InsertedOn = message.InsertedOn,
                            DequeueCount = message.DequeueCount,
                            ExpiresOn = message.ExpiresOn
                        });
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize queue message: {MessageId}", message.MessageId);
                    // Add a preview with limited information
                    previews.Add(new EvalProcessingRequestPreviewDto
                    {
                        MessageId = message.MessageId,
                        AgentId = "Unknown",
                        Priority = "Unknown",
                        InsertedOn = message.InsertedOn,
                        DequeueCount = message.DequeueCount,
                        ExpiresOn = message.ExpiresOn,
                        HasParsingError = true
                    });
                }
            }

            return Ok(previews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error peeking at evaluation processing queue messages");
            return CreateErrorResponse<List<EvalProcessingRequestPreviewDto>>("Failed to peek queue messages", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Manually trigger evaluation processing request for testing purposes
    /// </summary>
    /// <param name="request">Processing request details</param>
    /// <returns>Success response</returns>
    /// <response code="200">Request sent successfully</response>
    /// <response code="400">Invalid input</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("queue/test-message")]
    [ProducesResponseType(typeof(EvalProcessingQueueOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<EvalProcessingQueueOperationResult>> SendTestProcessingRequest([FromBody] EvalProcessingRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return CreateValidationErrorResponse<EvalProcessingQueueOperationResult>();
            }

            _logger.LogInformation("Sending test evaluation processing request for EvalRunId: {EvalRunId}", request.EvalRunId);

            var messageContent = JsonSerializer.Serialize(request, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            var queueName = _configHelper.GetEvalProcessingRequestsQueueName();
            var success = await _queueService.SendMessageAsync(queueName, messageContent);

            if (success)
            {
                return Ok(new EvalProcessingQueueOperationResult
                {
                    Success = true,
                    Message = $"Test processing request sent successfully for EvalRunId: {request.EvalRunId}",
                    QueueName = queueName
                });
            }

            return CreateErrorResponse<EvalProcessingQueueOperationResult>("Failed to send test processing request", StatusCodes.Status500InternalServerError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test evaluation processing request for EvalRunId: {EvalRunId}", request.EvalRunId);
            return CreateErrorResponse<EvalProcessingQueueOperationResult>("Failed to send test processing request", StatusCodes.Status500InternalServerError);
        }
    }
}

/// <summary>
/// DTO for evaluation processing queue status
/// </summary>
public class EvalProcessingQueueStatusDto
{
    public string QueueName { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public int ApproximateMessageCount { get; set; }
    public DateTime CheckedAt { get; set; }
}

/// <summary>
/// DTO for evaluation processing request preview
/// </summary>
public class EvalProcessingRequestPreviewDto
{
    public string MessageId { get; set; } = string.Empty;
    public Guid EvalRunId { get; set; }
    public string MetricsConfigurationId { get; set; } = string.Empty;
    public string EnrichedDatasetId { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTimeOffset? InsertedOn { get; set; }
    public long DequeueCount { get; set; }
    public DateTimeOffset? ExpiresOn { get; set; }
    public bool HasParsingError { get; set; }
}

/// <summary>
/// DTO for evaluation processing queue operation results
/// </summary>
public class EvalProcessingQueueOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
}