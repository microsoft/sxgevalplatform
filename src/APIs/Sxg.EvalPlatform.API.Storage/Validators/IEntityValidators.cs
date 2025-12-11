
namespace Sxg.EvalPlatform.API.Storage.Validators
{
    public interface IEntityValidators
    {
        Task<bool> IsValidDatasetId(string datasetId, string agentId = "");
        Task<bool> IsValidEvalRunId(Guid evalRunId);
        Task<bool> IsValidMetricsConfigurationId(string metricsConfigurationId, string agentId = "");
    }
}