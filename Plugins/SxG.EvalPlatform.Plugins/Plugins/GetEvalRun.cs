namespace SxG.EvalPlatform.Plugins
{
    using System;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
    using Newtonsoft.Json;
    using SxG.EvalPlatform.Plugins.Common.Framework;
    using SxG.EvalPlatform.Plugins.Models;
    using SxG.EvalPlatform.Plugins.Models.Requests;
    using SxG.EvalPlatform.Plugins.Models.Responses;
    using SxG.EvalPlatform.Plugins.CustomApis;

    public class GetEvalRun : PluginBase
    {
        public GetEvalRun(string unsecureConfig, string secureConfig) : base(unsecureConfig, secureConfig)
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
            var environmentVariableService = localContext.EnvironmentVariableService;
            var managedIdentityService = localContext.ManagedIdentityService;
            var organizationService = localContext.OrganizationService;

            try
            {
                tracingService.Trace($"{nameof(GetEvalRun)}: Starting execution");

                // Extract request parameters from input parameters
                var request = ExtractRequestFromContext(context, tracingService);
                
                // Validate request
                if (!request.IsValid())
                {
                    string validationError = request.GetValidationError();
                    tracingService.Trace($"{nameof(GetEvalRun)}: Validation failed - {validationError}");
                    
                    var errorResponse = GetEvalRunResponse.CreateError(validationError);
                    SetResponseParameters(context, errorResponse, tracingService);
                    return;
                }

                tracingService.Trace($"{nameof(GetEvalRun)}: Searching for eval run with EvalRunId: {request.EvalRunId}");

                // Parse the provided GUID for direct query
                if (!Guid.TryParse(request.EvalRunId, out Guid evalRunGuid))
                {
                    var errorResponse = GetEvalRunResponse.CreateError("Invalid EvalRunId format");
                    SetResponseParameters(context, errorResponse, tracingService);
                    return;
                }

                // Query directly using EvalRunId (Primary Key) for faster performance
                try
                {
                    Entity evalRunEntityRecord = organizationService.Retrieve(
                        EvalRunEntity.EntityLogicalName,
                        evalRunGuid,
                        new ColumnSet(
                            EvalRunEntity.Fields.EvalRunId,
                            EvalRunEntity.Fields.Id,
                            EvalRunEntity.Fields.AgentId,
                            EvalRunEntity.Fields.EnvironmentId,
                            EvalRunEntity.Fields.AgentSchemaName,
                            EvalRunEntity.Fields.Status,
                            EvalRunEntity.Fields.Dataset,
                            EvalRunEntity.Fields.CreatedOn,
                            EvalRunEntity.Fields.ModifiedOn
                        )
                    );

                    EvalRunEntity evalRun = EvalRunEntity.FromEntity(evalRunEntityRecord);
                    tracingService.Trace($"{nameof(GetEvalRun)}: Found eval run - EvalRunId: {evalRun.EvalRunId}, AgentId: {evalRun.AgentId}, Status: {evalRun.GetStatusName()}");
                    tracingService.Trace($"{nameof(GetEvalRun)}: Dataset JSON string length: {(evalRun.Dataset?.Length ?? 0)}");

                    // Create success response (this will parse the dataset JSON automatically)
                    var response = GetEvalRunResponse.CreateSuccess(evalRun);
                    tracingService.Trace($"{nameof(GetEvalRun)}: Dataset parsed into {response.Dataset?.Count ?? 0} items");
                    
                    SetResponseParameters(context, response, tracingService);
                }
                catch (Exception retrieveEx)
                {
                    tracingService.Trace($"{nameof(GetEvalRun)}: Record not found or error retrieving: {retrieveEx.Message}");
                    
                    // Create not found response
                    var response = GetEvalRunResponse.CreateSuccess(null);
                    SetResponseParameters(context, response, tracingService);
                }

                tracingService.Trace($"{nameof(GetEvalRun)}: Execution completed successfully using Dataverse Managed Identity.");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(GetEvalRun)}: Exception occurred - " + ex.Message);
                tracingService.Trace($"{nameof(GetEvalRun)}: Stack trace - " + ex.StackTrace);

                // Create error response
                var errorResponse = GetEvalRunResponse.CreateError($"Internal server error: {ex.Message}");
                SetResponseParameters(context, errorResponse, tracingService);

                throw new InvalidPluginExecutionException($"{nameof(GetEvalRun)} :: Error :: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Extracts request parameters from plugin execution context
        /// </summary>
        /// <param name="context">Plugin execution context</param>
        /// <param name="tracingService">Tracing service</param>
        /// <returns>GetEvalRunRequest object</returns>
        private GetEvalRunRequest ExtractRequestFromContext(IPluginExecutionContext context, ITracingService tracingService)
        {
            var request = new GetEvalRunRequest();

            // Extract from input parameters
            if (context.InputParameters.Contains(CustomApiConfig.GetEvalRun.RequestParameters.EvalRunId))
                request.EvalRunId = context.InputParameters[CustomApiConfig.GetEvalRun.RequestParameters.EvalRunId]?.ToString();

            tracingService.Trace($"{nameof(GetEvalRun)}: Extracted EvalRunId from context: {request.EvalRunId}");

            return request;
        }

        /// <summary>
        /// Sets response parameters in the plugin execution context
        /// </summary>
        /// <param name="context">Plugin execution context</param>
        /// <param name="response">Response object</param>
        /// <param name="tracingService">Tracing service</param>
        private void SetResponseParameters(IPluginExecutionContext context, GetEvalRunResponse response, ITracingService tracingService)
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

                tracingService.Trace($"{nameof(GetEvalRun)}: Response parameters set successfully");
                tracingService.Trace($"{nameof(GetEvalRun)}: Dataset serialized back to JSON for output parameter");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(GetEvalRun)}: Error setting response parameters - {ex.Message}");
                // Continue execution - response setting failure shouldn't break the plugin
            }
        }
    }
}