using Sxg.EvalPlatform.API.Storage.TableEntities;

namespace Sxg.EvalPlatform.API.Storage.Services;

/// <summary>
/// Interface for EvalRun table operations
/// </summary>
public interface IEvalRunTableService
{
    /// <summary>
    /// Create a new evaluation run
    /// </summary>
    /// <param name="entity">EvalRun table entity to create</param>
    /// <param name="auditUser">User performing the operation (for audit logging)</param>
    /// <returns>Created EvalRun table entity</returns>
    Task<EvalRunTableEntity> CreateEvalRunAsync(EvalRunTableEntity entity, string? auditUser = null);

    /// <summary>
    /// Update evaluation run status
    /// </summary>
    /// <param name="agentId">Agent ID (PartitionKey)</param>
    /// <param name="evalRunId">Evaluation run ID (RowKey)</param>
    /// <param name="status">New status</param>
    /// <param name="lastUpdatedBy">Updated by user</param>
    /// <returns>Updated EvalRun table entity or null if not found</returns>
    Task<EvalRunTableEntity?> UpdateEvalRunStatusAsync(string agentId, Guid evalRunId, string status, string? lastUpdatedBy = null);

    /// <summary>
    /// Get evaluation run by agent ID and evaluation run ID
    /// </summary>
    /// <param name="agentId">Agent ID (PartitionKey)</param>
    /// <param name="evalRunId">Evaluation run ID (RowKey)</param>
    /// <returns>EvalRun table entity or null if not found</returns>
    Task<EvalRunTableEntity?> GetEvalRunByIdAsync(string agentId, Guid evalRunId);

    /// <summary>
    /// Get evaluation run by ID (searches across all partitions)
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <returns>EvalRun table entity or null if not found</returns>
    Task<EvalRunTableEntity?> GetEvalRunByIdAsync(Guid evalRunId);

    /// <summary>
    /// Get all evaluation runs for an agent
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <returns>List of EvalRun table entities</returns>
    Task<List<EvalRunTableEntity>> GetEvalRunsByAgentIdAsync(string agentId);

    Task<IList<EvalRunTableEntity>> GetEvalRunsByAgentIdAndDateFilterAsync(string agentId, DateTime? startDate, DateTime? endDate);

    /// <summary>
    /// Update an evaluation run entity
    /// </summary>
    /// <param name="entity">EvalRun table entity to update</param>
    /// <param name="auditUser">User performing the operation (for audit logging)</param>
    /// <returns>Updated EvalRun table entity</returns>
    Task<EvalRunTableEntity> UpdateEvalRunAsync(EvalRunTableEntity entity, string? auditUser = null);
}