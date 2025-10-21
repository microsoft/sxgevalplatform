using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;
using SxgEvalPlatformApi.RequestHandlers;

namespace SxgEvalPlatformApi.Controllers
{
    [Route("api/v1/datasets")]
    public class EvalDatasetController : BaseController
    {
        private readonly IDataSetRequestHandler _dataSetRequestHandler;
        private readonly IConfiguration _configuration;

        public EvalDatasetController(
            IDataSetRequestHandler dataSetRequestHandler,
            IConfiguration configuration,
            ILogger<EvalDatasetController> logger)
            : base(logger)
        {
            _dataSetRequestHandler = dataSetRequestHandler;
            _configuration = configuration;

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
        public async Task<ActionResult<IList<DatasetMetadataDto>>> GetDatasetsByAgentId([FromQuery] string agentId)
        {
            try
            {
                _logger.LogInformation("Request to retrieve all datasets for agent: {AgentId}", agentId);

                ValidateAndAddToModelState(agentId, "agentId", "agentid");
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid or missing agent ID");
                    return CreateValidationErrorResponse<IList<DatasetMetadataDto>>();
                }

                var datasets = await _dataSetRequestHandler.GetDatasetsByAgentIdAsync(agentId);

                if (!datasets.Any())
                {
                    _logger.LogInformation("No datasets found for agent: {AgentId}", agentId);
                    return NotFound($"No datasets found for agent: {agentId}");
                }

                _logger.LogInformation("Retrieved {Count} datasets for agent: {AgentId}",
                    datasets.Count, agentId);

                return Ok(datasets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving datasets for agent: {AgentId}", agentId);
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
        public async Task<IActionResult> GetDatasetById(Guid datasetId)
        {
            try
            {
                _logger.LogInformation("Request to retrieve dataset content for dataset: {DatasetId}", datasetId);

                var datasetJson = await _dataSetRequestHandler.GetDatasetByIdAsync(datasetId.ToString());

                if (string.IsNullOrEmpty(datasetJson))
                {
                    _logger.LogInformation("Dataset not found: {DatasetId}", datasetId);
                    return NotFound($"Dataset not found: {datasetId}");
                }

                // Parse JSON to return as typed object
                var datasetContent = System.Text.Json.JsonSerializer.Deserialize<List<EvalDataset>>(datasetJson,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    });

                _logger.LogInformation("Successfully retrieved dataset content for dataset: {DatasetId}", datasetId);
                return Ok(datasetContent);
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Invalid JSON format in dataset: {DatasetId}", datasetId);
                return CreateErrorResponse("Dataset contains invalid JSON format", 500);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving dataset: {DatasetId}", datasetId);
                return CreateErrorResponse($"Failed to retrieve dataset: {datasetId}", 500);
            }
        }

        /// <summary>
        /// Get dataset metadata by dataset ID
        /// </summary>
        /// <param name="datasetId">Unique ID of the dataset</param>
        /// <returns>Dataset metadata</returns>
        /// <response code="200">Dataset metadata retrieved successfully</response>
        /// <response code="404">Dataset not found</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("{datasetId}/metadata")]
        [ProducesResponseType(typeof(DatasetMetadataDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DatasetMetadataDto>> GetDatasetMetadataById(Guid datasetId)
        {
            try
            {
                _logger.LogInformation("Request to retrieve dataset metadata for dataset: {DatasetId}", datasetId);

                var metadata = await _dataSetRequestHandler.GetDatasetMetadataByIdAsync(datasetId.ToString());

                if (metadata == null)
                {
                    _logger.LogInformation("Dataset metadata not found: {DatasetId}", datasetId);
                    return NotFound($"Dataset metadata not found: {datasetId}");
                }

                _logger.LogInformation("Successfully retrieved dataset metadata for dataset: {DatasetId}", datasetId);
                return Ok(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving dataset metadata: {DatasetId}", datasetId);
                return CreateErrorResponse<DatasetMetadataDto>(
                    $"Failed to retrieve dataset metadata: {datasetId}", 500);
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
        public async Task<ActionResult<DatasetSaveResponseDto>> SaveDataset([FromBody] SaveDatasetDto saveDatasetDto)
        {
            try
            {
                _logger.LogInformation("Request to save dataset: {DatasetName} for agent: {AgentId}, type: {DatasetType}",
                    saveDatasetDto.DatasetName, saveDatasetDto.AgentId, saveDatasetDto.DatasetType);

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for dataset save request");
                    return CreateValidationErrorResponse<DatasetSaveResponseDto>();
                }

                var result = await _dataSetRequestHandler.SaveDatasetAsync(saveDatasetDto);

                if (result.Status == "error")
                {
                    _logger.LogError("Dataset save failed: {Message}", result.Message);
                    return CreateErrorResponse<DatasetSaveResponseDto>(
                        "Failed to save dataset", StatusCodes.Status500InternalServerError);
                }

                _logger.LogInformation("Dataset processed successfully: {DatasetId}, Status: {Status}",
                    result.DatasetId, result.Status);

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
                _logger.LogError(ex, "Error occurred while saving dataset for agent: {AgentId}",
                    saveDatasetDto.AgentId);
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
        public async Task<ActionResult<DatasetSaveResponseDto>> UpdateDataset(
            [FromRoute] Guid datasetId,
            [FromBody] UpdateDatasetDto updateDatasetDto)
        {
            try
            {
                _logger.LogInformation("Request to update dataset: {DatasetId}",
                    datasetId);

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for dataset update");
                    return CreateValidationErrorResponse<DatasetSaveResponseDto>();
                }

                var result = await _dataSetRequestHandler.UpdateDatasetAsync(datasetId.ToString(), updateDatasetDto);

                if (result.Status == "error")
                {
                    _logger.LogError("Dataset update failed: {Message}", result.Message);
                    return CreateErrorResponse<DatasetSaveResponseDto>(
                        result.Message, StatusCodes.Status500InternalServerError);
                }

                _logger.LogInformation("Dataset updated successfully: {DatasetId}", 
                    result.DatasetId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating dataset: {DatasetId}",
                    datasetId);
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
        public async Task<IActionResult> DeleteDataset([FromRoute] Guid datasetId)
        {
            try
            {
                _logger.LogInformation("Request to delete dataset: {DatasetId}", datasetId);

                bool deleted = await _dataSetRequestHandler.DeleteDatasetAsync(datasetId.ToString());

                if (!deleted)
                {
                    _logger.LogWarning("Dataset not found for deletion: {DatasetId}", datasetId);
                    return NotFound($"Dataset with ID '{datasetId}' not found");
                }

                _logger.LogInformation("Dataset deleted successfully: {DatasetId}", datasetId);
                return Ok(new { message = $"Dataset '{datasetId}' deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting dataset: {DatasetId}", datasetId);
                return StatusCode(500, new { message = "Failed to delete dataset", error = ex.Message });
            }
        }

        #endregion
    }
}
