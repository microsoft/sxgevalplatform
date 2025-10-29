namespace SxG.EvalPlatform.Plugins
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using SxG.EvalPlatform.Plugins.Common.Framework;
    using SxG.EvalPlatform.Plugins.Models;
    using SxG.EvalPlatform.Plugins.Models.Requests;
    using SxG.EvalPlatform.Plugins.Models.Responses;
    using SxG.EvalPlatform.Plugins.CustomApis;

    /// <summary>
    /// Plugin for publishing enriched dataset by retrieving stored dataset and calling external API
    /// </summary>
    public class PublishEnrichedDataset : PluginBase
    {
        private const string ExternalApiUrl = "https://sxgevalapidev.azurewebsites.net/api/v1/eval/runs/{0}/enriched-dataset";

        public PublishEnrichedDataset(string unsecureConfig, string secureConfig) : base(unsecureConfig, secureConfig)
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
                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Starting execution");

                // Extract request parameters from input parameters
                var request = ExtractRequestFromContext(context, tracingService);

                // Validate request
                if (!request.IsValid())
                {
                    string validationError = request.GetValidationError();
                    tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Validation failed - {validationError}");
                    
                    var errorResponse = PublishEnrichedDatasetResponse.CreateError(validationError, request.EvalRunId);
                    SetResponseParameters(context, errorResponse, tracingService);
                    return;
                }

                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Request validated successfully - EvalRunId: {request.EvalRunId}");

                // Retrieve dataset from the evaluation run record
                string dataset = RetrieveDatasetFromEvalRun(request.EvalRunId, organizationService, tracingService);
                if (string.IsNullOrEmpty(dataset))
                {
                    var errorResponse = PublishEnrichedDatasetResponse.CreateError("No dataset found in eval run record or failed to retrieve dataset", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, tracingService);
                    return;
                }

                // Call external API to publish enriched dataset
                bool publishSuccess = CallExternalPublishApi(request.EvalRunId, dataset, tracingService);
                if (!publishSuccess)
                {
                    var errorResponse = PublishEnrichedDatasetResponse.CreateError("Failed to publish enriched dataset to external API", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, tracingService);
                    return;
                }

                // Update status to Completed
                UpdateEvalRunStatus(request.EvalRunId, EvalRunEntity.StatusValues.Completed, organizationService, tracingService);

                // Create success response
                var response = PublishEnrichedDatasetResponse.CreateSuccess();
                SetResponseParameters(context, response, tracingService);

                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Execution completed successfully");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Exception occurred - " + ex.Message);
                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Stack trace - " + ex.StackTrace);

                // Create error response
                var errorResponse = PublishEnrichedDatasetResponse.CreateError($"Internal server error: {ex.Message}");
                SetResponseParameters(context, errorResponse, tracingService);

                throw new InvalidPluginExecutionException($"{nameof(PublishEnrichedDataset)} :: Error :: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Extracts request parameters from plugin execution context
        /// </summary>
        /// <param name="context">Plugin execution context</param>
        /// <param name="tracingService">Tracing service</param>
        /// <returns>PublishEnrichedDatasetRequest object</returns>
        private PublishEnrichedDatasetRequest ExtractRequestFromContext(IPluginExecutionContext context, ITracingService tracingService)
        {
            var request = new PublishEnrichedDatasetRequest();

            // Extract from input parameters
            if (context.InputParameters.Contains(CustomApiConfig.PublishEnrichedDataset.RequestParameters.EvalRunId))
                request.EvalRunId = context.InputParameters[CustomApiConfig.PublishEnrichedDataset.RequestParameters.EvalRunId]?.ToString();

            tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Extracted request parameters from context");
            tracingService.Trace($"{nameof(PublishEnrichedDataset)}: EvalRunId: {request.EvalRunId}");

            return request;
        }

        /// <summary>
        /// Retrieves dataset from eval run record
        /// </summary>
        /// <param name="evalRunId">Eval run ID</param>
        /// <param name="organizationService">Organization service</param>
        /// <param name="tracingService">Tracing service</param>
        /// <returns>Dataset JSON string or null if not found</returns>
        private string RetrieveDatasetFromEvalRun(string evalRunId, IOrganizationService organizationService, ITracingService tracingService)
        {
            try
            {
                // Parse the EvalRunId GUID for direct query using Primary Key
                if (!Guid.TryParse(evalRunId, out Guid evalRunGuid))
                {
                    tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Invalid EvalRunId format: {evalRunId}");
                    return null;
                }

                // Retrieve the record directly using EvalRunId (Primary Key) for faster performance
                Entity evalRunRecord = organizationService.Retrieve(
                    EvalRunEntity.EntityLogicalName,
                    evalRunGuid,
                    new ColumnSet(EvalRunEntity.Fields.Dataset)
                );

                string dataset = evalRunRecord.GetAttributeValue<string>(EvalRunEntity.Fields.Dataset);
                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Successfully retrieved dataset from eval run record");
                
                return dataset;
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Exception retrieving dataset from eval run record - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Updates eval run status
        /// </summary>
        /// <param name="evalRunId">Eval run ID</param>
        /// <param name="statusValue">New status integer value</param>
        /// <param name="organizationService">Organization service</param>
        /// <param name="tracingService">Tracing service</param>
        private void UpdateEvalRunStatus(string evalRunId, int statusValue, IOrganizationService organizationService, ITracingService tracingService)
        {
            try
            {
                // Parse the EvalRunId GUID for direct update using Primary Key
                if (!Guid.TryParse(evalRunId, out Guid evalRunGuid))
                {
                    tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Invalid EvalRunId format for status update: {evalRunId}");
                    return;
                }

                // Update the record directly using EvalRunId (Primary Key) for faster performance
                Entity updateEntity = new Entity(EvalRunEntity.EntityLogicalName)
                {
                    Id = evalRunGuid
                };

                updateEntity[EvalRunEntity.Fields.Status] = new OptionSetValue(statusValue);

                organizationService.Update(updateEntity);

                // Get status name for logging
                var tempEntity = new EvalRunEntity { Status = statusValue };
                string statusName = tempEntity.GetStatusName();
                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Successfully updated eval run status to {statusName} (value: {statusValue})");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Exception updating eval run status - {ex.Message}");
            }
        }

        /// <summary>
        /// Calls external API to publish enriched dataset
        /// </summary>
        /// <param name="evalRunId">Eval run ID</param>
        /// <param name="dataset">Dataset JSON string to publish</param>
        /// <param name="tracingService">Tracing service</param>
        /// <returns>True if publish successful</returns>
        private bool CallExternalPublishApi(string evalRunId, string dataset, ITracingService tracingService)
        {
            try
            {
                string url = string.Format(ExternalApiUrl, evalRunId);
                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Calling external API: {url}");

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.Method = "POST";
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Timeout = 30000; // 30 seconds

                // Prepare request body with enrichedDataset property
                string requestBody = CreatePublishRequestBody(dataset);
                byte[] data = Encoding.UTF8.GetBytes(requestBody);
                httpWebRequest.ContentLength = data.Length;

                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Request body: {requestBody}");

                using (Stream stream = httpWebRequest.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
                {
                    if (httpWebResponse.StatusCode == HttpStatusCode.OK || httpWebResponse.StatusCode == HttpStatusCode.Created)
                    {
                        tracingService.Trace($"{nameof(PublishEnrichedDataset)}: External API call successful");
                        return true;
                    }
                    else
                    {
                        tracingService.Trace($"{nameof(PublishEnrichedDataset)}: External API returned status: {httpWebResponse.StatusCode}");
                        return false;
                    }
                }
            }
            catch (WebException webEx)
            {
                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: WebException occurred - {webEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Exception in CallExternalPublishApi - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates request body for external publish API
        /// </summary>
        /// <param name="dataset">Dataset JSON string to publish</param>
        /// <returns>JSON request body</returns>
        private string CreatePublishRequestBody(string dataset)
        {
            // Create JSON request body in the format: { "enrichedDataset": [...] }
            // The dataset is already a JSON string, so we need to embed it properly
            return $"{{\"enrichedDataset\":{dataset}}}";
        }

        /// <summary>
        /// Sets response parameters in the plugin execution context
        /// </summary>
        /// <param name="context">Plugin execution context</param>
        /// <param name="response">Response object</param>
        /// <param name="tracingService">Tracing service</param>
        private void SetResponseParameters(IPluginExecutionContext context, PublishEnrichedDatasetResponse response, ITracingService tracingService)
        {
            try
            {
                context.OutputParameters[CustomApiConfig.PublishEnrichedDataset.ResponseProperties.Success] = response.Success;
                context.OutputParameters[CustomApiConfig.PublishEnrichedDataset.ResponseProperties.Message] = response.Message;
                context.OutputParameters[CustomApiConfig.PublishEnrichedDataset.ResponseProperties.Timestamp] = response.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Response parameters set successfully");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(PublishEnrichedDataset)}: Error setting response parameters - {ex.Message}");
                // Continue execution - response setting failure shouldn't break the plugin
            }
        }
    }
}