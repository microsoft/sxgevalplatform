using SxgEvalPlatformApi.Models.Security;

namespace SxgEvalPlatformApi.Services
{
    /// <summary>
    /// Service for logging security events for MISE compliance
    /// Logs are sent to Application Insights and structured for SIEM integration
    /// </summary>
    public interface ISecurityEventLogger
    {
        /// <summary>
        /// Log a successful authentication event
        /// </summary>
        Task LogAuthenticationSuccessAsync(string userId, string? userEmail, string authenticationType, string ipAddress);

        /// <summary>
        /// Log a failed authentication attempt
        /// </summary>
        Task LogAuthenticationFailureAsync(string reason, string ipAddress, string? userAgent, Dictionary<string, object>? details = null);

        /// <summary>
        /// Log an authorization failure (403 Forbidden)
        /// </summary>
        Task LogAuthorizationFailureAsync(string userId, string resource, string action, string reason, string ipAddress);

        /// <summary>
        /// Log an authorization success
        /// </summary>
        Task LogAuthorizationSuccessAsync(string userId, string resource, string action);

        /// <summary>
        /// Log suspicious activity that might indicate an attack
        /// </summary>
        Task LogSuspiciousActivityAsync(string userId, string activityType, string ipAddress, Dictionary<string, object> details);

        /// <summary>
        /// Log a configuration change
        /// </summary>
        Task LogConfigurationChangeAsync(string changedBy, string configKey, string? oldValue, string? newValue, string ipAddress);

        /// <summary>
        /// Log sensitive data access
        /// </summary>
        Task LogSensitiveDataAccessAsync(string userId, string dataType, string resourceId, string action);

        /// <summary>
        /// Log rate limit exceeded
        /// </summary>
        Task LogRateLimitExceededAsync(string identifier, string policyName, string ipAddress);

        /// <summary>
        /// Log an invalid or expired token attempt
        /// </summary>
        Task LogInvalidTokenAsync(string reason, string ipAddress, string? tokenHint = null);

        /// <summary>
        /// Log a generic security event
        /// </summary>
        Task LogSecurityEventAsync(SecurityEvent securityEvent);

        /// <summary>
        /// Log multiple security events in batch
        /// </summary>
        Task LogSecurityEventsBatchAsync(IEnumerable<SecurityEvent> securityEvents);
    }
}
