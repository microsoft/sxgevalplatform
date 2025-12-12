namespace SxG.EvalPlatform.Plugins
{
    using System;
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
    /// Plugin for updating the enriched dataset file in the cr890_datasetfile column
    /// Used after Power Automate flow enriches the dataset with copilot responses
    /// </summary>
    public class UpdateEnrichedDatasetFile : PluginBase
    {
        public UpdateEnrichedDatasetFile(string unsecureConfig, string secureConfig) : base(unsecureConfig, secureConfig)
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
            var organizationService = localContext.OrganizationService;

            try
            {
                loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: Starting execution");
                var request = ExtractRequestFromContext(context, loggingService);

                if (!request.IsValid())
                {
                    string validationError = request.GetValidationError();
                    loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: Validation failed - {validationError}", Services.TraceSeverity.Warning);

                    var errorResponse = UpdateEnrichedDatasetFileResponse.CreateError(validationError, request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: Request validated successfully - EvalRunId: {request.EvalRunId}");

                if (!Guid.TryParse(request.EvalRunId, out Guid evalRunGuid))
                {
                    loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: Invalid EvalRunId format: {request.EvalRunId}", Services.TraceSeverity.Error);
                    var errorResponse = UpdateEnrichedDatasetFileResponse.CreateError("Invalid EvalRunId format", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Upload enriched dataset as file to file column using Dataverse file blocks API
                bool uploadSuccess = UploadEnrichedDatasetAsFile(evalRunGuid, request.EnrichedDatasetJson, organizationService, loggingService);
                if (!uploadSuccess)
                {
                    var errorResponse = UpdateEnrichedDatasetFileResponse.CreateError("Failed to upload enriched dataset as file", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Update the eval run record status to Updated (2)
                bool updateSuccess = UpdateEvalRunStatus(evalRunGuid, organizationService, loggingService);
                if (!updateSuccess)
                {
                    var errorResponse = UpdateEnrichedDatasetFileResponse.CreateError("Failed to update eval run status", request.EvalRunId);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                loggingService.LogEvent("UpdateEnrichedDatasetFileSuccess", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "EvalRunId", request.EvalRunId },
                    { "DatasetSize", request.EnrichedDatasetJson?.Length.ToString() ?? "0" }
                });

                var response = UpdateEnrichedDatasetFileResponse.CreateSuccess();
                SetResponseParameters(context, response, loggingService);

                loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: Execution completed successfully");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateEnrichedDatasetFile)}: Exception occurred");

                // Create error response
                var errorResponse = UpdateEnrichedDatasetFileResponse.CreateError($"Internal server error: {ex.Message}");
                SetResponseParameters(context, errorResponse, loggingService);

                throw new InvalidPluginExecutionException($"{nameof(UpdateEnrichedDatasetFile)} :: Error :: " + ex.Message, ex);
            }
            finally
            {
                loggingService.Flush();
            }
        }

        /// <summary>
        /// Extracts request parameters from plugin execution context
        /// </summary>
        private UpdateEnrichedDatasetFileRequest ExtractRequestFromContext(IPluginExecutionContext context, Services.IPluginLoggingService loggingService)
        {
            var request = new UpdateEnrichedDatasetFileRequest();

            // Extract evalRunId parameter
            if (context.InputParameters.Contains(CustomApiConfig.UpdateEnrichedDatasetFile.RequestParameters.EvalRunId))
                request.EvalRunId = context.InputParameters[CustomApiConfig.UpdateEnrichedDatasetFile.RequestParameters.EvalRunId]?.ToString();

            // Extract enrichedDatasetJson parameter
            if (context.InputParameters.Contains(CustomApiConfig.UpdateEnrichedDatasetFile.RequestParameters.EnrichedDatasetJson))
                request.EnrichedDatasetJson = context.InputParameters[CustomApiConfig.UpdateEnrichedDatasetFile.RequestParameters.EnrichedDatasetJson]?.ToString();

            loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: Extracted request parameters from context");
            loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: EvalRunId: {request.EvalRunId}, EnrichedDatasetJson length: {request.EnrichedDatasetJson?.Length ?? 0}");

            return request;
        }

        /// <summary>
        /// Uploads enriched dataset as file to Dataverse file column using file blocks API
        /// </summary>
        private bool UploadEnrichedDatasetAsFile(Guid evalRunGuid, string enrichedDatasetJson, IOrganizationService organizationService, Services.IPluginLoggingService loggingService)
        {
            try
            {
                loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: Starting enriched file upload process for EvalRunId: {evalRunGuid}");

                // Convert JSON string to byte array
                byte[] fileContent = Encoding.UTF8.GetBytes(enrichedDatasetJson);
                loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: File content size: {fileContent.Length} bytes");

                // Generate filename with enriched indicator and timestamp
                string fileName = $"dataset_enriched_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
                loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: File name: {fileName}");

                var initializeRequest = new OrganizationRequest("InitializeFileBlocksUpload")
                {
                    ["Target"] = new EntityReference("cr890_evalrun", evalRunGuid),
                    ["FileAttributeName"] = "cr890_datasetfile",
                    ["FileName"] = fileName
                };

                loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: Initializing file blocks upload");
                var initializeResponse = organizationService.Execute(initializeRequest);
                string fileContinuationToken = (string)initializeResponse["FileContinuationToken"];
                loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: File blocks upload initialized, token received");

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

                    loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: Uploading block {blockNumber}, size: {currentBlockSize} bytes");
                    organizationService.Execute(uploadBlockRequest);
                    blockNumber++;
                }

                loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: All blocks uploaded, total blocks: {blockNumber}");

                var blockList = new System.Collections.Generic.List<string>();
                for (int i = 0; i < blockNumber; i++)
                {
                    blockList.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(i.ToString("0000"))));
                }

                var commitRequest = new OrganizationRequest("CommitFileBlocksUpload")
                {
                    ["BlockList"] = blockList.ToArray(),
                    ["FileContinuationToken"] = fileContinuationToken,
                    ["FileName"] = fileName,
                    ["MimeType"] = "application/json"
                };

                loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: Committing file blocks upload");
                var commitResponse = organizationService.Execute(commitRequest);
                
                // FileId is returned as Guid, not string
                Guid fileId = (Guid)commitResponse["FileId"];

                loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: Enriched file upload completed successfully, FileId: {fileId}");
                loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: Enriched dataset stored as file in cr890_datasetfile column");

                return true;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateEnrichedDatasetFile)}: Exception uploading enriched dataset as file");
                return false;
            }
        }

        /// <summary>
        /// Updates eval run record status after enriched file upload
        /// </summary>
        private bool UpdateEvalRunStatus(Guid evalRunGuid, IOrganizationService organizationService, Services.IPluginLoggingService loggingService)
        {
            try
            {
                // Update status to Updated (2)
                var updateEntity = new Entity("cr890_evalrun", evalRunGuid);
                updateEntity["cr890_status"] = new OptionSetValue(2);  // Status = Updated

                organizationService.Update(updateEntity);

                loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: Successfully updated eval run status to Updated (2)");
                return true;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateEnrichedDatasetFile)}: Exception updating eval run status");
                return false;
            }
        }

        /// <summary>
        /// Sets response parameters in the plugin execution context
        /// </summary>
        private void SetResponseParameters(IPluginExecutionContext context, UpdateEnrichedDatasetFileResponse response, Services.IPluginLoggingService loggingService)
        {
            try
            {
                context.OutputParameters[CustomApiConfig.UpdateEnrichedDatasetFile.ResponseProperties.Success] = response.Success;
                context.OutputParameters[CustomApiConfig.UpdateEnrichedDatasetFile.ResponseProperties.Message] = response.Message;
                context.OutputParameters[CustomApiConfig.UpdateEnrichedDatasetFile.ResponseProperties.Timestamp] = response.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                loggingService.Trace($"{nameof(UpdateEnrichedDatasetFile)}: Response parameters set successfully");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(UpdateEnrichedDatasetFile)}: Error setting response parameters");
            }
        }
    }
}
