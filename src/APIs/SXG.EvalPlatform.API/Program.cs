using AutoMapper;
using Microsoft.OpenApi.Models;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using SxgEvalPlatformApi;
using SxgEvalPlatformApi.RequestHandlers;
using SxgEvalPlatformApi.Services;
using SxgEvalPlatformApi.Middleware;
using SXG.EvalPlatform.API.Middleware;  // ? Added for UserContextMiddleware
using System.Reflection;
using SxgEvalPlatformApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure logging first
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add core services
builder.Services.AddControllers();

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
builder.Services.AddBusinessServices(builder.Configuration);

var app = builder.Build();

// Configure middleware pipeline
app.UseMiddleware<TelemetryMiddleware>();

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
    "SXG Evaluation Platform API starting up - Environment: {Environment}, Authentication: {AuthStatus}, UserContext: Enabled",
    app.Environment.EnvironmentName,
    authEnabled 
      ? "ENABLED" + (!string.IsNullOrEmpty(builder.Configuration["AzureAd:ClientId"]) ? " (Configured)" : " (NOT Configured - will fail!)")
        : "DISABLED (Anonymous Access)"
);

app.Run();

// Make the implicit Program class public for integration testing
public partial class Program { }