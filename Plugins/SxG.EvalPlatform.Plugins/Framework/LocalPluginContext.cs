namespace SxG.EvalPlatform.Plugins.Common.Framework
{
    using System;
    using Microsoft.Xrm.Sdk;
    using SxG.EvalPlatform.Plugins.Common.Implementation;
    using IEnvironmentVariableService = Interfaces.IEnvironmentVariableService;

    public class LocalPluginContext : ILocalPluginContext
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "LocalPluginContext")]
        public IServiceProvider ServiceProvider { get; private set; }

        public IManagedIdentityService ManagedIdentityService { get; private set; }
        /// <summary>
        /// The Microsoft Dynamics 365 on behalf of token service.
        /// </summary>
        public IOnBehalfOfTokenService OnBehalfOfTokenService { get; private set; }

        /// <summary>
        /// The Microsoft Dynamics 365 organization service.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "LocalPluginContext")]
        public IOrganizationService OrganizationService { get; private set; }

        /// <summary>
        /// The Microsoft Dynamics 365 organization service which has System Admin Rights.
        /// </summary>
        private IOrganizationService systemService = null;
        public IOrganizationService SystemService
        {
            get
            {
                if (systemService == null)
                {
                    systemService = Factory.CreateOrganizationService(null);
                }

                return systemService;
            }
        }

        /// <summary>
        /// Used to Generate OrganizationService
        /// </summary>
        private IOrganizationServiceFactory Factory { get; set; }

        /// <summary>
        /// IPluginExecutionContext contains information that describes the run-time environment in which the plug-in executes, information related to the execution pipeline, and entity business information.
        /// </summary>
        public IPluginExecutionContext PluginExecutionContext { get; private set; }

        /// <summary>
        /// Synchronous registered plug-ins can post the execution context to the Microsoft Azure Service Bus. <br/>
        /// It is through this notification service that synchronous plug-ins can send brokered messages to the Microsoft Azure Service Bus.
        /// </summary>
        public IServiceEndpointNotificationService NotificationService { get; private set; }

        /// <summary>
        /// Provides logging run-time trace information for plug-ins.
        /// </summary>
        public ITracingService TracingService { get; private set; }

        /// <summary>
        /// Provides access to environment variables
        /// </summary>
        private IEnvironmentVariableService _environmentVariableService = null;

        public IEnvironmentVariableService EnvironmentVariableService
        {
            get
            {
                if (_environmentVariableService == null)
                {
                    _environmentVariableService = new EnvironmentVariableService(OrganizationService);
                }

                return _environmentVariableService;
            }
        }

        private LocalPluginContext()
        {
        }

        /// <summary>
        /// Context with different service references that provide different utilities like logging, secret access etc..
        /// </summary>
        /// <param name="serviceProvider"> an object that provides support to fetch other custom objects </param>
        /// <param name="secureConfig"> The Secure Config passed to plugin </param>
        public LocalPluginContext(IServiceProvider serviceProvider, string secureConfig)
        {
            if (serviceProvider == null)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new InvalidPluginExecutionException("serviceProvider");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }

            // Obtain the execution context service from the service provider.
            PluginExecutionContext = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            // Obtain the tracing service from the service provider.
            TracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            // Get the notification service from the service provider.
            NotificationService = (IServiceEndpointNotificationService)serviceProvider.GetService(typeof(IServiceEndpointNotificationService));

            // Obtain the organization factory service from the service provider.
            Factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            // Use the factory to generate the organization service.
            OrganizationService = Factory.CreateOrganizationService(PluginExecutionContext.UserId);

            // Obtain the organization On Behalf Of Token Service from the service provider.
            OnBehalfOfTokenService = (IOnBehalfOfTokenService)serviceProvider.GetService(typeof(IOnBehalfOfTokenService));

            ManagedIdentityService = (IManagedIdentityService)serviceProvider.GetService(typeof(IManagedIdentityService));
        }

        /// <summary>
        /// Writes a trace message to the CRM trace log.
        /// </summary>
        /// <param name="message">Message name to trace.</param>
        public void Trace(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || TracingService == null)
            {
                return;
            }

            if (PluginExecutionContext == null)
            {
                TracingService.Trace(message);
            }
            else
            {
                TracingService.Trace(
                    "{0}, Correlation Id: {1}, Initiating User: {2}",
                    message,
                    PluginExecutionContext.CorrelationId,
                    PluginExecutionContext.InitiatingUserId);
            }
        }
    }
}
