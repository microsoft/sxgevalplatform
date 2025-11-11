using AutoMapper;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Models;
using System.Text.Json;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Interface for DataSet request handler operations
    /// </summary>
    public interface IDataSetRequestHandler
    {
        /// <summary>
        /// Save a dataset (create new or update existing)
        /// </summary>
        /// <param name="saveDatasetDto">Dataset save request</param>
        /// <returns>Dataset save response</returns>
        Task<DatasetSaveResponseDto> SaveDatasetAsync(SaveDatasetDto saveDatasetDto);

        /// <summary>
        /// Get all datasets for an agent
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <returns>List of dataset metadata</returns>
        Task<IList<DatasetMetadataDto>> GetDatasetsByAgentIdAsync(string agentId);

        /// <summary>
        /// Get dataset content by dataset ID
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <returns>Dataset content as JSON string</returns>
        Task<string?> GetDatasetByIdAsync(string datasetId);

        /// <summary>
        /// Get dataset metadata by dataset ID
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <returns>Dataset metadata</returns>
        Task<DatasetMetadataDto?> GetDatasetMetadataByIdAsync(string datasetId);

        /// <summary>
        /// Update an existing dataset
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <param name="updateDatasetDto">Update dataset request</param>
        /// <returns>Dataset save response</returns>
        Task<DatasetSaveResponseDto> UpdateDatasetAsync(string datasetId, UpdateDatasetDto updateDatasetDto);

        /// <summary>
        /// Delete a dataset by dataset ID
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteDatasetAsync(string datasetId);
    }

    /// <summary>
    /// Request handler for dataset operations using the storage project services with caching support
    /// </summary>
    public class DataSetRequestHandler : IDataSetRequestHandler
    {
        private readonly IDataSetTableService _dataSetTableService;
        private readonly IAzureBlobStorageService _blobStorageService;
        private readonly IConfigHelper _configHelper;
        private readonly ILogger<DataSetRequestHandler> _logger;
        private readonly IMapper _mapper;
        private readonly ICacheManager _cacheManager;

        // Cache key patterns
        private const string DATASET_CONTENT_CACHE_KEY = "dataset_content:{0}"; // dataset_content:datasetId
        private const string DATASET_METADATA_CACHE_KEY = "dataset_metadata:{0}"; // dataset_metadata:datasetId
        private const string DATASETS_BY_AGENT_CACHE_KEY = "datasets_agent:{0}"; // datasets_agent:agentId

        public DataSetRequestHandler(
            IDataSetTableService dataSetTableService,
            IAzureBlobStorageService blobStorageService,
            ILogger<DataSetRequestHandler> logger,
            IMapper mapper,
            IConfigHelper configHelper,
            ICacheManager cacheManager)
        {
            _dataSetTableService = dataSetTableService;
            _logger = logger;
            _mapper = mapper;
            _blobStorageService = blobStorageService;
            _configHelper = configHelper;
            _cacheManager = cacheManager;
        }

        /// <summary>
        /// Save a dataset (upsert: create new or update existing) and update cache
        /// </summary>
        public async Task<DatasetSaveResponseDto> SaveDatasetAsync(SaveDatasetDto saveDatasetDto)
        {
            try
            {
                _logger.LogInformation("Saving dataset: {DatasetName} for agent: {AgentId}, type: {DatasetType}",
                    saveDatasetDto.DatasetName, saveDatasetDto.AgentId, saveDatasetDto.DatasetType);

                // Check if dataset already exists (upsert behavior)
                var existingDatasets = await _dataSetTableService.GetDataSetsByDatasetNameAsync(
                    saveDatasetDto.AgentId, saveDatasetDto.DatasetName);

                var existingDataset = existingDatasets
                    .FirstOrDefault(d => d.DatasetType.Equals(saveDatasetDto.DatasetType, StringComparison.OrdinalIgnoreCase));

                if (existingDataset != null)
                {
                    // UPDATE existing dataset instead of returning error
                    _logger.LogInformation("Dataset already exists, updating: {DatasetId}", existingDataset.DatasetId);
                    
                    var updateDto = new UpdateDatasetDto
                    {
                        DatasetRecords = saveDatasetDto.DatasetRecords
                     };
   
                    return await UpdateDatasetAsync(existingDataset.DatasetId, updateDto);
                }

                // CREATE new dataset
                var datasetId = Guid.NewGuid().ToString();
                var currentTime = DateTime.UtcNow;

                // Create entity with GUID
                var entity = _mapper.Map<DataSetTableEntity>(saveDatasetDto);
                entity.DatasetId = datasetId;
                entity.RowKey = datasetId;
                entity.CreatedBy = "System"; // Default since UserMetadata is no longer required
                entity.CreatedOn = currentTime;
                entity.LastUpdatedBy = "System"; // Default since UserMetadata is no longer required
                entity.LastUpdatedOn = currentTime;

                // Create blob path with GUID in filename
                var blobContainer = CommonUtils.TrimAndRemoveSpaces(saveDatasetDto.AgentId);
                var blobFileName = $"{saveDatasetDto.DatasetType}_{saveDatasetDto.DatasetName}_{datasetId}.json";
                var blobFilePath = $"{_configHelper.GetDatasetsFolderName()}/{blobFileName}";
                
                entity.BlobFilePath = blobFilePath;
                entity.ContainerName = blobContainer;

                // Save dataset content to blob (write to backend store first)
                var datasetContent = JsonSerializer.Serialize(saveDatasetDto.DatasetRecords, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var blobWriteResult = await _blobStorageService.WriteBlobContentAsync(blobContainer, blobFilePath, datasetContent);

                // Save metadata to table storage
                var savedEntity = await _dataSetTableService.SaveDataSetAsync(entity);

                // After successful creation, update cache
                var metadataDto = ToDatasetMetadataDto(savedEntity);

                // Cache the content and metadata
                var contentCacheKey = string.Format(DATASET_CONTENT_CACHE_KEY, datasetId);
                var metadataCacheKey = string.Format(DATASET_METADATA_CACHE_KEY, datasetId);
                
                await _cacheManager.SetAsync(contentCacheKey, datasetContent, TimeSpan.FromHours(2));
                await _cacheManager.SetAsync(metadataCacheKey, metadataDto, TimeSpan.FromHours(2));

                // Invalidate agent-based list cache
                await InvalidateAgentDatasetCaches(saveDatasetDto.AgentId);

                var response = new DatasetSaveResponseDto
                {
                    DatasetId = savedEntity.DatasetId,
                    Status = "created",
                    Message = "Dataset created successfully",
                    CreatedBy = savedEntity.CreatedBy,
                    CreatedOn = savedEntity.CreatedOn,
                    LastUpdatedBy = savedEntity.LastUpdatedBy,
                    LastUpdatedOn = savedEntity.LastUpdatedOn
                };

                _logger.LogInformation("Successfully created dataset with ID: {DatasetId} and updated cache",
                    savedEntity.DatasetId);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save dataset for Agent: {AgentId}",
                    saveDatasetDto.AgentId);

                return new DatasetSaveResponseDto
                {
                    DatasetId = string.Empty,
                    Status = "error",
                    Message = $"Failed to save dataset: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Get all datasets for an agent with caching support
        /// </summary>
        public async Task<IList<DatasetMetadataDto>> GetDatasetsByAgentIdAsync(string agentId)
        {
            try
            {
                _logger.LogInformation("Retrieving all datasets for Agent: {AgentId}", agentId);

                // Check cache first
                var cacheKey = string.Format(DATASETS_BY_AGENT_CACHE_KEY, agentId);
                var cachedResult = await _cacheManager.GetAsync<IList<DatasetMetadataDto>>(cacheKey);

                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached datasets for Agent: {AgentId}", agentId);
                    return cachedResult;
                }

                // If not in cache, fetch from storage
                var entities = await _dataSetTableService.GetAllDataSetsByAgentIdAsync(agentId);
                var datasets = entities.Select(ToDatasetMetadataDto).ToList();

                // Cache the result (cache for 30 minutes for list queries)
                await _cacheManager.SetAsync(cacheKey, datasets, TimeSpan.FromMinutes(30));
                _logger.LogDebug("Cached datasets for Agent: {AgentId}", agentId);

                _logger.LogInformation("Retrieved {Count} datasets for Agent: {AgentId}",
                    datasets.Count, agentId);

                return datasets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve datasets for Agent: {AgentId}", agentId);
                throw;
            }
        }

        /// <summary>
        /// Get dataset content by ID with caching support
        /// </summary>
        public async Task<string?> GetDatasetByIdAsync(string datasetId)
        {
            try
            {
                _logger.LogInformation("Retrieving dataset content for DatasetId: {DatasetId}", datasetId);

                // Check cache first
                var cacheKey = string.Format(DATASET_CONTENT_CACHE_KEY, datasetId);
                var cachedResult = await _cacheManager.GetAsync<string>(cacheKey);

                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached dataset content for DatasetId: {DatasetId}", datasetId);
                    return cachedResult;
                }

                // If not in cache, fetch from storage
                var entity = await _dataSetTableService.GetDataSetByIdAsync(datasetId);
                if (entity == null)
                {
                    _logger.LogInformation("Dataset not found for DatasetId: {DatasetId}", datasetId);
                    return null;
                }

                var blobPath = entity.BlobFilePath;
                var blobContainer = entity.ContainerName;

                var blobContent = await _blobStorageService.ReadBlobContentAsync(blobContainer, blobPath);

                if (string.IsNullOrEmpty(blobContent))
                {
                    throw new Exception($"Dataset blob not found: {blobContainer}/{blobPath}");
                }

                // Cache the result (cache for 2 hours)
                await _cacheManager.SetAsync(cacheKey, blobContent, TimeSpan.FromHours(2));
                _logger.LogDebug("Cached dataset content for DatasetId: {DatasetId}", datasetId);

                _logger.LogInformation("Retrieved dataset content for DatasetId: {DatasetId}", datasetId);
                return blobContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve dataset content for DatasetId: {DatasetId}", datasetId);
                throw;
            }
        }

        /// <summary>
        /// Get dataset metadata by ID with caching support
        /// </summary>
        public async Task<DatasetMetadataDto?> GetDatasetMetadataByIdAsync(string datasetId)
        {
            try
            {
                _logger.LogInformation("Retrieving dataset metadata for DatasetId: {DatasetId}", datasetId);

                // Check cache first
                var cacheKey = string.Format(DATASET_METADATA_CACHE_KEY, datasetId);
                var cachedResult = await _cacheManager.GetAsync<DatasetMetadataDto>(cacheKey);

                if (cachedResult != null)
                {
                    _logger.LogDebug("Returning cached dataset metadata for DatasetId: {DatasetId}", datasetId);
                    return cachedResult;
                }

                // If not in cache, fetch from storage
                var entity = await _dataSetTableService.GetDataSetByIdAsync(datasetId);
                if (entity == null)
                {
                    _logger.LogInformation("Dataset metadata not found for DatasetId: {DatasetId}", datasetId);
                    return null;
                }

                var metadata = ToDatasetMetadataDto(entity);

                // Cache the result (cache for 2 hours)
                await _cacheManager.SetAsync(cacheKey, metadata, TimeSpan.FromHours(2));
                _logger.LogDebug("Cached dataset metadata for DatasetId: {DatasetId}", datasetId);

                _logger.LogInformation("Retrieved dataset metadata for DatasetId: {DatasetId}", datasetId);
                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve dataset metadata for DatasetId: {DatasetId}", datasetId);
                throw;
            }
        }

        /// <summary>
        /// Update an existing dataset and update cache
        /// </summary>
        public async Task<DatasetSaveResponseDto> UpdateDatasetAsync(string datasetId, UpdateDatasetDto updateDatasetDto)
        {
            try
            {
                _logger.LogInformation("Updating dataset with ID: {DatasetId}", datasetId);

                // Get existing dataset
                var existingEntity = await _dataSetTableService.GetDataSetByIdAsync(datasetId);
                if (existingEntity == null)
                {
                    _logger.LogWarning("Dataset not found with ID: {DatasetId}", datasetId);
                    return new DatasetSaveResponseDto
                    {
                        DatasetId = datasetId,
                        Status = "error",
                        Message = $"Dataset with ID '{datasetId}' not found"
                    };
                }

                var currentTime = DateTime.UtcNow;

                // Update audit fields
                existingEntity.LastUpdatedBy = "System"; // Default since UserMetadata is no longer required
                existingEntity.LastUpdatedOn = currentTime;

                // Update blob with new dataset content (write to backend store first)
                var blobContainer = existingEntity.ContainerName;
                var blobFilePath = existingEntity.BlobFilePath;

                // Save updated dataset content to blob
                var datasetContent = JsonSerializer.Serialize(updateDatasetDto.DatasetRecords, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var blobWriteResult = await _blobStorageService.WriteBlobContentAsync(blobContainer, blobFilePath, datasetContent);

                // Save updated metadata to table storage
                var savedEntity = await _dataSetTableService.SaveDataSetAsync(existingEntity);

                // After successful update, update cache
                var metadataDto = ToDatasetMetadataDto(savedEntity);
   
                // Update cache with new content and metadata
                var contentCacheKey = string.Format(DATASET_CONTENT_CACHE_KEY, datasetId);
                var metadataCacheKey = string.Format(DATASET_METADATA_CACHE_KEY, datasetId);
      
                await _cacheManager.SetAsync(contentCacheKey, datasetContent, TimeSpan.FromHours(2));
                await _cacheManager.SetAsync(metadataCacheKey, metadataDto, TimeSpan.FromHours(2));

                // Invalidate agent-based list cache
                await InvalidateAgentDatasetCaches(existingEntity.AgentId);

                var response = new DatasetSaveResponseDto
                {
                    DatasetId = savedEntity.DatasetId,
                    Status = "updated",
                    Message = "Dataset updated successfully",
                    CreatedBy = savedEntity.CreatedBy,
                    CreatedOn = savedEntity.CreatedOn,
                    LastUpdatedBy = savedEntity.LastUpdatedBy,
                    LastUpdatedOn = savedEntity.LastUpdatedOn
                };

                _logger.LogInformation("Successfully updated dataset with ID: {DatasetId} and updated cache",
                    savedEntity.DatasetId);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update dataset with ID: {DatasetId}", datasetId);
                return new DatasetSaveResponseDto
                {
                    DatasetId = datasetId,
                    Status = "error",
                    Message = "Failed to update dataset: " + ex.Message
                };
            }
        }

        /// <summary>
        /// Delete a dataset and update cache
        /// </summary>
        public async Task<bool> DeleteDatasetAsync(string datasetId)
        {
            try
            {
                _logger.LogInformation("Deleting dataset with ID: {DatasetId}", datasetId);

                // First get the dataset to get the AgentId and blob path for deletion
                var existingDataset = await _dataSetTableService.GetDataSetByIdAsync(datasetId);

                if (existingDataset == null)
                {
                    _logger.LogWarning("Dataset with ID: {DatasetId} not found", datasetId);
                    return false;
                }

                // Delete from storage first
                bool deleted = await _dataSetTableService.DeleteDataSetAsync(existingDataset.AgentId, datasetId);

                if (deleted)
                {
                    // After successful deletion, remove from cache
                    var contentCacheKey = string.Format(DATASET_CONTENT_CACHE_KEY, datasetId);
                    var metadataCacheKey = string.Format(DATASET_METADATA_CACHE_KEY, datasetId);
     
                    await _cacheManager.RemoveAsync(contentCacheKey);
                    await _cacheManager.RemoveAsync(metadataCacheKey);

                    // Invalidate agent-based list cache
                    await InvalidateAgentDatasetCaches(existingDataset.AgentId);

                    _logger.LogInformation("Dataset with ID: {DatasetId} deleted successfully and removed from cache", datasetId);
      
                    // Also delete the blob file if it exists
                    try
                    {
                        var containerName = $"agent-{CommonUtils.TrimAndRemoveSpaces(existingDataset.AgentId)}";
                        var blobPath = $"datasets/{datasetId}.json";
        
                        // Check if blob exists before attempting to delete
                        bool blobExists = await _blobStorageService.BlobExistsAsync(containerName, blobPath);
                        if (blobExists)
                        {
                            await _blobStorageService.DeleteBlobAsync(containerName, blobPath);
                            _logger.LogInformation("Dataset blob file deleted: {ContainerName}/{BlobPath}", containerName, blobPath);
                        }
                    }
                    catch (Exception blobEx)
                    {
                        _logger.LogWarning(blobEx, "Failed to delete blob file for dataset ID: {DatasetId}, but table record was deleted", datasetId);
                        // Continue - table deletion was successful, blob deletion failure is not critical
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to delete dataset with ID: {DatasetId}", datasetId);
                }

                return deleted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while deleting dataset: {DatasetId}", datasetId);
                return false;
            }
        }

        /// <summary>
        /// Invalidate agent-based dataset caches
        /// </summary>
        private async Task InvalidateAgentDatasetCaches(string agentId)
        {
            try
            {
                _logger.LogDebug("Invalidating dataset caches for AgentId: {AgentId}", agentId);

                // In a production system, you might want to implement cache tagging or use a more sophisticated cache invalidation strategy
                // For now, we just log this since wildcard cache removal isn't easily supported across all cache providers

                var statistics = await _cacheManager.GetStatisticsAsync();
                _logger.LogDebug("Cache statistics after potential invalidation - Type: {CacheType}, Items: {ItemCount}",
                 statistics.CacheType, statistics.ItemCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error invalidating dataset caches for AgentId: {AgentId}", agentId);
                // Don't throw - cache invalidation failure shouldn't break the main operation
            }
        }

        #region Private Helper Methods

        
        /// <summary>
        /// Convert DataSetTableEntity to DatasetMetadataDto
        /// </summary>
        private DatasetMetadataDto ToDatasetMetadataDto(DataSetTableEntity entity)
        {
            return _mapper.Map<DatasetMetadataDto>(entity);
        }

        #endregion
    }
}
