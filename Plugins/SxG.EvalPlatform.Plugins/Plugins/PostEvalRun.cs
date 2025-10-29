namespace SxG.EvalPlatform.Plugins
{
    using System;
    using Microsoft.Xrm.Sdk;
    using SxG.EvalPlatform.Plugins.Common.Framework;
    using SxG.EvalPlatform.Plugins.Models;
    using SxG.EvalPlatform.Plugins.Models.Requests;
    using SxG.EvalPlatform.Plugins.Models.Responses;
    using SxG.EvalPlatform.Plugins.CustomApis;

    public class PostEvalRun : PluginBase
    {
        public PostEvalRun(string unsecureConfig, string secureConfig) : base(unsecureConfig, secureConfig)
        {
            // TODO: Implement your custom configuration handling
            // https://docs.microsoft.com/powerapps/developer/common-data-service/register-plug-in#set-configuration-data
        }

        protected override void ExecuteCrmPlugin(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException(nameof(localContext));
            }

            var context = localContext.PluginExecutionContext;
            var tracingService = localContext.TracingService;
            var environmentVariableService = localContext.EnvironmentVariableService;
            var managedIdentityService = localContext.ManagedIdentityService;
            var organizationService = localContext.OrganizationService;

            try
            {
                tracingService.Trace($"{nameof(PostEvalRun)}: Starting execution");

                // Extract request parameters from input parameters
                var request = ExtractRequestFromContext(context, tracingService);

                // Validate request
                if (!request.IsValid())
                {
                    string validationError = request.GetValidationError();
                    tracingService.Trace($"{nameof(PostEvalRun)}: Validation failed - {validationError}");

                    var errorResponse = PostEvalRunResponse.CreateError(validationError);
                    SetResponseParameters(context, errorResponse, tracingService);
                    return;
                }

                tracingService.Trace($"{nameof(PostEvalRun)}: Request validated successfully - EvalRunId: {request.EvalRunId}");

                // Parse the provided GUID
                if (!Guid.TryParse(request.EvalRunId, out Guid evalRunGuid))
                {
                    var errorResponse = PostEvalRunResponse.CreateError("Invalid EvalRunId format");
                    SetResponseParameters(context, errorResponse, tracingService);
                    return;
                }

                // Create eval run entity with provided data
                var evalRunEntity = new EvalRunEntity
                {
                    EvalRunId = evalRunGuid,
                    Id = evalRunGuid.ToString(), // Same value as EvalRunId stored as string
                    DatasetId = request.DatasetId, // Store DatasetId from request
                    AgentId = request.AgentId, // Set from request
                    EnvironmentId = request.EnvironmentId, // Set from request
                    AgentSchemaName = request.AgentSchemaName, // Set from request
                    Status = EvalRunEntity.StatusValues.New // Default status as integer
                };

                // Convert to Dataverse entity and create record
                Entity entity = evalRunEntity.ToEntity();
                Guid createdId = organizationService.Create(entity);

                tracingService.Trace($"{nameof(PostEvalRun)}: Successfully created eval run record with ID: {createdId}");
                tracingService.Trace($"{nameof(PostEvalRun)}: DatasetId: {request.DatasetId}, AgentId: {request.AgentId}, EnvironmentId: {request.EnvironmentId}, AgentSchemaName: {request.AgentSchemaName}");
                tracingService.Trace($"{nameof(PostEvalRun)}: Status set to: {evalRunEntity.GetStatusName()} (value: {evalRunEntity.Status})");

                // Create success response
                var response = PostEvalRunResponse.CreateSuccess(evalRunEntity);
                SetResponseParameters(context, response, tracingService);

                tracingService.Trace($"{nameof(PostEvalRun)}: Execution completed successfully using Dataverse Managed Identity.");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(PostEvalRun)}: Exception occurred - " + ex.Message);
                tracingService.Trace($"{nameof(PostEvalRun)}: Stack trace - " + ex.StackTrace);

                // Create error response
                var errorResponse = PostEvalRunResponse.CreateError($"Internal server error: {ex.Message}");
                SetResponseParameters(context, errorResponse, tracingService);

                throw new InvalidPluginExecutionException($"{nameof(PostEvalRun)} :: Error :: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Extracts request parameters from plugin execution context
        /// </summary>
        /// <param name="context">Plugin execution context</param>
        /// <param name="tracingService">Tracing service</param>
        /// <returns>PostEvalRunRequest object</returns>
        private PostEvalRunRequest ExtractRequestFromContext(IPluginExecutionContext context, ITracingService tracingService)
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

            tracingService.Trace($"{nameof(PostEvalRun)}: Extracted request parameters from context");
            tracingService.Trace($"{nameof(PostEvalRun)}: EvalRunId: {request.EvalRunId}, DatasetId: {request.DatasetId}, AgentId: {request.AgentId}, EnvironmentId: {request.EnvironmentId}, AgentSchemaName: {request.AgentSchemaName}");

            return request;
        }

        /// <summary>
        /// Sets response parameters in the plugin execution context
        /// </summary>
        /// <param name="context">Plugin execution context</param>
        /// <param name="response">Response object</param>
        /// <param name="tracingService">Tracing service</param>
        private void SetResponseParameters(IPluginExecutionContext context, PostEvalRunResponse response, ITracingService tracingService)
        {
            try
            {
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.Success] = response.Success;
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.Message] = response.Message;
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.Timestamp] = response.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                tracingService.Trace($"{nameof(PostEvalRun)}: Response parameters set successfully");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(PostEvalRun)}: Error setting response parameters - {ex.Message}");
                // Continue execution - response setting failure shouldn't break the plugin
            }
        }
    }
}
