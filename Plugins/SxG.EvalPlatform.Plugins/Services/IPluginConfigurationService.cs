namespace SxG.EvalPlatform.Plugins.Services
{
    /// <summary>
    /// Interface for accessing plugin configuration from environment variables
    /// </summary>
    public interface IPluginConfigurationService
    {
        /// <summary>
        /// Gets the base URL for the Eval API
        /// </summary>
        /// <returns>Base URL (e.g., https://sxgevalapidev.azurewebsites.net)</returns>
        string GetEvalApiBaseUrl();

        /// <summary>
        /// Gets the full URL for datasets API
        /// </summary>
        /// <param name="datasetId">Optional dataset ID to append</param>
        /// <returns>Full dataset API URL</returns>
        string GetDatasetsApiUrl(string datasetId = null);

        /// <summary>
        /// Gets the full URL for eval runs API
        /// </summary>
        /// <param name="evalRunId">Optional eval run ID to append</param>
        /// <returns>Full eval runs API URL</returns>
        string GetEvalRunsApiUrl(string evalRunId = null);

        /// <summary>
        /// Gets the full URL for enriched dataset publish API
        /// </summary>
        /// <param name="evalRunId">Eval run ID</param>
        /// <returns>Full enriched dataset API URL</returns>
        string GetEnrichedDatasetApiUrl(string evalRunId);

        /// <summary>
        /// Gets the API timeout in seconds
        /// </summary>
        /// <returns>Timeout in seconds (default: 30)</returns>
        int GetApiTimeoutSeconds();

        /// <summary>
        /// Gets whether Application Insights logging is enabled
        /// </summary>
        /// <returns>True if enabled, false otherwise</returns>
        bool IsAppInsightsLoggingEnabled();

        /// <summary>
        /// Gets whether Dataverse audit logging is enabled
        /// </summary>
        /// <returns>True if enabled, false otherwise</returns>
        bool IsAuditLoggingEnabled();

        /// <summary>
        /// Gets the Application Insights connection string
        /// </summary>
        /// <returns>Connection string for Application Insights</returns>
        string GetAppInsightsConnectionString();

        /// <summary>
        /// Gets the file upload flow URL for file upload operations
        /// </summary>
        /// <returns>File upload flow trigger URL</returns>
        string GetFileUploadFlowUrl();
    }
}
