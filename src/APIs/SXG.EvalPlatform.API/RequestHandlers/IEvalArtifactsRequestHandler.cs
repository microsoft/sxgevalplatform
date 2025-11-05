using SxgEvalPlatformApi.Models;
using System.Text.Json;

namespace SxgEvalPlatformApi.RequestHandlers
{
    /// <summary>
    /// Interface for evaluation artifacts request handling operations
    /// </summary>
    public interface IEvalArtifactsRequestHandler
    {
        /// <summary>
        /// Get both metrics configuration and dataset content for an evaluation run
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <returns>Combined artifacts or null if not found</returns>
        Task<EvalArtifactsDto?> GetEvalArtifactsAsync(Guid evalRunId);

        /// <summary>
        /// Get only metrics configuration for an evaluation run
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <returns>Metrics configuration artifact or null if not found</returns>
        Task<MetricsConfigurationArtifactDto?> GetMetricsConfigurationArtifactAsync(Guid evalRunId);

        /// <summary>
        /// Get only dataset content for an evaluation run
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <returns>Dataset artifact or null if not found</returns>
        Task<DatasetArtifactDto?> GetDatasetArtifactAsync(Guid evalRunId);

        /// <summary>
        /// Store enriched dataset content for an evaluation run
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <param name="enrichedDataset">Enriched dataset JSON content</param>
        /// <returns>Response with success status and blob path</returns>
        Task<EnrichedDatasetResponseDto> StoreEnrichedDatasetAsync(Guid evalRunId, JsonElement enrichedDataset);

        /// <summary>
        /// Get enriched dataset content for an evaluation run
        /// </summary>
        /// <param name="evalRunId">Evaluation run ID</param>
        /// <returns>Enriched dataset artifact or null if not found</returns>
        Task<EnrichedDatasetArtifactDto?> GetEnrichedDatasetArtifactAsync(Guid evalRunId);
    }
}