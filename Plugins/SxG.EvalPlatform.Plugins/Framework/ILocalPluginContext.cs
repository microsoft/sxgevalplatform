using SxG.EvalPlatform.Plugins.Common.Interfaces;

namespace SxG.EvalPlatform.Plugins.Common.Framework
{
    using System;
    using Microsoft.Xrm.Sdk;
    using IEnvironmentVariableService = IEnvironmentVariableService;

    public interface ILocalPluginContext
    {
        /// <summary>
        /// Synchronous registered plug-ins can post the execution context to the Microsoft Azure Service Bus. <br/>
        /// It is through this notification service that synchronous plug-ins can send brokered messages to the Microsoft Azure Service Bus.
        /// </summary>
        IServiceEndpointNotificationService NotificationService { get; }

        /// <summary>
        /// The Microsoft Dynamics 365 on BehalfOf service.
        /// </summary>
        IOnBehalfOfTokenService OnBehalfOfTokenService { get; }

        /// <summary>
        /// The Microsoft Dynamics 365 organization service.
        /// </summary>
        IOrganizationService OrganizationService { get; }

        /// <summary>
        /// IPluginExecutionContext contains information that describes the run-time environment in which the plug-in executes, information related to the execution pipeline, and entity business information.
        /// </summary>
        IPluginExecutionContext PluginExecutionContext { get; }

        /// <summary>
        /// Defines a mechanism for retrieving a service object; that is, an object that
        /// provides custom support to other objects.
        /// </summary>
        IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// The Microsoft Dynamics 365 organization service which has System Admin Rights.
        /// </summary>
        IOrganizationService SystemService { get; }

        /// <summary>
        /// Provides logging run-time trace information for plug-ins.
        /// </summary>
        ITracingService TracingService { get; }

        /// <summary>
        /// The environment variable service
        /// </summary>
        IEnvironmentVariableService EnvironmentVariableService { get; }

        /// <summary>
        /// Writes a trace message to the CRM trace log.
        /// </summary>
        /// <param name="message">Message name to trace.</param>
        void Trace(string message);
    }
}
