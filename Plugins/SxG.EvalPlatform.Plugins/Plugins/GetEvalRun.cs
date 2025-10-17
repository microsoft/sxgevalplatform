namespace SxG.EvalPlatform.Plugins.Plugins
{
    using System;
    using Microsoft.Xrm.Sdk;
    using Microsoft.Xrm.Sdk.Query;
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
                tracingService.Trace($"{nameof(GetEvalRun)}: Starting execution");

                // Extract request parameters from input parameters
                var request = ExtractRequestFromContext(context, tracingService);
                
                // Validate request
                if (!request.IsValid())
                {
                    string validationError = request.GetValidationError();
                    tracingService.Trace($"{nameof(GetEvalRun)}: Validation failed - {validationError}");
                    
                    var errorResponse = GetEvalRunResponse.CreateError(validationError, 400);
                    SetResponseParameters(context, errorResponse, tracingService);
                    return;
                }

                tracingService.Trace($"{nameof(GetEvalRun)}: Searching for eval job with Id: {request.Id}");

                // Query to get eval job record by Id (Primary Name Column)
                QueryExpression query = new QueryExpression(EvalJobEntity.EntityLogicalName)
                {
                    ColumnSet = new ColumnSet(
                        EvalJobEntity.Fields.EvalJobId,
                        EvalJobEntity.Fields.Id,
                        EvalJobEntity.Fields.AgentId,
                        EvalJobEntity.Fields.EnvironmentId,
                        EvalJobEntity.Fields.SchemaName,
                        EvalJobEntity.Fields.Status,
                        EvalJobEntity.Fields.Input,
                        EvalJobEntity.Fields.Output,
                        EvalJobEntity.Fields.CreatedOn,
                        EvalJobEntity.Fields.ModifiedOn
                    ),
                    Criteria = new FilterExpression()
                };

                // Filter by Id (Primary Name Column)
                query.Criteria.AddCondition(EvalJobEntity.Fields.Id, ConditionOperator.Equal, request.Id);

                // Execute the query
                EntityCollection evalJobEntities = organizationService.RetrieveMultiple(query);

                EvalJobEntity evalJob = null;
                if (evalJobEntities.Entities.Count > 0)
                {
                    evalJob = EvalJobEntity.FromEntity(evalJobEntities.Entities[0]);
                    tracingService.Trace($"{nameof(GetEvalRun)}: Found eval job - EvalJobId: {evalJob.EvalJobId}, AgentId: {evalJob.AgentId}, Status: {evalJob.Status}, SchemaName: {evalJob.SchemaName}");
                }
                else
                {
                    tracingService.Trace($"{nameof(GetEvalRun)}: No eval job found for Id: {request.Id}");
                }

                // Create response
                var response = GetEvalRunResponse.CreateSuccess(evalJob);
                SetResponseParameters(context, response, tracingService);

                tracingService.Trace($"{nameof(GetEvalRun)}: Execution completed successfully using Dataverse Managed Identity.");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(GetEvalRun)}: Exception occurred - " + ex.Message);
                tracingService.Trace($"{nameof(GetEvalRun)}: Stack trace - " + ex.StackTrace);

                // Create error response
                var errorResponse = GetEvalRunResponse.CreateError($"Internal server error: {ex.Message}", 500);
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
            if (context.InputParameters.Contains(CustomApiConfig.GetEvalRun.RequestParameters.Id))
                request.Id = context.InputParameters[CustomApiConfig.GetEvalRun.RequestParameters.Id]?.ToString();

            tracingService.Trace($"{nameof(GetEvalRun)}: Extracted Id from context: {request.Id}");

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
                // Basic response properties
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.Success] = response.Success;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.Message] = response.Message;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.StatusCode] = response.StatusCode;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.Timestamp] = response.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                // Serialize eval job to string (since Custom API output parameters have limitations on complex types)
                if (response.EvalJob != null)
                {
                    string serializedEvalJob = SerializeEvalJob(response.EvalJob);
                    context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.EvalJob] = serializedEvalJob;
                }
                else
                {
                    context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.EvalJob] = "null";
                }

                // Additional individual response properties
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.EvalJobId] = response.EvalJobId;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.AgentId] = response.AgentId;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.EnvironmentId] = response.EnvironmentId;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.SchemaName] = response.SchemaName;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.Status] = response.Status;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.Input] = response.Input;
                context.OutputParameters[CustomApiConfig.GetEvalRun.ResponseProperties.Output] = response.Output;

                tracingService.Trace($"{nameof(GetEvalRun)}: Response parameters set successfully");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(GetEvalRun)}: Error setting response parameters - {ex.Message}");
                // Continue execution - response setting failure shouldn't break the plugin
            }
        }

        /// <summary>
        /// Serializes eval job to JSON string format for Custom API response
        /// </summary>
        /// <param name="evalJob">The eval job entity</param>
        /// <returns>JSON string representation</returns>
        private string SerializeEvalJob(EvalJobEntity evalJob)
        {
            try
            {
                // Simple JSON serialization (avoiding dependencies)
                return "{" +
                    $"\"EvalJobId\":\"{evalJob.EvalJobId}\"," +
                    $"\"Id\":\"{evalJob.Id}\"," +
                    $"\"AgentId\":\"{evalJob.AgentId}\"," +
                    $"\"EnvironmentId\":\"{evalJob.EnvironmentId}\"," +
                    $"\"SchemaName\":\"{evalJob.SchemaName}\"," +
                    $"\"Status\":{evalJob.Status}," +
                    $"\"Input\":\"{EscapeJsonString(evalJob.Input)}\"," +
                    $"\"Output\":\"{EscapeJsonString(evalJob.Output)}\"," +
                    $"\"CreatedOn\":\"{evalJob.CreatedOn?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")}\"," +
                    $"\"ModifiedOn\":\"{evalJob.ModifiedOn?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")}\"" +
                    "}";
            }
            catch
            {
                return "null";
            }
        }

        /// <summary>
        /// Escapes special characters for JSON string
        /// </summary>
        /// <param name="input">Input string</param>
        /// <returns>Escaped string</returns>
        private string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
    }
}