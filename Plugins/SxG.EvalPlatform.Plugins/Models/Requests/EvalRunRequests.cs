namespace SxG.EvalPlatform.Plugins.Models.Requests
{
    using System;

    /// <summary>
    /// Request model for PostEvalRun Custom API
    /// </summary>
    public class PostEvalRunRequest
    {
        /// <summary>
        /// Agent identifier (required) - GUID stored as string
        /// </summary>
        public string AgentId { get; set; }

        /// <summary>
        /// Environment identifier (required) - GUID stored as string
        /// </summary>
        public string EnvironmentId { get; set; }

        /// <summary>
        /// Schema name (required)
        /// </summary>
        public string SchemaName { get; set; }

        /// <summary>
        /// Input JSON (required) - Multi Line of Text
        /// </summary>
        public string Input { get; set; }

        /// <summary>
        /// Validates the request model
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(AgentId) &&
                   !string.IsNullOrWhiteSpace(EnvironmentId) &&
                   !string.IsNullOrWhiteSpace(SchemaName) &&
                   !string.IsNullOrWhiteSpace(Input);
        }

        /// <summary>
        /// Gets validation error message
        /// </summary>
        /// <returns>Validation error message or null if valid</returns>
        public string GetValidationError()
        {
            if (string.IsNullOrWhiteSpace(AgentId))
                return "AgentId is required";
            
            if (string.IsNullOrWhiteSpace(EnvironmentId))
                return "EnvironmentId is required";
            
            if (string.IsNullOrWhiteSpace(SchemaName))
                return "SchemaName is required";
            
            if (string.IsNullOrWhiteSpace(Input))
                return "Input is required";

            return null;
        }
    }

    /// <summary>
    /// Request model for GetEvalRun Custom API
    /// </summary>
    public class GetEvalRunRequest
    {
        /// <summary>
        /// Id identifier (required) - GUID stored as string (Primary Name Column)
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Validates the request model
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Id);
        }

        /// <summary>
        /// Gets validation error message
        /// </summary>
        /// <returns>Validation error message or null if valid</returns>
        public string GetValidationError()
        {
            if (string.IsNullOrWhiteSpace(Id))
                return "Id is required";

            return null;
        }
    }
}