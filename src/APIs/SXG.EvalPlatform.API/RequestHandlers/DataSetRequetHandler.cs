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
    /// Request handler for dataset operations using the storage project services with caching support
    /// </summary>
    public class DataSetRequestHandler : IDataSetRequestHandler
    {
        private readonly IDataSetTableService _dataSetTableService;
        private readonly IAzureBlobStorageService _blobStorageService;
        private readonly IConfigHelper _configHelper;
        private readonly ILogger<DataSetRequestHandler> _logger;
        private readonly IMapper _mapper;
                    

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
        }

        /// <summary>
        /// Save a dataset (upsert: create new or update existing based on AgentId, DatasetName, and DatasetType)
        /// </summary>
        public async Task<DatasetSaveResponseDto> SaveDatasetAsync(SaveDatasetDto saveDatasetDto)
        {
            try
            {
                _logger.LogInformation("Saving dataset: {DatasetName} for agent: {AgentId}, type: {DatasetType}",
                    saveDatasetDto.DatasetName, saveDatasetDto.AgentId, saveDatasetDto.DatasetType);

                // Check if dataset already exists with same AgentId, DatasetName, and DatasetType
                var existingDataset = await FindExistingDatasetAsync(
                    saveDatasetDto.AgentId,
                    saveDatasetDto.DatasetName,
                    saveDatasetDto.DatasetType);

                if (existingDataset != null)
                {
                    // UPDATE existing dataset
                    _logger.LogInformation("Dataset already exists, updating: {DatasetId}", existingDataset.DatasetId);
                    return await UpdateExistingDatasetAsync(existingDataset, saveDatasetDto.DatasetRecords);
                }

                // CREATE new dataset
                return await CreateNewDatasetAsync(saveDatasetDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save dataset for Agent: {AgentId}", saveDatasetDto.AgentId);
                return CreateErrorResponse(string.Empty, ex.Message);
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

                var entities = await _dataSetTableService.GetAllDataSetsByAgentIdAsync(agentId);
                var datasets = entities.Select(ToDatasetMetadataDto).ToList();

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
        /// Get dataset content by ID with caching support and deserialization
        /// </summary>
        public async Task<List<EvalDataset>?> GetDatasetByIdAsync(string datasetId)
        {
            try
            {
                _logger.LogInformation("Retrieving dataset content for DatasetId: {DatasetId}", datasetId);

                var entity = await _dataSetTableService.GetDataSetByIdAsync(datasetId);

                if (entity == null)
                {
                    _logger.LogInformation("Dataset not found for DatasetId: {DatasetId}", datasetId);
                    return null;
                }

                var blobContent = await _blobStorageService.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath);

                if (string.IsNullOrEmpty(blobContent))
                {
                    throw new Exception($"Dataset blob not found: {entity.ContainerName}/{entity.BlobFilePath}");
                }
                
                // Deserialize JSON to List<EvalDataset>
                var datasetContent = JsonSerializer.Deserialize<List<EvalDataset>>(blobContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (datasetContent == null)
                {
                    throw new JsonException($"Failed to deserialize dataset content for DatasetId: {datasetId}");
                }

                _logger.LogInformation("Retrieved and deserialized dataset content for DatasetId: {DatasetId}, Records: {RecordCount}",
                    datasetId, datasetContent.Count);

                return datasetContent;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Invalid JSON format in dataset blob for DatasetId: {DatasetId}", datasetId);
                throw new InvalidOperationException($"Dataset contains invalid JSON format: {jsonEx.Message}", jsonEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve dataset content for DatasetId: {DatasetId}", datasetId);
                throw;
            }
        }

        /// <summary>
        /// Get dataset content as raw JSON string by ID (for backward compatibility or special cases)
        /// </summary>
        public async Task<string?> GetDatasetByIdAsJsonAsync(string datasetId)
        {
            try
            {
                _logger.LogInformation("Retrieving dataset content as JSON for DatasetId: {DatasetId}", datasetId);

                var entity = await _dataSetTableService.GetDataSetByIdAsync(datasetId);

                if (entity == null)
                {
                    _logger.LogInformation("Dataset not found for DatasetId: {DatasetId}", datasetId);
                    return null;
                }

                var blobContent = await _blobStorageService.ReadBlobContentAsync(entity.ContainerName, entity.BlobFilePath);

                if (string.IsNullOrEmpty(blobContent))
                {
                    throw new Exception($"Dataset blob not found: {entity.ContainerName}/{entity.BlobFilePath}");
                }

                _logger.LogInformation("Retrieved dataset content as JSON for DatasetId: {DatasetId}", datasetId);

                return blobContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve dataset content as JSON for DatasetId: {DatasetId}", datasetId);
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

                var entity = await _dataSetTableService.GetDataSetByIdAsync(datasetId);

                if (entity == null)
                {
                    _logger.LogInformation("Dataset metadata not found for DatasetId: {DatasetId}", datasetId);
                    return null;
                }

                var metadata = ToDatasetMetadataDto(entity);
                
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
        /// Update an existing dataset by ID
        /// </summary>
        public async Task<DatasetSaveResponseDto> UpdateDatasetAsync(string datasetId, UpdateDatasetDto updateDatasetDto)
        {
            try
            {
                _logger.LogInformation("Updating dataset with ID: {DatasetId}", datasetId);

                var existingEntity = await _dataSetTableService.GetDataSetByIdAsync(datasetId);

                if (existingEntity == null)
                {
                    _logger.LogWarning("Dataset not found with ID: {DatasetId}", datasetId);
                    return CreateErrorResponse(datasetId, $"Dataset with ID '{datasetId}' not found");
                }

                return await UpdateExistingDatasetAsync(existingEntity, updateDatasetDto.DatasetRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update dataset with ID: {DatasetId}", datasetId);
                return CreateErrorResponse(datasetId, ex.Message);
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
                    _logger.LogInformation("Dataset with ID: {DatasetId} deleted successfully", datasetId);
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

        #region Private Helper Methods

        /// <summary>
        /// Find existing dataset by AgentId, DatasetName, and DatasetType
        /// </summary>
        private async Task<DataSetTableEntity?> FindExistingDatasetAsync(string agentId, string datasetName, string datasetType)
        {
            var existingDatasets = await _dataSetTableService.GetDataSetsByDatasetNameAsync(agentId, datasetName);
            return existingDatasets.FirstOrDefault(d =>
       d.DatasetType.Equals(datasetType, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Create a new dataset
        /// </summary>
        private async Task<DatasetSaveResponseDto> CreateNewDatasetAsync(SaveDatasetDto saveDatasetDto)
        {
            var datasetId = Guid.NewGuid().ToString();
            var currentTime = DateTime.UtcNow;

            // Create entity
            var entity = _mapper.Map<DataSetTableEntity>(saveDatasetDto);
            entity.DatasetId = datasetId;
            entity.RowKey = datasetId;
            entity.CreatedBy = "System";
            entity.CreatedOn = currentTime;
            entity.LastUpdatedBy = "System";
            entity.LastUpdatedOn = currentTime;

            // Set blob storage paths
            var (blobContainer, blobFilePath) = CreateBlobPaths(saveDatasetDto, datasetId);
            entity.BlobFilePath = blobFilePath;
            entity.ContainerName = blobContainer;

            // Serialize dataset content
            var datasetContent = SerializeDatasetRecords(saveDatasetDto.DatasetRecords);

            // Write to blob storage first
            await _blobStorageService.WriteBlobContentAsync(blobContainer, blobFilePath, datasetContent);

            // Save metadata to table storage
            var savedEntity = await _dataSetTableService.SaveDataSetAsync(entity);
                       

            _logger.LogInformation("Successfully created dataset with ID: {DatasetId}", savedEntity.DatasetId);

            return CreateSuccessResponse(savedEntity, "created", "Dataset created successfully");
        }

        /// <summary>
        /// Update an existing dataset
        /// </summary>
        private async Task<DatasetSaveResponseDto> UpdateExistingDatasetAsync(DataSetTableEntity existingEntity, List<EvalDataset> datasetRecords)
        {
            var currentTime = DateTime.UtcNow;

            // Update audit fields
            existingEntity.LastUpdatedBy = "System";
            existingEntity.LastUpdatedOn = currentTime;

            // Serialize dataset content
            var datasetContent = SerializeDatasetRecords(datasetRecords);

            // Update blob storage first
            await _blobStorageService.WriteBlobContentAsync(
               existingEntity.ContainerName,
           existingEntity.BlobFilePath,
            datasetContent);

            // Save updated metadata to table storage
            var savedEntity = await _dataSetTableService.SaveDataSetAsync(existingEntity);
                        
            _logger.LogInformation("Successfully updated dataset with ID: {DatasetId}", savedEntity.DatasetId);

            return CreateSuccessResponse(savedEntity, "updated", "Dataset updated successfully");
        }

        /// <summary>
        /// Create blob storage container and file path
        /// </summary>
        private (string container, string filePath) CreateBlobPaths(SaveDatasetDto saveDatasetDto, string datasetId)
        {
            var blobContainer = CommonUtils.TrimAndRemoveSpaces(saveDatasetDto.AgentId);
            var blobFileName = $"{saveDatasetDto.DatasetType}_{saveDatasetDto.DatasetName}_{datasetId}.json";
            var blobFilePath = $"{_configHelper.GetDatasetsFolderName()}/{blobFileName}";

            return (blobContainer, blobFilePath);
        }

        /// <summary>
        /// Serialize dataset records to JSON
        /// </summary>
        private string SerializeDatasetRecords(List<EvalDataset> datasetRecords)
        {
            return JsonSerializer.Serialize(datasetRecords, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }


        /// <summary>
        /// Create success response DTO
        /// </summary>
        private DatasetSaveResponseDto CreateSuccessResponse(DataSetTableEntity savedEntity, string status, string message)
        {
            return new DatasetSaveResponseDto
            {
                DatasetId = savedEntity.DatasetId,
                Status = status,
                Message = message,
                CreatedBy = savedEntity.CreatedBy,
                CreatedOn = savedEntity.CreatedOn,
                LastUpdatedBy = savedEntity.LastUpdatedBy,
                LastUpdatedOn = savedEntity.LastUpdatedOn
            };
        }

        /// <summary>
        /// Create error response DTO
        /// </summary>
        private DatasetSaveResponseDto CreateErrorResponse(string datasetId, string errorMessage)
        {
            return new DatasetSaveResponseDto
            {
                DatasetId = datasetId,
                Status = "error",
                Message = $"Failed to save dataset: {errorMessage}"
            };
        }

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
