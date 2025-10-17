namespace SxG.EvalPlatform.Plugins.Plugins
{
    using System;
    using Microsoft.Xrm.Sdk;
    using SxG.EvalPlatform.Plugins.Common.Framework;

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

            try
            {
                tracingService.Trace($"{nameof(GetEvalRun)}: Starting execution");

                tracingService.Trace($"{nameof(GetEvalRun)}: Execution completed successfully using Dataverse Managed Identity.");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"{nameof(GetEvalRun)}: Exception occurred - " + ex.Message);
                tracingService.Trace($"{nameof(GetEvalRun)}: Stack trace - " + ex.StackTrace);

                throw new InvalidPluginExecutionException("{nameof(PostEvalJob)} :: Error :: " + ex.Message, ex);
            }
        }
    }
}