using AutoMapper;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Models;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Request handler for evaluation run operations using the storage project services
    /// </summary>
    public class EvalRunRequestHandler : IEvalRunRequestHandler
    {
        private readonly IEvalRunTableService _evalRunTableService;
        private readonly IAzureQueueStorageService _queueStorageService;
        private readonly IDataVerseAPIService _dataVerseAPIService;
        private readonly IConfigHelper _configHelper;
        private readonly ILogger<EvalRunRequestHandler> _logger;
        private readonly IMapper _mapper;

        public EvalRunRequestHandler(
            IEvalRunTableService evalRunTableService,
            IAzureQueueStorageService queueStorageService,
            IDataVerseAPIService dataVerseAPIService,
            ILogger<EvalRunRequestHandler> logger,
            IMapper mapper,
            IConfigHelper configHelper)
        {
            _evalRunTableService = evalRunTableService;
            _queueStorageService = queueStorageService;
            _dataVerseAPIService = dataVerseAPIService;
            _configHelper = configHelper;
            _logger = logger;
            _mapper = mapper;
        }

        /// <summary>
        /// Create a new evaluation run
        /// </summary>
        public async Task<EvalRunDto> CreateEvalRunAsync(CreateEvalRunDto createDto)
        {
            try
            {
                var evalRunId = Guid.NewGuid();
                var currentDateTime = DateTime.UtcNow;
                
                // Store container name and data path separately for better storage handling
                var containerName = CommonUtils.TrimAndRemoveSpaces(createDto.AgentId); // Ensure valid container name for Storage
                var filePath = $"{_configHelper.EvalResultsFolderName}/{evalRunId}/"; // Create folder structure for multiple output files
                
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
                    FilePath = filePath,
                    Type = createDto.Type,
                    EnvironmentId = createDto.EnvironmentId.ToString(),
                    AgentSchemaName = createDto.AgentSchemaName
                };
                
                // Set the RowKey to the GUID string
                entity.RowKey = evalRunId.ToString();

                var createdEntity = await _evalRunTableService.CreateEvalRunAsync(entity);
                
                _logger.LogInformation("Created evaluation run with ID: {EvalRunId}", evalRunId);

                // Send dataset enrichment request to DataVerse API
                await SendDatasetEnrichmentToDataVerseAsync(evalRunId, createDto);
                
                return MapEntityToDto(createdEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating evaluation run for AgentId: {AgentId}, DataSetId: {DataSetId}", 
                    createDto.AgentId, createDto.DataSetId);
                throw;
            }
        }

        /// <summary>
        /// Send dataset enrichment request to DataVerse API
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <param name="createDto">Original create DTO with all evaluation run details</param>
        private async Task SendDatasetEnrichmentToDataVerseAsync(Guid evalRunId,  CreateEvalRunDto createDto)
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

                // Send to DataVerse API using the injected service
                var dataVerseResponse = await _dataVerseAPIService.PostEvalRunAsync(dataVerseRequest);

                if (dataVerseResponse.Success)
                {
                    _logger.LogInformation("Successfully posted evaluation run request to DataVerse API for EvalRunId: {EvalRunId}. Status: {StatusCode}", 
                        evalRunId, dataVerseResponse.StatusCode);
                }
                else
                {
                    _logger.LogWarning("Failed to post evaluation run request to DataVerse API for EvalRunId: {EvalRunId}. Status: {StatusCode}, Message: {Message}", 
                        evalRunId, dataVerseResponse.StatusCode, dataVerseResponse.Message);
                }

                // Also create enrichment request for Azure Storage Queue
                //var enrichmentRequest = new DatasetEnrichmentRequest
                //{
                //    EvalRunId = evalRunId,
                //    DatasetId = createDto.DataSetId,
                //    AgentId = createDto.AgentId,
                //    RequestedAt = DateTime.UtcNow,
                //    Priority = "Normal",
                //    Metadata = new Dictionary<string, object>
                //    {
                //        { "Type", createDto.Type },
                //        { "EnvironmentId", createDto.EnvironmentId },
                //        { "AgentSchemaName", createDto.AgentSchemaName },
                //        { "MetricsConfigurationId", createDto.MetricsConfigurationId }
                //    }
                //};

                //// Optionally also send to Azure Storage Queue for backup processing
                //try
                //{
                //    var messageContent = JsonSerializer.Serialize(enrichmentRequest, new JsonSerializerOptions 
                //    { 
                //        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                //    });

                //    var queueName = _configHelper.GetDatasetEnrichmentRequestsQueueName();
                //    var queueSuccess = await _queueStorageService.SendMessageAsync(queueName, messageContent);

                //    if (queueSuccess)
                //    {
                //        _logger.LogInformation("Successfully sent dataset enrichment request to queue {QueueName} for EvalRunId: {EvalRunId}", queueName, evalRunId);
                //    }
                //    else
                //    {
                //        _logger.LogWarning("Failed to send dataset enrichment request to queue {QueueName} for EvalRunId: {EvalRunId}", queueName, evalRunId);
                //    }
                //}
                //catch (Exception queueEx)
                //{
                //    _logger.LogError(queueEx, "Error sending to Azure Storage Queue for EvalRunId: {EvalRunId}", evalRunId);
                //    // Don't fail the overall process if queue fails
                //}
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the evaluation run creation
                // The enrichment can be retried or handled separately
                _logger.LogError(ex, "Error sending dataset enrichment request to DataVerse API for EvalRunId: {EvalRunId}, DatasetId: {DatasetId}",
                    evalRunId, createDto.DataSetId);
                
                // Don't throw here to prevent failing the evaluation run creation
                // The DataVerse integration is supplementary to the core functionality
            }
        }

        /// <summary>
        /// Update evaluation run status
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

                var updatedEntity = await _evalRunTableService.UpdateEvalRunStatusAsync(
                    updateDto.AgentId, 
                    updateDto.EvalRunId, 
                    normalizedStatus, 
                    "System");

                if (updatedEntity == null)
                {
                    _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", updateDto.EvalRunId);
                    return null;
                }
                
                _logger.LogInformation("Updated evaluation run status to {Status} for ID: {EvalRunId}", 
                    normalizedStatus, updateDto.EvalRunId);
                
                return MapEntityToDto(updatedEntity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating evaluation run status for ID: {EvalRunId}", updateDto.EvalRunId);
                throw;
            }
        }

        /// <summary>
        /// Get evaluation run by ID
        /// </summary>
        public async Task<EvalRunDto?> GetEvalRunByIdAsync(string agentId, Guid evalRunId)
        {
            try
            {
                var entity = await _evalRunTableService.GetEvalRunByIdAsync(agentId, evalRunId);
                
                if (entity == null)
                {
                    _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
                    return null;
                }

                return MapEntityToDto(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation run with ID: {EvalRunId}", evalRunId);
                throw;
            }
        }

        /// <summary>
        /// Get evaluation run by ID (searches across all partitions - less efficient)
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
                
                return MapEntityToDto(entity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving evaluation run with ID: {EvalRunId}", evalRunId);
                throw;
            }
        }

        /// <summary>
        /// Get all evaluation runs for an agent
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
        /// Get evaluation run entity with internal details (for internal use only)
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

        /// <summary>
        /// Map EvalRunTableEntity to EvalRunDto
        /// </summary>
        private EvalRunDto MapEntityToDto(EvalRunTableEntity entity)
        {
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
                CompletedDatetime = entity.CompletedDatetime
                // Note: FilePath and ContainerName are internal details and not exposed to API consumers
            };
        }
    }
}