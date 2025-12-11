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
        /// Caller information is automatically obtained from ICallerIdentificationService
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <param name="saveDto">Evaluation result data to save</param>
        /// <returns>Save response and processing result</returns>
        Task<(EvaluationResultSaveResponseDto? EvalResponse, APIRequestProcessingResultDto? RequestProcesingResult)> SaveEvaluationResultAsync(
            Guid evalRunId,
            SaveEvaluationResultDto saveDto);

        /// <summary>
        /// Get evaluation results by EvalRunId
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <returns>Evaluation results or error response</returns>
        Task<(EvaluationResultResponseDto? EvalResponse, APIRequestProcessingResultDto? RequestProcesingResult)> GetEvaluationResultByIdAsync(Guid evalRunId);

        /// <summary>
        /// Get evaluation runs for an agent within a date range
        /// </summary>
        /// <param name="agentId">Agent ID</param>
        /// <param name="startDateTime">Start date filter (optional)</param>
        /// <param name="endDateTime">End date filter (optional)</param>
        /// <returns>List of evaluation runs</returns>
        Task<IList<EvalRunDto>> GetEvalRunsByAgentIdAsync(string agentId, DateTime? startDateTime, DateTime? endDateTime);
    }
}