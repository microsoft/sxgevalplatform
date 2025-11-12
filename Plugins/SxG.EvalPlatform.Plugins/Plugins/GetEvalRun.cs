namespace SxG.EvalPlatform.Plugins
{
    using System;
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
                                "cr890_agentschemaname", "cr890_status", "cr890_dataset", "createdon", "modifiedon")
                        ).ToEntity<EvalRun>();

                    // Convert early-bound entity to DTO for response
                    EvalRunDto evalRun = ConvertToDto(evalRunRecord);

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
        /// Converts early-bound entity to DTO
        /// </summary>
        private EvalRunDto ConvertToDto(EvalRun entity)
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
                Dataset = entity.cr890_Dataset,
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