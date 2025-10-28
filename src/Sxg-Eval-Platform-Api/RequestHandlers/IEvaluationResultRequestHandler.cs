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
        /// <param name="startDateTime"></param>
        /// <param name="endDateTime"></param>
        /// <returns>List of evaluation runs for the agent</returns>
        Task<IList<EvalRunDto>> GetEvalRunsByAgentIdAsync(string agentId, DateTime? startDateTime, DateTime? endDateTime);
        
    }
}