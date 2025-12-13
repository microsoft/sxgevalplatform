namespace SxgEvalPlatformApi.Models.Security
{
    /// <summary>
    /// Represents a security event for MISE compliance logging
    /// </summary>
    public class SecurityEvent
    {
        /// <summary>
        /// Unique identifier for this security event
        /// </summary>
        public string EventId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Type of security event (Authentication, Authorization, SuspiciousActivity, etc.)
        /// </summary>
        public SecurityEventType EventType { get; set; }

        /// <summary>
        /// Timestamp when the event occurred (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Severity level of the security event
        /// </summary>
        public SecurityEventSeverity Severity { get; set; }

        /// <summary>
        /// User identifier (OID, email, or service principal name)
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// User email address if available
        /// </summary>
        public string? UserEmail { get; set; }

        /// <summary>
        /// Application/Service Principal ID if applicable
        /// </summary>
        public string? ApplicationId { get; set; }

        /// <summary>
        /// Application name if applicable
        /// </summary>
        public string? ApplicationName { get; set; }

        /// <summary>
        /// Azure AD Tenant ID
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// IP address of the caller
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// User agent string from the request
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// HTTP method (GET, POST, PUT, DELETE, etc.)
        /// </summary>
        public string? HttpMethod { get; set; }

        /// <summary>
        /// Request path
        /// </summary>
        public string? RequestPath { get; set; }

        /// <summary>
        /// HTTP status code
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Detailed message about the security event
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Additional details about the event (JSON serializable)
        /// </summary>
        public Dictionary<string, object>? Details { get; set; }

        /// <summary>
        /// Exception message if an error occurred
        /// </summary>
        public string? ExceptionMessage { get; set; }

        /// <summary>
        /// Exception type if an error occurred
        /// </summary>
        public string? ExceptionType { get; set; }

        /// <summary>
        /// Correlation ID for tracking related events
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Whether this event should trigger an alert
        /// </summary>
        public bool RequiresAlert { get; set; }

        /// <summary>
        /// Resource that was accessed or attempted to be accessed
        /// </summary>
        public string? Resource { get; set; }

        /// <summary>
        /// Action that was performed or attempted
        /// </summary>
        public string? Action { get; set; }

        /// <summary>
        /// Result of the action (Success, Failure, Blocked, etc.)
        /// </summary>
        public string? Result { get; set; }

        /// <summary>
        /// Geographic location if available
        /// </summary>
        public string? Location { get; set; }

        /// <summary>
        /// Risk score (0-100) if calculated
        /// </summary>
        public int? RiskScore { get; set; }
    }

    /// <summary>
    /// Types of security events tracked for MISE compliance
    /// </summary>
    public enum SecurityEventType
    {
        /// <summary>
        /// Successful authentication
        /// </summary>
        AuthenticationSuccess,

        /// <summary>
        /// Failed authentication attempt
        /// </summary>
        AuthenticationFailure,

        /// <summary>
        /// Authorization denied
        /// </summary>
        AuthorizationFailure,

        /// <summary>
        /// Authorization granted
        /// </summary>
        AuthorizationSuccess,

        /// <summary>
        /// Invalid token provided
        /// </summary>
        InvalidToken,

        /// <summary>
        /// Expired token used
        /// </summary>
        ExpiredToken,

        /// <summary>
        /// Suspicious activity detected
        /// </summary>
        SuspiciousActivity,

        /// <summary>
        /// Rate limit exceeded
        /// </summary>
        RateLimitExceeded,

        /// <summary>
        /// Configuration changed
        /// </summary>
        ConfigurationChange,

        /// <summary>
        /// Sensitive data accessed
        /// </summary>
        SensitiveDataAccess,

        /// <summary>
        /// Data modification
        /// </summary>
        DataModification,

        /// <summary>
        /// Unusual access pattern
        /// </summary>
        UnusualAccessPattern,

        /// <summary>
        /// Privilege escalation attempt
        /// </summary>
        PrivilegeEscalation,

        /// <summary>
        /// Account lockout
        /// </summary>
        AccountLockout,

        /// <summary>
        /// Security policy violation
        /// </summary>
        PolicyViolation,

        /// <summary>
        /// API abuse detected
        /// </summary>
        ApiAbuse
    }

    /// <summary>
    /// Severity levels for security events
    /// </summary>
    public enum SecurityEventSeverity
    {
        /// <summary>
        /// Informational - routine security event
        /// </summary>
        Informational,

        /// <summary>
        /// Low - minor security concern
        /// </summary>
        Low,

        /// <summary>
        /// Medium - moderate security concern
        /// </summary>
        Medium,

        /// <summary>
        /// High - significant security concern
        /// </summary>
        High,

        /// <summary>
        /// Critical - immediate security threat
        /// </summary>
        Critical
    }
}
