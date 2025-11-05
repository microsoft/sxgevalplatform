using SxgEvalPlatformApi.Models;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Interface for evaluation result request handling operations
    /// </summary>
    public interface IEvaluationResultRequestHandler
    {
        /// <summary>
        /// Save evaluation results to blob storage
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <param name="saveDto">Evaluation result data</param>
        /// <returns>Save operation response</returns>
        Task<EvaluationResultSaveResponseDto> SaveEvaluationResultAsync(Guid evalRunId, SaveEvaluationResultDto saveDto);
        
        /// <summary>
        /// Get evaluation results by EvalRunId
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <returns>Evaluation results or error response</returns>
        Task<EvaluationResultResponseDto> GetEvaluationResultByIdAsync(Guid evalRunId);

        /// <summary>
        /// Get all evaluation runs for a specific agent
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <returns>List of evaluation runs for the agent</returns>
        
        /// <summary>
        /// Get evaluation results for a specific agent within a date range
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="startDateTime">Start date and time</param>
        /// <param name="endDateTime">End date and time</param>
        /// <returns>List of evaluation results within the specified date range</returns>
        Task<IList<EvaluationResultResponseDto>> GetEvaluationResultsByDateRangeAsync(string agentId, DateTime startDateTime, DateTime endDateTime);
    }
}