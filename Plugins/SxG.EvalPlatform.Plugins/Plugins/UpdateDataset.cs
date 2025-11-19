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
    using SxG.EvalPlatform.Plugins.Services;
    using Microsoft.Crm.Sdk.Messages;

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

                // Update external status to "EnrichingDataset" before fetching dataset
                bool statusUpdateSuccess = UpdateExternalEvalRunStatus(request.EvalRunId, "EnrichingDataset", loggingService, configService);
                if (!statusUpdateSuccess)
                {
                    loggingService.Trace($"{nameof(UpdateDataset)}: Warning - Failed to update external status to EnrichingDataset, continuing with dataset fetch", TraceSeverity.Warning);
                    // Continue execution even if status update fails
                }

                // Call external dataset API to get dataset data using datasetId
                var datasetJson = CallExternalDatasetApi(request.DatasetId, loggingService, configService);
                if (datasetJson == null)
                {
                    var errorResponse = UpdateDatasetResponse.CreateError("Failed to retrieve dataset from external API", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Update the eval run record with retrieved dataset content (store as file column instead of text field)
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
        /// <param name="loggingService">Logging service</param>
        /// <param name="configService">Configuration service</param>
        /// <returns>True if update successful, false otherwise</returns>
        private bool UpdateExternalEvalRunStatus(string evalRunId, string status, IPluginLoggingService loggingService, IPluginConfigurationService configService)
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
        /// <param name="loggingService">Logging service</param>
        /// <param name="configService">Configuration service</param>
        /// <returns>Dataset JSON string or null if failed</returns>
        private string CallExternalDatasetApi(string datasetId, IPluginLoggingService loggingService, IPluginConfigurationService configService)
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
        /// Updates eval run record by uploading dataset JSON into file column and setting status
        /// </summary>
        /// /// <param name="evalRunId">Eval run ID</param>
        /// <param name="datasetJson">Retrieved dataset content as JSON string</param>
        /// <param name="organizationService">Organization service</param>
        /// <param name="loggingService">Logging service</param>
        /// <returns>True if update successful</returns>
        private bool UpdateEvalRunRecord(string evalRunId, string datasetJson, IOrganizationService organizationService, IPluginLoggingService loggingService)
        {
            try
            {
                if (!Guid.TryParse(evalRunId, out Guid evalRunGuid))
                {
                    loggingService.Trace($"{nameof(UpdateDataset)}: Invalid EvalRunId format: {evalRunId}", TraceSeverity.Error);
                    return false;
                }

                if (string.IsNullOrEmpty(datasetJson))
                {
                    loggingService.Trace($"{nameof(UpdateDataset)}: Dataset JSON is empty", TraceSeverity.Warning);
                    return false;
                }

                var fileName = $"dataset-{evalRunGuid}.json";
                byte[] dataBytes = Encoding.UTF8.GetBytes(datasetJson);
                loggingService.Trace($"{nameof(UpdateDataset)}: Preparing to upload file '{fileName}' size {dataBytes.Length} bytes to file column cr890_datasetfile");

                // Initialize upload
                var initReq = new InitializeFileBlocksUploadRequest
                {
                    Target = new EntityReference("cr890_evalrun", evalRunGuid),
                    FileAttributeName = "cr890_datasetfile",
                    FileName = fileName
                };
                var initResp = (InitializeFileBlocksUploadResponse)organizationService.Execute(initReq);
                var uploadId = initResp.FileContinuationToken;
                loggingService.Trace($"{nameof(UpdateDataset)}: File upload initialized. UploadId={uploadId}");

                // Upload blocks (single or multiple depending on size)
                const int blockSize = 4 * 1024 * 1024; //4MB block size
                var blockIds = new System.Collections.Generic.List<string>();
                int offset = 0;
                int blockIndex = 0;
                while (offset < dataBytes.Length)
                {
                    int remaining = dataBytes.Length - offset;
                    int currentSize = remaining > blockSize ? blockSize : remaining;
                    byte[] block = new byte[currentSize];
                    Buffer.BlockCopy(dataBytes, offset, block, 0, currentSize);
                    string blockId = blockIndex.ToString();

                    var uploadBlockReq = new UploadBlockRequest
                    {
                        FileContinuationToken = uploadId,
                        BlockId = blockId,
                        BlockData = block
                    };
                    organizationService.Execute(uploadBlockReq);

                    blockIds.Add(blockId);
                    offset += currentSize;
                    blockIndex++;
                }
                loggingService.Trace($"{nameof(UpdateDataset)}: Uploaded {blockIds.Count} block(s) for file '{fileName}'");

                // Commit upload
                var commitReq = new CommitFileBlocksUploadRequest
                {
                    FileContinuationToken = uploadId,
                    FileName = fileName,
                    MimeType = "application/json",
                    BlockList = blockIds.ToArray()
                };
                organizationService.Execute(commitReq);
                loggingService.Trace($"{nameof(UpdateDataset)}: File blocks committed successfully for '{fileName}'");

                // Update status field only (OptionSetValue2 = Updated)
                var statusEntity = new Entity("cr890_evalrun", evalRunGuid);
                statusEntity["cr890_status"] = new OptionSetValue(2);
                organizationService.Update(statusEntity);

                loggingService.Trace($"{nameof(UpdateDataset)}: Successfully updated status to Updated (2) after file upload");
                return true;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateDataset)}: Exception updating eval run record (file upload)");
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