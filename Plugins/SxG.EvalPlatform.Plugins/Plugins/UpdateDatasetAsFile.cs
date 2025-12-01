namespace SxG.EvalPlatform.Plugins
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Messages;
    using SxG.EvalPlatform.Plugins.Common.Framework;
    using SxG.EvalPlatform.Plugins.Models;
    using SxG.EvalPlatform.Plugins.Models.Requests;
    using SxG.EvalPlatform.Plugins.Models.Responses;
    using SxG.EvalPlatform.Plugins.CustomApis;
    using SxG.EvalPlatform.Plugins.Services;

    /// <summary>
    /// Plugin for updating dataset from external eval datasets API and storing as file in Dataverse file column
    /// This plugin is DLP-compliant and uses only 1st party Dataverse SDK connections
    /// </summary>
    public class UpdateDatasetAsFile : PluginBase
    {
        public UpdateDatasetAsFile(string unsecureConfig, string secureConfig) : base(unsecureConfig, secureConfig)
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
                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Starting execution");

                // Extract request parameters from input parameters
                var request = ExtractRequestFromContext(context, loggingService);

                // Validate request
                if (!request.IsValid())
                {
                    string validationError = request.GetValidationError();
                    loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Validation failed - {validationError}", TraceSeverity.Warning);

                    var errorResponse = UpdateDatasetResponse.CreateError(validationError, request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Validate datasetId is provided
                if (string.IsNullOrWhiteSpace(request.DatasetId))
                {
                    loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Validation failed - DatasetId is required", TraceSeverity.Warning);
                    var errorResponse = UpdateDatasetResponse.CreateError("DatasetId is required", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Request validated successfully - EvalRunId: {request.EvalRunId}, DatasetId: {request.DatasetId}");

                // Update external status to "EnrichingDataset" before fetching dataset
                bool statusUpdateSuccess = UpdateExternalEvalRunStatus(request.EvalRunId, "EnrichingDataset", loggingService, configService);
                if (!statusUpdateSuccess)
                {
                    loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Warning - Failed to update external status to EnrichingDataset, continuing with dataset fetch", TraceSeverity.Warning);
                }

                // Call external dataset API to get dataset data using datasetId
                var datasetJson = CallExternalDatasetApi(request.DatasetId, loggingService, configService);
                if (datasetJson == null)
                {
                    var errorResponse = UpdateDatasetResponse.CreateError("Failed to retrieve dataset from external API", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Parse the EvalRunId GUID
                if (!Guid.TryParse(request.EvalRunId, out Guid evalRunGuid))
                {
                    loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Invalid EvalRunId format: {request.EvalRunId}", TraceSeverity.Error);
                    var errorResponse = UpdateDatasetResponse.CreateError("Invalid EvalRunId format", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Upload dataset as file to file column using Dataverse file blocks API
                bool uploadSuccess = UploadDatasetAsFile(evalRunGuid, request.DatasetId, datasetJson, organizationService, loggingService);
                if (!uploadSuccess)
                {
                    var errorResponse = UpdateDatasetResponse.CreateError("Failed to upload dataset as file", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Update the eval run record status
                bool updateSuccess = UpdateEvalRunStatus(evalRunGuid, organizationService, loggingService);
                if (!updateSuccess)
                {
                    var errorResponse = UpdateDatasetResponse.CreateError("Failed to update eval run status", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Log event to Application Insights
                loggingService.LogEvent("UpdateDatasetAsFileSuccess", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "EvalRunId", request.EvalRunId },
                    { "DatasetId", request.DatasetId },
                    { "DatasetSize", datasetJson?.Length.ToString() ?? "0" }
                });

                // Create success response
                var response = UpdateDatasetResponse.CreateSuccess();
                SetResponseParameters(context, response, loggingService);

                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Execution completed successfully");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateDatasetAsFile)}: Exception occurred");

                // Create error response
                var errorResponse = UpdateDatasetResponse.CreateError($"Internal server error: {ex.Message}");
                SetResponseParameters(context, errorResponse, loggingService);

                throw new InvalidPluginExecutionException($"{nameof(UpdateDatasetAsFile)} :: Error :: " + ex.Message, ex);
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
        private UpdateDatasetRequest ExtractRequestFromContext(IPluginExecutionContext context, IPluginLoggingService loggingService)
        {
            var request = new UpdateDatasetRequest();

            // Extract evalRunId parameter
            if (context.InputParameters.Contains(CustomApiConfig.UpdateDatasetAsFile.RequestParameters.EvalRunId))
                request.EvalRunId = context.InputParameters[CustomApiConfig.UpdateDatasetAsFile.RequestParameters.EvalRunId]?.ToString();

            // Extract datasetId parameter
            if (context.InputParameters.Contains(CustomApiConfig.UpdateDatasetAsFile.RequestParameters.DatasetId))
                request.DatasetId = context.InputParameters[CustomApiConfig.UpdateDatasetAsFile.RequestParameters.DatasetId]?.ToString();

            loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Extracted request parameters from context");
            loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: EvalRunId: {request.EvalRunId}, DatasetId: {request.DatasetId}");

            return request;
        }

        /// <summary>
        /// Updates external eval run status via API call
        /// </summary>
        private bool UpdateExternalEvalRunStatus(string evalRunId, string status, IPluginLoggingService loggingService, IPluginConfigurationService configService)
        {
            var startTime = DateTimeOffset.UtcNow;
            try
            {
                string url = $"{configService.GetEvalRunsStatusApiUrl(evalRunId)}";
                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Calling external status API: {url}");

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.Method = "PUT";
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Timeout = configService.GetApiTimeoutSeconds() * 1000;

                string requestBody = $"{{\"status\":\"{status}\"}}";
                byte[] data = Encoding.UTF8.GetBytes(requestBody);
                httpWebRequest.ContentLength = data.Length;

                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Status update request body: {requestBody}");

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
                        loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Successfully updated external status to {status}");
                        return true;
                    }
                    else
                    {
                        loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: External status API returned status: {httpWebResponse.StatusCode}", TraceSeverity.Warning);
                        return false;
                    }
                }
            }
            catch (WebException webEx)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("EvalAPI", "UpdateStatus", startTime, duration, false);
                loggingService.LogException(webEx, $"{nameof(UpdateDatasetAsFile)}: WebException updating external status");
                return false;
            }
            catch (Exception ex)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("EvalAPI", "UpdateStatus", startTime, duration, false);
                loggingService.LogException(ex, $"{nameof(UpdateDatasetAsFile)}: Exception updating external status");
                return false;
            }
        }

        /// <summary>
        /// Calls external dataset API to retrieve dataset data
        /// </summary>
        private string CallExternalDatasetApi(string datasetId, IPluginLoggingService loggingService, IPluginConfigurationService configService)
        {
            var startTime = DateTimeOffset.UtcNow;
            try
            {
                string url = configService.GetDatasetsApiUrl(datasetId);
                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Calling external dataset API: {url}");

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
                            loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: External dataset API response received, length: {responseBody.Length} characters");
                            return responseBody;
                        }
                    }
                    else
                    {
                        loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: External dataset API returned status: {httpWebResponse.StatusCode}", TraceSeverity.Warning);
                        return null;
                    }
                }
            }
            catch (WebException webEx)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("EvalAPI", $"GetDataset/{datasetId}", startTime, duration, false);
                loggingService.LogException(webEx, $"{nameof(UpdateDatasetAsFile)}: WebException occurred");
                return null;
            }
            catch (Exception ex)
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                loggingService.LogDependency("EvalAPI", $"GetDataset/{datasetId}", startTime, duration, false);
                loggingService.LogException(ex, $"{nameof(UpdateDatasetAsFile)}: Exception in CallExternalDatasetApi");
                return null;
            }
        }

        /// <summary>
        /// Uploads dataset as file to Dataverse file column using file blocks API
        /// This approach is DLP-compliant using only 1st party Dataverse SDK
        /// </summary>
        private bool UploadDatasetAsFile(Guid evalRunGuid, string datasetId, string datasetJson, IOrganizationService organizationService, IPluginLoggingService loggingService)
        {
            try
            {
                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Starting file upload process for EvalRunId: {evalRunGuid}");

                // Convert JSON string to byte array
                byte[] fileContent = Encoding.UTF8.GetBytes(datasetJson);
                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: File content size: {fileContent.Length} bytes");

                // Generate filename with dataset ID and timestamp
                string fileName = $"dataset_{datasetId}_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: File name: {fileName}");

                // Step 1: Initialize file blocks upload
                var initializeRequest = new OrganizationRequest("InitializeFileBlocksUpload")
                {
                    ["Target"] = new EntityReference("cr890_evalrun", evalRunGuid),
                    ["FileAttributeName"] = "cr890_datasetfile",
                    ["FileName"] = fileName
                };

                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Initializing file blocks upload");
                var initializeResponse = organizationService.Execute(initializeRequest);
                string fileContinuationToken = (string)initializeResponse["FileContinuationToken"];
                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: File blocks upload initialized, token received");

                // Step 2: Upload file content in blocks
                // For files < 4MB, we can upload in a single block
                const int blockSize = 4 * 1024 * 1024; // 4MB block size
                int blockNumber = 0;

                for (int offset = 0; offset < fileContent.Length; offset += blockSize)
                {
                    int currentBlockSize = Math.Min(blockSize, fileContent.Length - offset);
                    byte[] blockData = new byte[currentBlockSize];
                    Array.Copy(fileContent, offset, blockData, 0, currentBlockSize);

                    var uploadBlockRequest = new OrganizationRequest("UploadBlock")
                    {
                        ["BlockId"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(blockNumber.ToString("0000"))),
                        ["BlockData"] = blockData,  // Pass byte[] directly, not Base64 string
                        ["FileContinuationToken"] = fileContinuationToken
                    };

                    loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Uploading block {blockNumber}, size: {currentBlockSize} bytes");
                    organizationService.Execute(uploadBlockRequest);
                    blockNumber++;
                }

                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: All blocks uploaded, total blocks: {blockNumber}");

                // Step 3: Build block list for commit
                var blockList = new System.Collections.Generic.List<string>();
                for (int i = 0; i < blockNumber; i++)
                {
                    blockList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(i.ToString("0000"))));
                }

                // Step 4: Commit file blocks upload
                var commitRequest = new OrganizationRequest("CommitFileBlocksUpload")
                {
                    ["BlockList"] = blockList.ToArray(),
                    ["FileContinuationToken"] = fileContinuationToken,
                    ["FileName"] = fileName,
                    ["MimeType"] = "application/json"
                };

                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Committing file blocks upload");
                var commitResponse = organizationService.Execute(commitRequest);
                
                // FileId is returned as Guid, not string
                Guid fileId = (Guid)commitResponse["FileId"];

                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: File upload completed successfully, FileId: {fileId}");
                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Dataset stored as file in cr890_datasetfile column");

                return true;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateDatasetAsFile)}: Exception uploading dataset as file");
                return false;
            }
        }

        /// <summary>
        /// Updates eval run record status after file upload
        /// </summary>
        private bool UpdateEvalRunStatus(Guid evalRunGuid, IOrganizationService organizationService, IPluginLoggingService loggingService)
        {
            try
            {
                // Update status to Updated (2)
                var updateEntity = new Entity("cr890_evalrun", evalRunGuid);
                updateEntity["cr890_status"] = new OptionSetValue(2);  // Status = Updated

                organizationService.Update(updateEntity);

                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Successfully updated eval run status to Updated (2)");
                return true;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateDatasetAsFile)}: Exception updating eval run status");
                return false;
            }
        }

        /// <summary>
        /// Sets response parameters in the plugin execution context
        /// </summary>
        private void SetResponseParameters(IPluginExecutionContext context, UpdateDatasetResponse response, IPluginLoggingService loggingService)
        {
            try
            {
                context.OutputParameters[CustomApiConfig.UpdateDatasetAsFile.ResponseProperties.Success] = response.Success;
                context.OutputParameters[CustomApiConfig.UpdateDatasetAsFile.ResponseProperties.Message] = response.Message;
                context.OutputParameters[CustomApiConfig.UpdateDatasetAsFile.ResponseProperties.Timestamp] = response.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                loggingService.Trace($"{nameof(UpdateDatasetAsFile)}: Response parameters set successfully");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateDatasetAsFile)}: Error setting response parameters");
            }
        }
    }
}
