namespace SxG.EvalPlatform.Plugins
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using Microsoft.Xrm.Sdk;
    using SxG.EvalPlatform.Plugins.Common;
    using SxG.EvalPlatform.Plugins.Common.Framework;
    using SxG.EvalPlatform.Plugins.Models.Requests;
    using SxG.EvalPlatform.Plugins.Models.Responses;
    using SxG.EvalPlatform.Plugins.CustomApis;
    using SxG.EvalPlatform.Plugins.Services;

    /// <summary>
    /// Plugin for updating dataset from external eval datasets API and updating eval run records
    /// </summary>
    public class UpdateDataset : PluginBase
    {
        public UpdateDataset(string unsecureConfig, string secureConfig) : base(unsecureConfig, secureConfig)
        {
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException(nameof(localContext));
            }

            var context = localContext.PluginExecutionContext;
            var loggingService = localContext.LoggingService;
            var configService = localContext.ConfigurationService;
            var organizationService = localContext.OrganizationService;
            var managedIdentityService = localContext.ManagedIdentityService;

            try
            {
                loggingService.Trace($"{nameof(UpdateDataset)}: Starting execution");

                // Extract request parameters from input parameters
                var request = ExtractRequestFromContext(context, loggingService);

                // Validate request
                if (!request.IsValid())
                {
                    string validationError = request.GetValidationError();
                    loggingService.Trace($"{nameof(UpdateDataset)}: Validation failed - {validationError}", TraceSeverity.Warning);

                    var errorResponse = UpdateDatasetResponse.CreateError(validationError, request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Validate datasetId is provided
                if (string.IsNullOrWhiteSpace(request.DatasetId))
                {
                    loggingService.Trace($"{nameof(UpdateDataset)}: Validation failed - DatasetId is required", TraceSeverity.Warning);
                    var errorResponse = UpdateDatasetResponse.CreateError("DatasetId is required", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                loggingService.Trace($"{nameof(UpdateDataset)}: Request validated successfully - EvalRunId: {request.EvalRunId}, DatasetId: {request.DatasetId}");

                // Get authentication token for external API calls
                string apiScope = configService.GetApiScope();
                string authToken = AuthTokenHelper.AcquireToken(managedIdentityService, loggingService, apiScope);

                // Update external status to "EnrichingDataset" before fetching dataset
                bool statusUpdateSuccess = UpdateExternalEvalRunStatus(request.EvalRunId, "EnrichingDataset", authToken, loggingService, configService);
                if (!statusUpdateSuccess)
                {
                    loggingService.Trace($"{nameof(UpdateDataset)}: Warning - Failed to update external status to EnrichingDataset, continuing with dataset fetch", TraceSeverity.Warning);
                    // Continue execution even if status update fails
                }

                // Call external dataset API to get dataset data using datasetId
                var datasetJson = CallExternalDatasetApi(request.DatasetId, authToken, loggingService, configService);
                if (datasetJson == null)
                {
                    var errorResponse = UpdateDatasetResponse.CreateError("Failed to retrieve dataset from external API", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Update the eval run record with retrieved dataset content
                bool updateSuccess = UpdateEvalRunRecord(request.EvalRunId, datasetJson, organizationService, loggingService);
                if (!updateSuccess)
                {
                    var errorResponse = UpdateDatasetResponse.CreateError("Failed to update eval run record", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Log event to Application Insights
                loggingService.LogEvent("UpdateDatasetSuccess", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "EvalRunId", request.EvalRunId },
                    { "DatasetId", request.DatasetId },
                    { "DatasetSize", datasetJson?.Length.ToString() ?? "0" }
                });

                // Create success response
                var response = UpdateDatasetResponse.CreateSuccess();
                SetResponseParameters(context, response, loggingService);

                loggingService.Trace($"{nameof(UpdateDataset)}: Execution completed successfully");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateDataset)}: Exception occurred");

                // Create error response
                var errorResponse = UpdateDatasetResponse.CreateError($"Internal server error: {ex.Message}");
                SetResponseParameters(context, errorResponse, loggingService);

                throw new InvalidPluginExecutionException($"{nameof(UpdateDataset)} :: Error :: " + ex.Message, ex);
            }
            finally
            {
                // Flush telemetry
                loggingService.Flush();
            }
        }

        /// <summary>
        /// Extracts request parameters from plugin execution context
        /// </summary>
        /// <param name="context">Plugin execution context</param>
        /// <param name="loggingService">Logging service</param>
        /// <returns>UpdateDatasetRequest object</returns>
        private UpdateDatasetRequest ExtractRequestFromContext(IPluginExecutionContext context, IPluginLoggingService loggingService)
        {
            var request = new UpdateDatasetRequest();

            // Extract evalRunId parameter
            if (context.InputParameters.Contains(CustomApiConfig.UpdateDataset.RequestParameters.EvalRunId))
                request.EvalRunId = context.InputParameters[CustomApiConfig.UpdateDataset.RequestParameters.EvalRunId]?.ToString();

            // Extract datasetId parameter
            if (context.InputParameters.Contains(CustomApiConfig.UpdateDataset.RequestParameters.DatasetId))
                request.DatasetId = context.InputParameters[CustomApiConfig.UpdateDataset.RequestParameters.DatasetId]?.ToString();

            loggingService.Trace($"{nameof(UpdateDataset)}: Extracted request parameters from context");
            loggingService.Trace($"{nameof(UpdateDataset)}: EvalRunId: {request.EvalRunId}, DatasetId: {request.DatasetId}");

            return request;
        }

        /// <summary>
        /// Updates external eval run status via API call
        /// </summary>
        /// <param name="evalRunId">Eval Run ID</param>
        /// <param name="status">Status to set (e.g., "EnrichingDataset")</param>
        /// <param name="authToken">Authentication bearer token</param>
        /// <param name="loggingService">Logging service</param>
        /// <param name="configService">Configuration service</param>
        /// <returns>True if update successful, false otherwise</returns>
        private bool UpdateExternalEvalRunStatus(string evalRunId, string status, string authToken, IPluginLoggingService loggingService, IPluginConfigurationService configService)
        {
            var startTime = DateTimeOffset.UtcNow;
            try
            {
                string url = $"{configService.GetEvalRunsApiUrl(evalRunId)}/status";
                loggingService.Trace($"{nameof(UpdateDataset)}: Calling external status API: {url}");

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

                loggingService.Trace($"{nameof(UpdateDataset)}: Status update request body: {requestBody}");

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
                        loggingService.Trace($"{nameof(UpdateDataset)}: Successfully updated external status to {status}");
                        return true;
                    }
                    else
                    {
                        loggingService.Trace($"{nameof(UpdateDataset)}: External status API returned status: {httpWebResponse.StatusCode}", TraceSeverity.Warning);
                        return false;
                    }
                }
            }
            catch (WebException webEx)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("EvalAPI", "UpdateStatus", startTime, duration, false);
                loggingService.LogException(webEx, $"{nameof(UpdateDataset)}: WebException updating external status");
                return false;
            }
            catch (Exception ex)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("EvalAPI", "UpdateStatus", startTime, duration, false);
                loggingService.LogException(ex, $"{nameof(UpdateDataset)}: Exception updating external status");
                return false;
            }
        }

        /// <summary>
        /// Calls external dataset API to retrieve dataset data
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <param name="authToken">Authentication bearer token</param>
        /// <param name="loggingService">Logging service</param>
        /// <param name="configService">Configuration service</param>
        /// <returns>Dataset JSON string or null if failed</returns>
        private string CallExternalDatasetApi(string datasetId, string authToken, IPluginLoggingService loggingService, IPluginConfigurationService configService)
        {
            var startTime = DateTimeOffset.UtcNow;
            try
            {
                string url = configService.GetDatasetsApiUrl(datasetId);
                loggingService.Trace($"{nameof(UpdateDataset)}: Calling external dataset API: {url}");

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.Method = "GET";
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Timeout = configService.GetApiTimeoutSeconds() * 1000;

                // Add authorization header if token is available
                AuthTokenHelper.AddAuthorizationHeader(httpWebRequest, loggingService, authToken);

                using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
                {
                    var duration = DateTimeOffset.UtcNow - startTime;
                    bool success = httpWebResponse.StatusCode == HttpStatusCode.OK;

                    loggingService.LogDependency("EvalAPI", url, startTime, duration, success);

                    if (success)
                    {
                        using (Stream responseStream = httpWebResponse.GetResponseStream())
                        using (StreamReader reader = new StreamReader(responseStream))
                        {
                            string responseBody = reader.ReadToEnd();
                            loggingService.Trace($"{nameof(UpdateDataset)}: External dataset API response received: {responseBody}");
                            return responseBody;
                        }
                    }
                    else
                    {
                        loggingService.Trace($"{nameof(UpdateDataset)}: External dataset API returned status: {httpWebResponse.StatusCode}", TraceSeverity.Warning);
                        return null;
                    }
                }
            }
            catch (WebException webEx)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("EvalAPI", $"GetDataset/{datasetId}", startTime, duration, false);
                loggingService.LogException(webEx, $"{nameof(UpdateDataset)}: WebException occurred");
                return null;
            }
            catch (Exception ex)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("EvalAPI", $"GetDataset/{datasetId}", startTime, duration, false);
                loggingService.LogException(ex, $"{nameof(UpdateDataset)}: Exception in CallExternalDatasetApi");
                return null;
            }
        }

        /// <summary>
        /// Updates eval run record with retrieved dataset content
        /// </summary>
        /// <param name="evalRunId">Eval run ID</param>
        /// <param name="datasetJson">Retrieved dataset content as JSON string</param>
        /// <param name="organizationService">Organization service</param>
        /// <param name="loggingService">Logging service</param>
        /// <returns>True if update successful</returns>
        private bool UpdateEvalRunRecord(string evalRunId, string datasetJson, IOrganizationService organizationService, IPluginLoggingService loggingService)
        {
            try
            {
                // Parse the EvalRunId GUID for direct update using Primary Key
                if (!Guid.TryParse(evalRunId, out Guid evalRunGuid))
                {
                    loggingService.Trace($"{nameof(UpdateDataset)}: Invalid EvalRunId format: {evalRunId}", TraceSeverity.Error);
                    return false;
                }

                // Update using late-bound entity to avoid serialization issues with Elastic tables
                var updateEntity = new Entity("cr890_evalrun", evalRunGuid);
                updateEntity["cr890_dataset"] = datasetJson;
                updateEntity["cr890_status"] = new OptionSetValue(2);  // Status = Updated

                organizationService.Update(updateEntity);

                loggingService.Trace($"{nameof(UpdateDataset)}: Successfully updated eval run record");
                loggingService.Trace($"{nameof(UpdateDataset)}: Dataset content length: {datasetJson?.Length ?? 0} characters, Status set to Updated (2)");
                return true;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateDataset)}: Exception updating eval run record");
                return false;
            }
        }

        /// <summary>
        /// Sets response parameters in the plugin execution context
        /// </summary>
        /// <param name="context">Plugin execution context</param>
        /// <param name="response">Response object</param>
        /// <param name="loggingService">Logging service</param>
        private void SetResponseParameters(IPluginExecutionContext context, UpdateDatasetResponse response, IPluginLoggingService loggingService)
        {
            try
            {
                context.OutputParameters[CustomApiConfig.UpdateDataset.ResponseProperties.Success] = response.Success;
                context.OutputParameters[CustomApiConfig.UpdateDataset.ResponseProperties.Message] = response.Message;
                context.OutputParameters[CustomApiConfig.UpdateDataset.ResponseProperties.Timestamp] = response.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                loggingService.Trace($"{nameof(UpdateDataset)}: Response parameters set successfully");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateDataset)}: Error setting response parameters");
            }
        }
    }
}