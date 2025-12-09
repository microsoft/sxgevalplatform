namespace SxG.EvalPlatform.Plugins.Services
{
    using System;
    using SxG.EvalPlatform.Plugins.Common.Interfaces;

    /// <summary>
    /// Service for accessing plugin configuration from environment variables
    /// </summary>
    public class PluginConfigurationService : IPluginConfigurationService
    {
        private readonly IEnvironmentVariableService _environmentVariableService;

        // Environment variable schema names
        private const string EvalApiBaseUrlKey = "cr890_EvalApiBaseUrl";
        private const string ApiTimeoutSecondsKey = "cr890_ApiTimeoutSeconds";
        private const string EnableAppInsightsLoggingKey = "cr890_EnableAppInsightsLogging";
        private const string EnableAuditLoggingKey = "cr890_EnableAuditLogging";
        private const string AppInsightsConnectionStringKey = "cr890_AppInsightsConnectionString";
        private const string EnableNestedCallLoggingKey = "cr890_EnableNestedCallLogging";
        private const string MaxTelemetryDepthKey = "cr890_MaxTelemetryDepth";
        private const string ApiScopeKey = "cr890_ApiScope";

        // Default values
        private const string DefaultEvalApiBaseUrl = "https://sxgevalapidev.azurewebsites.net";
        private const int DefaultApiTimeoutSeconds = 30;
        private const string DefaultApiScope = "443bbe62-c474-49f7-884c-d1b5a23eb735/.default";

        public PluginConfigurationService(IEnvironmentVariableService environmentVariableService)
        {
            _environmentVariableService = environmentVariableService ?? throw new ArgumentNullException(nameof(environmentVariableService));
        }

        /// <summary>
        /// Gets the base URL for the Eval API
        /// </summary>
        /// <returns>Base URL (e.g., https://sxgevalapidev.azurewebsites.net)</returns>
        public string GetEvalApiBaseUrl()
        {
            try
            {
                string baseUrl = _environmentVariableService.GetString(EvalApiBaseUrlKey);
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    return DefaultEvalApiBaseUrl;
                }
                // Remove trailing slash if present
                return baseUrl.TrimEnd('/');
            }
            catch
            {
                // Return default if environment variable not found
                return DefaultEvalApiBaseUrl;
            }
        }

        /// <summary>
        /// Gets the full URL for datasets API
        /// </summary>
        /// <param name="datasetId">Optional dataset ID to append</param>
        /// <returns>Full dataset API URL</returns>
        public string GetDatasetsApiUrl(string datasetId = null)
        {
            string baseUrl = GetEvalApiBaseUrl();
            if (string.IsNullOrWhiteSpace(datasetId))
            {
                return $"{baseUrl}/api/v1/eval/datasets";
            }
            return $"{baseUrl}/api/v1/eval/datasets/{datasetId}";
        }

        /// <summary>
        /// Gets the full URL for eval runs API
        /// </summary>
        /// <param name="evalRunId">Optional eval run ID to append</param>
        /// <returns>Full eval runs API URL</returns>
        public string GetEvalRunsStatusApiUrl(string evalRunId)
        {
            string baseUrl = GetEvalApiBaseUrl();
            return $"{baseUrl}/api/v1/eval/runs/{evalRunId}/status";
        }

        /// <summary>
        /// Gets the full URL for enriched dataset publish API
        /// </summary>
        /// <param name="evalRunId">Eval run ID</param>
        /// <returns>Full enriched dataset API URL</returns>
        public string GetEnrichedDatasetApiUrl(string evalRunId)
        {
            string baseUrl = GetEvalApiBaseUrl();
            return $"{baseUrl}/api/v1/eval/runs/{evalRunId}/enriched-dataset";
        }

        /// <summary>
        /// Gets the API timeout in seconds
        /// </summary>
        /// <returns>Timeout in seconds (default: 30)</returns>
        public int GetApiTimeoutSeconds()
        {
            try
            {
                decimal timeout = _environmentVariableService.GetDecimal(ApiTimeoutSecondsKey);
                return (int)timeout;
            }
            catch
            {
                return DefaultApiTimeoutSeconds;
            }
        }

        /// <summary>
        /// Gets whether Application Insights logging is enabled
        /// </summary>
        /// <returns>True if enabled, false otherwise</returns>
        public bool IsAppInsightsLoggingEnabled()
        {
            try
            {
                return _environmentVariableService.GetBool(EnableAppInsightsLoggingKey);
            }
            catch
            {
                // Default to disabled if not configured
                return false;
            }
        }

        /// <summary>
        /// Gets whether Dataverse audit logging is enabled
        /// </summary>
        /// <returns>True if enabled, false otherwise</returns>
        public bool IsAuditLoggingEnabled()
        {
            try
            {
                return _environmentVariableService.GetBool(EnableAuditLoggingKey);
            }
            catch
            {
                // Default to enabled for backward compatibility
                return true;
            }
        }

        /// <summary>
        /// Gets the Application Insights connection string
        /// </summary>
        /// <returns>Connection string for Application Insights</returns>
        public string GetAppInsightsConnectionString()
        {
            try
            {
                return _environmentVariableService.GetString(AppInsightsConnectionStringKey);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets whether to log telemetry for nested plugin calls (depth > 1)
        /// </summary>
        /// <returns>True if nested calls should be logged, false otherwise</returns>
        public bool ShouldLogNestedCalls()
        {
            try
            {
                return _environmentVariableService.GetBool(EnableNestedCallLoggingKey);
            }
            catch
            {
                // Default to enabled for complete telemetry
                return true;
            }
        }

        /// <summary>
        /// Gets the maximum depth to log (0 = all depths)
        /// </summary>
        /// <returns>Maximum depth to log, 0 means no limit</returns>
        public int GetMaxTelemetryDepth()
        {
            try
            {
                return (int)_environmentVariableService.GetDecimal(MaxTelemetryDepthKey);
            }
            catch
            {
                // 0 = no limit, log all depths
                return 0;
            }
        }

        /// <summary>
        /// Gets the OAuth scope for external API authentication
        /// </summary>
        /// <returns>OAuth scope (e.g., "443bbe62-c474-49f7-884c-d1b5a23eb735/.default")</returns>
        public string GetApiScope()
        {
            try
            {
                string scope = _environmentVariableService.GetString(ApiScopeKey);
                if (string.IsNullOrWhiteSpace(scope))
                {
                    return DefaultApiScope;
                }
                return scope;
            }
            catch
            {
                // Return default if environment variable not found
                return DefaultApiScope;
            }
        }
    }
}
