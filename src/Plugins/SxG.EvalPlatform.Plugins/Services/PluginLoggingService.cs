namespace SxG.EvalPlatform.Plugins.Services
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Xrm.Sdk;

    /// <summary>
    /// Logging service that supports both Dataverse audit logs and Application Insights
    /// </summary>
    public class PluginLoggingService : IPluginLoggingService, IDisposable
    {
        private readonly ITracingService _tracingService;
        private readonly IPluginExecutionContext _executionContext;
        private readonly IPluginConfigurationService _configService;
        private readonly AppInsightsTelemetryHelper _appInsightsHelper;
        private readonly int _currentDepth;
        private bool _disposed = false;

        public PluginLoggingService(
                ITracingService tracingService,
                IPluginExecutionContext executionContext,
                IPluginConfigurationService configService)
        {
            _tracingService = tracingService ?? throw new ArgumentNullException(nameof(tracingService));
            _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));

            _currentDepth = _executionContext?.Depth ?? 0;

            // Initialize Application Insights if enabled and within depth limits
            _appInsightsHelper = new AppInsightsTelemetryHelper();
            if (_configService.IsAppInsightsLoggingEnabled() && ShouldLogAtCurrentDepth())
            {
                string connectionString = _configService.GetAppInsightsConnectionString();
                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    bool initialized = _appInsightsHelper.Initialize(connectionString);
                    if (!initialized)
                    {
                        // Log warning to Dataverse trace but don't fail
                        if (_tracingService != null)
                        {
                            _tracingService.Trace($"[Warning] Application Insights initialization failed at depth {_currentDepth}: {_appInsightsHelper.FailureReason}");
                        }
                    }
                    else
                    {
                        if (_tracingService != null)
                        {
                            _tracingService.Trace($"[Info] Application Insights telemetry initialized successfully at depth {_currentDepth}");
                        }
                    }
                }
            }
            else if (!ShouldLogAtCurrentDepth())
            {
                if (_tracingService != null)
                {
                    _tracingService.Trace($"[Info] Application Insights telemetry skipped for depth {_currentDepth} (nested call logging disabled or exceeds max depth)");
                }
            }
        }

        /// <summary>
        /// Determines if telemetry should be logged at the current execution depth
        /// </summary>
        private bool ShouldLogAtCurrentDepth()
        {
            // Always log depth 1 (initial call)
            if (_currentDepth <= 1)
                return true;

            // Check configuration for nested calls
            if (!_configService.ShouldLogNestedCalls())
                return false;

            // Check max depth limit
            int maxDepth = _configService.GetMaxTelemetryDepth();
            if (maxDepth > 0 && _currentDepth > maxDepth)
                return false;

            return true;
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

            // Log to Application Insights if available
            if (_appInsightsHelper.IsAvailable)
            {
                var properties = GetContextProperties();
                _appInsightsHelper.TrackTrace(message, severity, properties);
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

            // Log to Application Insights if available
            if (_appInsightsHelper.IsAvailable)
            {
                var properties = GetContextProperties();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    properties["CustomMessage"] = message;
                }
                _appInsightsHelper.TrackException(exception, properties);
            }
        }

        /// <summary>
        /// Logs a custom event with properties
        /// </summary>
        public void LogEvent(string eventName, Dictionary<string, string> properties = null)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                return;

            // Merge context properties with custom properties
            var allProperties = GetContextProperties();
            if (properties != null)
            {
                foreach (var kvp in properties)
                {
                    allProperties[kvp.Key] = kvp.Value;
                }
            }

            // Log to Dataverse audit logs if enabled
            if (_configService.IsAuditLoggingEnabled() && _tracingService != null)
            {
                string propertiesString = properties != null
                    ? string.Join(", ", properties)
                    : "No properties";
                _tracingService.Trace(FormatTraceMessage($"Event: {eventName}, Properties: {propertiesString}", TraceSeverity.Information));
            }

            // Log to Application Insights if available
            if (_appInsightsHelper.IsAvailable)
            {
                _appInsightsHelper.TrackEvent(eventName, allProperties);
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

            // Log to Application Insights if available
            if (_appInsightsHelper.IsAvailable)
            {
                string resultCode = success ? "200" : "500";
                _appInsightsHelper.TrackDependency(
                    "HTTP",
                    dependencyName,
                    commandName,
                    null,
                    startTime,
                    duration,
                    resultCode,
                    success);
            }
        }

        /// <summary>
        /// Flushes any pending telemetry
        /// </summary>
        public void Flush()
        {
            if (_appInsightsHelper != null && _appInsightsHelper.IsAvailable)
            {
                _appInsightsHelper.Flush();
            }
        }

        /// <summary>
        /// Gets context properties for telemetry
        /// </summary>
        private Dictionary<string, string> GetContextProperties()
        {
            var properties = new Dictionary<string, string>();
            
            if (_executionContext != null)
            {
                properties["CorrelationId"] = _executionContext.CorrelationId.ToString();
                properties["InitiatingUserId"] = _executionContext.InitiatingUserId.ToString();
                properties["OrganizationName"] = _executionContext.OrganizationName ?? "Unknown";
                properties["MessageName"] = _executionContext.MessageName ?? "Unknown";
                properties["Stage"] = _executionContext.Stage.ToString();
                properties["Mode"] = _executionContext.Mode.ToString();
                properties["Depth"] = _executionContext.Depth.ToString();
                
                // Use OperationId for linking the entire operation chain
                properties["OperationId"] = _executionContext.OperationId.ToString();
                
                if (_executionContext.PrimaryEntityName != null)
                {
                    properties["PrimaryEntityName"] = _executionContext.PrimaryEntityName;
                }
                
                if (_executionContext.PrimaryEntityId != Guid.Empty)
                {
                    properties["PrimaryEntityId"] = _executionContext.PrimaryEntityId.ToString();
                }
            }
            
            return properties;
        }

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
                    if (_appInsightsHelper != null)
                    {
                        _appInsightsHelper.Dispose();
                    }
                }
                _disposed = true;
            }
        }
    }
}
