namespace SxG.EvalPlatform.Plugins.CustomApis
{
    /// <summary>
    /// Configuration constants for Custom APIs
    /// </summary>
    public static class CustomApiConfig
    {
        /// <summary>
        /// PostEvalRun Custom API configuration
        /// </summary>
        public static class PostEvalRun
        {
            public const string ApiName = "cr890_PostEvalRun";
            public const string DisplayName = "Post Eval Run";
            public const string Description = "Creates a new eval job record";
            public const string PluginTypeName = "SxG.EvalPlatform.Plugins.Plugins.PostEvalRun";
            public const string ExecutePrivilegeName = "cr890_PostEvalRun";
            public const bool AllowedCustomProcessing = true;
            public const bool IsFunction = false; // POST operation
            public const bool IsPrivate = false;
            
            /// <summary>
            /// Request parameters for PostEvalRun API
            /// </summary>
            public static class RequestParameters
            {
                public const string AgentId = "AgentId";
                public const string EnvironmentId = "EnvironmentId";
                public const string SchemaName = "SchemaName";
                public const string Input = "Input";
            }

            /// <summary>
            /// Response properties for PostEvalRun API
            /// </summary>
            public static class ResponseProperties
            {
                public const string Success = "Success";
                public const string Id = "Id";
                public const string Message = "Message";
                public const string StatusCode = "StatusCode";
                public const string Timestamp = "Timestamp";
                // Additional response properties
                public const string EvalJobId = "EvalJobId";
                public const string AgentId = "AgentId";
                public const string EnvironmentId = "EnvironmentId";
                public const string SchemaName = "SchemaName";
                public const string Status = "Status";
            }
        }

        /// <summary>
        /// GetEvalRun Custom API configuration
        /// </summary>
        public static class GetEvalRun
        {
            public const string ApiName = "cr890_GetEvalRun";
            public const string DisplayName = "Get Eval Run";
            public const string Description = "Retrieves eval job record by Id";
            public const string PluginTypeName = "SxG.EvalPlatform.Plugins.Plugins.GetEvalRun";
            public const string ExecutePrivilegeName = "cr890_GetEvalRun";
            public const bool AllowedCustomProcessing = true;
            public const bool IsFunction = true; // GET operation
            public const bool IsPrivate = false;

            /// <summary>
            /// Request parameters for GetEvalRun API
            /// </summary>
            public static class RequestParameters
            {
                public const string Id = "Id";
            }

            /// <summary>
            /// Response properties for GetEvalRun API
            /// </summary>
            public static class ResponseProperties
            {
                public const string Success = "Success";
                public const string EvalJob = "EvalJob";
                public const string Message = "Message";
                public const string StatusCode = "StatusCode";
                public const string Timestamp = "Timestamp";
                // Additional response properties
                public const string EvalJobId = "EvalJobId";
                public const string AgentId = "AgentId";
                public const string EnvironmentId = "EnvironmentId";
                public const string SchemaName = "SchemaName";
                public const string Status = "Status";
                public const string Input = "Input";
                public const string Output = "Output";
            }
        }
    }
}