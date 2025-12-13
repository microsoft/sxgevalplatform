using Microsoft.Extensions.Caching.Memory;
using SxgEvalPlatformApi.Services;
using System.Diagnostics;

namespace SxgEvalPlatformApi.Middleware
{
    /// <summary>
    /// Middleware for security event monitoring and suspicious activity detection
    /// Implements MISE compliance requirements for security logging
    /// </summary>
    public sealed class SecurityMonitoringMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityMonitoringMiddleware> _logger;
        private readonly ISecurityEventLogger _securityEventLogger;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;

        public SecurityMonitoringMiddleware(
            RequestDelegate next,
            ILogger<SecurityMonitoringMiddleware> logger,
            ISecurityEventLogger securityEventLogger,
            IConfiguration configuration,
            IMemoryCache memoryCache)
        {
            _next = next;
            _logger = logger;
            _securityEventLogger = securityEventLogger;
            _configuration = configuration;
            _memoryCache = memoryCache;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = context.Request.Headers.UserAgent.ToString();
            var requestPath = context.Request.Path.Value ?? string.Empty;
            var httpMethod = context.Request.Method;

            try
            {
                // Call next middleware
                await _next(context);

                // After response - check for security-relevant status codes
                await CheckResponseForSecurityEventsAsync(context, ipAddress, userAgent, requestPath, httpMethod);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in SecurityMonitoringMiddleware");
                throw;
            }
        }

        private async Task CheckResponseForSecurityEventsAsync(
            HttpContext context, 
            string ipAddress, 
            string userAgent, 
            string requestPath,
            string httpMethod)
        {
            var statusCode = context.Response.StatusCode;

            // Log authentication failures (401)
            if (statusCode == 401)
            {
                await HandleAuthenticationFailureAsync(ipAddress, userAgent, requestPath, httpMethod);
            }

            // Log authorization failures (403)
            else if (statusCode == 403)
            {
                await HandleAuthorizationFailureAsync(context, ipAddress, requestPath, httpMethod);
            }

            // Log rate limit exceeded (429)
            else if (statusCode == 429)
            {
                await HandleRateLimitExceededAsync(context, ipAddress, requestPath);
            }

            // Log suspicious 4xx errors that might indicate probing
            else if (statusCode >= 400 && statusCode < 500 && statusCode != 404)
            {
                await CheckForSuspiciousActivityAsync(context, ipAddress, userAgent, requestPath, statusCode);
            }

            // Log successful access to sensitive endpoints
            else if (statusCode >= 200 && statusCode < 300)
            {
                await LogSensitiveEndpointAccessAsync(context, requestPath, httpMethod);
            }
        }

        private async Task HandleAuthenticationFailureAsync(string ipAddress, string userAgent, string requestPath, string httpMethod)
        {
            // Track failed attempts
            TrackFailedAttempt(ipAddress);

            // Log the authentication failure
            await _securityEventLogger.LogAuthenticationFailureAsync(
                reason: "Authentication token missing or invalid",
                ipAddress: ipAddress,
                userAgent: userAgent,
                details: new Dictionary<string, object>
                {
                    ["RequestPath"] = requestPath,
                    ["HttpMethod"] = httpMethod,
                    ["FailedAttemptCount"] = GetFailedAttemptCount(ipAddress)
                });

            // Check if this IP has too many failed attempts
            if (GetFailedAttemptCount(ipAddress) >= 5)
            {
                await _securityEventLogger.LogSuspiciousActivityAsync(
                    userId: "unknown",
                    activityType: "Multiple authentication failures",
                    ipAddress: ipAddress,
                    details: new Dictionary<string, object>
                    {
                        ["FailedAttemptCount"] = GetFailedAttemptCount(ipAddress),
                        ["LastAttemptPath"] = requestPath,
                        ["TimeWindow"] = "10 minutes"
                    });
            }
        }

        private async Task HandleAuthorizationFailureAsync(HttpContext context, string ipAddress, string requestPath, string httpMethod)
        {
            var userId = context.User?.FindFirst("oid")?.Value ?? 
                         context.User?.FindFirst("sub")?.Value ?? 
                         "unknown";

            await _securityEventLogger.LogAuthorizationFailureAsync(
                userId: userId,
                resource: requestPath,
                action: httpMethod,
                reason: "Insufficient permissions",
                ipAddress: ipAddress);
        }

        private async Task HandleRateLimitExceededAsync(HttpContext context, string ipAddress, string requestPath)
        {
            var userId = context.User?.FindFirst("oid")?.Value ?? 
                         context.User?.Identity?.Name ?? 
                         ipAddress;

            await _securityEventLogger.LogRateLimitExceededAsync(
                identifier: userId,
                policyName: DeterminePolicyName(context),
                ipAddress: ipAddress);
        }

