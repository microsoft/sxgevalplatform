namespace SxG.EvalPlatform.Plugins
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using Microsoft.Xrm.Sdk;
    using SxG.EvalPlatform.Plugins.Common.Framework;
    using SxG.EvalPlatform.Plugins.Models;
    using SxG.EvalPlatform.Plugins.Models.Requests;
    using SxG.EvalPlatform.Plugins.Models.Responses;
    using SxG.EvalPlatform.Plugins.CustomApis;

    /// <summary>
    /// Plugin for updating dataset from external eval datasets API and updating eval run records
    /// </summary>
    public class UpdateDataset : PluginBase
    {
        private const string ExternalDatasetApiUrl = "https://sxgevalapidev.azurewebsites.net/api/v1/eval/datasets";
        private const string ExternalStatusApiUrl = "https://sxgevalapidev.azurewebsites.net/api/v1/eval/runs";

        public UpdateDataset(string unsecureConfig, string secureConfig) : base(unsecureConfig, secureConfig)
        {
            // TODO: Implement your custom configuration handling
            // https://docs.Microsoft.com/powerapps/developer/common-data-service/register-plug-in#set-configuration-data
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException(nameof(localContext));
            }

            var context = localContext.PluginExecutionContext;
            var tracingService = localContext.TracingService;
            var organizationService = localContext.OrganizationService;

            try
            {
                tracingService.Trace($"{nameof(UpdateDataset)}: Starting execution");

                // Extract request parameters from input parameters
                var request = ExtractRequestFromContext(context, tracingService);

                // Validate request
                if (!request.IsValid())
                {
                    string validationError = request.GetValidationError();
                    tracingService.Trace($"{nameof(UpdateDataset)}: Validation failed - {validationError}");
                    
                    var errorResponse = UpdateDatasetResponse.CreateError(validationError, request.EvalRunId);
                    SetResponseParameters(context, errorResponse, tracingService);
                    return;
                }

                // Validate datasetId is provided
                if (string.IsNullOrWhiteSpace(request.DatasetId))
                {
                    tracingService.Trace($"{nameof(UpdateDataset)}: Validation failed - DatasetId is required");
                    var errorResponse = UpdateDatasetResponse.CreateError("DatasetId is required", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, tracingService);
                    return;
                }

                tracingService.Trace($"{nameof(UpdateDataset)}: Request validated successfully - EvalRunId: {request.EvalRunId}, DatasetId: {request.DatasetId}");

                // Update external status to "EnrichingDataset" before fetching dataset
                bool statusUpdateSuccess = UpdateExternalEvalRunStatus(request.EvalRunId, "EnrichingDataset", tracingService);
                if (!statusUpdateSuccess)
                {
                    tracingService.Trace($"{nameof(UpdateDataset)}: Warning - Failed to update external status to EnrichingDataset, continuing with dataset fetch");
                    // Continue execution even if status update fails
                }

                // Call external dataset API to get dataset data using datasetId
                var datasetJson = CallExternalDatasetApi(request.DatasetId, tracingService);
                if (datasetJson == null)
                {
                    var errorResponse = UpdateDatasetResponse.CreateError("Failed to retrieve dataset from external API", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, tracingService);
                    return;
                }

                // Update the eval run record with retrieved dataset content
                bool updateSuccess = UpdateEvalRunRecord(request.EvalRunId, datasetJson, organizationService, tracingService);
                if (!updateSuccess)
                {
                    var errorResponse = UpdateDatasetResponse.CreateError("Failed to update eval run record", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, tracingService);
                    return;
                }

                // Create success response
                var response = UpdateDatasetResponse.CreateSuccess();
                SetResponseParameters(context, response, tracingService);

                tracingService.Trace($"{nameof(UpdateDataset)}: Execution completed successfully");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(UpdateDataset)}: Exception occurred - " + ex.Message);
                tracingService.Trace($"{nameof(UpdateDataset)}: Stack trace - " + ex.StackTrace);

                // Create error response
                var errorResponse = UpdateDatasetResponse.CreateError($"Internal server error: {ex.Message}");
                SetResponseParameters(context, errorResponse, tracingService);

                throw new InvalidPluginExecutionException($"{nameof(UpdateDataset)} :: Error :: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Extracts request parameters from plugin execution context
        /// </summary>
        /// <param name="context">Plugin execution context</param>
        /// <param name="tracingService">Tracing service</param>
        /// <returns>UpdateDatasetRequest object</returns>
        private UpdateDatasetRequest ExtractRequestFromContext(IPluginExecutionContext context, ITracingService tracingService)
        {
            var request = new UpdateDatasetRequest();

            // Extract evalRunId parameter
            if (context.InputParameters.Contains(CustomApiConfig.UpdateDataset.RequestParameters.EvalRunId))
                request.EvalRunId = context.InputParameters[CustomApiConfig.UpdateDataset.RequestParameters.EvalRunId]?.ToString();

            // Extract datasetId parameter
            if (context.InputParameters.Contains(CustomApiConfig.UpdateDataset.RequestParameters.DatasetId))
                request.DatasetId = context.InputParameters[CustomApiConfig.UpdateDataset.RequestParameters.DatasetId]?.ToString();

            tracingService.Trace($"{nameof(UpdateDataset)}: Extracted request parameters from context");
            tracingService.Trace($"{nameof(UpdateDataset)}: EvalRunId: {request.EvalRunId}, DatasetId: {request.DatasetId}");

            return request;
        }

        /// <summary>
        /// Updates external eval run status via API call
        /// </summary>
        /// <param name="evalRunId">Eval Run ID</param>
        /// <param name="status">Status to set (e.g., "EnrichingDataset")</param>
        /// <param name="tracingService">Tracing service</param>
        /// <returns>True if update successful, false otherwise</returns>
        private bool UpdateExternalEvalRunStatus(string evalRunId, string status, ITracingService tracingService)
        {
            try
            {
                string url = $"{ExternalStatusApiUrl}/{evalRunId}/status";
                tracingService.Trace($"{nameof(UpdateDataset)}: Calling external status API: {url}");

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.Method = "PUT";
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Timeout = 30000; // 30 seconds

                // Prepare request body
                string requestBody = $"{{\"status\":\"{status}\"}}";
                byte[] data = Encoding.UTF8.GetBytes(requestBody);
                httpWebRequest.ContentLength = data.Length;

                tracingService.Trace($"{nameof(UpdateDataset)}: Status update request body: {requestBody}");

                using (Stream requestStream = httpWebRequest.GetRequestStream())
                {
                    requestStream.Write(data, 0, data.Length);
                }

                using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
                {
                    if (httpWebResponse.StatusCode == HttpStatusCode.OK || httpWebResponse.StatusCode == HttpStatusCode.NoContent)
                    {
                        tracingService.Trace($"{nameof(UpdateDataset)}: Successfully updated external status to {status}");
                        return true;
                    }
                    else
                    {
                        tracingService.Trace($"{nameof(UpdateDataset)}: External status API returned status: {httpWebResponse.StatusCode}");
                        return false;
                    }
                }
            }
            catch (WebException webEx)
            {
                tracingService.Trace($"{nameof(UpdateDataset)}: WebException updating external status - {webEx.Message}");
                if (webEx.Response != null)
                {
                    using (Stream responseStream = webEx.Response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        string errorResponse = reader.ReadToEnd();
                        tracingService.Trace($"{nameof(UpdateDataset)}: Status update error response: {errorResponse}");
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(UpdateDataset)}: Exception updating external status - {ex.Message}");
                tracingService.Trace($"{nameof(UpdateDataset)}: Stack trace - {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Calls external dataset API to retrieve dataset data
        /// </summary>
        /// <param name="datasetId">Dataset ID</param>
        /// <param name="tracingService">Tracing service</param>
        /// <returns>Dataset JSON string or null if failed</returns>
        private string CallExternalDatasetApi(string datasetId, ITracingService tracingService)
        {
            try
            {
                string url = $"{ExternalDatasetApiUrl}/{datasetId}";
                tracingService.Trace($"{nameof(UpdateDataset)}: Calling external dataset API: {url}");

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.Method = "GET";
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Timeout = 30000; // 30 seconds

                using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
                {
                    if (httpWebResponse.StatusCode == HttpStatusCode.OK)
                    {
                        using (Stream responseStream = httpWebResponse.GetResponseStream())
                        using (StreamReader reader = new StreamReader(responseStream))
                        {
                            string responseBody = reader.ReadToEnd();
                            tracingService.Trace($"{nameof(UpdateDataset)}: External dataset API response received: {responseBody}");

                            // Return the raw JSON array string as-is for storage
                            return responseBody;
                        }
                    }
                    else
                    {
                        tracingService.Trace($"{nameof(UpdateDataset)}: External dataset API returned status: {httpWebResponse.StatusCode}");
                        return null;
                    }
                }
            }
            catch (WebException webEx)
            {
                tracingService.Trace($"{nameof(UpdateDataset)}: WebException occurred - {webEx.Message}");
                if (webEx.Response != null)
                {
                    using (Stream responseStream = webEx.Response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        string errorResponse = reader.ReadToEnd();
                        tracingService.Trace($"{nameof(UpdateDataset)}: Error response: {errorResponse}");
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(UpdateDataset)}: Exception in CallExternalDatasetApi - {ex.Message}");
                tracingService.Trace($"{nameof(UpdateDataset)}: Stack trace - {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Updates eval run record with retrieved dataset content
        /// </summary>
        /// <param name="evalRunId">Eval run ID</param>
        /// <param name="datasetJson">Retrieved dataset content as JSON string</param>
        /// <param name="organizationService">Organization service</param>
        /// <param name="tracingService">Tracing service</param>
        /// <returns>True if update successful</returns>
        private bool UpdateEvalRunRecord(string evalRunId, string datasetJson, IOrganizationService organizationService, ITracingService tracingService)
        {
            try
            {
                // Parse the EvalRunId GUID for direct update using Primary Key
                if (!Guid.TryParse(evalRunId, out Guid evalRunGuid))
                {
                    tracingService.Trace($"{nameof(UpdateDataset)}: Invalid EvalRunId format: {evalRunId}");
                    return false;
                }

                // Update the record directly using EvalRunId (Primary Key) for faster performance
                Entity updateEntity = new Entity(EvalRunEntity.EntityLogicalName)
                {
                    Id = evalRunGuid
                };

                // Set data from external dataset API
                if (!string.IsNullOrEmpty(datasetJson))
                {
                    updateEntity[EvalRunEntity.Fields.Dataset] = datasetJson;
                    tracingService.Trace($"{nameof(UpdateDataset)}: Dataset content length: {datasetJson.Length} characters");
                }
                else
                {
                    tracingService.Trace($"{nameof(UpdateDataset)}: Warning - No dataset content to store");
                }

                // Set status as OptionSetValue for Choice field (Updated status)
                updateEntity[EvalRunEntity.Fields.Status] = new OptionSetValue(EvalRunEntity.StatusValues.Updated);

                organizationService.Update(updateEntity);

                tracingService.Trace($"{nameof(UpdateDataset)}: Successfully updated eval run record");
                tracingService.Trace($"{nameof(UpdateDataset)}: Dataset content stored, Status set to Updated (value: {EvalRunEntity.StatusValues.Updated})");
                return true;
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(UpdateDataset)}: Exception updating eval run record - {ex.Message}");
                tracingService.Trace($"{nameof(UpdateDataset)}: Stack trace - {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Sets response parameters in the plugin execution context
        /// </summary>
        /// <param name="context">Plugin execution context</param>
        /// <param name="response">Response object</param>
        /// <param name="tracingService">Tracing service</param>
        private void SetResponseParameters(IPluginExecutionContext context, UpdateDatasetResponse response, ITracingService tracingService)
        {
            try
            {
                context.OutputParameters[CustomApiConfig.UpdateDataset.ResponseProperties.Success] = response.Success;
                context.OutputParameters[CustomApiConfig.UpdateDataset.ResponseProperties.Message] = response.Message;
                context.OutputParameters[CustomApiConfig.UpdateDataset.ResponseProperties.Timestamp] = response.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                tracingService.Trace($"{nameof(UpdateDataset)}: Response parameters set successfully");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(UpdateDataset)}: Error setting response parameters - {ex.Message}");
                // Continue execution - response setting failure shouldn't break the plugin
            }
        }
    }
}