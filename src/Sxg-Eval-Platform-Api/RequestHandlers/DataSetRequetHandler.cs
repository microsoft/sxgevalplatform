using AutoMapper;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Cache;
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
    /// Request handler for dataset operations using the storage project services
    /// </summary>
    public class DataSetRequestHandler : IDataSetRequestHandler
    {
        private readonly IDataSetTableService _dataSetTableService;
        private readonly IAzureBlobStorageService _blobStorageService;
        private readonly IConfigHelper _configHelper;
        private readonly ILogger<DataSetRequestHandler> _logger;
        private readonly IMapper _mapper;
        private readonly IEvalDatasetCache _datasetCache;

        public DataSetRequestHandler(
            IDataSetTableService dataSetTableService,
            IAzureBlobStorageService blobStorageService,
            ILogger<DataSetRequestHandler> logger,
            IMapper mapper,
            IConfigHelper configHelper,
            IEvalDatasetCache datasetCache)
        {
            _dataSetTableService = dataSetTableService;
            _logger = logger;
            _mapper = mapper;
            _blobStorageService = blobStorageService;
            _configHelper = configHelper;
            _datasetCache = datasetCache;
        }

        public async Task<DatasetSaveResponseDto> SaveDatasetAsync(SaveDatasetDto saveDatasetDto)
        {
            try
            {
                _logger.LogInformation("Creating dataset: {DatasetName} for agent: {AgentId}, type: {DatasetType}",
                    saveDatasetDto.DatasetName, saveDatasetDto.AgentId, saveDatasetDto.DatasetType);

                // Fast duplicate check using Redis cache first
                var cachedDatasets = await _datasetCache.GetDatasetsByAgentAsync(saveDatasetDto.AgentId);
                if (cachedDatasets != null)
                {
                    var existingCachedDataset = cachedDatasets
                        .FirstOrDefault(d => d.DatasetName.Equals(saveDatasetDto.DatasetName, StringComparison.OrdinalIgnoreCase) &&
                                           d.DatasetType.Equals(saveDatasetDto.DatasetType, StringComparison.OrdinalIgnoreCase));
                    
                    if (existingCachedDataset != null)
                    {
                        _logger.LogInformation("Duplicate dataset detected in cache for Agent: {AgentId}, Dataset: {DatasetName}, Type: {DatasetType}", 
                            saveDatasetDto.AgentId, saveDatasetDto.DatasetName, saveDatasetDto.DatasetType);
                        return new DatasetSaveResponseDto
                        {
                            DatasetId = existingCachedDataset.DatasetId,
                            Status = "conflict",
                            Message = $"Dataset with name '{saveDatasetDto.DatasetName}' and type '{saveDatasetDto.DatasetType}' already exists for agent '{saveDatasetDto.AgentId}'",
                            CreatedBy = existingCachedDataset.CreatedBy,
                            CreatedOn = existingCachedDataset.CreatedOn,
                            LastUpdatedBy = existingCachedDataset.LastUpdatedBy,
                            LastUpdatedOn = existingCachedDataset.LastUpdatedOn
                        };
                    }
                }

                // Fallback to database check if cache miss (for thorough validation)
                var existingDatasets = await _dataSetTableService.GetDataSetsByDatasetNameAsync(
                    saveDatasetDto.AgentId, saveDatasetDto.DatasetName);

                var existingDataset = existingDatasets
                    .FirstOrDefault(d => d.DatasetType.Equals(saveDatasetDto.DatasetType, StringComparison.OrdinalIgnoreCase));

                if (existingDataset != null)
                {
                    _logger.LogInformation("Duplicate dataset detected in database for Agent: {AgentId}, Dataset: {DatasetName}, Type: {DatasetType}", 
                        saveDatasetDto.AgentId, saveDatasetDto.DatasetName, saveDatasetDto.DatasetType);
                    return new DatasetSaveResponseDto
                    {
                        DatasetId = existingDataset.DatasetId,
                        Status = "conflict",
                        Message = $"Dataset with name '{saveDatasetDto.DatasetName}' and type '{saveDatasetDto.DatasetType}' already exists for agent '{saveDatasetDto.AgentId}'",
                        CreatedBy = existingDataset.CreatedBy,
                        CreatedOn = existingDataset.CreatedOn,
                        LastUpdatedBy = existingDataset.LastUpdatedBy,
                        LastUpdatedOn = existingDataset.LastUpdatedOn
                    };
                }

                // Generate GUID first for filename
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

                // Save dataset content to blob
                var datasetContent = JsonSerializer.Serialize(saveDatasetDto.DatasetRecords, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var blobWriteResult = await _blobStorageService.WriteBlobContentAsync(blobContainer, blobFilePath, datasetContent);

                // Save metadata to table storage
                var savedEntity = await _dataSetTableService.SaveDataSetAsync(entity);

                // Cache the new dataset metadata and content (write-through pattern)
                try
                {
                    var datasetMetadataDto = ToDatasetMetadataDto(savedEntity);
                    await _datasetCache.SetDatasetMetadataAsync(datasetMetadataDto);
                    await _datasetCache.SetDatasetContentAsync(savedEntity.DatasetId, datasetContent);

                    // Refresh agent's dataset list cache
                    var allDatasets = await _dataSetTableService.GetAllDataSetsByAgentIdAsync(savedEntity.AgentId);
                    var allDatasetDtos = allDatasets.Select(ToDatasetMetadataDto).ToList();
                    await _datasetCache.SetDatasetsByAgentAsync(savedEntity.AgentId, allDatasetDtos);

                    _logger.LogDebug("Cached new dataset: {DatasetId} for Agent: {AgentId}", 
                        savedEntity.DatasetId, savedEntity.AgentId);
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "Failed to cache dataset {DatasetId} after creation", savedEntity.DatasetId);
                    // Continue - cache failure shouldn't fail the operation
                }

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

                _logger.LogInformation("Successfully created dataset with ID: {DatasetId} by user: {UserEmail}",
                    savedEntity.DatasetId, "System");

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

        public async Task<IList<DatasetMetadataDto>> GetDatasetsByAgentIdAsync(string agentId)
        {
            try
            {
                _logger.LogInformation("Retrieving all datasets for Agent: {AgentId}", agentId);

                // Try cache first
                var cachedDatasets = await _datasetCache.GetDatasetsByAgentAsync(agentId);
                if (cachedDatasets != null)
                {
                    _logger.LogInformation("Retrieved {Count} datasets from cache for Agent: {AgentId}", 
                        cachedDatasets.Count, agentId);
                    return cachedDatasets;
                }

                // Cache miss - fetch from database
                var entities = await _dataSetTableService.GetAllDataSetsByAgentIdAsync(agentId);
                var datasets = entities.Select(ToDatasetMetadataDto).ToList();

                // Cache the results
                try
                {
                    await _datasetCache.SetDatasetsByAgentAsync(agentId, datasets);
                    _logger.LogDebug("Cached {Count} datasets for Agent: {AgentId}", datasets.Count, agentId);
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "Failed to cache datasets for Agent: {AgentId}", agentId);
                    // Continue - cache failure shouldn't fail the operation
                }

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

        public async Task<string?> GetDatasetByIdAsync(string datasetId)
        {
            try
            {
                _logger.LogInformation("Retrieving dataset content for DatasetId: {DatasetId}", datasetId);

                // Try cache first
                var cachedContent = await _datasetCache.GetDatasetContentAsync(datasetId);
                if (cachedContent != null)
                {
                    _logger.LogInformation("Retrieved dataset content from cache for DatasetId: {DatasetId}", datasetId);
                    return cachedContent;
                }

                // Cache miss - fetch from storage
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

                // Cache the content
                try
                {
                    await _datasetCache.SetDatasetContentAsync(datasetId, blobContent);
                    _logger.LogDebug("Cached dataset content for DatasetId: {DatasetId}", datasetId);
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "Failed to cache dataset content for DatasetId: {DatasetId}", datasetId);
                    // Continue - cache failure shouldn't fail the operation
                }

                _logger.LogInformation("Retrieved dataset content for DatasetId: {DatasetId}", datasetId);
                return blobContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve dataset content for DatasetId: {DatasetId}", datasetId);
                throw;
            }
        }

        public async Task<DatasetMetadataDto?> GetDatasetMetadataByIdAsync(string datasetId)
        {
            try
            {
                _logger.LogInformation("Retrieving dataset metadata for DatasetId: {DatasetId}", datasetId);

                // Try cache first
                var cachedMetadata = await _datasetCache.GetDatasetMetadataAsync(datasetId);
                if (cachedMetadata != null)
                {
                    _logger.LogInformation("Retrieved dataset metadata from cache for DatasetId: {DatasetId}", datasetId);
                    return cachedMetadata;
                }

                // Cache miss - fetch from database
                var entity = await _dataSetTableService.GetDataSetByIdAsync(datasetId);
                if (entity == null)
                {
                    _logger.LogInformation("Dataset metadata not found for DatasetId: {DatasetId}", datasetId);
                    return null;
                }

                var metadata = ToDatasetMetadataDto(entity);

                // Cache the metadata
                try
                {
                    await _datasetCache.SetDatasetMetadataAsync(metadata);
                    _logger.LogDebug("Cached dataset metadata for DatasetId: {DatasetId}", datasetId);
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "Failed to cache dataset metadata for DatasetId: {DatasetId}", datasetId);
                    // Continue - cache failure shouldn't fail the operation
                }

                _logger.LogInformation("Retrieved dataset metadata for DatasetId: {DatasetId}", datasetId);
                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve dataset metadata for DatasetId: {DatasetId}", datasetId);
                throw;
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Convert SaveDatasetDto to DataSetTableEntity
        /// </summary>
        private DataSetTableEntity ToDataSetTableEntity(SaveDatasetDto dto)
        {
            return _mapper.Map<DataSetTableEntity>(dto);
        }

        /// <summary>
        /// Convert DataSetTableEntity to DatasetMetadataDto
        /// </summary>
        private DatasetMetadataDto ToDatasetMetadataDto(DataSetTableEntity entity)
        {
            return _mapper.Map<DatasetMetadataDto>(entity);
        }

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

                // Update blob with new dataset content
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

                // Update cache with new data (write-through pattern)
                try
                {
                    var updatedMetadataDto = ToDatasetMetadataDto(savedEntity);
                    await _datasetCache.SetDatasetMetadataAsync(updatedMetadataDto);
                    await _datasetCache.SetDatasetContentAsync(savedEntity.DatasetId, datasetContent);

                    // Refresh agent's dataset list cache
                    var allDatasets = await _dataSetTableService.GetAllDataSetsByAgentIdAsync(savedEntity.AgentId);
                    var allDatasetDtos = allDatasets.Select(ToDatasetMetadataDto).ToList();
                    await _datasetCache.SetDatasetsByAgentAsync(savedEntity.AgentId, allDatasetDtos);

                    _logger.LogDebug("Updated cache for dataset: {DatasetId} for Agent: {AgentId}", 
                        savedEntity.DatasetId, savedEntity.AgentId);
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx, "Failed to update cache for dataset {DatasetId} after update", savedEntity.DatasetId);
                    // Continue - cache failure shouldn't fail the operation
                }

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

                _logger.LogInformation("Successfully updated dataset with ID: {DatasetId} by user: {UserEmail}",
                    savedEntity.DatasetId, "System");

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

                // Delete from table storage
                bool deleted = await _dataSetTableService.DeleteDataSetAsync(existingDataset.AgentId, datasetId);

                if (deleted)
                {
                    _logger.LogInformation("Dataset with ID: {DatasetId} deleted successfully", datasetId);
                    
                    // Remove from cache (both metadata and content)
                    try
                    {
                        await _datasetCache.RemoveDatasetAsync(datasetId);

                        // Refresh agent's dataset list cache with remaining datasets
                        var remainingDatasets = await _dataSetTableService.GetAllDataSetsByAgentIdAsync(existingDataset.AgentId);
                        var remainingDatasetDtos = remainingDatasets.Select(ToDatasetMetadataDto).ToList();
                        await _datasetCache.SetDatasetsByAgentAsync(existingDataset.AgentId, remainingDatasetDtos);

                        _logger.LogDebug("Removed dataset from cache: {DatasetId} for Agent: {AgentId}", 
                            datasetId, existingDataset.AgentId);
                    }
                    catch (Exception cacheEx)
                    {
                        _logger.LogWarning(cacheEx, "Failed to remove dataset {DatasetId} from cache after deletion", datasetId);
                        // Continue - cache failure shouldn't fail the operation
                    }
                    
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

        #endregion
    }
}
