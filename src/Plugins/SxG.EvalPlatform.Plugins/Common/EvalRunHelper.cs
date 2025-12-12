namespace SxG.EvalPlatform.Plugins.Common
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using Microsoft.Xrm.Sdk;
    using SxG.EvalPlatform.Plugins.Services;

    /// <summary>
    /// Helper class for common eval run operations across plugins
    /// </summary>
    public static class EvalRunHelper
    {
        /// <summary>
        /// Updates external eval run status via API call
        /// </summary>
        /// <param name="evalRunId">Eval Run ID</param>
        /// <param name="status">Status to set (e.g., "EnrichingDataset", "Failed", "DatasetEnrichmentFailed")</param>
        /// <param name="authToken">Authentication bearer token</param>
        /// <param name="loggingService">Logging service</param>
        /// <param name="configService">Configuration service</param>
        /// <param name="callerName">Name of the calling plugin for logging</param>
        /// <returns>True if update successful, false otherwise</returns>
        public static bool UpdateExternalEvalRunStatus(
            string evalRunId, 
            string status, 
            string authToken, 
            IPluginLoggingService loggingService, 
            IPluginConfigurationService configService,
            string callerName = "EvalRunHelper")
        {
            var startTime = DateTimeOffset.UtcNow;
            try
            {
                string url = $"{configService.GetEvalRunsStatusApiUrl(evalRunId)}";
                loggingService.Trace($"{callerName}: Calling external status API: {url}");

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.Method = "PUT";
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Timeout = configService.GetApiTimeoutSeconds() * 1000;

                // Add authorization header if token is available
                AuthTokenHelper.AddAuthorizationHeader(httpWebRequest, loggingService, authToken);

                // Prepare request body
                string requestBody = $"{{\"status\":\"{status}\"}}";
                byte[] data = Encoding.UTF8.GetBytes(requestBody);
                httpWebRequest.ContentLength = data.Length;

                loggingService.Trace($"{callerName}: Status update request body: {requestBody}");

                using (Stream requestStream = httpWebRequest.GetRequestStream())
                {
                    requestStream.Write(data, 0, data.Length);
                }

                using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
                {
                    var duration = DateTimeOffset.UtcNow - startTime;
                    bool success = httpWebResponse.StatusCode == HttpStatusCode.OK || httpWebResponse.StatusCode == HttpStatusCode.NoContent;

                    loggingService.LogDependency("EvalAPI", url, startTime, duration, success);

                    if (success)
                    {
                        loggingService.Trace($"{callerName}: Successfully updated external status to {status}");
                        return true;
                    }
                    else
                    {
                        loggingService.Trace($"{callerName}: External status API returned status: {httpWebResponse.StatusCode}", TraceSeverity.Warning);
                        return false;
                    }
                }
            }
            catch (WebException webEx)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("EvalAPI", "UpdateStatus", startTime, duration, false);
                loggingService.LogException(webEx, $"{callerName}: WebException updating external status");
                return false;
            }
            catch (Exception ex)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("EvalAPI", "UpdateStatus", startTime, duration, false);
                loggingService.LogException(ex, $"{callerName}: Exception updating external status");
                return false;
            }
        }

        /// <summary>
        /// Updates eval run record with status in Dataverse
        /// </summary>
        /// <param name="evalRunId">Eval run ID</param>
        /// <param name="statusValue">New status integer value</param>
        /// <param name="organizationService">Organization service</param>
        /// <param name="loggingService">Logging service</param>
        /// <param name="callerName">Name of the calling plugin for logging</param>
        /// <returns>True if update successful</returns>
        public static bool UpdateEvalRunStatus(
            string evalRunId, 
            int statusValue, 
            IOrganizationService organizationService, 
            IPluginLoggingService loggingService,
            string callerName = "EvalRunHelper")
        {
            try
            {
                // Parse the EvalRunId GUID for direct update using Primary Key
                if (!Guid.TryParse(evalRunId, out Guid evalRunGuid))
                {
                    loggingService.Trace($"{callerName}: Invalid EvalRunId format: {evalRunId}", TraceSeverity.Error);
                    return false;
                }

                // Update using late-bound entity to avoid serialization issues with Elastic tables
                var updateEntity = new Entity("cr890_evalrun", evalRunGuid);
                updateEntity["cr890_status"] = new OptionSetValue(statusValue);

                organizationService.Update(updateEntity);

                loggingService.Trace($"{callerName}: Successfully updated eval run status to {statusValue}");
                return true;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{callerName}: Exception updating eval run status");
                return false;
            }
        }
    }
}
