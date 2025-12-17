namespace SxG.EvalPlatform.Plugins.Models.Requests
{
    /// <summary>
    /// Base request model for all eval run-related Custom APIs
    /// </summary>
    public abstract class EvalRunRequest
    {
        public string EvalRunId { get; set; }

        public virtual bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(EvalRunId);
        }

        public virtual string GetValidationError()
        {
            if (string.IsNullOrWhiteSpace(EvalRunId))
                return "EvalRunId is required";
            return null;
        }
    }

    /// <summary>
    /// Request model for PostEvalRun Custom API
    /// </summary>
    public class PostEvalRunRequest : EvalRunRequest
    {
        public string DatasetId { get; set; }
        public string AgentId { get; set; }
        public string EnvironmentId { get; set; }
        public string AgentSchemaName { get; set; }
    }

    /// <summary>
    /// Request model for GetEvalRun Custom API
    /// </summary>
    public class GetEvalRunRequest : EvalRunRequest
    {
        // Only requires EvalRunId from base class
    }

    /// <summary>
    /// Request model for UpdateDatasetAsFile Custom API
    /// </summary>
    public class UpdateDatasetAsFileRequest : EvalRunRequest
    {
        public string DatasetId { get; set; }

        public override bool IsValid()
        {
            return base.IsValid() && !string.IsNullOrWhiteSpace(DatasetId);
        }

        public override string GetValidationError()
        {
            string baseError = base.GetValidationError();
            if (!string.IsNullOrWhiteSpace(baseError))
                return baseError;

            if (string.IsNullOrWhiteSpace(DatasetId))
                return "DatasetId is required";

            return null;
        }
    }

    /// <summary>
    /// Request model for PublishEnrichedDataset Custom API
    /// </summary>
    public class PublishEnrichedDatasetRequest : EvalRunRequest
    {
        // Only requires EvalRunId from base class
    }

    /// <summary>
    /// Request model for UpdateFailedState Custom API
    /// </summary>
    public class UpdateFailedStateRequest : EvalRunRequest
    {
        // Only requires EvalRunId from base class
    }

    /// <summary>
    /// Request model for UpdateEnrichedDatasetFile Custom API
    /// </summary>
    public class UpdateEnrichedDatasetFileRequest : EvalRunRequest
    {
        public string EnrichedDatasetJson { get; set; }

        public override bool IsValid()
        {
            return base.IsValid() && !string.IsNullOrWhiteSpace(EnrichedDatasetJson);
        }

        public override string GetValidationError()
        {
            string baseError = base.GetValidationError();
            if (!string.IsNullOrWhiteSpace(baseError))
                return baseError;

            if (string.IsNullOrWhiteSpace(EnrichedDatasetJson))
                return "EnrichedDatasetJson is required";

            return null;
        }
    }
}