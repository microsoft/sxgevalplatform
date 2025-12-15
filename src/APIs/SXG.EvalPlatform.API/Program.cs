using SxgEvalPlatformApi.Services;
using SxgEvalPlatformApi.Middleware;
using SXG.EvalPlatform.API.Middleware;  // ? Added for UserContextMiddleware
using SxgEvalPlatformApi.Extensions;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure logging first
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add core services

builder.Services.AddControllers();

// Add rate limiting policies
var rateLimitingOptions = builder.Configuration.GetSection("RateLimiting").Get<RateLimitingOptions>();

// Validate rate limiting configuration
if (rateLimitingOptions == null)
{
    var earlyLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Program>();
    earlyLogger.LogWarning(
        "?? Rate limiting configuration not found in appsettings.json - using fallback defaults " +
        "(IpPolicy: 100 req/60s, UserPolicy: 50 req/60s, SessionPolicy: 30 req/60s)");
}

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("IpPolicy", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitingOptions?.IpPolicy?.PermitLimit ?? 100,
            Window = TimeSpan.FromSeconds(rateLimitingOptions?.IpPolicy?.WindowSeconds ?? 60),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.AddPolicy("UserPolicy", context =>
    {
        var userId = context.User?.Identity?.IsAuthenticated == true
            ? context.User.FindFirst("oid")?.Value ?? context.User.Identity.Name ?? "anonymous"
            : "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitingOptions?.UserPolicy?.PermitLimit ?? 50,
            Window = TimeSpan.FromSeconds(rateLimitingOptions?.UserPolicy?.WindowSeconds ?? 60),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.AddPolicy("SessionPolicy", context =>
    {
        var sessionId = context.Request.Headers["X-Session-Id"].FirstOrDefault()
            ?? context.Request.Cookies["SessionId"]
            ?? "nosession";
        return RateLimitPartition.GetFixedWindowLimiter(sessionId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitingOptions?.SessionPolicy?.PermitLimit ?? 30,
            Window = TimeSpan.FromSeconds(rateLimitingOptions?.SessionPolicy?.WindowSeconds ?? 60),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

//builder.Services.AddRateLimiter(options =>
//{
//    // Per IP address
//    options.AddPolicy("IpPolicy", context =>
//    {
//        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
//        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
//        {
//            PermitLimit = 100, // requests per minute per IP
//            Window = TimeSpan.FromMinutes(1),
//            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
//            QueueLimit = 0
//        });
//    });

//    // Per user (from claims)
//    options.AddPolicy("UserPolicy", context =>
//    {
//        var userId = context.User?.Identity?.IsAuthenticated == true
//            ? context.User.FindFirst("oid")?.Value ?? context.User.Identity.Name ?? "anonymous"
//            : "anonymous";
//        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
//        {
//            PermitLimit = 50, // requests per minute per user
//            Window = TimeSpan.FromMinutes(1),
//            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
//            QueueLimit = 0
//        });
//    });

//    // Per session (from header/cookie)
//    options.AddPolicy("SessionPolicy", context =>
//    {
//        var sessionId = context.Request.Headers["X-Session-Id"].FirstOrDefault()
//            ?? context.Request.Cookies["SessionId"]
//            ?? "nosession";
//        return RateLimitPartition.GetFixedWindowLimiter(sessionId, _ => new FixedWindowRateLimiterOptions
//        {
//            PermitLimit = 30, // requests per minute per session
//            Window = TimeSpan.FromMinutes(1),
//            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
//            QueueLimit = 0
//        });
//    });
//});

// Add HttpContextAccessor (required for CallerIdentificationService)
builder.Services.AddHttpContextAccessor();

// Add custom service extensions
builder.Services.AddOpenTelemetryServices(builder.Configuration, builder.Environment);
builder.Services.AddSwaggerServices();
builder.Services.AddCorsServices();

// ? Azure AD Authentication (feature flag controlled)
var authEnabled = builder.Configuration.GetValue<bool>("FeatureFlags:EnableAuthentication", false);
builder.Services.AddAzureAdAuthentication(builder.Configuration);

// ?? MISE Compliance: Register security event logging service
// Ensure memory cache is available for middleware and tracking
builder.Services.AddMemoryCache();

// Register SecurityEventQueue as a single instance and run it as hosted service
builder.Services.AddSingleton<SecurityEventQueue>();
builder.Services.AddSingleton<ISecurityEventQueue>(sp => sp.GetRequiredService<SecurityEventQueue>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<SecurityEventQueue>());

builder.Services.AddSingleton<ISecurityEventLogger, SecurityEventLogger>();
// Register security event queue (background service) and logger
builder.Services.AddSingleton<ISecurityEventQueue, SecurityEventQueue>();
builder.Services.AddHostedService<ServiceFactoryHostedService<ISecurityEventQueue>>();
builder.Services.AddSingleton<ISecurityEventLogger, SecurityEventLogger>();

builder.Services.AddAutoMapperServices();
builder.Services.AddHttpClientServices();
builder.Services.AddServiceBus(builder.Configuration);
builder.Services.AddBusinessServices(builder.Configuration);


var app = builder.Build();

// ?? MISE Compliance: Validate Application Insights configuration
var appInsightsConnectionString = builder.Configuration["Telemetry:AppInsightsConnectionString"];
var securityLoggingEnabled = builder.Configuration.GetValue<bool>("SecurityLogging:Enabled", true);

if (securityLoggingEnabled && string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    var validationLogger = app.Services.GetRequiredService<ILogger<Program>>();
    validationLogger.LogCritical(
        "?????? MISE COMPLIANCE VIOLATION ??????\n" +
        "Application Insights connection string not configured!\n" +
        "Security events will only log to console, NOT to SIEM.\n" +
        "Configure 'Telemetry:AppInsightsConnectionString' in appsettings.json or environment variables.\n" +
        "To disable this check, set 'SecurityLogging:Enabled' to false.");
    
    // In production, throw an exception to prevent startup without SIEM
    if (app.Environment.IsProduction())
    {
        throw new InvalidOperationException(
            "MISE Compliance Violation: Application Insights must be configured in production for SIEM integration. " +
            "Set 'Telemetry:AppInsightsConnectionString' in configuration.");
    }
}

// Configure middleware pipeline

app.UseMiddleware<TelemetryMiddleware>();

// ?? MISE Compliance: Security monitoring middleware for threat detection
app.UseMiddleware<SecurityMonitoringMiddleware>();

// Enable rate limiting globally (all endpoints)
app.UseRateLimiter();

// Enable Swagger in all environments for API documentation
// Must come BEFORE authentication middleware to allow anonymous access
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SXG Evaluation Platform API V1");
    c.RoutePrefix = "swagger";
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    c.DefaultModelsExpandDepth(-1);
    
    // Configure OAuth 2.0 for Swagger (only if authentication is enabled)
    if (authEnabled && !string.IsNullOrEmpty(builder.Configuration["AzureAd:ClientId"]))
    {
        c.OAuthClientId(builder.Configuration["AzureAd:ClientId"]);
     c.OAuthUsePkce();
      c.OAuthScopeSeparator(" ");
    }
});

app.UseHttpsRedirection();
app.UseCors("AllowAll");

// Authentication/Authorization - Swagger endpoints are already registered above, so they're accessible
app.UseAuthentication();

// ? ADDED: Extract user context from headers when service principals call the API
// This middleware runs after authentication and adds delegated user claims for proper user tracking
app.UseUserContext();

app.UseAuthorization();

app.MapControllers();

// Log application startup with telemetry
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var telemetryService = app.Services.GetRequiredService<IOpenTelemetryService>();

using var startupActivity = telemetryService.StartActivity("Application.Startup");
telemetryService.AddActivityTags(new Dictionary<string, object>
{
    ["environment"] = app.Environment.EnvironmentName,
    ["version"] = builder.Configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0",
    ["machine_name"] = Environment.MachineName,
    ["authentication_enabled"] = authEnabled,
    ["authentication_configured"] = !string.IsNullOrEmpty(builder.Configuration["AzureAd:ClientId"]),
    ["user_context_middleware_enabled"] = true  // ? Track that middleware is active
});

logger.LogInformation(
    "SXG Evaluation Platform API starting up - Environment: {Environment}, Authentication: {AuthStatus}, UserContext: Enabled, MISE Security: Enabled",
    app.Environment.EnvironmentName,
    authEnabled 
      ? "ENABLED" + (!string.IsNullOrEmpty(builder.Configuration["AzureAd:ClientId"]) ? " (Configured)" : " (NOT Configured - will fail!)")
        : "DISABLED (Anonymous Access)"
);

// ?? MISE Compliance: Log application startup as security event
var securityLogger = app.Services.GetService<ISecurityEventLogger>();
if (securityLogger != null)
{
    // Get container/host identifier (works in local dev, Docker, and Azure Container Apps)
    var hostIdentifier = Environment.GetEnvironmentVariable("CONTAINER_APP_REPLICA_NAME")  // Azure Container Apps
        ?? Environment.GetEnvironmentVariable("HOSTNAME")                                    // Generic Linux/Docker
        ?? Environment.MachineName                                                           // Windows/Local dev
        ?? "localhost";                                                                      // Fallback

    _ = securityLogger.LogConfigurationChangeAsync(
        changedBy: "System",
        configKey: "Application.Startup",
        oldValue: null,
        newValue: $"Environment: {app.Environment.EnvironmentName}, Auth: {authEnabled}, Host: {hostIdentifier}",
        ipAddress: hostIdentifier);  // Use host identifier instead of IP
}

app.Run();

// Make the implicit Program class public for integration testing
public partial class Program { }