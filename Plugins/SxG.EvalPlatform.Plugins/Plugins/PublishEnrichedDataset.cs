namespace SxG.EvalPlatform.Plugins
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using SxG.EvalPlatform.Plugins.Common;
    using SxG.EvalPlatform.Plugins.Common.Framework;
    using SxG.EvalPlatform.Plugins.Models;
    using SxG.EvalPlatform.Plugins.Models.Requests;
    using SxG.EvalPlatform.Plugins.Models.Responses;
    using SxG.EvalPlatform.Plugins.CustomApis;
    using SxG.EvalPlatform.Plugins.Services;

    /// <summary>
    /// Plugin for publishing enriched dataset by retrieving stored dataset and calling external API
    /// </summary>
    public class PublishEnrichedDataset : PluginBase
    {
        public PublishEnrichedDataset(string unsecureConfig, string secureConfig) : base(unsecureConfig, secureConfig)
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
                loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Starting execution");

                // Extract request parameters from input parameters
                var request = ExtractRequestFromContext(context, loggingService);

                // Validate request
                if (!request.IsValid())
                {
                    string validationError = request.GetValidationError();
                    loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Validation failed - {validationError}", TraceSeverity.Warning);

                    var errorResponse = PublishEnrichedDatasetResponse.CreateError(validationError, request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Request validated successfully - EvalRunId: {request.EvalRunId}");

                // Retrieve dataset from the evaluation run record
                string dataset = RetrieveDatasetFromEvalRun(request.EvalRunId, organizationService, loggingService);
                if (string.IsNullOrEmpty(dataset))
                {
                    var errorResponse = PublishEnrichedDatasetResponse.CreateError("No dataset found in eval run record or failed to retrieve dataset", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Get authentication token for external API calls
                string apiScope = configService.GetApiScope();
                string authToken = AuthTokenHelper.AcquireToken(managedIdentityService, loggingService, apiScope);

                // Call external API to publish enriched dataset
                bool publishSuccess = CallExternalPublishApi(request.EvalRunId, dataset, authToken, loggingService, configService);
                if (!publishSuccess)
                {
                    var errorResponse = PublishEnrichedDatasetResponse.CreateError("Failed to publish enriched dataset to external API", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Update status to Completed (value: 3)
                UpdateEvalRunStatus(request.EvalRunId, 3, organizationService, loggingService);

                // Log event to Application Insights
                loggingService.LogEvent("PublishEnrichedDatasetSuccess", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "EvalRunId", request.EvalRunId },
                    { "DatasetSize", dataset?.Length.ToString() ?? "0" }
                });

                // Create success response
                var response = PublishEnrichedDatasetResponse.CreateSuccess();
                SetResponseParameters(context, response, loggingService);

                loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Execution completed successfully");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(PublishEnrichedDataset)}: Exception occurred");

                // Create error response
                var errorResponse = PublishEnrichedDatasetResponse.CreateError($"Internal server error: {ex.Message}");
                SetResponseParameters(context, errorResponse, loggingService);

                throw new InvalidPluginExecutionException($"{nameof(PublishEnrichedDataset)} :: Error :: " + ex.Message, ex);
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
        /// <returns>PublishEnrichedDatasetRequest object</returns>
        private PublishEnrichedDatasetRequest ExtractRequestFromContext(IPluginExecutionContext context, IPluginLoggingService loggingService)
        {
            var request = new PublishEnrichedDatasetRequest();

            // Extract from input parameters
            if (context.InputParameters.Contains(CustomApiConfig.PublishEnrichedDataset.RequestParameters.EvalRunId))
                request.EvalRunId = context.InputParameters[CustomApiConfig.PublishEnrichedDataset.RequestParameters.EvalRunId]?.ToString();

            loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Extracted request parameters from context");
            loggingService.Trace($"{nameof(PublishEnrichedDataset)}: EvalRunId: {request.EvalRunId}");

            return request;
        }

        /// <summary>
        /// Retrieves dataset from eval run record's file column
        /// </summary>
        /// <param name="evalRunId">Eval run ID</param>
        /// <param name="organizationService">Organization service</param>
        /// <param name="loggingService">Logging service</param>
        /// <returns>Dataset JSON string or null if not found</returns>
        private string RetrieveDatasetFromEvalRun(string evalRunId, IOrganizationService organizationService, IPluginLoggingService loggingService)
        {
            try
            {
                // Parse the EvalRunId GUID for direct query using Primary Key
                if (!Guid.TryParse(evalRunId, out Guid evalRunGuid))
                {
                    loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Invalid EvalRunId format: {evalRunId}", TraceSeverity.Error);
                    return null;
                }

                loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Retrieving dataset file from cr890_datasetfile column");

                // Download the dataset file from cr890_datasetfile column using file blocks API
                string dataset = DownloadDatasetFile(evalRunGuid, organizationService, loggingService);
                
                if (string.IsNullOrEmpty(dataset))
                {
                    loggingService.Trace($"{nameof(PublishEnrichedDataset)}: No dataset file found in cr890_datasetfile column", TraceSeverity.Warning);
                    return null;
                }

                loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Successfully retrieved dataset from file column, length: {dataset.Length}");
                return dataset;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(PublishEnrichedDataset)}: Exception retrieving dataset from eval run record");
                return null;
            }
        }

        /// <summary>
        /// Downloads dataset file from Dataverse file column using file blocks API
        /// </summary>
        private string DownloadDatasetFile(Guid evalRunGuid, IOrganizationService organizationService, IPluginLoggingService loggingService)
        {
            try
            {
                loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Starting file download process for EvalRunId: {evalRunGuid}");

                var initializeRequest = new OrganizationRequest("InitializeFileBlocksDownload")
                {
                    ["Target"] = new EntityReference("cr890_evalrun", evalRunGuid),
                    ["FileAttributeName"] = "cr890_datasetfile"
                };

                loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Initializing file blocks download");
                var initializeResponse = organizationService.Execute(initializeRequest);
                string fileContinuationToken = (string)initializeResponse["FileContinuationToken"];
                long fileSize = (long)initializeResponse["FileSizeInBytes"];
                loggingService.Trace($"{nameof(PublishEnrichedDataset)}: File blocks download initialized, size: {fileSize} bytes");

                const int blockSize = 4 * 1024 * 1024; // 4MB block size
                var fileBytes = new System.Collections.Generic.List<byte>();
                long offset = 0;

                while (offset < fileSize)
                {
                    long currentBlockSize = Math.Min(blockSize, fileSize - offset);

                    var downloadBlockRequest = new OrganizationRequest("DownloadBlock")
                    {
                        ["Offset"] = offset,
                        ["BlockLength"] = currentBlockSize,
                        ["FileContinuationToken"] = fileContinuationToken
                    };

                    loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Downloading block at offset {offset}, size: {currentBlockSize} bytes");
                    var downloadBlockResponse = organizationService.Execute(downloadBlockRequest);
                    byte[] blockData = (byte[])downloadBlockResponse["Data"];
                    fileBytes.AddRange(blockData);

                    offset += currentBlockSize;
                }

                loggingService.Trace($"{nameof(PublishEnrichedDataset)}: All blocks downloaded, total size: {fileBytes.Count} bytes");

                // Convert byte array to string
                string datasetJson = Encoding.UTF8.GetString(fileBytes.ToArray());
                loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Dataset file downloaded and converted to string");

                return datasetJson;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(PublishEnrichedDataset)}: Exception downloading dataset file");
                return null;
            }
        }

        /// <summary>
        /// Updates eval run status
        /// </summary>
        /// <param name="evalRunId">Eval run ID</param>
        /// <param name="statusValue">New status integer value</param>
        /// <param name="organizationService">Organization service</param>
        /// <param name="loggingService">Logging service</param>
        private void UpdateEvalRunStatus(string evalRunId, int statusValue, IOrganizationService organizationService, IPluginLoggingService loggingService)
        {
            try
            {
                // Parse the EvalRunId GUID for direct update using Primary Key
                if (!Guid.TryParse(evalRunId, out Guid evalRunGuid))
                {
                    loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Invalid EvalRunId format for status update: {evalRunId}", TraceSeverity.Error);
                    return;
                }

                // Update using late-bound entity to avoid serialization issues with Elastic tables
                var updateEntity = new Entity("cr890_evalrun", evalRunGuid);
                updateEntity["cr890_status"] = new OptionSetValue(statusValue);

                organizationService.Update(updateEntity);

                loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Successfully updated eval run status to {statusValue}");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(PublishEnrichedDataset)}: Exception updating eval run status");
            }
        }

        /// <summary>
        /// Calls external API to publish enriched dataset
        /// </summary>
        /// <param name="evalRunId">Eval run ID</param>
        /// <param name="dataset">Dataset JSON string to publish</param>
        /// <param name="authToken">Authentication bearer token</param>
        /// <param name="loggingService">Logging service</param>
        /// <param name="configService">Configuration service</param>
        /// <returns>True if publish successful</returns>
        private bool CallExternalPublishApi(string evalRunId, string dataset, string authToken, IPluginLoggingService loggingService, IPluginConfigurationService configService)
        {
            var startTime = DateTimeOffset.UtcNow;
            try
            {
                string url = configService.GetEnrichedDatasetApiUrl(evalRunId);
                loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Calling external API: {url}");

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.Method = "POST";
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Timeout = configService.GetApiTimeoutSeconds() * 1000;

                // Add authorization header if token is available
                AuthTokenHelper.AddAuthorizationHeader(httpWebRequest, loggingService, authToken);

                // Prepare request body with enrichedDataset property
                string requestBody = CreatePublishRequestBody(dataset);
                byte[] data = Encoding.UTF8.GetBytes(requestBody);
                httpWebRequest.ContentLength = data.Length;

                loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Request body length: {requestBody.Length} characters");

                using (Stream stream = httpWebRequest.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
                {
                    var duration = DateTimeOffset.UtcNow - startTime;
                    bool success = httpWebResponse.StatusCode == HttpStatusCode.OK || httpWebResponse.StatusCode == HttpStatusCode.Created;

                    loggingService.LogDependency("EvalAPI", url, startTime, duration, success);

                    if (success)
                    {
                        loggingService.Trace($"{nameof(PublishEnrichedDataset)}: External API call successful");
                        return true;
                    }
                    else
                    {
                        loggingService.Trace($"{nameof(PublishEnrichedDataset)}: External API returned status: {httpWebResponse.StatusCode}", TraceSeverity.Warning);
                        return false;
                    }
                }
            }
            catch (WebException webEx)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("EvalAPI", $"PublishEnrichedDataset/{evalRunId}", startTime, duration, false);
                loggingService.LogException(webEx, $"{nameof(PublishEnrichedDataset)}: WebException occurred");
                return false;
            }
            catch (Exception ex)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("EvalAPI", $"PublishEnrichedDataset/{evalRunId}", startTime, duration, false);
                loggingService.LogException(ex, $"{nameof(PublishEnrichedDataset)}: Exception in CallExternalPublishApi");
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
        /// <param name="loggingService">Logging service</param>
        private void SetResponseParameters(IPluginExecutionContext context, PublishEnrichedDatasetResponse response, IPluginLoggingService loggingService)
        {
            try
            {
                context.OutputParameters[CustomApiConfig.PublishEnrichedDataset.ResponseProperties.Success] = response.Success;
                context.OutputParameters[CustomApiConfig.PublishEnrichedDataset.ResponseProperties.Message] = response.Message;
                context.OutputParameters[CustomApiConfig.PublishEnrichedDataset.ResponseProperties.Timestamp] = response.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                loggingService.Trace($"{nameof(PublishEnrichedDataset)}: Response parameters set successfully");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(PublishEnrichedDataset)}: Error setting response parameters");
            }
        }
    }
}