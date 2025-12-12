namespace SxG.EvalPlatform.Plugins.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Helper class for Application Insights telemetry with sandbox environment safety.
    /// Uses lazy initialization to gracefully handle assembly load failures in sandbox.
    /// </summary>
    public class AppInsightsTelemetryHelper : IDisposable
    {
        private IAppInsightsWrapper _wrapper;
        private bool _isInitialized;
        private bool _initializationFailed;
        private string _failureReason;
        private bool _disposed;

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
                // Create wrapper in isolated method to catch assembly load failures
                _wrapper = CreateWrapper(connectionString);
                _isInitialized = true;
                return true;
            }
            catch (System.IO.FileNotFoundException ex)
            {
                _initializationFailed = true;
                _failureReason = $"Application Insights assembly not found: {ex.Message}";
                return false;
            }
            catch (System.IO.FileLoadException ex)
            {
                _initializationFailed = true;
                _failureReason = $"Application Insights assembly load failed (sandbox restriction or version mismatch): {ex.Message}";
                return false;
            }
            catch (TypeLoadException ex)
            {
                _initializationFailed = true;
                _failureReason = $"Application Insights type load failed: {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                _initializationFailed = true;
                _failureReason = $"Initialization failed: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Creates the Application Insights wrapper in an isolated method.
        /// This ensures assembly load exceptions are catchable.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static IAppInsightsWrapper CreateWrapper(string connectionString)
        {
            return new AppInsightsWrapper(connectionString);
        }

        /// <summary>
        /// Gets whether telemetry is available
        /// </summary>
        public bool IsAvailable => _isInitialized && _wrapper != null;

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
                _wrapper.TrackTrace(message, severity, properties);
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
                _wrapper.TrackException(exception, properties);
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
                _wrapper.TrackEvent(eventName, properties);
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
                _wrapper.TrackDependency(dependencyType, target, dependencyName, data, startTime, duration, resultCode, success);
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
                _wrapper.Flush();
            }
            catch
            {
                // Silently fail - don't break plugin execution
            }
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
                        _wrapper?.Dispose();
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

    /// <summary>
    /// Interface for Application Insights wrapper to enable isolation
    /// </summary>
    internal interface IAppInsightsWrapper : IDisposable
    {
        void TrackTrace(string message, TraceSeverity severity, Dictionary<string, string> properties);
        void TrackException(Exception exception, Dictionary<string, string> properties);
        void TrackEvent(string eventName, Dictionary<string, string> properties);
        void TrackDependency(string dependencyType, string target, string dependencyName,
            string data, DateTimeOffset startTime, TimeSpan duration, string resultCode, bool success);
        void Flush();
    }

    /// <summary>
    /// Wrapper class that contains all Application Insights types.
    /// Isolated to ensure assembly load failures are catchable.
    /// </summary>
    internal sealed class AppInsightsWrapper : IAppInsightsWrapper
    {
        private readonly Microsoft.ApplicationInsights.TelemetryClient _telemetryClient;
        private readonly Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration _configuration;
        private bool _disposed;

        public AppInsightsWrapper(string connectionString)
        {
            _configuration = Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration.CreateDefault();
            _configuration.ConnectionString = connectionString;
            _telemetryClient = new Microsoft.ApplicationInsights.TelemetryClient(_configuration);
        }

        public void TrackTrace(string message, TraceSeverity severity, Dictionary<string, string> properties)
        {
            var severityLevel = ConvertSeverity(severity);
            _telemetryClient.TrackTrace(message, severityLevel, properties);
        }

        public void TrackException(Exception exception, Dictionary<string, string> properties)
        {
            _telemetryClient.TrackException(exception, properties);
        }

        public void TrackEvent(string eventName, Dictionary<string, string> properties)
        {
            _telemetryClient.TrackEvent(eventName, properties);
        }

        public void TrackDependency(string dependencyType, string target, string dependencyName,
            string data, DateTimeOffset startTime, TimeSpan duration, string resultCode, bool success)
        {
            _telemetryClient.TrackDependency(dependencyType, target, dependencyName, data, startTime, duration, resultCode, success);
        }

        public void Flush()
        {
            _telemetryClient.Flush();
            System.Threading.Thread.Sleep(1000);
        }

        private static Microsoft.ApplicationInsights.DataContracts.SeverityLevel ConvertSeverity(TraceSeverity severity)
        {
            switch (severity)
            {
                case TraceSeverity.Verbose:
                    return Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Verbose;
                case TraceSeverity.Warning:
                    return Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning;
                case TraceSeverity.Error:
                    return Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Error;
                case TraceSeverity.Critical:
                    return Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Critical;
                case TraceSeverity.Information:
                default:
                    return Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Information;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _configuration?.Dispose();
                _disposed = true;
            }
        }
    }
}
