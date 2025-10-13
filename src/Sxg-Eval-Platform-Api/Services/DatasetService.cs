using SxgEvalPlatformApi.Models;
using System.Text.Json;

namespace SxgEvalPlatformApi.Services;

/// <summary>
/// Service for dataset operations using Azure Table Storage for metadata and Blob Storage for content
/// </summary>
public class DatasetService : IDatasetService
{
    private readonly IAzureTableService _tableService;
    private readonly IAzureBlobStorageService _blobService;
    private readonly ILogger<DatasetService> _logger;

    public DatasetService(
        IAzureTableService tableService,
        IAzureBlobStorageService blobService,
        ILogger<DatasetService> logger)
    {
        _tableService = tableService;
        _blobService = blobService;
        _logger = logger;
    }

    public async Task<DatasetSaveResponseDto> SaveDatasetAsync(SaveDatasetDto saveDatasetDto)
    {
        try
        {
            _logger.LogInformation("Processing dataset: {FileName} for agent: {AgentId}, type: {DatasetType}", 
                saveDatasetDto.FileName, saveDatasetDto.AgentId, saveDatasetDto.DatasetType);

            // Validate the dataset
            var validationResult = ValidateDataset(saveDatasetDto);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Dataset validation failed: {Error}", validationResult.ErrorMessage);
                return new DatasetSaveResponseDto
                {
                    DatasetId = string.Empty,
                    Status = "error",
                    Message = validationResult.ErrorMessage
                };
            }

            // Check if dataset already exists
            var existingMetadata = await _tableService.GetExistingDatasetMetadataAsync(
                saveDatasetDto.AgentId, saveDatasetDto.DatasetType, saveDatasetDto.FileName);

            string datasetId;
            string containerName;
            string blobFilePath;
            bool isUpdate = existingMetadata != null;
            string status;
            string message;

            if (isUpdate)
            {
                // Use existing metadata
                datasetId = existingMetadata!.DatasetId;
                containerName = existingMetadata.ContainerName;
                blobFilePath = existingMetadata.BlobFilePath;
                status = "updated";
                message = "Dataset updated successfully";
                
                _logger.LogInformation("Updating existing dataset with ID: {DatasetId}", datasetId);
            }
            else
            {
                // Create new dataset
                datasetId = Guid.NewGuid().ToString();
                containerName = saveDatasetDto.AgentId.ToLowerInvariant();
                blobFilePath = $"datasets/{datasetId}.json";
                status = "created";
                message = "Dataset created successfully";
                
                _logger.LogInformation("Creating new dataset with ID: {DatasetId}", datasetId);
            }

            // Serialize dataset to JSON
            var datasetJson = JsonSerializer.Serialize(saveDatasetDto.DatasetRecords, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Save/update blob in storage
            var blobSuccess = await _blobService.WriteBlobContentAsync(containerName, blobFilePath, datasetJson);
            
            if (!blobSuccess)
            {
                throw new InvalidOperationException("Failed to save dataset to blob storage");
            }

            // Create or update metadata for table storage
            var metadata = new DatasetMetadataEntity
            {
                DatasetId = datasetId,
                LastUpdatedOn = DateTime.UtcNow,
                AgentId = saveDatasetDto.AgentId,
                ContainerName = containerName,
                BlobFilePath = blobFilePath,
                DatasetType = saveDatasetDto.DatasetType,
                FileName = saveDatasetDto.FileName,
                RecordCount = saveDatasetDto.DatasetRecords.Count
            };

            // If updating, preserve the original partition key and row key
            if (isUpdate)
            {
                metadata.PartitionKey = existingMetadata!.PartitionKey;
                metadata.RowKey = existingMetadata.RowKey;
                metadata.ETag = existingMetadata.ETag; // For optimistic concurrency
            }

            await _tableService.SaveDatasetMetadataAsync(metadata);

            _logger.LogInformation("Successfully {Action} dataset with ID: {DatasetId}", 
                isUpdate ? "updated" : "created", datasetId);

            return new DatasetSaveResponseDto
            {
                DatasetId = datasetId,
                Status = status,
                Message = message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing dataset for agent: {AgentId}", saveDatasetDto.AgentId);
            return new DatasetSaveResponseDto
            {
                DatasetId = string.Empty,
                Status = "error",
                Message = $"Failed to process dataset: {ex.Message}"
            };
        }
    }

    public async Task<DatasetListResponseDto> GetDatasetListByAgentIdAsync(string agentId)
    {
        try
        {
            _logger.LogInformation("Retrieving dataset list for agent: {AgentId}", agentId);

            var metadataList = await _tableService.GetAllDatasetMetadataByAgentIdAsync(agentId);
            
            var datasets = metadataList.Select(m => new DatasetMetadataDto
            {
                DatasetId = m.DatasetId,
                LastUpdatedOn = m.LastUpdatedOn,
                AgentId = m.AgentId,
                DatasetType = m.DatasetType,
                FileName = m.FileName,
                RecordCount = m.RecordCount
            }).OrderByDescending(d => d.LastUpdatedOn).ToList();

            _logger.LogInformation("Retrieved {Count} datasets for agent: {AgentId}", datasets.Count, agentId);

            return new DatasetListResponseDto
            {
                AgentId = agentId,
                Datasets = datasets
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dataset list for agent: {AgentId}", agentId);
            return new DatasetListResponseDto
            {
                AgentId = agentId,
                Datasets = new List<DatasetMetadataDto>()
            };
        }
    }

    public async Task<string?> GetDatasetByIdAsync(string datasetId)
    {
        try
        {
            _logger.LogInformation("Retrieving dataset content for dataset: {DatasetId}", datasetId);

            // Get metadata first
            var metadata = await _tableService.GetDatasetMetadataByIdAsync(datasetId);
            
            if (metadata == null)
            {
                _logger.LogInformation("Dataset metadata not found for dataset: {DatasetId}", datasetId);
                return null;
            }

            // Read dataset content from blob storage
            var datasetJson = await _blobService.ReadBlobContentAsync(metadata.ContainerName, metadata.BlobFilePath);
            
            if (string.IsNullOrEmpty(datasetJson))
            {
                _logger.LogWarning("Dataset content is empty for dataset: {DatasetId}", datasetId);
                return null;
            }

            _logger.LogInformation("Successfully retrieved dataset content for dataset: {DatasetId}", datasetId);
            return datasetJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dataset content for dataset: {DatasetId}", datasetId);
            return null;
        }
    }

    public async Task<DatasetMetadataDto?> GetDatasetMetadataByIdAsync(string datasetId)
    {
        try
        {
            _logger.LogInformation("Retrieving dataset metadata for dataset: {DatasetId}", datasetId);

            var metadata = await _tableService.GetDatasetMetadataByIdAsync(datasetId);
            
            if (metadata == null)
            {
                _logger.LogInformation("Dataset metadata not found for dataset: {DatasetId}", datasetId);
                return null;
            }

            return new DatasetMetadataDto
            {
                DatasetId = metadata.DatasetId,
                LastUpdatedOn = metadata.LastUpdatedOn,
                AgentId = metadata.AgentId,
                DatasetType = metadata.DatasetType,
                FileName = metadata.FileName,
                RecordCount = metadata.RecordCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dataset metadata for dataset: {DatasetId}", datasetId);
            return null;
        }
    }

    private (bool IsValid, string ErrorMessage) ValidateDataset(SaveDatasetDto saveDatasetDto)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(saveDatasetDto.AgentId))
        {
            return (false, "AgentId is required");
        }

        if (string.IsNullOrWhiteSpace(saveDatasetDto.FileName))
        {
            return (false, "FileName is required");
        }

        if (saveDatasetDto.DatasetRecords == null || !saveDatasetDto.DatasetRecords.Any())
        {
            return (false, "DatasetRecords cannot be empty");
        }

        // Validate each record
        for (int i = 0; i < saveDatasetDto.DatasetRecords.Count; i++)
        {
            var record = saveDatasetDto.DatasetRecords[i];
            
            if (string.IsNullOrWhiteSpace(record.Prompt))
            {
                return (false, $"Record {i + 1}: Prompt is required");
            }

            if (string.IsNullOrWhiteSpace(record.GroundTruth))
            {
                return (false, $"Record {i + 1}: GroundTruth is required");
            }
        }

        // Additional validation for dataset type
        if (string.IsNullOrWhiteSpace(saveDatasetDto.DatasetType) || 
            (saveDatasetDto.DatasetType != DatasetTypes.Synthetic && saveDatasetDto.DatasetType != DatasetTypes.Golden))
        {
            return (false, "Invalid DatasetType. Must be either 'Synthetic' or 'Golden'");
        }

        return (true, string.Empty);
    }
}