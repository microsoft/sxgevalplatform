using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using SxgEvalPlatformApi.Models.Security;
using System.Diagnostics;
using System.Text.Json;

namespace SxgEvalPlatformApi.Services
{
    /// <summary>
    /// Implementation of security event logger for MISE compliance
    /// Logs to both ILogger (for local/console) and Application Insights (for SIEM)
    /// Uses OpenTelemetry Activity context for distributed tracing correlation
    /// </summary>
    public class SecurityEventLogger : ISecurityEventLogger
    {
        private readonly ILogger<SecurityEventLogger> _logger;
        private readonly TelemetryClient? _telemetryClient;
        private readonly IConfiguration _configuration;
        private readonly bool _enableApplicationInsights;

        public SecurityEventLogger(
            ILogger<SecurityEventLogger> logger,
            IConfiguration configuration,
            TelemetryClient? telemetryClient = null)
        {
            _logger = logger;
            _configuration = configuration;
            _telemetryClient = telemetryClient;
            _enableApplicationInsights = configuration.GetValue<bool>("OpenTelemetry:EnableApplicationInsights", true);
        }

        public Task LogAuthenticationSuccessAsync(string userId, string? userEmail, string authenticationType, string ipAddress)
        {
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.AuthenticationSuccess,
                Severity = SecurityEventSeverity.Informational,
                UserId = userId,
                UserEmail = userEmail,
                IpAddress = ipAddress,
                Message = $"User authenticated successfully via {authenticationType}",
                Details = new Dictionary<string, object>
                {
                    ["AuthenticationType"] = authenticationType,
                    ["Success"] = true
                },
                Result = "Success"
            };

            return LogSecurityEventAsync(securityEvent);
        }

        public Task LogAuthenticationFailureAsync(string reason, string ipAddress, string? userAgent, Dictionary<string, object>? details = null)
        {
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.AuthenticationFailure,
                Severity = SecurityEventSeverity.High,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Message = $"Authentication failed: {reason}",
                Details = details ?? new Dictionary<string, object>(),
                Result = "Failure",
                RequiresAlert = true,
                RiskScore = 70
            };

