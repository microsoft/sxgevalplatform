using SxgEvalPlatformApi.Services;
using SxgEvalPlatformApi.Middleware;
using SXG.EvalPlatform.API.Middleware;  // ? Added for UserContextMiddleware
using SxgEvalPlatformApi.Extensions;
using System.Threading.RateLimiting;
using Microsoft.Identity.ServiceEssentials;  // Added for UseMise middleware

var builder = WebApplication.CreateBuilder(args);

// Configure logging first
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add core services

builder.Services.AddControllers();

// Add rate limiting policies
var rateLimitingOptions = builder.Configuration.GetSection("RateLimiting").Get<RateLimitingOptions>();


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



// Add HttpContextAccessor (required for CallerIdentificationService)
builder.Services.AddHttpContextAccessor();

// Add custom service extensions
builder.Services.AddOpenTelemetryServices(builder.Configuration, builder.Environment);
builder.Services.AddSwaggerServices();
builder.Services.AddCorsServices();

// ? Azure AD Authentication (feature flag controlled)
var authEnabled = builder.Configuration.GetValue<bool>("FeatureFlags:EnableAuthentication", false);
builder.Services.AddAzureAdAuthentication(builder.Configuration);

builder.Services.AddAutoMapperServices();
builder.Services.AddHttpClientServices();
builder.Services.AddServiceBus(builder.Configuration);
builder.Services.AddBusinessServices(builder.Configuration);


var app = builder.Build();

// Configure middleware pipeline


app.UseMiddleware<TelemetryMiddleware>();

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
// MISE authentication middleware is automatically configured via AddMiseWithDefaultModules
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
    "SXG Evaluation Platform API starting up - Environment: {Environment}, Authentication: {AuthStatus}, UserContext: Enabled",
    app.Environment.EnvironmentName,
    authEnabled 
      ? "ENABLED" + (!string.IsNullOrEmpty(builder.Configuration["AzureAd:ClientId"]) ? " (Configured)" : " (NOT Configured - will fail!)")
        : "DISABLED (Anonymous Access)"
);

app.Run();

// Make the implicit Program class public for integration testing
public partial class Program { }