using AutoMapper;
using Microsoft.OpenApi.Models;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using SxgEvalPlatformApi;
using SxgEvalPlatformApi.RequestHandlers;
using System.Reflection;

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

// Add AutoMapper
//builder.Services.AddAutoMapper(typeof(Program));
builder.Services.AddAutoMapper(typeof(MappingProfile));

// Add custom services
//builder.Services.AddScoped<IEvaluationService, EvaluationService>();
builder.Services.AddScoped<IAzureBlobStorageService, AzureBlobStorageService>();
builder.Services.AddScoped<IMetricsConfigTableService, MetricsConfigTableService>();
builder.Services.AddScoped<IDataSetTableService, DataSetTableService>();
builder.Services.AddScoped<IMetricsConfigurationRequestHandler, MetricsConfigurationRequestHandler>();
builder.Services.AddScoped<IConfigHelper, ConfigHelper>();

// Register Azure services from Storage project
//builder.Services.AddScoped<Sxg.EvalPlatform.API.Storage.Services.IAzureBlobStorageService, Sxg.EvalPlatform.API.Storage.Services.AzureBlobStorageService>();
//builder.Services.AddScoped<Sxg.EvalPlatform.API.Storage.Services.IMetricsConfigTableService, Sxg.EvalPlatform.API.Storage.Services.MetricsConfigTableService>();
//builder.Services.AddScoped<Sxg.EvalPlatform.API.Storage.Services.IDataSetTableService, Sxg.EvalPlatform.API.Storage.Services.DataSetTableService>();

// Register alias for smooth transition - use storage project implementation for local interface
//builder.Services.AddScoped<SxgEvalPlatformApi.Services.IAzureBlobStorageService>(provider => 
//    new BlobStorageServiceAdapter(provider.GetRequiredService<Sxg.EvalPlatform.API.Storage.Services.IAzureBlobStorageService>()));

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