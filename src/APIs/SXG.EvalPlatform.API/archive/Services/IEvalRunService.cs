using SxgEvalPlatformApi.Models;

namespace SxgEvalPlatformApi.Services;

/// <summary>
/// Interface for evaluation run operations
/// </summary>
public interface IEvalRunService
{
    /// <summary>
    /// Create a new evaluation run
    /// </summary>
    /// <param name="createDto">Evaluation run creation data</param>
    /// <returns>Created evaluation run</returns>
    Task<EvalRunDto> CreateEvalRunAsync(CreateEvalRunDto createDto);
    
    /// <summary>
    /// Update evaluation run status
    /// </summary>
    /// <param name="updateDto">Status update data</param>
    /// <returns>Updated evaluation run</returns>
    Task<EvalRunDto?> UpdateEvalRunStatusAsync(UpdateEvalRunStatusDto updateDto);
    
    /// <summary>
    /// Get evaluation run by ID
    /// </summary>
    /// <param name="agentId">Agent ID (used as partition key)</param>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <returns>Evaluation run or null if not found</returns>
    Task<EvalRunDto?> GetEvalRunByIdAsync(string agentId, Guid evalRunId);
    
    /// <summary>
    /// Get evaluation run by ID (searches across all partitions - less efficient)
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <returns>Evaluation run or null if not found</returns>
    Task<EvalRunDto?> GetEvalRunByIdAsync(Guid evalRunId);
    
    /// <summary>
    /// Get all evaluation runs for an agent
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <returns>List of evaluation runs</returns>
    Task<List<EvalRunDto>> GetEvalRunsByAgentIdAsync(string agentId);
    
    /// <summary>
    /// Get evaluation run entity with internal details (for internal service use only)
    /// </summary>
    /// <param name="evalRunId">Evaluation run ID</param>
    /// <returns>Evaluation run entity with blob storage details or null if not found</returns>
    Task<EvalRunEntity?> GetEvalRunEntityByIdAsync(Guid evalRunId);
}