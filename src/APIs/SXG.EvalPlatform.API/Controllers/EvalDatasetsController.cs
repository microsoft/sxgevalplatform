using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.RequestHandlers;
using SxgEvalPlatformApi.Services;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json;


namespace SxgEvalPlatformApi.Controllers
{
    [Authorize]
    [Route("api/v1/eval/datasets")]
    [Route("api/eval/datasets")]
    public class EvalDatasetsController : BaseController
    {
        private readonly IDataSetRequestHandler _dataSetRequestHandler;
                
        public EvalDatasetsController(IDataSetRequestHandler dataSetRequestHandler,
                                     
                                      
                                      ILogger<EvalDatasetsController> logger,
                                      ICallerIdentificationService callerService,
                                      IOpenTelemetryService telemetryService) : base(logger, callerService, telemetryService)

        {
            _dataSetRequestHandler = dataSetRequestHandler;
            
            // Log controller initialization for debugging
            _logger.LogInformation("EvalDatasetController initialized");
        }

        #region GET Methods

        /// <summary>
        /// Get all datasets for an agent
        /// </summary>
        /// <param name="agentId">Unique ID of the agent (from query string)</param>
        /// <returns>All datasets associated with the agent</returns>
        /// <response code="200">Datasets retrieved successfully</response>
        /// <response code="404">No datasets found for this agent</response>
        /// <response code="500">Internal server error</response>
        [HttpGet]
        [ProducesResponseType(typeof(IList<DatasetMetadataDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IList<DatasetMetadataDto>>> GetDatasetsByAgentId([FromQuery, Required] string agentId)
        {
            using var activity = _telemetryService?.StartActivity("EvalDatasetsController.GetDatasetsByAgentId");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Add telemetry tags
                activity?.SetTag("agentId", CommonUtils.SanitizeForLog(agentId));
                activity?.SetTag("operation", "GetDatasetsByAgentId");

                _logger.LogInformation("Request to retrieve all datasets for agent: {AgentId}", CommonUtils.SanitizeForLog(agentId));

                if (!ModelState.IsValid)
                {
                    stopwatch.Stop();
                    activity?.SetTag("success", false);
                    activity?.SetTag("error.type", "ValidationFailed");
                    activity?.SetTag("validation.errorCount", ModelState.ErrorCount);
                    activity?.SetTag("http.status_code", 400);
                    activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                    _logger.LogWarning("Invalid or missing agent ID - AgentId: {AgentId}, Duration: {Duration}ms",
                      CommonUtils.SanitizeForLog(agentId), stopwatch.ElapsedMilliseconds);

                    return CreateValidationErrorResponse<IList<DatasetMetadataDto>>();
                }

                var datasets = await _dataSetRequestHandler.GetDatasetsByAgentIdAsync(agentId);

                if (!datasets.Any())
                {
                    stopwatch.Stop();
                    activity?.SetTag("success", false);
                    activity?.SetTag("error.type", "NotFound");
                    activity?.SetTag("http.status_code", 404);
                    activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                    _logger.LogInformation("No datasets found for agent: {AgentId}, Duration: {Duration}ms",
                       CommonUtils.SanitizeForLog(agentId), stopwatch.ElapsedMilliseconds);

                    return NotFound($"No datasets found for agent: {agentId}");
                }

                // Success telemetry
                stopwatch.Stop();
                activity?.SetTag("success", true);
                activity?.SetTag("http.status_code", 200);
                activity?.SetTag("dataset.count", datasets.Count);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogInformation("Retrieved {Count} datasets for agent: {AgentId}, Duration: {Duration}ms",
               datasets.Count, agentId, stopwatch.ElapsedMilliseconds);

                return Ok(datasets);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", ex.GetType().Name);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("http.status_code", 500);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogError(ex, "Error occurred while retrieving datasets for agent: {AgentId}, Duration: {Duration}ms",
               agentId, stopwatch.ElapsedMilliseconds);

                return CreateErrorResponse<IList<DatasetMetadataDto>>(
                           $"Failed to retrieve datasets for agent: {agentId}", 500);
            }
        }

        /// <summary>
        /// Get dataset content by dataset ID
        /// </summary>
        /// <param name="datasetId">Unique ID of the dataset</param>
        /// <returns>Dataset content as JSON</returns>
        /// <response code="200">Dataset content retrieved successfully</response>
        /// <response code="404">Dataset not found</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("{datasetId}")]
        [ProducesResponseType(typeof(List<EvalDataset>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IList<EvalDataset>>> GetDatasetById([FromRoute, Required] Guid datasetId)
        {
            using var activity = _telemetryService?.StartActivity("EvalDatasetsController.GetDatasetById");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Add telemetry tags
                activity?.SetTag("datasetId", datasetId.ToString());
                activity?.SetTag("operation", "GetDatasetById");

                _logger.LogInformation("Request to retrieve dataset content for dataset: {DatasetId}", datasetId);

                if (!ModelState.IsValid)
                {
                    stopwatch.Stop();
                    activity?.SetTag("success", false);
                    activity?.SetTag("error.type", "ValidationFailed");
                    activity?.SetTag("validation.errorCount", ModelState.ErrorCount);
                    activity?.SetTag("http.status_code", 400);
                    activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                    _logger.LogWarning($"Invalid or missing datasetId - Duration: {stopwatch.ElapsedMilliseconds}ms");
                    return CreateValidationErrorResponse<IList<EvalDataset>>();

                }

                // Request handler now handles deserialization
                var datasetContent = await _dataSetRequestHandler.GetDatasetByIdAsync(datasetId.ToString());

                if (datasetContent == null)
                {
                    stopwatch.Stop();
                    activity?.SetTag("success", false);
                    activity?.SetTag("error.type", "NotFound");
                    activity?.SetTag("http.status_code", 404);
                    activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                    _logger.LogInformation("Dataset not found: {DatasetId}, Duration: {Duration}ms",
                  datasetId, stopwatch.ElapsedMilliseconds);

                    return NotFound($"Dataset not found: {datasetId}");
                }

                // Success telemetry
                stopwatch.Stop();
                activity?.SetTag("success", true);
                activity?.SetTag("http.status_code", 200);
                activity?.SetTag("dataset.recordCount", datasetContent.Count);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogInformation("Successfully retrieved dataset content for dataset: {DatasetId}, Records: {RecordCount}, Duration: {Duration}ms",
                    datasetId, datasetContent.Count, stopwatch.ElapsedMilliseconds);

                return Ok(datasetContent);
            }
            catch (InvalidOperationException invalidOpEx) when (invalidOpEx.InnerException is JsonException)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", "JsonDeserializationError");
                activity?.SetTag("error.message", invalidOpEx.InnerException.Message);
                activity?.SetTag("http.status_code", 500);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogError(invalidOpEx, "Invalid JSON format in dataset: {DatasetId}, Duration: {Duration}ms",
                 datasetId, stopwatch.ElapsedMilliseconds);

                return CreateErrorResponse<IList<EvalDataset>>("Dataset contains invalid JSON format", 500);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", ex.GetType().Name);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("http.status_code", 500);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogError(ex, "Error occurred while retrieving dataset: {DatasetId}, Duration: {Duration}ms",
         datasetId, stopwatch.ElapsedMilliseconds);

                return CreateErrorResponse<IList<EvalDataset>>($"Failed to retrieve dataset: {datasetId}", 500);
            }
        }


        #endregion

        #region POST Methods

        /// <summary>
        /// Save evaluation dataset (creates new or updates existing based on AgentId, DatasetType, and DatasetName)
        /// </summary>
        /// <param name="saveDatasetDto">Dataset save request containing agent ID, dataset type, dataset name, and records</param>
        /// <returns>Dataset save response with dataset ID</returns>
        /// <response code="201">Dataset created successfully</response>
        /// <response code="200">Dataset updated successfully</response>
        /// <response code="400">Invalid input or validation failed</response>
        /// <response code="500">Internal server error</response>
        [HttpPost]
        [ProducesResponseType(typeof(DatasetSaveResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(DatasetSaveResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DatasetSaveResponseDto>> SaveDataset([FromBody, Required] SaveDatasetDto saveDatasetDto)
        {
            using var activity = _telemetryService?.StartActivity("EvalDatasetsController.SaveDataset");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Add telemetry tags
                activity?.SetTag("agentId", saveDatasetDto.AgentId);
                activity?.SetTag("dataset.name", saveDatasetDto.DatasetName);
                activity?.SetTag("dataset.type", saveDatasetDto.DatasetType);
                activity?.SetTag("dataset.recordCount", saveDatasetDto.DatasetRecords?.Count ?? 0);
                activity?.SetTag("operation", "SaveDataset");

                _logger.LogInformation("Request to save dataset: {DatasetName} for agent: {AgentId}, type: {DatasetType}",
                    CommonUtils.SanitizeForLog(saveDatasetDto.DatasetName), CommonUtils.SanitizeForLog(saveDatasetDto.AgentId), CommonUtils.SanitizeForLog(saveDatasetDto.DatasetType));

                if (!ModelState.IsValid)
                {
                    stopwatch.Stop();
                    activity?.SetTag("success", false);
                    activity?.SetTag("error.type", "ValidationFailed");
                    activity?.SetTag("validation.errorCount", ModelState.ErrorCount);
                    activity?.SetTag("http.status_code", 400);
                    activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                    _logger.LogWarning("Invalid model state for dataset save request - Dataset: {DatasetName}, Agent: {AgentId}, Duration: {Duration}ms",
                          CommonUtils.SanitizeForLog(saveDatasetDto.DatasetName), CommonUtils.SanitizeForLog(saveDatasetDto.AgentId), stopwatch.ElapsedMilliseconds);

                    return CreateValidationErrorResponse<DatasetSaveResponseDto>();
                }

                var result = await _dataSetRequestHandler.SaveDatasetAsync(saveDatasetDto);

                if (result.Status == "error")
                {
                    stopwatch.Stop();
                    activity?.SetTag("success", false);
                    activity?.SetTag("error.type", "SaveFailed");
                    activity?.SetTag("error.message", result.Message);
                    activity?.SetTag("http.status_code", 500);
                    activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                    _logger.LogError("Dataset save failed: {Message}, Dataset: {DatasetName}, Agent: {AgentId}, Duration: {Duration}ms",
            result.Message, saveDatasetDto.DatasetName, saveDatasetDto.AgentId, stopwatch.ElapsedMilliseconds);

                    return CreateErrorResponse<DatasetSaveResponseDto>(
                       "Failed to save dataset", StatusCodes.Status500InternalServerError);
                }

                // Success telemetry
                stopwatch.Stop();
                activity?.SetTag("success", true);
                activity?.SetTag("dataset.id", result.DatasetId);
                activity?.SetTag("dataset.status", result.Status);
                activity?.SetTag("http.status_code", result.Status == "created" ? 201 : 200);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogInformation("Dataset processed successfully: {DatasetId}, Status: {Status}, Dataset: {DatasetName}, Agent: {AgentId}, Duration: {Duration}ms",
                     result.DatasetId, result.Status, saveDatasetDto.DatasetName, saveDatasetDto.AgentId, stopwatch.ElapsedMilliseconds);

                if (result.Status == "created")
                {
                    return CreatedAtAction(
                   nameof(GetDatasetById),
                         new { datasetId = result.DatasetId },
               result);
                }
                else if (result.Status == "updated")
                {
                    return Ok(result);
                }
                else
                {
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", ex.GetType().Name);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("http.status_code", 500);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogError(ex, "Error occurred while saving dataset for agent: {AgentId}, Dataset: {DatasetName}, Duration: {Duration}ms",
                              saveDatasetDto.AgentId, saveDatasetDto.DatasetName, stopwatch.ElapsedMilliseconds);

                return CreateErrorResponse<DatasetSaveResponseDto>(
                     "Failed to save dataset", 500);
            }
        }

        /// <summary>
        /// Update an existing dataset
        /// </summary>
        /// <param name="datasetId">The ID of the dataset to update</param>
        /// <param name="updateDatasetDto">The updated dataset data</param>
        /// <returns>Updated dataset information</returns>
        /// <response code="200">Dataset updated successfully</response>
        /// <response code="400">Invalid input or validation failed</response>
        /// <response code="404">Dataset not found</response>
        /// <response code="500">Internal server error</response>
        [HttpPut("{datasetId}")]
        [ProducesResponseType(typeof(DatasetSaveResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DatasetSaveResponseDto>> UpdateDataset([FromRoute, Required] Guid datasetId, [FromBody, Required] UpdateDatasetDto updateDatasetDto)
        {
            using var activity = _telemetryService?.StartActivity("EvalDatasetsController.UpdateDataset");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Add telemetry tags
                activity?.SetTag("datasetId", datasetId.ToString());
                activity?.SetTag("dataset.recordCount", updateDatasetDto.DatasetRecords?.Count ?? 0);
                activity?.SetTag("operation", "UpdateDataset");

                _logger.LogInformation("Request to update dataset: {DatasetId}",
        datasetId);

                if (!ModelState.IsValid)
                {
                    stopwatch.Stop();
                    activity?.SetTag("success", false);
                    activity?.SetTag("error.type", "ValidationFailed");
                    activity?.SetTag("validation.errorCount", ModelState.ErrorCount);
                    activity?.SetTag("http.status_code", 400);
                    activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                    _logger.LogWarning("Invalid model state for dataset update - DatasetId: {DatasetId}, Duration: {Duration}ms",
                  datasetId, stopwatch.ElapsedMilliseconds);

                    return CreateValidationErrorResponse<DatasetSaveResponseDto>();
                }

                var result = await _dataSetRequestHandler.UpdateDatasetAsync(datasetId.ToString(), updateDatasetDto);

                if (result.Status == "error")
                {
                    stopwatch.Stop();
                    activity?.SetTag("success", false);
                    activity?.SetTag("error.type", "UpdateFailed");
                    activity?.SetTag("error.message", result.Message);
                    activity?.SetTag("http.status_code", 500);
                    activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                    _logger.LogError("Dataset update failed: {Message}, DatasetId: {DatasetId}, Duration: {Duration}ms",
                         result.Message, datasetId, stopwatch.ElapsedMilliseconds);

                    return CreateErrorResponse<DatasetSaveResponseDto>(
                        result.Message, StatusCodes.Status500InternalServerError);
                }

                // Success telemetry
                stopwatch.Stop();
                activity?.SetTag("success", true);
                activity?.SetTag("http.status_code", 200);
                activity?.SetTag("dataset.status", result.Status);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogInformation("Dataset updated successfully: {DatasetId}, Status: {Status}, Duration: {Duration}ms",
               result.DatasetId, result.Status, stopwatch.ElapsedMilliseconds);

                return Ok(result);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", ex.GetType().Name);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("http.status_code", 500);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogError(ex, "Error occurred while updating dataset: {DatasetId}, Duration: {Duration}ms",
                       datasetId, stopwatch.ElapsedMilliseconds);

                return CreateErrorResponse<DatasetSaveResponseDto>(
                 "Failed to update dataset", StatusCodes.Status500InternalServerError);
            }
        }

        #endregion

        #region DELETE Methods

        /// <summary>
        /// Delete a dataset by ID
        /// </summary>
        /// <param name="datasetId">The ID of the dataset to delete</param>
        /// <returns>Deletion result</returns>
        /// <response code="200">Dataset deleted successfully</response>
        /// <response code="404">Dataset with the specified ID not found</response>
        /// <response code="500">Internal server error</response>
        [HttpDelete("{datasetId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteDataset([FromRoute, Required] Guid datasetId)
        {
            using var activity = _telemetryService?.StartActivity("EvalDatasetsController.DeleteDataset");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Add telemetry tags
                activity?.SetTag("datasetId", datasetId.ToString());
                activity?.SetTag("operation", "DeleteDataset");

                _logger.LogInformation("Request to delete dataset: {DatasetId}", datasetId);

                bool deleted = await _dataSetRequestHandler.DeleteDatasetAsync(datasetId.ToString());

                if (!deleted)
                {
                    stopwatch.Stop();
                    activity?.SetTag("success", false);
                    activity?.SetTag("error.type", "NotFound");
                    activity?.SetTag("http.status_code", 404);
                    activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                    _logger.LogWarning("Dataset not found for deletion: {DatasetId}, Duration: {Duration}ms",
                       datasetId, stopwatch.ElapsedMilliseconds);

                    return NotFound($"Dataset with ID '{datasetId}' not found");
                }

                // Success telemetry
                stopwatch.Stop();
                activity?.SetTag("success", true);
                activity?.SetTag("http.status_code", 200);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogInformation("Dataset deleted successfully: {DatasetId}, Duration: {Duration}ms",
                     datasetId, stopwatch.ElapsedMilliseconds);

                return Ok(new { message = $"Dataset '{datasetId}' deleted successfully" });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                activity?.SetTag("success", false);
                activity?.SetTag("error.type", ex.GetType().Name);
                activity?.SetTag("error.message", ex.Message);
                activity?.SetTag("http.status_code", 500);
                activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);

                _logger.LogError(ex, "Error occurred while deleting dataset: {DatasetId}, Duration: {Duration}ms",
                  datasetId, stopwatch.ElapsedMilliseconds);

                return StatusCode(500, new { message = "Failed to delete dataset", error = ex.Message });
            }
        }

        #endregion
    }
}
