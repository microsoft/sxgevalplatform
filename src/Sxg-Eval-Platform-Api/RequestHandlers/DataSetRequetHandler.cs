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

        public DataSetRequestHandler(
            IDataSetTableService dataSetTableService,
            IAzureBlobStorageService blobStorageService,
            ILogger<DataSetRequestHandler> logger,
            IMapper mapper,
            IConfigHelper configHelper)
        {
            _dataSetTableService = dataSetTableService;
            _logger = logger;
            _mapper = mapper;
            _blobStorageService = blobStorageService;
            _configHelper = configHelper;
        }

        public async Task<DatasetSaveResponseDto> SaveDatasetAsync(SaveDatasetDto saveDatasetDto)
        {
            try
            {
                _logger.LogInformation("Creating/saving dataset: {DatasetName} for agent: {AgentId}, type: {DatasetType}",
                    saveDatasetDto.DatasetName, saveDatasetDto.AgentId, saveDatasetDto.DatasetType);

                // Check if dataset already exists
                var existingDatasets = await _dataSetTableService.GetDataSetsByDatasetNameAsync(
                    saveDatasetDto.AgentId, saveDatasetDto.DatasetName);

                var existingDataset = existingDatasets
                    .FirstOrDefault(d => d.DatasetType.Equals(saveDatasetDto.DatasetType, StringComparison.OrdinalIgnoreCase));

                DataSetTableEntity entity;
                bool isUpdate = false;

                var blobContainer = CommonUtils.TrimAndRemoveSpaces(saveDatasetDto.AgentId).ToLowerInvariant();
                string blobFileName;
                string blobFilePath;

                if (existingDataset != null)
                {
                    entity = existingDataset;
                    blobFileName = entity.BlobFilePath;
                    blobFilePath = entity.BlobFilePath;
                    isUpdate = true;
                }
                else
                {
                    entity = ToDataSetTableEntity(saveDatasetDto);
                    blobFileName = $"{saveDatasetDto.DatasetType}_{saveDatasetDto.DatasetName}_{entity.DatasetId}.json";
                    blobFilePath = $"{_configHelper.GetDatasetsFolderName()}/{blobFileName}";
                    entity.BlobFilePath = blobFilePath;
                    entity.ContainerName = blobContainer;
                }

                entity.LastUpdatedOn = DateTime.UtcNow;
                // TODO: Set LastUpdatedBy from Auth Token

                // Save dataset content to blob
                var datasetContent = JsonSerializer.Serialize(saveDatasetDto.DatasetRecords, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var blobWriteResult = await _blobStorageService.WriteBlobContentAsync(blobContainer, blobFilePath, datasetContent);

                // Save metadata to table storage
                var savedEntity = await _dataSetTableService.SaveDataSetAsync(entity);

                var response = new DatasetSaveResponseDto
                {
                    DatasetId = savedEntity.DatasetId,
                    Status = isUpdate ? "updated" : "created",
                    Message = isUpdate ? "Dataset updated successfully" : "Dataset created successfully"
                };

                _logger.LogInformation("Successfully {Action} dataset with ID: {DatasetId}",
                    isUpdate ? "updated" : "created", savedEntity.DatasetId);

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

                var entities = await _dataSetTableService.GetAllDataSetsByAgentIdAsync(agentId);

                var datasets = entities.Select(ToDatasetMetadataDto).ToList();

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
            return new DatasetMetadataDto
            {
                DatasetId = entity.DatasetId,
                AgentId = entity.AgentId,
                DatasetType = entity.DatasetType,
                DatasetName = entity.DatasetName,
                LastUpdatedOn = entity.LastUpdatedOn,
                RecordCount = 0 // TODO: Calculate from blob content if needed
            };
        }

        #endregion
    }
}
