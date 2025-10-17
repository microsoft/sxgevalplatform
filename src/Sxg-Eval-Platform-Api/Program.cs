using AutoMapper;
using Microsoft.OpenApi.Models;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using SxgEvalPlatformApi.RequestHandlers;
using SxgEvalPlatformApi.Services;
using System.Reflection;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SXG Evaluation Platform API",
        Version = "v1",
        Description = "API for SXG Evaluation Platform components",
        Contact = new OpenApiContact
        {
            Name = "SXG Team",
            Email = "sxg@microsoft.com"
        }
    });

    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add AutoMapper with explicit mapping profiles
builder.Services.AddAutoMapper(typeof(SxgEvalPlatformApi.MappingProfile));

// Add custom services
//builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddScoped<IAzureBlobStorageService, AzureBlobStorageService>();
builder.Services.AddScoped<IMetricsConfigTableService, MetricsConfigTableService>();
builder.Services.AddScoped<IDataSetTableService, DataSetTableService>();
builder.Services.AddScoped<IMetricsConfigurationRequestHandler, MetricsConfigurationRequestHandler>();
builder.Services.AddScoped<IDataSetRequestHandler, DataSetRequestHandler>();
builder.Services.AddScoped<IConfigHelper, ConfigHelper>();

// Add evaluation services
builder.Services.AddScoped<IEvalRunService, EvalRunService>();
builder.Services.AddScoped<IEvaluationResultService, EvaluationResultService>();

// Register Azure services from Storage project
//builder.Services.AddScoped<Sxg.EvalPlatform.API.Storage.Services.IAzureBlobStorageService, Sxg.EvalPlatform.API.Storage.Services.AzureBlobStorageService>();
//builder.Services.AddScoped<Sxg.Eval.Platform.API.Storage.Services.IMetricsConfigTableService, Sxg.Eval.Platform.API.Storage.Services.MetricsConfigTableService>();
//builder.Services.AddScoped<Sxg.Eval.Platform.API.Storage.Services.IDataSetTableService, Sxg.Eval.Platform.API.Storage.Services.DataSetTableService>();

// Register alias for smooth transition - use storage project implementation for local interface
//builder.Services.AddScoped<SxgEvalPlatformApi.Services.IAzureBlobStorageService>(provider => 
//    new BlobStorageServiceAdapter(provider.GetRequiredService<Sxg.Eval.Platform.API.Storage.Services.IAzureBlobStorageService>()));

// Register legacy Azure services (to be migrated)
//builder.Services.AddScoped<IAzureTableService, AzureTableService>();

// Register the new evaluation configuration service
//builder.Services.AddScoped<IEvaluationConfigurationService, NewEvaluationConfigurationService>();

// Register the dataset service
//builder.Services.AddScoped<IDatasetService, DatasetService>();

// Add logging
builder.Services.AddLogging();

// Add Rate Limiting for security
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    // General API rate limiting policy
    rateLimiterOptions.AddPolicy("ApiPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100, // 100 requests per minute per IP
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));

    // Strict rate limiting for resource-intensive operations (POST, PUT)
    rateLimiterOptions.AddPolicy("StrictApiPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20, // 20 requests per minute per IP
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            }));

    // Global fallback policy
    rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 200, // Global limit: 200 requests per minute per IP
                Window = TimeSpan.FromMinutes(1)
            }));

    rateLimiterOptions.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429; // Too Many Requests
        await context.HttpContext.Response.WriteAsync(
            "Rate limit exceeded. Please try again later.", 
            cancellationToken: token);
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// Enable Swagger in all environments for API documentation
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SXG Evaluation Platform API V1");
    c.RoutePrefix = "swagger"; // Set Swagger UI at /swagger
});

app.UseHttpsRedirection();

app.UseCors("AllowAll");

// Enable rate limiting middleware
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();