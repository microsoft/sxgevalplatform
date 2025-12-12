namespace SxG.EvalPlatform.Plugins.Services
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Helper class for Application Insights telemetry with sandbox environment safety
    /// Uses reflection to avoid hard dependencies on Application Insights assemblies
    /// </summary>
    public class AppInsightsTelemetryHelper : IDisposable
    {
        private object _telemetryClient;
        private Type _telemetryClientType;
        private Type _telemetryConfigurationType;
        private bool _isInitialized;
        private bool _initializationFailed;
        private string _failureReason;
        private bool _disposed = false;

        /// <summary>
        /// Initializes the telemetry helper with connection string
        /// </summary>
        /// <param name="connectionString">Application Insights connection string</param>
        /// <returns>True if initialization succeeded, false otherwise</returns>
        public bool Initialize(string connectionString)
        {
            if (_isInitialized)
                return true;

            if (_initializationFailed)
                return false;

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _initializationFailed = true;
                _failureReason = "Connection string is null or empty";
                return false;
            }

            try
            {
                // Try to load Application Insights assembly using reflection
                Assembly appInsightsAssembly = null;
                try
                {
                    appInsightsAssembly = Assembly.Load("Microsoft.ApplicationInsights, Version=2.22.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
                }
                catch
                {
                    // Try without version
                    appInsightsAssembly = Assembly.Load("Microsoft.ApplicationInsights");
                }

                if (appInsightsAssembly == null)
                {
                    _initializationFailed = true;
                    _failureReason = "Microsoft.ApplicationInsights assembly not found";
                    return false;
                }

                // Get TelemetryConfiguration type
                _telemetryConfigurationType = appInsightsAssembly.GetType("Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration");
                if (_telemetryConfigurationType == null)
                {
                    _initializationFailed = true;
                    _failureReason = "TelemetryConfiguration type not found";
                    return false;
                }

                // Get TelemetryClient type
                _telemetryClientType = appInsightsAssembly.GetType("Microsoft.ApplicationInsights.TelemetryClient");
                if (_telemetryClientType == null)
                {
                    _initializationFailed = true;
                    _failureReason = "TelemetryClient type not found";
                    return false;
                }

                // Create TelemetryConfiguration with connection string
                var createActiveMethod = _telemetryConfigurationType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static);
                if (createActiveMethod == null)
                {
                    _initializationFailed = true;
                    _failureReason = "CreateDefault method not found on TelemetryConfiguration";
                    return false;
                }

                var config = createActiveMethod.Invoke(null, null);
                if (config == null)
                {
                    _initializationFailed = true;
                    _failureReason = "Failed to create TelemetryConfiguration";
                    return false;
                }

                // Set connection string
                var connectionStringProperty = _telemetryConfigurationType.GetProperty("ConnectionString");
                if (connectionStringProperty != null)
                {
                    connectionStringProperty.SetValue(config, connectionString);
                }

                // Create TelemetryClient with configuration
                _telemetryClient = Activator.CreateInstance(_telemetryClientType, new object[] { config });
                if (_telemetryClient == null)
                {
                    _initializationFailed = true;
                    _failureReason = "Failed to create TelemetryClient";
                    return false;
                }

                _isInitialized = true;
                return true;
            }
            catch (System.IO.FileNotFoundException)
            {
                // Assembly not found - likely sandbox environment
                _initializationFailed = true;
                _failureReason = "Application Insights assembly not available (sandbox environment)";
                return false;
            }
            catch (System.IO.FileLoadException)
            {
                // Assembly load failed - likely sandbox restrictions
                _initializationFailed = true;
                _failureReason = "Application Insights assembly could not be loaded (sandbox restrictions)";
                return false;
            }
            catch (Exception ex)
            {
                // General initialization failure
                _initializationFailed = true;
                _failureReason = $"Initialization failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Gets whether telemetry is available
        /// </summary>
        public bool IsAvailable => _isInitialized && _telemetryClient != null;

        /// <summary>
        /// Gets the failure reason if initialization failed
        /// </summary>
        public string FailureReason => _failureReason;

        /// <summary>
        /// Tracks a trace message
        /// </summary>
        public void TrackTrace(string message, TraceSeverity severity, Dictionary<string, string> properties = null)
        {
            if (!IsAvailable)
                return;

            try
            {
                var severityLevel = ConvertToAppInsightsSeverity(severity);
                var trackTraceMethod = _telemetryClientType.GetMethod("TrackTrace", 
                    new Type[] { typeof(string), severityLevel.GetType(), typeof(IDictionary<string, string>) });
                
                if (trackTraceMethod != null)
                {
                    trackTraceMethod.Invoke(_telemetryClient, new object[] { message, severityLevel, properties });
                }
            }
            catch
            {
                // Silently fail - don't break plugin execution
            }
        }

        /// <summary>
        /// Tracks an exception
        /// </summary>
        public void TrackException(Exception exception, Dictionary<string, string> properties = null)
        {
            if (!IsAvailable || exception == null)
                return;

            try
            {
                var trackExceptionMethod = _telemetryClientType.GetMethod("TrackException",
                    new Type[] { typeof(Exception), typeof(IDictionary<string, string>), typeof(IDictionary<string, double>) });

                if (trackExceptionMethod != null)
                {
                    trackExceptionMethod.Invoke(_telemetryClient, new object[] { exception, properties, null });
                }
            }
            catch
            {
                // Silently fail - don't break plugin execution
            }
        }

        /// <summary>
        /// Tracks a custom event
        /// </summary>
        public void TrackEvent(string eventName, Dictionary<string, string> properties = null)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(eventName))
                return;

            try
            {
                var trackEventMethod = _telemetryClientType.GetMethod("TrackEvent",
                    new Type[] { typeof(string), typeof(IDictionary<string, string>), typeof(IDictionary<string, double>) });

                if (trackEventMethod != null)
                {
                    trackEventMethod.Invoke(_telemetryClient, new object[] { eventName, properties, null });
                }
            }
            catch
            {
                // Silently fail - don't break plugin execution
            }
        }

        /// <summary>
        /// Tracks a dependency call
        /// </summary>
        public void TrackDependency(string dependencyType, string target, string dependencyName, 
            string data, DateTimeOffset startTime, TimeSpan duration, string resultCode, bool success)
        {
            if (!IsAvailable)
                return;

            try
            {
                var trackDependencyMethod = _telemetryClientType.GetMethod("TrackDependency",
                    new Type[] { typeof(string), typeof(string), typeof(string), typeof(string), 
                        typeof(DateTimeOffset), typeof(TimeSpan), typeof(string), typeof(bool) });

                if (trackDependencyMethod != null)
                {
                    trackDependencyMethod.Invoke(_telemetryClient, 
                        new object[] { dependencyType, target, dependencyName, data, startTime, duration, resultCode, success });
                }
            }
            catch
            {
                // Silently fail - don't break plugin execution
            }
        }

        /// <summary>
        /// Flushes telemetry
        /// </summary>
        public void Flush()
        {
            if (!IsAvailable)
                return;

            try
            {
                var flushMethod = _telemetryClientType.GetMethod("Flush", Type.EmptyTypes);
                if (flushMethod != null)
                {
                    flushMethod.Invoke(_telemetryClient, null);
                }

                // Wait a bit for flush to complete
                System.Threading.Thread.Sleep(1000);
            }
            catch
            {
                // Silently fail - don't break plugin execution
            }
        }

        /// <summary>
        /// Converts TraceSeverity to Application Insights SeverityLevel
        /// </summary>
        private object ConvertToAppInsightsSeverity(TraceSeverity severity)
        {
            try
            {
                // Get SeverityLevel enum from Application Insights assembly
                var severityLevelType = _telemetryClientType.Assembly.GetType("Microsoft.ApplicationInsights.DataContracts.SeverityLevel");
                if (severityLevelType != null)
                {
                    switch (severity)
                    {
                        case TraceSeverity.Information:
                            return Enum.Parse(severityLevelType, "Information");
                        case TraceSeverity.Warning:
                            return Enum.Parse(severityLevelType, "Warning");
                        case TraceSeverity.Error:
                            return Enum.Parse(severityLevelType, "Error");
                        case TraceSeverity.Critical:
                            return Enum.Parse(severityLevelType, "Critical");
                        default:
                            return Enum.Parse(severityLevelType, "Information");
                    }
                }
            }
            catch
            {
                // Fall back to Information level
            }

            // Return null if conversion fails - method will handle it
            return null;
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
                    try
                    {
                        if (IsAvailable)
                        {
                            Flush();
                        }

                        // Dispose telemetry client if it implements IDisposable
                        if (_telemetryClient is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    catch
                    {
                        // Silently fail on disposal
                    }
                }
                _disposed = true;
            }
        }
    }
}
