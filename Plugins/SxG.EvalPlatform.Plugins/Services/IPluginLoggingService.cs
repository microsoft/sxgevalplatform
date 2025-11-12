namespace SxG.EvalPlatform.Plugins.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Interface for logging service that supports both Dataverse audit logs and Application Insights
    /// </summary>
    public interface IPluginLoggingService
    {
        /// <summary>
        /// Logs a trace message
        /// </summary>
        /// <param name="message">Message to log</param>
        void Trace(string message);

        /// <summary>
        /// Logs a trace message with severity level
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="severity">Severity level (Information, Warning, Error)</param>
        void Trace(string message, TraceSeverity severity);

        /// <summary>
        /// Logs an exception
        /// </summary>
        /// <param name="exception">Exception to log</param>
        /// <param name="message">Optional additional message</param>
        void LogException(Exception exception, string message = null);

        /// <summary>
        /// Logs a custom event with properties
        /// </summary>
        /// <param name="eventName">Name of the event</param>
        /// <param name="properties">Dictionary of properties</param>
        void LogEvent(string eventName, Dictionary<string, string> properties = null);

        /// <summary>
        /// Logs a dependency call (e.g., external API call)
        /// </summary>
        /// <param name="dependencyName">Name of the dependency</param>
        /// <param name="commandName">Command/endpoint called</param>
        /// <param name="startTime">Start time of the call</param>
        /// <param name="duration">Duration of the call</param>
        /// <param name="success">Whether the call was successful</param>
        void LogDependency(string dependencyName, string commandName, DateTimeOffset startTime, TimeSpan duration, bool success);

        /// <summary>
        /// Flushes any pending telemetry (for Application Insights)
        /// </summary>
        void Flush();
    }

    /// <summary>
    /// Trace severity levels
    /// </summary>
    public enum TraceSeverity
    {
        Information = 0,
        Warning = 1,
        Error = 2,
        Critical = 3
    }
}
