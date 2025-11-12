namespace SxG.EvalPlatform.Plugins.Services
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Xrm.Sdk;

    /// <summary>
    /// Logging service that supports Dataverse audit logs
    /// Note: Application Insights support removed due to Dataverse sandbox limitations
    /// </summary>
    public class PluginLoggingService : IPluginLoggingService, IDisposable
    {
        private readonly ITracingService _tracingService;
        private readonly IPluginExecutionContext _executionContext;
        private readonly IPluginConfigurationService _configService;
        private bool _disposed = false;

        public PluginLoggingService(
                ITracingService tracingService,
                IPluginExecutionContext executionContext,
                IPluginConfigurationService configService)
        {
            _tracingService = tracingService ?? throw new ArgumentNullException(nameof(tracingService));
            _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        /// <summary>
        /// Logs a trace message
        /// </summary>
        public void Trace(string message)
        {
            Trace(message, TraceSeverity.Information);
        }

        /// <summary>
        /// Logs a trace message with severity level
        /// </summary>
        public void Trace(string message, TraceSeverity severity)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            // Log to Dataverse audit logs if enabled
            if (_configService.IsAuditLoggingEnabled() && _tracingService != null)
            {
                string formattedMessage = FormatTraceMessage(message, severity);
                _tracingService.Trace(formattedMessage);
            }
        }

        /// <summary>
        /// Logs an exception
        /// </summary>
        public void LogException(Exception exception, string message = null)
        {
            if (exception == null)
                return;

            string exceptionMessage = string.IsNullOrWhiteSpace(message)
                ? $"Exception: {exception.Message}"
                : $"{message} - Exception: {exception.Message}";

            // Log to Dataverse audit logs if enabled
            if (_configService.IsAuditLoggingEnabled() && _tracingService != null)
            {
                _tracingService.Trace(FormatTraceMessage(exceptionMessage, TraceSeverity.Error));
                _tracingService.Trace($"Stack Trace: {exception.StackTrace}");
            }
        }

        /// <summary>
        /// Logs a custom event with properties
        /// </summary>
        public void LogEvent(string eventName, Dictionary<string, string> properties = null)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                return;

            // Log to Dataverse audit logs if enabled
            if (_configService.IsAuditLoggingEnabled() && _tracingService != null)
            {
                string propertiesString = properties != null
                    ? string.Join(", ", properties)
                    : "No properties";
                _tracingService.Trace(FormatTraceMessage($"Event: {eventName}, Properties: {propertiesString}", TraceSeverity.Information));
            }
        }

        /// <summary>
        /// Logs a dependency call (e.g., external API call)
        /// </summary>
        public void LogDependency(string dependencyName, string commandName, DateTimeOffset startTime, TimeSpan duration, bool success)
        {
            if (string.IsNullOrWhiteSpace(dependencyName))
                return;

            // Log to Dataverse audit logs if enabled
            if (_configService.IsAuditLoggingEnabled() && _tracingService != null)
            {
                string message = $"Dependency: {dependencyName}, Command: {commandName}, Duration: {duration.TotalMilliseconds}ms, Success: {success}";
                _tracingService.Trace(FormatTraceMessage(message, TraceSeverity.Information));
            }
        }

        /// <summary>
        /// Flushes any pending telemetry (no-op for Dataverse logging)
        /// </summary>
        public void Flush()
        {
            // No-op for Dataverse audit logs
        }

        /// <summary>
        /// Formats trace message with context information and severity
        /// </summary>
        private string FormatTraceMessage(string message, TraceSeverity severity)
        {
            string severityPrefix = severity == TraceSeverity.Information ? "" : $"[{severity}] ";
            return $"{severityPrefix}{message}, Correlation Id: {_executionContext.CorrelationId}, Initiating User: {_executionContext.InitiatingUserId}";
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Nothing to dispose for Dataverse logging
                }
                _disposed = true;
            }
        }
    }
}
