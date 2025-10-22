using AutoMapper;
using SxgEvalPlatformApi.Models;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Request handler for evaluation run operations using the storage project services
    /// </summary>
    public class EvalRunRequestHandler : IEvalRunRequestHandler
    {
        private readonly IEvalRunTableService _evalRunTableService;
        private readonly IAzureBlobStorageService _blobStorageService;
        private readonly IConfigHelper _configHelper;
        private readonly ILogger<EvalRunRequestHandler> _logger;
        private readonly IMapper _mapper;

        public EvalRunRequestHandler(
            IEvalRunTableService evalRunTableService,
            IAzureBlobStorageService blobStorageService,
            ILogger<EvalRunRequestHandler> logger,
            IMapper mapper,
            IConfigHelper configHelper)
        {
            _evalRunTableService = evalRunTableService;
            _blobStorageService = blobStorageService;
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
                var blobFilePath = $"evalresults/{evalRunId}/"; // Create folder structure for multiple output files
                
                var entity = new EvalRunTableEntity
                {
                    EvalRunId = evalRunId, // Store as GUID
                    AgentId = createDto.AgentId, // This will also set PartitionKey
                    DataSetId = createDto.DataSetId.ToString(),
                    MetricsConfigurationId = createDto.MetricsConfigurationId.ToString(),
                    Status = Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunStatusConstants.Queued,
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
        /// Update evaluation run status
        /// </summary>
        public async Task<EvalRunDto?> UpdateEvalRunStatusAsync(UpdateEvalRunStatusDto updateDto)
        {
            try
            {
                // Normalize the status to ensure consistent casing in storage
                string normalizedStatus = updateDto.Status;
                if (string.Equals(updateDto.Status, Models.EvalRunStatusConstants.Queued, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunStatusConstants.Queued;
                else if (string.Equals(updateDto.Status, Models.EvalRunStatusConstants.Running, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunStatusConstants.Running;
                else if (string.Equals(updateDto.Status, Models.EvalRunStatusConstants.Completed, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunStatusConstants.Completed;
                else if (string.Equals(updateDto.Status, Models.EvalRunStatusConstants.Failed, StringComparison.OrdinalIgnoreCase))
                    normalizedStatus = Sxg.EvalPlatform.API.Storage.TableEntities.EvalRunStatusConstants.Failed;

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
        public async Task<List<EvalRunDto>> GetEvalRunsByAgentIdAsync(string agentId)
        {
            try
            {
                var entities = await _evalRunTableService.GetEvalRunsByAgentIdAsync(agentId);
                
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