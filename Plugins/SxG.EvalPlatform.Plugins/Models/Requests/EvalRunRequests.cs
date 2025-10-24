namespace SxG.EvalPlatform.Plugins.Models.Requests
{
    using System;

    /// <summary>
    /// Request model for PostEvalRun Custom API (expanded version)
    /// </summary>
    public class PostEvalRunRequest
    {
        /// <summary>
        /// Eval Run identifier (required) - GUID stored as string
        /// </summary>
        public string EvalRunId { get; set; }

        /// <summary>
        /// Agent identifier (optional) - String
        /// </summary>
        public string AgentId { get; set; }

        /// <summary>
        /// Environment identifier (optional) - GUID stored as string
        /// </summary>
        public string EnvironmentId { get; set; }

        /// <summary>
        /// Agent schema name (optional) - String
        /// </summary>
        public string AgentSchemaName { get; set; }

        /// <summary>
        /// Validates the request model
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(EvalRunId) && IsValidGuid(EvalRunId);
        }

        /// <summary>
        /// Gets validation error message
        /// </summary>
        /// <returns>Validation error message or null if valid</returns>
        public string GetValidationError()
        {
            if (string.IsNullOrWhiteSpace(EvalRunId))
                return "EvalRunId is required";
            
            if (!IsValidGuid(EvalRunId))
                return "EvalRunId must be a valid GUID";

            // Validate EnvironmentId if provided
            if (!string.IsNullOrWhiteSpace(EnvironmentId) && !IsValidGuid(EnvironmentId))
                return "EnvironmentId must be a valid GUID if provided";

            return null;
        }

        /// <summary>
        /// Validates if string is a valid GUID
        /// </summary>
        /// <param name="guidString">GUID string</param>
        /// <returns>True if valid GUID</returns>
        private bool IsValidGuid(string guidString)
        {
            return Guid.TryParse(guidString, out _);
        }
    }

    /// <summary>
    /// Request model for GetEvalRun Custom API
    /// </summary>
    public class GetEvalRunRequest
    {
        /// <summary>
        /// EvalRunId identifier (required) - GUID stored as string
        /// </summary>
        public string EvalRunId { get; set; }

        /// <summary>
        /// Validates the request model
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(EvalRunId) && IsValidGuid(EvalRunId);
        }

        /// <summary>
        /// Gets validation error message
        /// </summary>
        /// <returns>Validation error message or null if valid</returns>
        public string GetValidationError()
        {
            if (string.IsNullOrWhiteSpace(EvalRunId))
                return "EvalRunId is required";

            if (!IsValidGuid(EvalRunId))
                return "EvalRunId must be a valid GUID";

            return null;
        }

        /// <summary>
        /// Validates if string is a valid GUID
        /// </summary>
        /// <param name="guidString">GUID string</param>
        /// <returns>True if valid GUID</returns>
        private bool IsValidGuid(string guidString)
        {
            return Guid.TryParse(guidString, out _);
        }
    }

    /// <summary>
    /// Shared request model for UpdateDataset and PublishEnrichedDataset APIs - Only requires evalRunId
    /// </summary>
    public class EvalRunRequest
    {
        /// <summary>
        /// Eval Run identifier (required) - GUID stored as string
        /// </summary>
        public string EvalRunId { get; set; }

        /// <summary>
        /// Validates the request model
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(EvalRunId) && IsValidGuid(EvalRunId);
        }

        /// <summary>
        /// Gets validation error message
        /// </summary>
        /// <returns>Validation error message or null if valid</returns>
        public string GetValidationError()
        {
            if (string.IsNullOrWhiteSpace(EvalRunId))
                return "EvalRunId is required";

            if (!IsValidGuid(EvalRunId))
                return "EvalRunId must be a valid GUID";

            return null;
        }

        /// <summary>
        /// Validates if string is a valid GUID
        /// </summary>
        /// <param name="guidString">GUID string</param>
        /// <returns>True if valid GUID</returns>
        private bool IsValidGuid(string guidString)
        {
            return Guid.TryParse(guidString, out _);
        }
    }

    /// <summary>
    /// Request model for UpdateDataset Custom API - Uses shared EvalRunRequest
    /// </summary>
    public class UpdateDatasetRequest : EvalRunRequest
    {
        // Inherits all functionality from EvalRunRequest
    }

    /// <summary>
    /// Request model for PublishEnrichedDataset Custom API - Uses shared EvalRunRequest
    /// </summary>
    public class PublishEnrichedDatasetRequest : EvalRunRequest
    {
        // Inherits all functionality from EvalRunRequest
    }
}