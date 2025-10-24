namespace SxG.EvalPlatform.Plugins
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using SxG.EvalPlatform.Plugins.Common.Framework;
    using SxG.EvalPlatform.Plugins.Models;
    using SxG.EvalPlatform.Plugins.Models.Requests;
    using SxG.EvalPlatform.Plugins.Models.Responses;
    using SxG.EvalPlatform.Plugins.CustomApis;

    /// <summary>
    /// Plugin for updating dataset from external eval artifacts API and updating eval run records
    /// </summary>
    public class UpdateDataset : PluginBase
    {
        private const string ExternalDatasetApiUrl = "https://sxgevalapidev.azurewebsites.net/api/v1/eval/artifacts/dataset";

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

                tracingService.Trace($"{nameof(UpdateDataset)}: Request validated successfully - EvalRunId: {request.EvalRunId}");

                // Call external dataset API to get dataset data
                var datasetResponse = CallExternalDatasetApi(request.EvalRunId, tracingService);
                if (datasetResponse == null)
                {
                    var errorResponse = UpdateDatasetResponse.CreateError("Failed to retrieve dataset from external API", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, tracingService);
                    return;
                }

                // Update the eval run record with retrieved dataset content
                bool updateSuccess = UpdateEvalRunRecord(request.EvalRunId, datasetResponse.DatasetContent, organizationService, tracingService);
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

            // Extract only the evalRunId parameter
            if (context.InputParameters.Contains(CustomApiConfig.UpdateDataset.RequestParameters.EvalRunId))
                request.EvalRunId = context.InputParameters[CustomApiConfig.UpdateDataset.RequestParameters.EvalRunId]?.ToString();

            tracingService.Trace($"{nameof(UpdateDataset)}: Extracted request parameters from context");
            tracingService.Trace($"{nameof(UpdateDataset)}: EvalRunId: {request.EvalRunId}");

            return request;
        }

        /// <summary>
        /// Calls external dataset API to retrieve dataset data
        /// </summary>
        /// <param name="evalRunId">Eval Run ID</param>
        /// <param name="tracingService">Tracing service</param>
        /// <returns>External API response data</returns>
        private ExternalDatasetResponse CallExternalDatasetApi(string evalRunId, ITracingService tracingService)
        {
            try
            {
                string url = $"{ExternalDatasetApiUrl}?evalRunId={evalRunId}";
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

                            // Parse the response
                            return ParseExternalDatasetApiResponse(responseBody, tracingService);
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
                return null;
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(UpdateDataset)}: Exception in CallExternalDatasetApi - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parses external dataset API response using JSON deserialization
        /// </summary>
        /// <param name="responseBody">Response body from external API</param>
        /// <param name="tracingService">Tracing service</param>
        /// <returns>Parsed response data</returns>
        private ExternalDatasetResponse ParseExternalDatasetApiResponse(string responseBody, ITracingService tracingService)
        {
            try
            {
                tracingService.Trace($"{nameof(UpdateDataset)}: Parsing external dataset API response JSON");

                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    tracingService.Trace($"{nameof(UpdateDataset)}: Response body is empty or null");
                    return null;
                }

                // Parse the JSON response
                var jsonResponse = JObject.Parse(responseBody);
                
                // Extract the required fields from the response
                var response = new ExternalDatasetResponse
                {
                    EvalRunId = jsonResponse.Value<string>("evalRunId"),
                    AgentId = jsonResponse.Value<string>("agentId"),
                    DataSetId = jsonResponse.Value<string>("dataSetId"),
                    DatasetContent = null // Will be set below
                };

                // Extract datasetContent array and serialize it back to JSON string for storage
                var datasetContentToken = jsonResponse["datasetContent"];
                if (datasetContentToken != null)
                {
                    // Convert the datasetContent array to JSON string for storage in Dataverse
                    // Use JsonConvert.SerializeObject for maximum compatibility with .NET Framework 4.6.2
                    response.DatasetContent = JsonConvert.SerializeObject(datasetContentToken);
                    
                    // Count items if it's an array
                    int itemCount = 0;
                    if (datasetContentToken is JArray datasetArray)
                    {
                        itemCount = datasetArray.Count;
                    }
                    tracingService.Trace($"{nameof(UpdateDataset)}: Successfully parsed datasetContent with {itemCount} items");
                }
                else
                {
                    tracingService.Trace($"{nameof(UpdateDataset)}: No datasetContent found in response");
                }

                tracingService.Trace($"{nameof(UpdateDataset)}: Successfully parsed external dataset API response");
                tracingService.Trace($"{nameof(UpdateDataset)}: EvalRunId: {response.EvalRunId}, AgentId: {response.AgentId}, DataSetId: {response.DataSetId}");
                
                return response;
            }
            catch (JsonReaderException jsonEx)
            {
                tracingService.Trace($"{nameof(UpdateDataset)}: JSON parsing error - {jsonEx.Message}");
                tracingService.Trace($"{nameof(UpdateDataset)}: Invalid JSON response: {responseBody}");
                return null;
            }
            catch (JsonException jsonEx)
            {
                tracingService.Trace($"{nameof(UpdateDataset)}: JSON processing error - {jsonEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(UpdateDataset)}: Exception parsing external dataset API response - {ex.Message}");
                tracingService.Trace($"{nameof(UpdateDataset)}: Response body: {responseBody}");
                return null;
            }
        }

        /// <summary>
        /// Updates eval run record with retrieved dataset content
        /// </summary>
        /// <param name="evalRunId">Eval run ID</param>
        /// <param name="datasetContent">Retrieved dataset content as JSON string</param>
        /// <param name="organizationService">Organization service</param>
        /// <param name="tracingService">Tracing service</param>
        /// <returns>True if update successful</returns>
        private bool UpdateEvalRunRecord(string evalRunId, string datasetContent, IOrganizationService organizationService, ITracingService tracingService)
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
                if (!string.IsNullOrEmpty(datasetContent))
                {
                    updateEntity[EvalRunEntity.Fields.Dataset] = datasetContent;
                    tracingService.Trace($"{nameof(UpdateDataset)}: Dataset content length: {datasetContent.Length} characters");
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

        /// <summary>
        /// Helper class for external dataset API response
        /// </summary>
        private class ExternalDatasetResponse
        {
            public string EvalRunId { get; set; }
            public string AgentId { get; set; }
            public string DataSetId { get; set; }
            public string DatasetContent { get; set; }
        }
    }
}