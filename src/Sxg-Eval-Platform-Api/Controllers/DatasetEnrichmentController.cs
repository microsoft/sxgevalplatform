//using Microsoft.AspNetCore.Mvc;
//using Sxg.EvalPlatform.API.Storage.Services;
//using Sxg.EvalPlatform.API.Storage;
//using SxgEvalPlatformApi.Controllers;
//using SxgEvalPlatformApi.Models;
//using System.Text.Json;

//namespace SxgEvalPlatformApi.Controllers;

///// <summary>
///// Controller for monitoring and managing dataset enrichment queue
///// </summary>
//[ApiController]
//[Route("api/v1/dataset-enrichment")]
//public class DatasetEnrichmentController : BaseController
//{
//    //private readonly IAzureQueueStorageService _queueService;
//    private readonly IConfigHelper _configHelper;

//    public DatasetEnrichmentController(
//        //IAzureQueueStorageService queueService,
//        IConfigHelper configHelper,
//        ILogger<DatasetEnrichmentController> logger)
//        : base(logger)
//    {
//        //_queueService = queueService;
//        _configHelper = configHelper;
//    }

//    ///// <summary>
//    ///// Get status of the dataset enrichment queue
//    ///// </summary>
//    ///// <returns>Queue status information</returns>
//    ///// <response code="200">Queue status retrieved successfully</response>
//    ///// <response code="500">Internal server error</response>
//    //[HttpGet("queue/status")]
//    //[ProducesResponseType(typeof(EnrichmentQueueStatusDto), StatusCodes.Status200OK)]
//    //[ProducesResponseType(StatusCodes.Status500InternalServerError)]
//    //public async Task<ActionResult<EnrichmentQueueStatusDto>> GetQueueStatus()
//    //{
//    //    try
//    //    {
//    //        _logger.LogInformation("Getting dataset enrichment queue status");

//    //        var queueName = _configHelper.GetDatasetEnrichmentRequestsQueueName();
//    //        var exists = await _queueService.QueueExistsAsync(queueName);
//    //        var messageCount = exists ? await _queueService.GetApproximateMessageCountAsync(queueName) : 0;

//    //        var status = new EnrichmentQueueStatusDto
//    //        {
//    //            QueueName = queueName,
//    //            Exists = exists,
//    //            ApproximateMessageCount = messageCount,
//    //            CheckedAt = DateTime.UtcNow
//    //        };

//    //        return Ok(status);
//    //    }
//    //    catch (Exception ex)
//    //    {
//    //        _logger.LogError(ex, "Error getting dataset enrichment queue status");
//    //        return CreateErrorResponse<EnrichmentQueueStatusDto>("Failed to get queue status", StatusCodes.Status500InternalServerError);
//    //    }
//    //}

//    /// <summary>
//    /// Peek at pending enrichment requests in the queue
//    /// </summary>
//    /// <param name="maxMessages">Maximum number of messages to peek (1-32)</param>
//    /// <returns>List of pending enrichment requests</returns>
//    /// <response code="200">Messages retrieved successfully</response>
//    /// <response code="400">Invalid parameters</response>
//    /// <response code="500">Internal server error</response>
//    //[HttpGet("queue/peek")]
//    //[ProducesResponseType(typeof(List<EnrichmentRequestPreviewDto>), StatusCodes.Status200OK)]
//    //[ProducesResponseType(StatusCodes.Status400BadRequest)]
//    //[ProducesResponseType(StatusCodes.Status500InternalServerError)]
//    //public async Task<ActionResult<List<EnrichmentRequestPreviewDto>>> PeekQueueMessages([FromQuery] int maxMessages = 5)
//    //{
//    //    try
//    //    {
//    //        if (maxMessages < 1 || maxMessages > 32)
//    //        {
//    //            return CreateBadRequestResponse<List<EnrichmentRequestPreviewDto>>("maxMessages", "maxMessages must be between 1 and 32");
//    //        }

//    //        _logger.LogInformation("Peeking at dataset enrichment queue messages, MaxMessages: {MaxMessages}", maxMessages);

//    //        var queueName = _configHelper.GetDatasetEnrichmentRequestsQueueName();
//    //        var messages = await _queueService.PeekMessagesAsync(queueName, maxMessages);
//    //        var previews = new List<EnrichmentRequestPreviewDto>();

//    //        foreach (var message in messages)
//    //        {
//    //            try
//    //            {
//    //                var enrichmentRequest = JsonSerializer.Deserialize<DatasetEnrichmentRequest>(message.MessageText, 
//    //                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

