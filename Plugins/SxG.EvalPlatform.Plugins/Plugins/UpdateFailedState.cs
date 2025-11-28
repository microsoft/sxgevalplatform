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
    /// Plugin for updating eval run status to Failed in Dataverse and external API
    /// </summary>
    public class UpdateFailedState : PluginBase
    {
        public UpdateFailedState(string unsecureConfig, string secureConfig) : base(unsecureConfig, secureConfig)
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
                loggingService.Trace($"{nameof(UpdateFailedState)}: Starting execution");

                // Extract request parameters from input parameters
                var request = ExtractRequestFromContext(context, loggingService);

                // Validate request
                if (!request.IsValid())
                {
                    string validationError = request.GetValidationError();
                    loggingService.Trace($"{nameof(UpdateFailedState)}: Validation failed - {validationError}", TraceSeverity.Warning);

                    var errorResponse = UpdateFailedStateResponse.CreateError(validationError, request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                loggingService.Trace($"{nameof(UpdateFailedState)}: Request validated successfully - EvalRunId: {request.EvalRunId}");

                // Update Dataverse record status to Failed
                bool dataverseUpdateSuccess = UpdateEvalRunStatusInDataverse(request.EvalRunId, organizationService, loggingService);
                if (!dataverseUpdateSuccess)
                {
                    var errorResponse = UpdateFailedStateResponse.CreateError("Failed to update eval run status in Dataverse", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Get authentication token for external API call
                string apiScope = configService.GetApiScope();
                string authToken = AuthTokenHelper.AcquireToken(managedIdentityService, loggingService, apiScope);

                // Update external API status to Failed
                bool externalUpdateSuccess = UpdateExternalEvalRunStatus(request.EvalRunId, "DatasetEnrichmentFailed", authToken, loggingService, configService);
                if (!externalUpdateSuccess)
                {
                    loggingService.Trace($"{nameof(UpdateFailedState)}: Warning - Failed to update external status, but Dataverse update succeeded", TraceSeverity.Warning);
                    // Continue execution - Dataverse update succeeded
                }

                // Log event
                loggingService.LogEvent("UpdateFailedStateSuccess", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "EvalRunId", request.EvalRunId },
                    { "DataverseUpdate", "Success" },
                    { "ExternalApiUpdate", externalUpdateSuccess ? "Success" : "Failed" }
                });

                // Create success response
                var response = UpdateFailedStateResponse.CreateSuccess();
                SetResponseParameters(context, response, loggingService);

                loggingService.Trace($"{nameof(UpdateFailedState)}: Execution completed successfully");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateFailedState)}: Exception occurred");

                // Create error response
                var errorResponse = UpdateFailedStateResponse.CreateError($"Internal server error: {ex.Message}");
                SetResponseParameters(context, errorResponse, loggingService);

                throw new InvalidPluginExecutionException($"{nameof(UpdateFailedState)} :: Error :: " + ex.Message, ex);
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
        /// <returns>UpdateFailedStateRequest object</returns>
        private UpdateFailedStateRequest ExtractRequestFromContext(IPluginExecutionContext context, IPluginLoggingService loggingService)
        {
            var request = new UpdateFailedStateRequest();

            // Extract evalRunId parameter
            if (context.InputParameters.Contains(CustomApiConfig.UpdateFailedState.RequestParameters.EvalRunId))
                request.EvalRunId = context.InputParameters[CustomApiConfig.UpdateFailedState.RequestParameters.EvalRunId]?.ToString();

            loggingService.Trace($"{nameof(UpdateFailedState)}: Extracted request parameters from context");
            loggingService.Trace($"{nameof(UpdateFailedState)}: EvalRunId: {request.EvalRunId}");

            return request;
        }

        /// <summary>
        /// Updates eval run status to Failed in Dataverse
        /// </summary>
        /// <param name="evalRunId">Eval run ID</param>
        /// <param name="organizationService">Organization service</param>
        /// <param name="loggingService">Logging service</param>
        /// <returns>True if update successful</returns>
        private bool UpdateEvalRunStatusInDataverse(string evalRunId, IOrganizationService organizationService, IPluginLoggingService loggingService)
        {
            try
            {
                // Parse the EvalRunId GUID for direct update using Primary Key
                if (!Guid.TryParse(evalRunId, out Guid evalRunGuid))
                {
                    loggingService.Trace($"{nameof(UpdateFailedState)}: Invalid EvalRunId format: {evalRunId}", TraceSeverity.Error);
                    return false;
                }

                // Update using late-bound entity to avoid serialization issues with Elastic tables
                var updateEntity = new Entity("cr890_evalrun", evalRunGuid);
                updateEntity["cr890_status"] = new OptionSetValue(4);  // Status = Failed (value: 4)

                organizationService.Update(updateEntity);

                loggingService.Trace($"{nameof(UpdateFailedState)}: Successfully updated eval run status to Failed (4) in Dataverse");
                return true;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateFailedState)}: Exception updating eval run status in Dataverse");
                return false;
            }
        }

        /// <summary>
        /// Updates external eval run status via API call
        /// </summary>
        /// <param name="evalRunId">Eval Run ID</param>
        /// <param name="status">Status to set (e.g., "Failed")</param>
        /// <param name="authToken">Authentication bearer token</param>
        /// <param name="loggingService">Logging service</param>
        /// <param name="configService">Configuration service</param>
        /// <returns>True if update successful, false otherwise</returns>
        private bool UpdateExternalEvalRunStatus(string evalRunId, string status, string authToken, IPluginLoggingService loggingService, IPluginConfigurationService configService)
        {
            var startTime = DateTimeOffset.UtcNow;
            try
            {
                string url = $"{configService.GetEvalRunsStatusApiUrl(evalRunId)}";
                loggingService.Trace($"{nameof(UpdateFailedState)}: Calling external status API: {url}");

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

                loggingService.Trace($"{nameof(UpdateFailedState)}: Status update request body: {requestBody}");

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
                        loggingService.Trace($"{nameof(UpdateFailedState)}: Successfully updated external status to {status}");
                        return true;
                    }
                    else
                    {
                        loggingService.Trace($"{nameof(UpdateFailedState)}: External status API returned status: {httpWebResponse.StatusCode}", TraceSeverity.Warning);
                        return false;
                    }
                }
            }
            catch (WebException webEx)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("EvalAPI", "UpdateStatus", startTime, duration, false);
                loggingService.LogException(webEx, $"{nameof(UpdateFailedState)}: WebException updating external status");
                return false;
            }
            catch (Exception ex)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("EvalAPI", "UpdateStatus", startTime, duration, false);
                loggingService.LogException(ex, $"{nameof(UpdateFailedState)}: Exception updating external status");
                return false;
            }
        }

        /// <summary>
        /// Sets response parameters in the plugin execution context
        /// </summary>
        /// <param name="context">Plugin execution context</param>
        /// <param name="response">Response object</param>
        /// <param name="loggingService">Logging service</param>
        private void SetResponseParameters(IPluginExecutionContext context, UpdateFailedStateResponse response, IPluginLoggingService loggingService)
        {
            try
            {
                context.OutputParameters[CustomApiConfig.UpdateFailedState.ResponseProperties.Success] = response.Success;
                context.OutputParameters[CustomApiConfig.UpdateFailedState.ResponseProperties.Message] = response.Message;
                context.OutputParameters[CustomApiConfig.UpdateFailedState.ResponseProperties.Timestamp] = response.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                loggingService.Trace($"{nameof(UpdateFailedState)}: Response parameters set successfully");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateFailedState)}: Error setting response parameters");
            }
        }
    }
}
