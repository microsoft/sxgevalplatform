using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using Sxg.EvalPlatform.API.Storage.Validators;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Models;
using System.ComponentModel.DataAnnotations;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Request handler for evaluation run operations using the storage project services with caching support
    /// </summary>
    public class EvalRunRequestHandler : IEvalRunRequestHandler
    {
        private readonly IEvalRunTableService _evalRunTableService;
        private readonly IDataVerseAPIService _dataVerseAPIService;
        private readonly IConfigHelper _configHelper;
        private readonly ILogger<EvalRunRequestHandler> _logger;
        private readonly IDataSetTableService _dataSetTableService;
        private readonly IMetricsConfigTableService _metricsConfigTableService;
        private readonly IEntityValidators _entityValidators; 

        public EvalRunRequestHandler(IEvalRunTableService evalRunTableService,
                                     IDataVerseAPIService dataVerseAPIService,
                                     ILogger<EvalRunRequestHandler> logger,
                                     IConfigHelper configHelper,
                                     ICacheManager cacheManager,
                                     IDataSetTableService dataSetTableService,
                                     IMetricsConfigTableService metricsConfigTableService,
                                     IEntityValidators entityValidators)
        {
            _evalRunTableService = evalRunTableService;
            _dataVerseAPIService = dataVerseAPIService;
            _configHelper = configHelper;
            _logger = logger;
            _dataSetTableService = dataSetTableService;
            _metricsConfigTableService = metricsConfigTableService;
            _entityValidators = entityValidators;
        }


        /// <summary>
        /// Create a new evaluation run and update cache
        /// </summary>
        public async Task<EvalRunDto> CreateEvalRunAsync(CreateEvalRunDto createDto)
        {
            try
            {
                var result = ValidateReferencedEntitiesAsync(createDto);

                if (!result.Result.isValid)
                {
                    _logger.LogError("Validation failed while creating evaluation run: {Message}", result.Result.message);
                    throw new ValidationException(result.Result.message);
                }

                var evalRunId = Guid.NewGuid();
                var currentDateTime = DateTime.UtcNow;

                // Store container name and blob path separately for better blob storage handling
                var containerName = CommonUtils.TrimAndRemoveSpaces(createDto.AgentId); // Ensure valid container name for Azure Blob Storage
                var blobFilePath = $"{_configHelper.EvalResultsFolderName()}"; // Create folder structure for multiple output files

                var entity = new EvalRunTableEntity
                {
                    EvalRunId = evalRunId, // Store as GUID
                    AgentId = createDto.AgentId, // This will also set PartitionKey
                    DataSetId = createDto.DataSetId.ToString(),
                    MetricsConfigurationId = createDto.MetricsConfigurationId.ToString(),
                    Status = CommonConstants.EvalRunStatus.RequestSubmitted,
                    LastUpdatedOn = currentDateTime,
                    StartedDatetime = currentDateTime,
                    ContainerName = containerName,
                    BlobFilePath = blobFilePath,
                    Type = createDto.Type,
                    EnvironmentId = createDto.EnvironmentId.ToString(),
                    AgentSchemaName = createDto.AgentSchemaName,
                    EvalRunName = createDto.EvalRunName
                };

                // Set the RowKey to the GUID string
                entity.RowKey = evalRunId.ToString();

                // Create in storage first
                var createdEntity = await _evalRunTableService.CreateEvalRunAsync(entity);
                var datasetEntity = await _dataSetTableService.GetDataSetByIdAsync(createDto.DataSetId.ToString());
                var metricsConfigEntity = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(createDto.MetricsConfigurationId.ToString());

                // After successful creation, update cache with enriched data
                var evalRunDto = await MapEntityToDtoAsync(createdEntity);
                evalRunDto.DataSetName = datasetEntity.DatasetName;
                evalRunDto.MetricsConfigurationName = metricsConfigEntity.ConfigurationName;
                              

                _logger.LogInformation("Created evaluation run with ID: {EvalRunId} and updated cache", evalRunId);

                // Send dataset enrichment request to DataVerse API (no caching needed for external API calls)
                var enrichmentRequestResult = await SendDatasetEnrichmentToDataVerseAsync(evalRunId, createDto);
                

                return evalRunDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating evaluation run for AgentId: {createDto.AgentId}, DataSetId: {createDto.DataSetId}");
                throw;
            }
        }

        /// <summary>
        /// Update evaluation run status and update cache
        /// </summary>
        public async Task<EvalRunDto?> UpdateEvalRunStatusAsync(UpdateEvalRunStatusDto updateDto)
        {
            try
            {
                // Normalize the status to ensure consistent casing in storage
                string normalizedStatus = updateDto.Status;

                if (string.Equals(updateDto.Status, CommonConstants.EvalRunStatus.EvalRunStarted, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = CommonConstants.EvalRunStatus.EvalRunStarted;
                else if (string.Equals(updateDto.Status, CommonConstants.EvalRunStatus.EnrichingDataset, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = CommonConstants.EvalRunStatus.EnrichingDataset;
                else if (string.Equals(updateDto.Status, CommonConstants.EvalRunStatus.EvalRunCompleted, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = CommonConstants.EvalRunStatus.EvalRunCompleted;
                else if (string.Equals(updateDto.Status, CommonConstants.EvalRunStatus.DatasetEnrichmentCompleted, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = CommonConstants.EvalRunStatus.DatasetEnrichmentCompleted;
                else if (string.Equals(updateDto.Status, CommonConstants.EvalRunStatus.EvalRunFailed, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = CommonConstants.EvalRunStatus.EvalRunFailed;
                else if (string.Equals(updateDto.Status, CommonConstants.EvalRunStatus.RequestSubmitted, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = CommonConstants.EvalRunStatus.RequestSubmitted;

                // Update in storage first
                var updatedEntity = await _evalRunTableService.UpdateEvalRunStatusAsync(updateDto.AgentId,
                                                                                        updateDto.EvalRunId,
                                                                                        normalizedStatus,
                                                                                        "System");
                if (updatedEntity == null)
                {
                    _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", updateDto.EvalRunId);
                    return null;
                }
                
                var evalRunDto = await MapEntityToDtoAsync(updatedEntity);

                _logger.LogInformation($"Updated evaluation run status to {normalizedStatus} for ID: {updateDto.EvalRunId} and updated cache");

                return evalRunDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating evaluation run status for ID: {EvalRunId}", updateDto.EvalRunId);
                throw;
            }
        }

        /// <summary>
        /// Get evaluation run by ID with caching support
        /// </summary>
        public async Task<EvalRunDto?> GetEvalRunByIdAsync(string agentId, Guid evalRunId)
        {
            try
            {
                // If not in cache, fetch from storage
                var entity = await _evalRunTableService.GetEvalRunByIdAsync(agentId, evalRunId);

                if (entity == null)
                {
                    _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
                    return null;
                }

                var result = await MapEntityToDtoAsync(entity);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation run with ID: {EvalRunId}", evalRunId);
                throw;
            }
        }

        /// <summary>
        /// Get evaluation run by ID (searches across all partitions) with caching support
        /// </summary>
        public async Task<EvalRunDto?> GetEvalRunByIdAsync(Guid evalRunId)
        {
            try
            {
                var entity = await _evalRunTableService.GetEvalRunByIdAsync(evalRunId);
                if (entity == null)
                {
                    _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
                    return null;
                }
                var result = await MapEntityToDtoAsync(entity);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation run with ID: {EvalRunId}", evalRunId);
                throw;
            }
        }

        /// <summary>
        /// Get all evaluation runs for an agent with caching support
        /// </summary>
        public async Task<IList<EvalRunDto>> GetEvalRunsByAgentIdAsync(string agentId, DateTime? startDateTime, DateTime? endDateTime)
        {
            try
            {
                var entities = await _evalRunTableService.GetEvalRunsByAgentIdAndDateFilterAsync(agentId, startDateTime, endDateTime);
                var results = entities.Select(MapEntityToDto).ToList();
                _logger.LogInformation("Retrieved {Count} evaluation runs for AgentId: {AgentId}",
                  results.Count, agentId);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation runs for AgentId: {AgentId}", agentId);
                throw;
            }
        }

        /// <summary>
        /// Get evaluation run entity with internal details (for internal use only) with caching support
        /// </summary>
        public async Task<EvalRunTableEntity?> GetEvalRunEntityByIdAsync(Guid evalRunId)
        {
            try
            {
                var entity = await _evalRunTableService.GetEvalRunByIdAsync(evalRunId);
                if (entity == null)
                {
                    _logger.LogWarning("Evaluation run entity not found with ID: {EvalRunId}", evalRunId);
                    return null;
                }
                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation run entity with ID: {EvalRunId}", evalRunId);
                throw;
            }
        }
              

        public async Task<(bool IsSuccessfull, string HttpStatusCode, string Message)> PlaceEnrichmentRequestToDataVerseAPI(Guid evalRunId)
        {
            try
            {
                var evalRunEntity = await _evalRunTableService.GetEvalRunByIdAsync(evalRunId);
                if (evalRunEntity == null)
                {
                    _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
                    return (false, StatusCodes.Status404NotFound.ToString(), "Evaluation run not found");
                }
                var createDto = new CreateEvalRunDto
                {
                    AgentId = evalRunEntity.AgentId,
                    EnvironmentId = evalRunEntity.EnvironmentId,
                    AgentSchemaName = evalRunEntity.AgentSchemaName,
                    DataSetId = Guid.Parse(evalRunEntity.DataSetId),
                    EvalRunName = evalRunEntity.EvalRunName,
                    MetricsConfigurationId = Guid.Parse(evalRunEntity.MetricsConfigurationId),
                    Type = evalRunEntity.Type 
                };
                return await SendDatasetEnrichmentToDataVerseAsync(evalRunId, createDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error placing enrichment request to DataVerse API for EvalRunId: {EvalRunId}", evalRunId);
                return (false, StatusCodes.Status500InternalServerError.ToString(), "Internal server error");
            }

        }

        /// <summary>
        /// Send dataset enrichment request to DataVerse API (no caching for external API calls)
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <param name="createDto">Original create DTO with all evaluation run details</param>
        private async Task<(bool IsSuccessfull, string HttpStatusCode, string Message)> SendDatasetEnrichmentToDataVerseAsync(Guid evalRunId, CreateEvalRunDto createDto)
        {
            try
            {
                _logger.LogInformation("Sending dataset enrichment request to DataVerse API for EvalRunId: {EvalRunId}, DatasetId: {DatasetId}",
                       evalRunId, createDto.DataSetId);

                // Create DataVerse API request object
                var dataVerseRequest = new DataVerseApiRequest
                {
                    EvalRunId = evalRunId.ToString(),
                    AgentId = createDto.AgentId,
                    EnvironmentId = createDto.EnvironmentId.ToString(),
                    AgentSchemaName = createDto.AgentSchemaName,
                    DatasetId = createDto.DataSetId.ToString()
                };

                // Send to DataVerse API using the injected service (no caching for external API calls)
                var dataVerseResponse = await _dataVerseAPIService.PostEvalRunAsync(dataVerseRequest);

                if (dataVerseResponse.Success)
                {
                    _logger.LogInformation("Successfully posted evaluation run request to DataVerse API for EvalRunId: {EvalRunId}. Status: {StatusCode}",
                           evalRunId, dataVerseResponse.StatusCode);
                    return (true, dataVerseResponse.StatusCode.ToString(), "Success");
                }
                else
                {
                    _logger.LogWarning("Failed to post evaluation run request to DataVerse API for EvalRunId: {EvalRunId}. Status: {StatusCode}, Message: {Message}",
                    evalRunId, dataVerseResponse.StatusCode, dataVerseResponse.Message);
                    return (false, dataVerseResponse.StatusCode.ToString(), dataVerseResponse.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending dataset enrichment request to DataVerse API for EvalRunId: {EvalRunId}, DatasetId: {DatasetId}",
                   evalRunId, createDto.DataSetId);
                return (false, StatusCodes.Status500InternalServerError.ToString(), "Internal server error");
            }
        }

        /// <summary>
        /// Map EvalRunTableEntity to EvalRunDto with enriched data (dataset name, metrics config name)
        /// </summary>
        private async Task<EvalRunDto> MapEntityToDtoAsync(EvalRunTableEntity entity)
        {
            var dto = new EvalRunDto
            {
                EvalRunId = entity.EvalRunId,
                MetricsConfigurationId = entity.MetricsConfigurationId,
                DataSetId = entity.DataSetId,
                AgentId = entity.AgentId,
                Status = entity.Status,
                LastUpdatedBy = entity.LastUpdatedBy,
                LastUpdatedOn = entity.LastUpdatedOn,
                StartedDatetime = entity.StartedDatetime,
                CompletedDatetime = entity.CompletedDatetime,
                EvalRunName = entity.EvalRunName
            };

            // Fetch DataSet name
            try
            {
                var dataset = await _dataSetTableService.GetDataSetByIdAsync(entity.DataSetId);
                dto.DataSetName = dataset?.DatasetName;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch dataset name for DataSetId: {DataSetId}", entity.DataSetId);
                dto.DataSetName = null; // Set to null if fetch fails
            }

            // Fetch Metrics Configuration name
            try
            {
                var metricsConfig = await _metricsConfigTableService.GetMetricsConfigurationByConfigurationIdAsync(entity.MetricsConfigurationId);
                dto.MetricsConfigurationName = metricsConfig?.ConfigurationName;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch metrics configuration name for MetricsConfigurationId: {MetricsConfigurationId}", entity.MetricsConfigurationId);
                dto.MetricsConfigurationName = null; // Set to null if fetch fails
            }

            return dto;
        }

        /// <summary>
        /// Map EvalRunTableEntity to EvalRunDto (synchronous version for backward compatibility)
        /// </summary>
        private EvalRunDto MapEntityToDto(EvalRunTableEntity entity)
        {
            // Use synchronous mapping without enriched data
            // This is used in scenarios where we don't need or can't afford the async lookups
            return new EvalRunDto
            {
                EvalRunId = entity.EvalRunId,
                MetricsConfigurationId = entity.MetricsConfigurationId,
                DataSetId = entity.DataSetId,
                AgentId = entity.AgentId,
                Status = entity.Status,
                LastUpdatedBy = entity.LastUpdatedBy,
                LastUpdatedOn = entity.LastUpdatedOn,
                StartedDatetime = entity.StartedDatetime,
                CompletedDatetime = entity.CompletedDatetime,
                EvalRunName = entity.EvalRunName,
                DataSetName = null, // Not fetched in synchronous version
                MetricsConfigurationName = null // Not fetched in synchronous version
            };
        }

        private async Task<(bool isValid, string message)> ValidateReferencedEntitiesAsync(CreateEvalRunDto createDto)
        {
            // Validate DataSet
            var isValidDataSet = await _entityValidators.IsValidDatasetId(createDto.DataSetId.ToString(), createDto.AgentId);
            if (!isValidDataSet)
            {
                return (false, $"DatasetId: {createDto.DataSetId} is invalid for AgentId: {createDto.AgentId}");
            }

            // Validate Metrics Configuration
            var isValidMetrics = await _entityValidators.IsValidMetricsConfigurationId(createDto.MetricsConfigurationId.ToString(), createDto.AgentId);
            if (!isValidMetrics)
            {
                return (false, $"MetricsConfigurationId: {createDto.MetricsConfigurationId} is invalid for AgentId: {createDto.AgentId}");
            }

            return (true, string.Empty);
        }
    }
}