using AutoMapper;
using SxgEvalPlatformApi.Models;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;
using System.Text.Json;
using static SXG.EvalPlatform.Common.CommonConstants;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Request handler for evaluation run operations using the storage project services
    /// </summary>
    public class EvalRunRequestHandler : IEvalRunRequestHandler
    {
        private readonly IEvalRunTableService _evalRunTableService;
        private readonly IAzureBlobStorageService _blobStorageService;
        private readonly IAzureQueueStorageService _queueStorageService;
        private readonly IConfigHelper _configHelper;
        private readonly ILogger<EvalRunRequestHandler> _logger;
        private readonly IMapper _mapper;

        public EvalRunRequestHandler(
            IEvalRunTableService evalRunTableService,
            IAzureBlobStorageService blobStorageService,
            IAzureQueueStorageService queueStorageService,
            ILogger<EvalRunRequestHandler> logger,
            IMapper mapper,
            IConfigHelper configHelper)
        {
            _evalRunTableService = evalRunTableService;
            _blobStorageService = blobStorageService;
            _queueStorageService = queueStorageService;
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
                
                // Store container name and blob path separately for better blob storage handling
                var containerName = CommonUtils.TrimAndRemoveSpaces(createDto.AgentId); // Ensure valid container name for Azure Blob Storage
                var blobFilePath = $"{_configHelper.EvalResultsFolderName}/{evalRunId}/"; // Create folder structure for multiple output files
                
                var entity = new EvalRunTableEntity
                {
                    EvalRunId = evalRunId, // Store as GUID
                    AgentId = createDto.AgentId, // This will also set PartitionKey
                    DataSetId = createDto.DataSetId.ToString(),
                    MetricsConfigurationId = createDto.MetricsConfigurationId.ToString(),
                    Status = EvalRunStatus.RequestSubmitted,
                    LastUpdatedOn = currentDateTime,
                    StartedDatetime = currentDateTime,
                    ContainerName = containerName,
                    BlobFilePath = blobFilePath,
                    Type = createDto.Type,
                    EnvironmentId = createDto.EnvironmentId.ToString(),
                    AgentSchemaName = createDto.AgentSchemaName
                };
                
                // Set the RowKey to the GUID string
                entity.RowKey = evalRunId.ToString();

                var createdEntity = await _evalRunTableService.CreateEvalRunAsync(entity);
                
                _logger.LogInformation("Created evaluation run with ID: {EvalRunId}", evalRunId);

                // Send message to queue for dataset enrichment
                await SendDatasetEnrichmentRequestAsync(evalRunId, createDto.DataSetId, createDto.AgentId);
                
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
        /// Send dataset enrichment request to Azure Storage Queue
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <param name="datasetId">Dataset ID</param>
        /// <param name="agentId">Agent ID</param>
        private async Task SendDatasetEnrichmentRequestAsync(Guid evalRunId, Guid datasetId, string agentId)
        {
            try
            {
                _logger.LogInformation("Sending dataset enrichment request to queue for EvalRunId: {EvalRunId}, DatasetId: {DatasetId}", 
                    evalRunId, datasetId);

                var enrichmentRequest = new DatasetEnrichmentRequest
                {
                    EvalRunId = evalRunId,
                    DatasetId = datasetId,
                    AgentId = agentId,
                    RequestedAt = DateTime.UtcNow,
                    Priority = "Normal"
                };

                var messageContent = JsonSerializer.Serialize(enrichmentRequest, new JsonSerializerOptions 
                { 
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
                });

                var queueName = _configHelper.GetDatasetEnrichmentRequestsQueueName();
                var success = await _queueStorageService.SendMessageAsync(queueName, messageContent);

                if (success)
                {
                    _logger.LogInformation("Successfully sent dataset enrichment request to queue {QueueName} for EvalRunId: {EvalRunId}", queueName, evalRunId);
                }
                else
                {
                    _logger.LogWarning("Failed to send dataset enrichment request to queue {QueueName} for EvalRunId: {EvalRunId}", queueName, evalRunId);
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the evaluation run creation
                // The enrichment can be retried or handled separately
                _logger.LogError(ex, "Error sending dataset enrichment request to queue for EvalRunId: {EvalRunId}, DatasetId: {DatasetId}", 
                    evalRunId, datasetId);
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

                if (string.Equals(updateDto.Status, EvalRunStatus.EvalRunStarted, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = EvalRunStatus.EvalRunStarted;
                else if (string.Equals(updateDto.Status, EvalRunStatus.EnrichingDataset, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = EvalRunStatus.EnrichingDataset;
                else if (string.Equals(updateDto.Status, EvalRunStatus.EvalRunCompleted, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = EvalRunStatus.EvalRunCompleted;
                else if (string.Equals(updateDto.Status, EvalRunStatus.DatasetEnrichmentCompleted, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = EvalRunStatus.DatasetEnrichmentCompleted;
                else if (string.Equals(updateDto.Status, EvalRunStatus.EvalRunFailed, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = EvalRunStatus.EvalRunFailed;
                else if (string.Equals(updateDto.Status, EvalRunStatus.RequestSubmitted, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = EvalRunStatus.RequestSubmitted;


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
                // Note: BlobFilePath and ContainerName are internal details and not exposed to API consumers
            };
        }
    }
}