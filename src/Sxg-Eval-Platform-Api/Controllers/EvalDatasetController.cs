using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Services;

namespace SxgEvalPlatformApi.Controllers
{
    [Route("api/v1/datasets")]
    public class EvalDatasetController : BaseController
    {
        private readonly IDatasetService _datasetService;

        public EvalDatasetController(
            IDatasetService datasetService,
            ILogger<EvalDatasetController> logger) : base(logger)
        {
            _datasetService = datasetService;
        }

        /// <summary>
        /// Save evaluation dataset (creates new or updates existing based on AgentId, DatasetType, and FileName)
        /// </summary>
        /// <param name="saveDatasetDto">Dataset save request containing agent ID, dataset type, filename, and records</param>
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
                _logger.LogInformation("Request to save dataset: {FileName} for agent: {AgentId}, type: {DatasetType}",
                    saveDatasetDto.FileName, saveDatasetDto.AgentId, saveDatasetDto.DatasetType);

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state for dataset save request");
                    return BadRequest(ModelState);
                }

                var result = await _datasetService.SaveDatasetAsync(saveDatasetDto);

                if (result.Status == "error")
                {
                    _logger.LogError("Dataset save failed: {Message}", result.Message);
                    return BadRequest(result);
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
        /// Get dataset list by agent ID
        /// </summary>
        /// <param name="agentId">Unique ID of the agent</param>
        /// <returns>List of dataset metadata for the agent</returns>
        /// <response code="200">Dataset list retrieved successfully</response>
        /// <response code="404">No datasets found for this agent</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("agent/{agentId}")]
        [ProducesResponseType(typeof(DatasetListResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DatasetListResponseDto>> GetDatasetListByAgentId(string agentId)
        {
            try
            {
                _logger.LogInformation("Request to retrieve dataset list for agent: {AgentId}", agentId);

                if (string.IsNullOrWhiteSpace(agentId))
                {
                    _logger.LogWarning("Agent ID is null or empty");
                    return BadRequest("Agent ID is required");
                }

                var result = await _datasetService.GetDatasetListByAgentIdAsync(agentId);

                if (!result.Datasets.Any())
                {
                    _logger.LogInformation("No datasets found for agent: {AgentId}", agentId);
                    return NotFound($"No datasets found for agent: {agentId}");
                }

                _logger.LogInformation("Retrieved {Count} datasets for agent: {AgentId}",
                    result.Datasets.Count, agentId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving dataset list for agent: {AgentId}", agentId);
                return CreateErrorResponse<DatasetListResponseDto>(
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
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetDatasetById(string datasetId)
        {
            try
            {
                _logger.LogInformation("Request to retrieve dataset content for dataset: {DatasetId}", datasetId);

                if (string.IsNullOrWhiteSpace(datasetId))
                {
                    _logger.LogWarning("Dataset ID is null or empty");
                    return BadRequest("Dataset ID is required");
                }

                var datasetJson = await _datasetService.GetDatasetByIdAsync(datasetId);

                if (string.IsNullOrEmpty(datasetJson))
                {
                    _logger.LogInformation("Dataset not found: {DatasetId}", datasetId);
                    return NotFound($"Dataset not found: {datasetId}");
                }

                // Parse JSON to return as object
                var datasetContent = System.Text.Json.JsonSerializer.Deserialize<object>(datasetJson);

                _logger.LogInformation("Successfully retrieved dataset content for dataset: {DatasetId}", datasetId);
                return Ok(datasetContent);
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Invalid JSON format in dataset: {DatasetId}", datasetId);
                return CreateErrorResponse("Dataset contains invalid JSON", 500);
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
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<DatasetMetadataDto>> GetDatasetMetadataById(string datasetId)
        {
            try
            {
                _logger.LogInformation("Request to retrieve dataset metadata for dataset: {DatasetId}", datasetId);

                if (string.IsNullOrWhiteSpace(datasetId))
                {
                    _logger.LogWarning("Dataset ID is null or empty");
                    return BadRequest("Dataset ID is required");
                }

                var metadata = await _datasetService.GetDatasetMetadataByIdAsync(datasetId);

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
    }
}
