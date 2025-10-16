using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using SxgEvalPlatformApi.Models;

namespace SxgEvalPlatformApi.Services;

/// <summary>
/// Service for evaluation run operations using Azure Table Storage
/// </summary>
public class EvalRunService : IEvalRunService
{
    private readonly TableClient _tableClient;
    private readonly ILogger<EvalRunService> _logger;
    private const string TableName = "EvalRun";

    public EvalRunService(IConfiguration configuration, ILogger<EvalRunService> logger)
    {
        _logger = logger;
        
        var accountName = configuration["AzureStorage:AccountName"];
        
        if (string.IsNullOrEmpty(accountName))
        {
            throw new ArgumentException("Azure Storage account name is not configured");
        }

        var tableUri = $"https://{accountName}.table.core.windows.net";
        var environment = configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") ?? "Production";
        
        if (environment == "Development")
        {
            // Use default Azure credentials for development
            var serviceClient = new TableServiceClient(new Uri(tableUri), new DefaultAzureCredential());
            _tableClient = serviceClient.GetTableClient(TableName);
        }
        else
        {
            // Use managed identity in production
            var serviceClient = new TableServiceClient(new Uri(tableUri), new ManagedIdentityCredential());
            _tableClient = serviceClient.GetTableClient(TableName);
        }
        
        // Ensure table exists
        try
        {
            _tableClient.CreateIfNotExists();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create or access table {TableName}", TableName);
            throw;
        }
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
            
            // Construct the blob file path where evaluation results will be stored
            var blobFilePath = $"{createDto.AgentId}/evaluations/{evalRunId}.json";
            
            var entity = new EvalRunEntity
            {
                PartitionKey = "EvalRun",
                RowKey = evalRunId.ToString(), // Convert GUID to string for RowKey
                EvalRunId = evalRunId, // Store as GUID
                AgentId = createDto.AgentId,
                DataSetId = createDto.DataSetId,
                MetricsConfigurationId = createDto.MetricsConfigurationId,
                Status = EvalRunStatusConstants.Queued,
                LastUpdatedOn = currentDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                StartedDatetime = currentDateTime,
                BlobFilePath = blobFilePath
            };

            await _tableClient.AddEntityAsync(entity);
            
            _logger.LogInformation("Created evaluation run with ID: {EvalRunId}", evalRunId);
            
            return MapEntityToDto(entity);
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
            // First, get the existing entity
            var response = await _tableClient.GetEntityAsync<EvalRunEntity>("EvalRun", updateDto.EvalRunId.ToString());
            var entity = response.Value;
            
            if (entity == null)
            {
                _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", updateDto.EvalRunId);
                return null;
            }

            // Update the status and timestamp
            entity.Status = updateDto.Status;
            entity.LastUpdatedOn = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            entity.LastUpdatedBy = "System"; // Automatically set by API
            
            // Set completion datetime if status is Completed or Failed
            if (updateDto.Status == EvalRunStatusConstants.Completed || 
                updateDto.Status == EvalRunStatusConstants.Failed)
            {
                entity.CompletedDatetime = DateTime.UtcNow;
            }

            await _tableClient.UpdateEntityAsync(entity, entity.ETag);
            
            _logger.LogInformation("Updated evaluation run status to {Status} for ID: {EvalRunId}", 
                updateDto.Status, updateDto.EvalRunId);
            
            return MapEntityToDto(entity);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", updateDto.EvalRunId);
            return null;
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
    public async Task<EvalRunDto?> GetEvalRunByIdAsync(Guid evalRunId)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<EvalRunEntity>("EvalRun", evalRunId.ToString());
            var entity = response.Value;
            
            if (entity == null)
            {
                _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
                return null;
            }

            return MapEntityToDto(entity);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Evaluation run not found with ID: {EvalRunId}", evalRunId);
            return null;
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
            var query = _tableClient.QueryAsync<EvalRunEntity>(
                filter: $"PartitionKey eq 'EvalRun' and AgentId eq '{agentId}'");
            
            var results = new List<EvalRunDto>();
            
            await foreach (var entity in query)
            {
                results.Add(MapEntityToDto(entity));
            }
            
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
    /// Map EvalRunEntity to EvalRunDto
    /// </summary>
    private static EvalRunDto MapEntityToDto(EvalRunEntity entity)
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
            CompletedDatetime = entity.CompletedDatetime,
            BlobFilePath = entity.BlobFilePath
        };
    }
}