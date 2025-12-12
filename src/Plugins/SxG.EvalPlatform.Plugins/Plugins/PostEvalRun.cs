namespace SxG.EvalPlatform.Plugins
{
    using System;
    using Microsoft.Xrm.Sdk;
    using SxG.EvalPlatform.Plugins.Common.Framework;
    using SxG.EvalPlatform.Plugins.Models;
    using SxG.EvalPlatform.Plugins.Models.Requests;
    using SxG.EvalPlatform.Plugins.Models.Responses;
    using SxG.EvalPlatform.Plugins.CustomApis;
    using SxG.EvalPlatform.Plugins.Services;

    public class PostEvalRun : PluginBase
    {
        public PostEvalRun(string unsecureConfig, string secureConfig) : base(unsecureConfig, secureConfig)
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
                loggingService.Trace($"{nameof(PostEvalRun)}: Starting execution");

                // Extract request parameters from input parameters
                var request = ExtractRequestFromContext(context, loggingService);

                // Validate request
                if (!request.IsValid())
                {
                    string validationError = request.GetValidationError();
                    loggingService.Trace($"{nameof(PostEvalRun)}: Validation failed - {validationError}", TraceSeverity.Warning);

                    var errorResponse = PostEvalRunResponse.CreateError(validationError);
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                loggingService.Trace($"{nameof(PostEvalRun)}: Request validated successfully - EvalRunId: {request.EvalRunId}");

                // Parse the provided GUID
                if (!Guid.TryParse(request.EvalRunId, out Guid evalRunGuid))
                {
                    var errorResponse = PostEvalRunResponse.CreateError("Invalid EvalRunId format");
                    SetResponseParameters(context, errorResponse, loggingService);
                    return;
                }

                // Create late-bound entity to avoid serialization issues with Elastic tables
                var evalRunEntity = new Entity("cr890_evalrun", evalRunGuid);

                // Set required fields
                evalRunEntity["cr890_id"] = evalRunGuid.ToString();  // Required: Primary name field
                evalRunEntity["cr890_status"] = new OptionSetValue(0);  // Required: Status = New
                evalRunEntity["ownerid"] = new EntityReference("systemuser", context.InitiatingUserId);  // Required: Owner

                // Only set optional fields if they have values
                if (!string.IsNullOrWhiteSpace(request.DatasetId))
                    evalRunEntity["cr890_datasetid"] = request.DatasetId;

                if (!string.IsNullOrWhiteSpace(request.AgentId))
                    evalRunEntity["cr890_agentid"] = request.AgentId;

                if (!string.IsNullOrWhiteSpace(request.EnvironmentId))
                    evalRunEntity["cr890_environmentid"] = request.EnvironmentId;

                if (!string.IsNullOrWhiteSpace(request.AgentSchemaName))
                    evalRunEntity["cr890_agentschemaname"] = request.AgentSchemaName;

                Guid createdId = organizationService.Create(evalRunEntity);

                loggingService.Trace($"{nameof(PostEvalRun)}: Successfully created eval run record with ID: {createdId}");
                loggingService.Trace($"{nameof(PostEvalRun)}: Owner: {context.InitiatingUserId}, DatasetId: {request.DatasetId}, AgentId: {request.AgentId}, EnvironmentId: {request.EnvironmentId}, AgentSchemaName: {request.AgentSchemaName}");
                loggingService.Trace($"{nameof(PostEvalRun)}: Status set to: New (0)");

                // Log event
                loggingService.LogEvent("PostEvalRunSuccess", new System.Collections.Generic.Dictionary<string, string>
                {
                    { "EvalRunId", createdId.ToString() },
                    { "AgentId", request.AgentId },
                    { "DatasetId", request.DatasetId }
                });

                // Create success response  
                var response = new PostEvalRunResponse
                {
                    Success = true,
                    Message = "Eval run created successfully",
                    Timestamp = DateTime.UtcNow
                };
                SetResponseParameters(context, response, loggingService);

                loggingService.Trace($"{nameof(PostEvalRun)}: Execution completed successfully");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(PostEvalRun)}: Exception occurred");

                // Create error response
                var errorResponse = PostEvalRunResponse.CreateError($"Internal server error: {ex.Message}");
                SetResponseParameters(context, errorResponse, loggingService);

                throw new InvalidPluginExecutionException($"{nameof(PostEvalRun)} :: Error :: " + ex.Message, ex);
            }
            finally
            {
                // Flush telemetry
                loggingService.Flush();
            }
        }

        private PostEvalRunRequest ExtractRequestFromContext(IPluginExecutionContext context, IPluginLoggingService loggingService)
        {
            var request = new PostEvalRunRequest();

            // Extract from input parameters
            if (context.InputParameters.Contains(CustomApiConfig.PostEvalRun.RequestParameters.EvalRunId))
                request.EvalRunId = context.InputParameters[CustomApiConfig.PostEvalRun.RequestParameters.EvalRunId]?.ToString();

            if (context.InputParameters.Contains(CustomApiConfig.PostEvalRun.RequestParameters.DatasetId))
                request.DatasetId = context.InputParameters[CustomApiConfig.PostEvalRun.RequestParameters.DatasetId]?.ToString();

            if (context.InputParameters.Contains(CustomApiConfig.PostEvalRun.RequestParameters.AgentId))
                request.AgentId = context.InputParameters[CustomApiConfig.PostEvalRun.RequestParameters.AgentId]?.ToString();

            if (context.InputParameters.Contains(CustomApiConfig.PostEvalRun.RequestParameters.EnvironmentId))
                request.EnvironmentId = context.InputParameters[CustomApiConfig.PostEvalRun.RequestParameters.EnvironmentId]?.ToString();

            if (context.InputParameters.Contains(CustomApiConfig.PostEvalRun.RequestParameters.AgentSchemaName))
                request.AgentSchemaName = context.InputParameters[CustomApiConfig.PostEvalRun.RequestParameters.AgentSchemaName]?.ToString();

            loggingService.Trace($"{nameof(PostEvalRun)}: Extracted request parameters from context");
            loggingService.Trace($"{nameof(PostEvalRun)}: EvalRunId: {request.EvalRunId}, DatasetId: {request.DatasetId}, AgentId: {request.AgentId}, EnvironmentId: {request.EnvironmentId}, AgentSchemaName: {request.AgentSchemaName}");

            return request;
        }

        private void SetResponseParameters(IPluginExecutionContext context, PostEvalRunResponse response, IPluginLoggingService loggingService)
        {
            try
            {
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.Success] = response.Success;
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.Message] = response.Message;
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.Timestamp] = response.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                loggingService.Trace($"{nameof(PostEvalRun)}: Response parameters set successfully");
            }
            catch (Exception ex)
            {
                loggingService.LogException(ex, $"{nameof(PostEvalRun)}: Error setting response parameters");
            }
        }
    }
}
