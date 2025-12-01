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
            public const string Description = "Creates a new eval run record";
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
                public const string EvalRunId = "evalRunId";
                public const string DatasetId = "datasetId";
                public const string AgentId = "agentId";
                public const string EnvironmentId = "environmentId";
                public const string AgentSchemaName = "agentSchemaName";
            }

            /// <summary>
            /// Response properties for PostEvalRun API (standardized format)
            /// </summary>
            public static class ResponseProperties
            {
                public const string Success = "success";
                public const string Message = "message";
                public const string Timestamp = "timestamp";
            }
        }

        /// <summary>
        /// GetEvalRun Custom API configuration
        /// </summary>
        public static class GetEvalRun
        {
            public const string ApiName = "cr890_GetEvalRun";
            public const string DisplayName = "Get Eval Run";
            public const string Description = "Retrieves eval run record by EvalRunId";
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
                public const string EvalRunId = "evalRunId";
            }

            /// <summary>
            /// Response properties for GetEvalRun API (detailed format with dataset)
            /// </summary>
            public static class ResponseProperties
            {
                public const string EvalRunId = "evalRunId";
                public const string Message = "message";
                public const string Timestamp = "timestamp";
                public const string AgentId = "agentId";
                public const string EnvironmentId = "environmentId";
                public const string AgentSchemaName = "agentSchemaName";
                public const string Status = "status";
                public const string Dataset = "dataset";
            }
        }

        /// <summary>
        /// UpdateDataset Custom API configuration - Accepts evalRunId and datasetId
        /// </summary>
        public static class UpdateDataset
        {
            public const string ApiName = "cr890_UpdateDataset";
            public const string DisplayName = "Update Dataset";
            public const string Description = "Updates dataset from external datasets API and updates eval run";
            public const string PluginTypeName = "SxG.EvalPlatform.Plugins.Plugins.UpdateDataset";
            public const string ExecutePrivilegeName = "cr890_UpdateDataset";
            public const bool AllowedCustomProcessing = true;
            public const bool IsFunction = false; // POST operation
            public const bool IsPrivate = false;

            /// <summary>
            /// Request parameters for UpdateDataset API (evalRunId and datasetId)
            /// </summary>
            public static class RequestParameters
            {
                public const string EvalRunId = "evalRunId";
                public const string DatasetId = "datasetId";
            }

            /// <summary>
            /// Response properties for UpdateDataset API (standardized format)
            /// </summary>
            public static class ResponseProperties
            {
                public const string Success = "success";
                public const string Message = "message";
                public const string Timestamp = "timestamp";
            }
        }

        /// <summary>
        /// PublishEnrichedDataset Custom API configuration (renamed from EnrichDataset)
        /// </summary>
        public static class PublishEnrichedDataset
        {
            public const string ApiName = "cr890_PublishEnrichedDataset";
            public const string DisplayName = "Publish Enriched Dataset";
            public const string Description = "Publishes enriched dataset to external API from stored dataset";
            public const string PluginTypeName = "SxG.EvalPlatform.Plugins.Plugins.PublishEnrichedDataset";
            public const string ExecutePrivilegeName = "cr890_PublishEnrichedDataset";
            public const bool AllowedCustomProcessing = true;
            public const bool IsFunction = false; // POST operation
            public const bool IsPrivate = false;

            /// <summary>
            /// Request parameters for PublishEnrichedDataset API (only evalRunId)
            /// </summary>
            public static class RequestParameters
            {
                public const string EvalRunId = "evalRunId";
            }

            /// <summary>
            /// Response properties for PublishEnrichedDataset API (standardized format)
            /// </summary>
            public static class ResponseProperties
            {
                public const string Success = "success";
                public const string Message = "message";
                public const string Timestamp = "timestamp";
            }
        }

        /// <summary>
        /// UpdateDatasetAsFile Custom API configuration - Stores dataset as file in Dataverse file column
        /// </summary>
        public static class UpdateDatasetAsFile
        {
            public const string ApiName = "cr890_UpdateDatasetAsFile";
            public const string DisplayName = "Update Dataset As File";
            public const string Description = "Updates dataset from external datasets API and stores as file in Dataverse file column (DLP-compliant)";
            public const string PluginTypeName = "SxG.EvalPlatform.Plugins.Plugins.UpdateDatasetAsFile";
            public const string ExecutePrivilegeName = "cr890_UpdateDatasetAsFile";
            public const bool AllowedCustomProcessing = true;
            public const bool IsFunction = false; // POST operation
            public const bool IsPrivate = false;

            /// <summary>
            /// Request parameters for UpdateDatasetAsFile API (evalRunId and datasetId)
            /// </summary>
            public static class RequestParameters
            {
                public const string EvalRunId = "evalRunId";
                public const string DatasetId = "datasetId";
            }

            /// <summary>
            /// Response properties for UpdateDatasetAsFile API (standardized format)
            /// </summary>
            public static class ResponseProperties
            {
                public const string Success = "success";
                public const string Message = "message";
                public const string Timestamp = "timestamp";
            }
        }

        /// <summary>
        /// UpdateEnrichedDatasetFile Custom API configuration - Updates enriched dataset file after Power Automate flow enrichment
        /// </summary>
        public static class UpdateEnrichedDatasetFile
        {
            public const string ApiName = "cr890_UpdateEnrichedDatasetFile";
            public const string DisplayName = "Update Enriched Dataset File";
            public const string Description = "Updates enriched dataset file after Power Automate flow enrichment";
            public const string PluginTypeName = "SxG.EvalPlatform.Plugins.Plugins.UpdateEnrichedDatasetFile";
            public const string ExecutePrivilegeName = "cr890_UpdateEnrichedDatasetFile";
            public const bool AllowedCustomProcessing = true;
            public const bool IsFunction = false; // POST operation
            public const bool IsPrivate = false;

            /// <summary>
            /// Request parameters for UpdateEnrichedDatasetFile API (evalRunId and enrichedDatasetJson)
            /// </summary>
            public static class RequestParameters
            {
                public const string EvalRunId = "evalRunId";
                public const string EnrichedDatasetJson = "enrichedDatasetJson";
            }

            /// <summary>
            /// Response properties for UpdateEnrichedDatasetFile API (standardized format)
            /// </summary>
            public static class ResponseProperties
            {
                public const string Success = "success";
                public const string Message = "message";
                public const string Timestamp = "timestamp";
            }
        }

        /// <summary>
        /// UpdateFailedState Custom API configuration
        /// </summary>
        public static class UpdateFailedState
        {
            public const string ApiName = "cr890_UpdateFailedState";
            public const string DisplayName = "Update Failed State";
            public const string Description = "Updates eval run status to Failed in Dataverse and external API";
            public const string PluginTypeName = "SxG.EvalPlatform.Plugins.Plugins.UpdateFailedState";
            public const string ExecutePrivilegeName = "cr890_UpdateFailedState";
            public const bool AllowedCustomProcessing = true;
            public const bool IsFunction = false; // POST operation
            public const bool IsPrivate = false;

            /// <summary>
            /// Request parameters for UpdateFailedState API (only evalRunId)
            /// </summary>
            public static class RequestParameters
            {
                public const string EvalRunId = "evalRunId";
            }

            /// <summary>
            /// Response properties for UpdateFailedState API (standardized format)
            /// </summary>
            public static class ResponseProperties
            {
                public const string Success = "success";
                public const string Message = "message";
                public const string Timestamp = "timestamp";
            }
        }

        /// <summary>
        /// HttpCall Custom API configuration
        /// </summary>
        public static class HttpCall
        {
            public const string ApiName = "cr890_HttpCall";
            public const string DisplayName = "Http Call";
            public const string Description = "Makes HTTP call to external Azure Web App API";
            public const string PluginTypeName = "SxG.EvalPlatform.Plugins.Plugins.HttpCall";
            public const string ExecutePrivilegeName = "cr890_HttpCall";
            public const bool AllowedCustomProcessing = true;
            public const bool IsFunction = false; // POST operation
            public const bool IsPrivate = false;

            /// <summary>
            /// Request parameters for HttpCall API
            /// </summary>
            public static class RequestParameters
            {
                public const string Url = "Url";
                public const string Method = "Method";
                public const string Headers = "Headers";
                public const string Body = "Body";
                public const string Timeout = "Timeout";
            }

            /// <summary>
            /// Response properties for HttpCall API
            /// </summary>
            public static class ResponseProperties
            {
                public const string Success = "Success";
                public const string Message = "Message";
                public const string StatusCode = "StatusCode";
                public const string Timestamp = "Timestamp";
                public const string ResponseBody = "ResponseBody";
                public const string ResponseHeaders = "ResponseHeaders";
                public const string ExecutionTime = "ExecutionTime";
            }
        }
    }
}