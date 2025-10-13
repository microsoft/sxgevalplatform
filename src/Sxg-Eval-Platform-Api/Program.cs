using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SxgEvalPlatformApi.Services;
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
builder.Services.AddAutoMapper(typeof(Program));

// Add custom services
builder.Services.AddScoped<IEvaluationService, EvaluationService>();

// Register Azure services
builder.Services.AddScoped<IAzureTableService, AzureTableService>();
builder.Services.AddScoped<IAzureBlobStorageService, AzureBlobStorageService>();

// Register the new evaluation configuration service
builder.Services.AddScoped<IEvaluationConfigurationService, NewEvaluationConfigurationService>();

// Register the dataset service
builder.Services.AddScoped<IDatasetService, DatasetService>();

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