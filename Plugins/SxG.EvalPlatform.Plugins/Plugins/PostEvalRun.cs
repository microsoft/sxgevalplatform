namespace SxG.EvalPlatform.Plugins.Plugins
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
                    
                    var errorResponse = PostEvalRunResponse.CreateError(validationError, 400);
                    SetResponseParameters(context, errorResponse, tracingService);
                    return;
                }

                tracingService.Trace($"{nameof(PostEvalRun)}: Request validated successfully - AgentId: {request.AgentId}, EnvironmentId: {request.EnvironmentId}, SchemaName: {request.SchemaName}, Input length: {request.Input?.Length ?? 0}");

                // Generate a new GUID that will be used for both EvalJobId and Id
                Guid newGuid = Guid.NewGuid();

                // Create eval job entity
                var evalJobEntity = new EvalJobEntity
                {
                    EvalJobId = newGuid,
                    Id = newGuid.ToString(), // Same value as EvalJobId stored as string
                    AgentId = request.AgentId,
                    EnvironmentId = request.EnvironmentId,
                    SchemaName = request.SchemaName,
                    Status = EvalJobEntity.StatusValues.New, // Default status
                    Input = request.Input
                    // Output is optional and not set during creation
                };

                // Convert to Dataverse entity and create record
                Entity entity = evalJobEntity.ToEntity();
                Guid createdId = organizationService.Create(entity);
                
                tracingService.Trace($"{nameof(PostEvalRun)}: Successfully created eval job record with ID: {createdId}");

                // Create success response with eval job entity
                var response = PostEvalRunResponse.CreateSuccess(evalJobEntity);
                SetResponseParameters(context, response, tracingService);

                tracingService.Trace($"{nameof(PostEvalRun)}: Execution completed successfully using Dataverse Managed Identity.");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(PostEvalRun)}: Exception occurred - " + ex.Message);
                tracingService.Trace($"{nameof(PostEvalRun)}: Stack trace - " + ex.StackTrace);

                // Create error response
                var errorResponse = PostEvalRunResponse.CreateError($"Internal server error: {ex.Message}", 500);
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
            if (context.InputParameters.Contains(CustomApiConfig.PostEvalRun.RequestParameters.AgentId))
                request.AgentId = context.InputParameters[CustomApiConfig.PostEvalRun.RequestParameters.AgentId]?.ToString();

            if (context.InputParameters.Contains(CustomApiConfig.PostEvalRun.RequestParameters.EnvironmentId))
                request.EnvironmentId = context.InputParameters[CustomApiConfig.PostEvalRun.RequestParameters.EnvironmentId]?.ToString();

            if (context.InputParameters.Contains(CustomApiConfig.PostEvalRun.RequestParameters.SchemaName))
                request.SchemaName = context.InputParameters[CustomApiConfig.PostEvalRun.RequestParameters.SchemaName]?.ToString();

            if (context.InputParameters.Contains(CustomApiConfig.PostEvalRun.RequestParameters.Input))
                request.Input = context.InputParameters[CustomApiConfig.PostEvalRun.RequestParameters.Input]?.ToString();

            tracingService.Trace($"{nameof(PostEvalRun)}: Extracted request parameters from context");

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
                // Basic response properties
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.Success] = response.Success;
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.Id] = response.Id;
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.Message] = response.Message;
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.StatusCode] = response.StatusCode;
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.Timestamp] = response.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                // Additional response properties
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.EvalJobId] = response.EvalJobId;
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.AgentId] = response.AgentId;
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.EnvironmentId] = response.EnvironmentId;
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.SchemaName] = response.SchemaName;
                context.OutputParameters[CustomApiConfig.PostEvalRun.ResponseProperties.Status] = response.Status;

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
