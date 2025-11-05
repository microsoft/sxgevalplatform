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
using System.Reflection;
using SxgEvalPlatformApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure logging first
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add core services
builder.Services.AddControllers();

// Add custom service extensions
builder.Services.AddOpenTelemetryServices(builder.Configuration, builder.Environment);
builder.Services.AddSwaggerServices();
builder.Services.AddCorsServices();
builder.Services.AddAutoMapperServices();
builder.Services.AddHttpClientServices();
builder.Services.AddBusinessServices();

var app = builder.Build();

// Configure middleware pipeline
app.UseMiddleware<TelemetryMiddleware>();

// Enable Swagger in all environments for API documentation
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SXG Evaluation Platform API V1");
    c.RoutePrefix = "swagger";
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    c.DefaultModelsExpandDepth(-1);
});

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
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
    ["machine_name"] = Environment.MachineName
});

logger.LogInformation("SXG Evaluation Platform API starting up - Environment: {Environment}",
    app.Environment.EnvironmentName);

app.Run();