//    //                if (enrichmentRequest != null)
//    //                {
//    //                    previews.Add(new EnrichmentRequestPreviewDto
//    //                    {
//    //                        MessageId = message.MessageId,
//    //                        EvalRunId = enrichmentRequest.EvalRunId,
//    //                        DatasetId = enrichmentRequest.DatasetId,
//    //                        AgentId = enrichmentRequest.AgentId,
//    //                        Priority = enrichmentRequest.Priority,
//    //                        RequestedAt = enrichmentRequest.RequestedAt,
//    //                        InsertedOn = message.InsertedOn,
//    //                        DequeueCount = message.DequeueCount,
//    //                        ExpiresOn = message.ExpiresOn
//    //                    });
//    //                }
//    //            }
//    //            catch (JsonException ex)
//    //            {
//    //                _logger.LogWarning(ex, "Failed to deserialize queue message: {MessageId}", message.MessageId);
//    //                // Add a preview with limited information
//    //                previews.Add(new EnrichmentRequestPreviewDto
//    //                {
//    //                    MessageId = message.MessageId,
//    //                    AgentId = "Unknown",
//    //                    Priority = "Unknown",
//    //                    InsertedOn = message.InsertedOn,
//    //                    DequeueCount = message.DequeueCount,
//    //                    ExpiresOn = message.ExpiresOn,
//    //                    HasParsingError = true
//    //                });
//    //            }
//    //        }

//    //        return Ok(previews);
//    //    }
//    //    catch (Exception ex)
//    //    {
//    //        _logger.LogError(ex, "Error peeking at dataset enrichment queue messages");
//    //        return CreateErrorResponse<List<EnrichmentRequestPreviewDto>>("Failed to peek queue messages", StatusCodes.Status500InternalServerError);
//    //    }
//    //}

//    /// <summary>
//    /// Manually trigger dataset enrichment request for testing purposes
//    /// </summary>
//    /// <param name="request">Enrichment request details</param>
//    /// <returns>Success response</returns>
//    /// <response code="200">Request sent successfully</response>
//    /// <response code="400">Invalid input</response>
//    /// <response code="500">Internal server error</response>
//    //[HttpPost("queue/test-message")]
//    //[ProducesResponseType(typeof(EnrichmentQueueOperationResult), StatusCodes.Status200OK)]
//    //[ProducesResponseType(StatusCodes.Status400BadRequest)]
//    //[ProducesResponseType(StatusCodes.Status500InternalServerError)]
//    //public async Task<ActionResult<EnrichmentQueueOperationResult>> SendTestEnrichmentRequest([FromBody] DatasetEnrichmentRequest request)
//    //{
//    //    try
//    //    {
//    //        if (!ModelState.IsValid)
//    //        {
//    //            return CreateValidationErrorResponse<EnrichmentQueueOperationResult>();
//    //        }

//    //        _logger.LogInformation("Sending test dataset enrichment request for EvalRunId: {EvalRunId}", request.EvalRunId);

//    //        var messageContent = JsonSerializer.Serialize(request, new JsonSerializerOptions 
//    //        { 
//    //            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
//    //        });

//    //        var queueName = _configHelper.GetDatasetEnrichmentRequestsQueueName();
//    //        var success = await _queueService.SendMessageAsync(queueName, messageContent);

//    //        if (success)
//    //        {
//    //            return Ok(new EnrichmentQueueOperationResult
//    //            {
//    //                Success = true,
//    //                Message = $"Test enrichment request sent successfully for EvalRunId: {request.EvalRunId}",
//    //                QueueName = queueName
//    //            });
//    //        }

//    //        return CreateErrorResponse<EnrichmentQueueOperationResult>("Failed to send test enrichment request", StatusCodes.Status500InternalServerError);
//    //    }
//    //    catch (Exception ex)
//    //    {
//    //        _logger.LogError(ex, "Error sending test dataset enrichment request for EvalRunId: {EvalRunId}", request.EvalRunId);
//    //        return CreateErrorResponse<EnrichmentQueueOperationResult>("Failed to send test enrichment request", StatusCodes.Status500InternalServerError);
//    //    }
//    //}
//}

///// <summary>
///// DTO for enrichment queue status
///// </summary>
//public class EnrichmentQueueStatusDto
//{
//    public string QueueName { get; set; } = string.Empty;
//    public bool Exists { get; set; }
//    public int ApproximateMessageCount { get; set; }
//    public DateTime CheckedAt { get; set; }
//}

///// <summary>
///// DTO for enrichment request preview
///// </summary>
//public class EnrichmentRequestPreviewDto
//{
//    public string MessageId { get; set; } = string.Empty;
//    public Guid EvalRunId { get; set; }
//    public Guid DatasetId { get; set; }
//    public string AgentId { get; set; } = string.Empty;
//    public string Priority { get; set; } = string.Empty;
//    public DateTime RequestedAt { get; set; }
//    public DateTimeOffset? InsertedOn { get; set; }
//    public long DequeueCount { get; set; }
//    public DateTimeOffset? ExpiresOn { get; set; }
//    public bool HasParsingError { get; set; }
//}

///// <summary>
///// DTO for enrichment queue operation results
///// </summary>
//public class EnrichmentQueueOperationResult
//{
//    public bool Success { get; set; }
//    public string Message { get; set; } = string.Empty;
//    public string QueueName { get; set; } = string.Empty;
//}