            return LogSecurityEventAsync(securityEvent);
        }

        public Task LogAuthorizationFailureAsync(string userId, string resource, string action, string reason, string ipAddress)
        {
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.AuthorizationFailure,
                Severity = SecurityEventSeverity.Medium,
                UserId = userId,
                Resource = resource,
                Action = action,
                IpAddress = ipAddress,
                Message = $"Authorization denied for user {userId} on resource {resource}: {reason}",
                Details = new Dictionary<string, object>
                {
                    ["Reason"] = reason
                },
                Result = "Denied",
                RequiresAlert = true,
                RiskScore = 50
            };

            return LogSecurityEventAsync(securityEvent);
        }

        public Task LogAuthorizationSuccessAsync(string userId, string resource, string action)
        {
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.AuthorizationSuccess,
                Severity = SecurityEventSeverity.Informational,
                UserId = userId,
                Resource = resource,
                Action = action,
                Message = $"User {userId} authorized for {action} on {resource}",
                Result = "Granted"
            };

            return LogSecurityEventAsync(securityEvent);
        }

        public Task LogSuspiciousActivityAsync(string userId, string activityType, string ipAddress, Dictionary<string, object> details)
        {
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.SuspiciousActivity,
                Severity = SecurityEventSeverity.Critical,
                UserId = userId,
                IpAddress = ipAddress,
                Message = $"Suspicious activity detected: {activityType}",
                Details = details,
                Result = "Detected",
                RequiresAlert = true,
                RiskScore = 90
            };

            return LogSecurityEventAsync(securityEvent);
        }

        public Task LogConfigurationChangeAsync(string changedBy, string configKey, string? oldValue, string? newValue, string ipAddress)
        {
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.ConfigurationChange,
                Severity = SecurityEventSeverity.Medium,
                UserId = changedBy,
                IpAddress = ipAddress,
                Message = $"Configuration changed: {configKey}",
                Details = new Dictionary<string, object>
                {
                    ["ConfigKey"] = configKey,
                    ["OldValue"] = oldValue ?? "null",
                    ["NewValue"] = newValue ?? "null"
                },
                Result = "Changed",
                RequiresAlert = true,
                RiskScore = 40
            };

            return LogSecurityEventAsync(securityEvent);
        }

        public Task LogSensitiveDataAccessAsync(string userId, string dataType, string resourceId, string action)
        {
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.SensitiveDataAccess,
                Severity = SecurityEventSeverity.Medium,
                UserId = userId,
                Resource = resourceId,
                Action = action,
                Message = $"Sensitive data accessed: {dataType} by {userId}",
                Details = new Dictionary<string, object>
                {
                    ["DataType"] = dataType
                },
                Result = "Accessed",
                RiskScore = 30
            };

            return LogSecurityEventAsync(securityEvent);
        }

        public Task LogRateLimitExceededAsync(string identifier, string policyName, string ipAddress)
        {
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.RateLimitExceeded,
                Severity = SecurityEventSeverity.Medium,
                UserId = identifier,
                IpAddress = ipAddress,
                Message = $"Rate limit exceeded for {policyName}",
                Details = new Dictionary<string, object>
                {
                    ["Policy"] = policyName,
                    ["Identifier"] = identifier
                },
                Result = "Blocked",
                RequiresAlert = true,
                RiskScore = 60
            };

            return LogSecurityEventAsync(securityEvent);
        }

        public Task LogInvalidTokenAsync(string reason, string ipAddress, string? tokenHint = null)
        {
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.InvalidToken,
                Severity = SecurityEventSeverity.High,
                IpAddress = ipAddress,
                Message = $"Invalid token attempt: {reason}",
                Details = new Dictionary<string, object>
                {
                    ["Reason"] = reason,
                    ["TokenHint"] = tokenHint ?? "not provided"
                },
                Result = "Rejected",
                RequiresAlert = true,
                RiskScore = 75
            };

            return LogSecurityEventAsync(securityEvent);
        }

        public Task LogSecurityEventAsync(SecurityEvent securityEvent)
        {
            try
            {
                // Auto-populate CorrelationId from OpenTelemetry TraceId if not already set
                if (string.IsNullOrWhiteSpace(securityEvent.CorrelationId))
                {
                    var currentActivity = Activity.Current;
                    if (currentActivity != null)
                    {
                        securityEvent.CorrelationId = currentActivity.TraceId.ToString();
                    }
                }

                // Log to ILogger with structured logging
                var logLevel = securityEvent.Severity switch
                {
                    SecurityEventSeverity.Critical => LogLevel.Critical,
                    SecurityEventSeverity.High => LogLevel.Critical,
                    SecurityEventSeverity.Medium => LogLevel.Warning,
                    SecurityEventSeverity.Low => LogLevel.Information,
                    SecurityEventSeverity.Informational => LogLevel.Information,
                    _ => LogLevel.Information
                };

                _logger.Log(logLevel,
                    "SECURITY_EVENT: {EventType} | {Severity} | {Message} | EventId: {EventId} | UserId: {UserId} | IP: {IpAddress} | Resource: {Resource} | Result: {Result}",
                    securityEvent.EventType,
                    securityEvent.Severity,
                    securityEvent.Message,
                    securityEvent.EventId,
                    securityEvent.UserId ?? "unknown",
                    securityEvent.IpAddress ?? "unknown",
                    securityEvent.Resource ?? "N/A",
                    securityEvent.Result ?? "N/A");

                // Send to Application Insights for SIEM integration
                if (_enableApplicationInsights && _telemetryClient != null)
                {
                    SendToApplicationInsights(securityEvent);
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log security event: {EventType}", securityEvent.EventType);
                return Task.CompletedTask;
            }
        }

        public Task LogSecurityEventsBatchAsync(IEnumerable<SecurityEvent> securityEvents)
        {
            var tasks = securityEvents.Select(LogSecurityEventAsync);
            return Task.WhenAll(tasks);
        }

        private void SendToApplicationInsights(SecurityEvent securityEvent)
        {
            if (_telemetryClient == null) return;

            try
            {
                var eventTelemetry = new EventTelemetry($"SecurityEvent_{securityEvent.EventType}")
                {
                    Timestamp = securityEvent.Timestamp
                };

                // OpenTelemetry: Add trace context from current Activity for distributed tracing correlation
                var currentActivity = Activity.Current;
                if (currentActivity != null)
                {
                    eventTelemetry.Context.Operation.Id = currentActivity.TraceId.ToString();
                    eventTelemetry.Context.Operation.ParentId = currentActivity.SpanId.ToString();
                    eventTelemetry.Properties["otel.trace_id"] = currentActivity.TraceId.ToString();
                    eventTelemetry.Properties["otel.span_id"] = currentActivity.SpanId.ToString();
                }

                // Add all properties for SIEM filtering and analysis
                eventTelemetry.Properties["EventId"] = securityEvent.EventId;
                eventTelemetry.Properties["EventType"] = securityEvent.EventType.ToString();
                eventTelemetry.Properties["Severity"] = securityEvent.Severity.ToString();
                eventTelemetry.Properties["Message"] = securityEvent.Message;
                eventTelemetry.Properties["Result"] = securityEvent.Result ?? "N/A";
                
                if (!string.IsNullOrWhiteSpace(securityEvent.UserId))
                    eventTelemetry.Properties["UserId"] = securityEvent.UserId;
                
                if (!string.IsNullOrWhiteSpace(securityEvent.UserEmail))
                    eventTelemetry.Properties["UserEmail"] = securityEvent.UserEmail;
                
                if (!string.IsNullOrWhiteSpace(securityEvent.ApplicationId))
                    eventTelemetry.Properties["ApplicationId"] = securityEvent.ApplicationId;
                
                if (!string.IsNullOrWhiteSpace(securityEvent.ApplicationName))
                    eventTelemetry.Properties["ApplicationName"] = securityEvent.ApplicationName;
                
                if (!string.IsNullOrWhiteSpace(securityEvent.TenantId))
                    eventTelemetry.Properties["TenantId"] = securityEvent.TenantId;
                
                if (!string.IsNullOrWhiteSpace(securityEvent.IpAddress))
                    eventTelemetry.Properties["IpAddress"] = securityEvent.IpAddress;
                
                if (!string.IsNullOrWhiteSpace(securityEvent.UserAgent))
                    eventTelemetry.Properties["UserAgent"] = securityEvent.UserAgent;
                
                if (!string.IsNullOrWhiteSpace(securityEvent.Resource))
                    eventTelemetry.Properties["Resource"] = securityEvent.Resource;
                
                if (!string.IsNullOrWhiteSpace(securityEvent.Action))
                    eventTelemetry.Properties["Action"] = securityEvent.Action;
                
                if (!string.IsNullOrWhiteSpace(securityEvent.CorrelationId))
                    eventTelemetry.Properties["CorrelationId"] = securityEvent.CorrelationId;

                if (securityEvent.StatusCode.HasValue)
                    eventTelemetry.Properties["StatusCode"] = securityEvent.StatusCode.Value.ToString();

                if (securityEvent.RiskScore.HasValue)
                    eventTelemetry.Metrics["RiskScore"] = securityEvent.RiskScore.Value;

                eventTelemetry.Properties["RequiresAlert"] = securityEvent.RequiresAlert.ToString();

                // Add details as JSON
                if (securityEvent.Details != null && securityEvent.Details.Any())
                {
                    eventTelemetry.Properties["Details"] = JsonSerializer.Serialize(securityEvent.Details);
                }

                // Add exception info if present
                if (!string.IsNullOrWhiteSpace(securityEvent.ExceptionMessage))
                {
                    eventTelemetry.Properties["ExceptionMessage"] = securityEvent.ExceptionMessage;
                    eventTelemetry.Properties["ExceptionType"] = securityEvent.ExceptionType ?? "Unknown";
                }

                _telemetryClient.TrackEvent(eventTelemetry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send security event to Application Insights");
            }
        }
    }
}