        private async Task CheckForSuspiciousActivityAsync(
            HttpContext context, 
            string ipAddress, 
            string userAgent, 
            string requestPath,
            int statusCode)
        {
            // Check for suspicious patterns
            var suspiciousIndicators = new List<string>();

            // Check for SQL injection patterns
            if (requestPath.Contains("'") || requestPath.Contains("--") || 
                requestPath.Contains("DROP", StringComparison.OrdinalIgnoreCase) ||
                requestPath.Contains("UNION", StringComparison.OrdinalIgnoreCase))
            {
                suspiciousIndicators.Add("Possible SQL injection attempt");
            }

            // Check for path traversal
            if (requestPath.Contains("..") || requestPath.Contains("~"))
            {
                suspiciousIndicators.Add("Possible path traversal attempt");
            }

            // Check for script injection
            if (requestPath.Contains("<script", StringComparison.OrdinalIgnoreCase) ||
                requestPath.Contains("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                suspiciousIndicators.Add("Possible XSS attempt");
            }

            // Check for suspicious user agents (bots, scanners)
            var suspiciousUserAgents = new[] { "sqlmap", "nikto", "nmap", "masscan", "python-requests", "curl" };
            if (suspiciousUserAgents.Any(ua => userAgent.Contains(ua, StringComparison.OrdinalIgnoreCase)))
            {
                suspiciousIndicators.Add("Suspicious user agent detected");
            }

            if (suspiciousIndicators.Any())
            {
                var userId = context.User?.FindFirst("oid")?.Value ?? "unknown";

                await _securityEventLogger.LogSuspiciousActivityAsync(
                    userId: userId,
                    activityType: "Potential attack detected",
                    ipAddress: ipAddress,
                    details: new Dictionary<string, object>
                    {
                        ["Indicators"] = suspiciousIndicators,
                        ["RequestPath"] = requestPath,
                        ["UserAgent"] = userAgent,
                        ["StatusCode"] = statusCode
                    });
            }
        }

        private async Task LogSensitiveEndpointAccessAsync(HttpContext context, string requestPath, string httpMethod)
        {
            // Define sensitive endpoints that should be logged
            var sensitiveEndpoints = new[]
            {
                "/api/v1/eval/results",
                "/api/v1/datasets",
                "/api/v1/eval/configurations"
            };

            if (sensitiveEndpoints.Any(endpoint => requestPath.StartsWith(endpoint, StringComparison.OrdinalIgnoreCase)))
            {
                var userId = context.User?.FindFirst("oid")?.Value ?? 
                             context.User?.FindFirst("sub")?.Value ?? 
                             "unknown";

                var userEmail = context.User?.FindFirst("email")?.Value ?? 
                                context.User?.FindFirst("preferred_username")?.Value;

                // Only log write operations (POST, PUT, DELETE) to reduce noise
                if (httpMethod != "GET")
                {
                    await _securityEventLogger.LogSensitiveDataAccessAsync(
                        userId: userId,
                        dataType: DetermineDataType(requestPath),
                        resourceId: requestPath,
                        action: httpMethod);
                }
            }
        }

        private string DeterminePolicyName(HttpContext context)
        {
            // Try to determine which rate limiting policy was triggered
            if (context.Request.Headers.ContainsKey("X-Session-Id"))
                return "SessionPolicy";
            
            if (context.User?.Identity?.IsAuthenticated == true)
                return "UserPolicy";
            
            return "IpPolicy";
        }

        private string DetermineDataType(string requestPath)
        {
            if (requestPath.Contains("/results", StringComparison.OrdinalIgnoreCase))
                return "EvaluationResults";
            
            if (requestPath.Contains("/datasets", StringComparison.OrdinalIgnoreCase))
                return "Dataset";
            
            if (requestPath.Contains("/configurations", StringComparison.OrdinalIgnoreCase))
                return "Configuration";
            
            return "Unknown";
        }

        #region Failed Attempt Tracking

        private void TrackFailedAttempt(string ipAddress)
        {
            var cacheKey = $"FailedAttempts_{ipAddress}";
            var tracker = _memoryCache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                return new FailedAttemptTracker();
            });

            if (tracker != null)
            {
                tracker.AddAttempt();
                _memoryCache.Set(cacheKey, tracker, TimeSpan.FromMinutes(10));
            }
        }

        private int GetFailedAttemptCount(string ipAddress)
        {
            var cacheKey = $"FailedAttempts_{ipAddress}";
            if (_memoryCache.TryGetValue(cacheKey, out FailedAttemptTracker? tracker) && tracker != null)
            {
                return tracker.Count;
            }
            return 0;
        }

        private class FailedAttemptTracker
        {
            public int Count { get; private set; }
            public DateTime LastAttempt { get; private set; }

            public void AddAttempt()
            {
                Count++;
                LastAttempt = DateTime.UtcNow;
            }
        }

        #endregion
    }
}
