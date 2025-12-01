namespace SxG.EvalPlatform.Plugins
{
    using System;
    using System.Text;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json;
    using SxG.EvalPlatform.Plugins.Common.Framework;
    using SxG.EvalPlatform.Plugins.Models;
    using SxG.EvalPlatform.Plugins.Models.DTO;
    using SxG.EvalPlatform.Plugins.Models.Requests;
    using SxG.EvalPlatform.Plugins.Models.Responses;
    using SxG.EvalPlatform.Plugins.CustomApis;
    using SxG.EvalPlatform.Plugins.Services;

    public class GetEvalRun : PluginBase
    {
        public GetEvalRun(string unsecureConfig, string secureConfig) : base(unsecureConfig, secureConfig)
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
                loggingService.Trace($"{nameof(GetEvalRun)}: Starting execution");

                // Extract request parameters from input parameters
                var request = ExtractRequestFromContext(context, loggingService);

                // Validate request
                if (!request.IsValid())
                {
                    string validationError = request.GetValidationError();
                    loggingService.Trace($"{nameof(GetEvalRun)}: Validation failed - {validationError}", TraceSeverity.Warning);

                    var errorResponse = GetEvalRunResponse.CreateError(validationError);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                loggingService.Trace($"{nameof(GetEvalRun)}: Searching for eval run with EvalRunId: {request.EvalRunId}");

                // Parse the provided GUID for direct query
                if (!Guid.TryParse(request.EvalRunId, out Guid evalRunGuid))
                {
                    var errorResponse = GetEvalRunResponse.CreateError("Invalid EvalRunId format");
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Query directly using EvalRunId (Primary Key) for faster performance using early-bound entity
                try
                {
                    var evalRunRecord = organizationService.Retrieve(
                        EvalRun.EntityLogicalName, evalRunGuid, 
                            new ColumnSet("cr890_evalrunid", "cr890_id", "cr890_datasetid", "cr890_agentid", "cr890_environmentid", 
                                "cr890_agentschemaname", "cr890_status", "createdon", "modifiedon")
                        ).ToEntity<EvalRun>();

                    // Download dataset from file column instead of reading from JSON column
                    loggingService.Trace($"{nameof(GetEvalRun)}: Downloading dataset from file column");
                    string datasetJson = DownloadDatasetFile(evalRunGuid, organizationService, loggingService);

                    // Convert early-bound entity to DTO for response
                    EvalRunDto evalRun = ConvertToDto(evalRunRecord, datasetJson);

                    loggingService.Trace($"{nameof(GetEvalRun)}: Found eval run - EvalRunId: {evalRun.EvalRunId}, AgentId: {evalRun.AgentId}, Status: {evalRun.GetStatusName()}");
                    loggingService.Trace($"{nameof(GetEvalRun)}: Dataset JSON string length: {(evalRun.Dataset?.Length ?? 0)}");

                    // Log event to Application Insights
                    loggingService.LogEvent("GetEvalRunSuccess", new System.Collections.Generic.Dictionary<string, string>{
                        { "EvalRunId", evalRun.EvalRunId.ToString() },
                        { "AgentId", evalRun.AgentId },
                        { "Status", evalRun.GetStatusName() }
                    });

                    // Create success response (this will parse the dataset JSON automatically)
                    var response = GetEvalRunResponse.CreateSuccess(evalRun);
                    loggingService.Trace($"{nameof(GetEvalRun)}: Dataset parsed into {response.Dataset?.Count ?? 0} items");

                    SetResponseParameters(context, response, loggingService);
                }
                catch (Exception retrieveEx)
                {
                    loggingService.Trace($"{nameof(GetEvalRun)}: Record not found or error retrieving: {retrieveEx.Message}", TraceSeverity.Warning);

                    // Create not found response
                    var response = GetEvalRunResponse.CreateSuccess(null);
                    SetResponseParameters(context, response, loggingService);
                }

                loggingService.Trace($"{nameof(GetEvalRun)}: Execution completed successfully");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(GetEvalRun)}: Exception occurred");

                // Create error response
                var errorResponse = GetEvalRunResponse.CreateError($"Internal server error: {ex.Message}");
                SetResponseParameters(context, errorResponse, loggingService);

                throw new InvalidPluginExecutionException($"{nameof(GetEvalRun)} :: Error :: " + ex.Message, ex);
            }
            finally
            {
                // Flush telemetry
                loggingService.Flush();
            }
        }

        /// <summary>
        /// Downloads dataset file from Dataverse file column using file blocks API
        /// </summary>
        private string DownloadDatasetFile(Guid evalRunGuid, IOrganizationService organizationService, IPluginLoggingService loggingService)
        {
            try
            {
                loggingService.Trace($"{nameof(GetEvalRun)}: Starting file download process for EvalRunId: {evalRunGuid}");

                // Step 1: Initialize file blocks download
                var initializeRequest = new OrganizationRequest("InitializeFileBlocksDownload")
                {
                    ["Target"] = new EntityReference("cr890_evalrun", evalRunGuid),
                    ["FileAttributeName"] = "cr890_datasetfile"
                };

                loggingService.Trace($"{nameof(GetEvalRun)}: Initializing file blocks download");
                var initializeResponse = organizationService.Execute(initializeRequest);
                string fileContinuationToken = (string)initializeResponse["FileContinuationToken"];
                long fileSize = (long)initializeResponse["FileSizeInBytes"];
                loggingService.Trace($"{nameof(GetEvalRun)}: File blocks download initialized, size: {fileSize} bytes");

                // Step 2: Download file content in blocks
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

                    loggingService.Trace($"{nameof(GetEvalRun)}: Downloading block at offset {offset}, size: {currentBlockSize} bytes");
                    var downloadBlockResponse = organizationService.Execute(downloadBlockRequest);
                    byte[] blockData = (byte[])downloadBlockResponse["Data"];
                    fileBytes.AddRange(blockData);

                    offset += currentBlockSize;
                }

                loggingService.Trace($"{nameof(GetEvalRun)}: All blocks downloaded, total size: {fileBytes.Count} bytes");

                // Convert byte array to string
                string datasetJson = Encoding.UTF8.GetString(fileBytes.ToArray());
                loggingService.Trace($"{nameof(GetEvalRun)}: Dataset file downloaded and converted to string");

                return datasetJson;
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(GetEvalRun)}: Exception downloading dataset file");
                return null;
            }
        }

        /// <summary>
        /// Converts early-bound entity to DTO
        /// </summary>
        private EvalRunDto ConvertToDto(EvalRun entity, string datasetJson)
        {
            if (entity == null)
                return null;

            return new EvalRunDto
            {
                EvalRunId = entity.Id,
                Id = entity.cr890_Id,
                DatasetId = entity.cr890_DatasetId,
                AgentId = entity.cr890_AgentId,
                EnvironmentId = entity.cr890_EnvironmentId,
                AgentSchemaName = entity.cr890_AgentSchemaName,
                Status = entity.cr890_Status.HasValue ? (int)entity.cr890_Status.Value : (int?)null,
                Dataset = datasetJson,
                CreatedOn = entity.CreatedOn,
                ModifiedOn = entity.ModifiedOn
            };
        }

        /// <summary>
        /// Extracts request parameters from plugin execution context
        /// </summary>
        private GetEvalRunRequest ExtractRequestFromContext(IPluginExecutionContext context, IPluginLoggingService loggingService)
        {
            var request = new GetEvalRunRequest();

            // Extract from input parameters
            if (context.InputParameters.Contains(CustomApiConfig.GetEvalRun.RequestParameters.EvalRunId))
                request.EvalRunId = context.InputParameters[CustomApiConfig.GetEvalRun.RequestParameters.EvalRunId]?.ToString();

            loggingService.Trace($"{nameof(GetEvalRun)}: Extracted EvalRunId from context: {request.EvalRunId}");

            return request;
        }

        /// <summary>
        /// Sets response parameters in the plugin execution context
        /// </summary>
        private void SetResponseParameters(IPluginExecutionContext context, GetEvalRunResponse response, IPluginLoggingService loggingService)
        {
            try
            {
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.EvalRunId] = response.EvalRunId;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.Message] = response.Message;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.Timestamp] = response.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.AgentId] = response.AgentId;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.EnvironmentId] = response.EnvironmentId;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.AgentSchemaName] = response.AgentSchemaName;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.Status] = response.Status;

                // Serialize the parsed dataset back to JSON string for output parameter
                string datasetJson = response.Dataset != null ? JsonConvert.SerializeObject(response.Dataset) : null;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.Dataset] = datasetJson;

                loggingService.Trace($"{nameof(GetEvalRun)}: Response parameters set successfully");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(GetEvalRun)}: Error setting response parameters");
            }
        }
    }
}