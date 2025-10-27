using AutoMapper;
using Microsoft.OpenApi.Models;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using SxgEvalPlatformApi;
using SxgEvalPlatformApi.RequestHandlers;
using SxgEvalPlatformApi.Services.Cache;
using SxgEvalPlatformApi.Services;
using SxgEvalPlatformApi.Cache;
using SxgEvalPlatformApi.Configuration;
using StackExchange.Redis;
using System.Reflection;
using Azure.Identity;
using Azure.Core;

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
builder.Services.AddAutoMapper(typeof(MappingProfile));

// Configure Redis settings
builder.Services.Configure<RedisConfiguration>(builder.Configuration.GetSection("Redis"));

// Configure Redis Connection with Microsoft Entra Authentication using factory
builder.Services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
{
    var config = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RedisConfiguration>>();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    return RedisConnectionFactory.CreateConnectionAsync(config, logger).GetAwaiter().GetResult();
});

// Also register the concrete type for services that need it
builder.Services.AddSingleton<ConnectionMultiplexer>(serviceProvider =>
    (ConnectionMultiplexer)serviceProvider.GetRequiredService<IConnectionMultiplexer>());

// Add Redis Cache Services
builder.Services.AddSingleton<IRedisCache, RedisCacheService>();
builder.Services.AddSingleton<IGenericCacheService, GenericCacheService>();

// Add specialized cache services for specific entities
builder.Services.AddScoped<IEvalConfigCache, EvalConfigCacheService>();
builder.Services.AddScoped<IEvalDatasetCache, EvalDatasetCacheService>();

// Add base storage services (concrete implementations without caching)
builder.Services.AddScoped<MetricsConfigTableService>();
builder.Services.AddScoped<AzureBlobStorageService>();
builder.Services.AddScoped<EvalRunTableService>();
builder.Services.AddScoped<DataSetTableService>();

// Add services with automatic caching using the generic decorator
// This replaces all the manual cache wrapper registrations
builder.Services.AddCachedService<IMetricsConfigTableService, MetricsConfigTableService>();
builder.Services.AddCachedService<IEvalRunTableService, EvalRunTableService>();
builder.Services.AddCachedService<IDataSetTableService, DataSetTableService>();
builder.Services.AddCachedService<IAzureBlobStorageService, AzureBlobStorageService>();

// Add other services (these will automatically use cached services via DI)
builder.Services.AddScoped<IMetricsConfigurationRequestHandler, MetricsConfigurationRequestHandler>();
builder.Services.AddScoped<IDataSetRequestHandler, DataSetRequestHandler>();
builder.Services.AddScoped<IConfigHelper, ConfigHelper>();

// Register evaluation request handlers (will use cached services)
builder.Services.AddScoped<IEvalRunRequestHandler, EvalRunRequestHandler>();
builder.Services.AddScoped<IEvaluationResultRequestHandler, EvaluationResultRequestHandler>();

// Add startup connection validation services
builder.Services.AddSingleton<IStartupConnectionValidator, StartupConnectionValidator>();
builder.Services.AddHostedService<StartupValidationHostedService>();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();