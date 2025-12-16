using AutoMapper;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Azure;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Extensions;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.Validators;
using SXG.EvalPlatform.Common;
using SxgEvalPlatformApi.RequestHandlers;
using SxgEvalPlatformApi.Services;
using System.Reflection;

namespace SxgEvalPlatformApi.Extensions;

/// <summary>
/// Extension methods for service collection configuration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configure OpenTelemetry services
    /// </summary>
    public static IServiceCollection AddOpenTelemetryServices(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "SXG-EvalPlatform-API";
        var serviceVersion = configuration["OpenTelemetry:ServiceVersion"] ?? "1.0.0";
        var appInsightsConnectionString = configuration["Telemetry:AppInsightsConnectionString"];
        var enableConsoleExporter = configuration.GetValue<bool>("OpenTelemetry:EnableConsoleExporter", false);
        var samplingRatio = configuration.GetValue<double>("OpenTelemetry:SamplingRatio", 1.0);

        // ? UPDATED: Cloud role name includes environment for better filtering
        var baseCloudRoleName = configuration["OpenTelemetry:CloudRoleName"] ?? serviceName;
        var cloudRoleName = $"{baseCloudRoleName}-{environment.EnvironmentName}";

        services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
           .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
       .AddAttributes(new Dictionary<string, object>
       {
           ["deployment.environment"] = environment.EnvironmentName,
           ["service.instance.id"] = Environment.MachineName,
           ["cloud.role"] = cloudRoleName,  // ? Now includes environment: "SXG-EvalPlatform-API-Development"
           ["app.role"] = cloudRoleName     // ? Alternative tag name
       }))
          .WithTracing(tracing =>
           {
               tracing
             .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.EnrichWithHttpRequest = (activity, httpRequest) =>
         {
             activity.SetTag("http.request.body.size", httpRequest.ContentLength ?? 0);
             activity.SetTag("http.request.user_agent", httpRequest.Headers.UserAgent.ToString());
             // ? UPDATED: Ensure cloud.role includes environment
             activity.SetTag("cloud.role", cloudRoleName);
         };
                options.EnrichWithHttpResponse = (activity, httpResponse) =>
               {
                   activity.SetTag("http.response.body.size", httpResponse.ContentLength ?? 0);
               };
            })
                    .AddHttpClientInstrumentation(options =>
                     {
                         options.RecordException = true;
                         options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
            {
                activity.SetTag("http.request.method", httpRequestMessage.Method.ToString());
                // ? UPDATED: Ensure cloud.role includes environment
                activity.SetTag("cloud.role", cloudRoleName);
            };
                         options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
             {
                 activity.SetTag("http.response.status_code", (int)httpResponseMessage.StatusCode);
             };
                     })
           .AddSqlClientInstrumentation(options =>
                   {
                       options.RecordException = true;
                       options.SetDbStatementForText = true;
                       // ? UPDATED: Ensure cloud.role includes environment
                       options.Enrich = (activity, eventName, rawObject) =>
              {
                  activity.SetTag("cloud.role", cloudRoleName);
              };
                   })
               .AddSource("SXG.EvalPlatform.API")
                   .SetSampler(new TraceIdRatioBasedSampler(samplingRatio));

               // Add console exporter if Enabled
               if (enableConsoleExporter)
               {
                   tracing.AddConsoleExporter();
               }

               // Add Application Insights exporter
               if (!string.IsNullOrEmpty(appInsightsConnectionString))
               {
                   tracing.AddAzureMonitorTraceExporter(options =>
                             {
                                 options.ConnectionString = appInsightsConnectionString;
                             });
               }
           })
            .WithMetrics(metrics =>
         {
             metrics
                  .AddAspNetCoreInstrumentation()
           .AddHttpClientInstrumentation()
          .AddMeter("SXG.EvalPlatform.API");

             // Add console exporter if Enabled
             if (enableConsoleExporter)
             {
                 metrics.AddConsoleExporter();
             }

             // Add Application Insights exporter
             if (!string.IsNullOrEmpty(appInsightsConnectionString))
             {
                 metrics.AddAzureMonitorMetricExporter(options =>
                       {
                           options.ConnectionString = appInsightsConnectionString;
                       });
             }
         });

        // Add Application Insights (traditional integration)
        if (!string.IsNullOrEmpty(appInsightsConnectionString))
        {
            services.AddApplicationInsightsTelemetry(options =>
              {
                  options.ConnectionString = appInsightsConnectionString;
                  options.EnableAdaptiveSampling = true;
                  options.EnableQuickPulseMetricStream = true;
                  options.EnableHeartbeat = true;
              });

            // ? UPDATED: Configure cloud role name with environment for Application Insights
            services.AddSingleton<ITelemetryInitializer>(new CloudRoleNameTelemetryInitializer(cloudRoleName));
        }

        // Add OpenTelemetry service
        services.AddSingleton<IOpenTelemetryService, OpenTelemetryService>();

        return services;
    }

    /// <summary>
    /// Configure Swagger/OpenAPI services
    /// </summary>
    public static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        // Build service provider to access configuration
        var serviceProvider = services.BuildServiceProvider();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        // Get Azure AD settings
        var clientId = configuration["AzureAd:ClientId"];
        var tenantId = configuration["AzureAd:TenantId"];
        var audience = configuration["AzureAd:Audience"] ?? $"api://{clientId}";

        services.AddSwaggerGen(c =>
   {
       c.SwaggerDoc("v1", new OpenApiInfo
       {
           Title = "SXG Evaluation Platform API",
           Version = "v1",
           Description = "API for SXG Evaluation Platform components with OpenTelemetry integration and Azure AD authentication",
           Contact = new OpenApiContact
           {
               Name = "SXG Team",
               Email = "sxg@microsoft.com"
           }
       });

       // Add JWT Bearer authentication to Swagger
       // Use Implicit Flow (simpler for Swagger UI, no client secret needed)
       c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
       {
           Type = SecuritySchemeType.OAuth2,
           Flows = new OpenApiOAuthFlows
           {
               Implicit = new OpenApiOAuthFlow
               {
                   AuthorizationUrl = new Uri($"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize"),
                   // ? REMOVED TokenUrl - not needed for Implicit flow
                   Scopes = new Dictionary<string, string>
         {
    { $"{audience}/EVALPlatformAPI.ReadWrite", "Read and write evaluations" }
       }
               }
           }
       });

       c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
       {
         new OpenApiSecurityScheme
         {
           Reference = new OpenApiReference
   {
      Type = ReferenceType.SecurityScheme,
     Id = "oauth2"
           }
 },
        new[] { $"{audience}/EVALPlatformAPI.ReadWrite" }
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

        return services;
    }

    /// <summary>
    /// Configure CORS policies
    /// </summary>
    public static IServiceCollection AddCorsServices(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
                     {
                         policy.AllowAnyOrigin()
                 .AllowAnyMethod()
           .AllowAnyHeader();
                     });
        });

        return services;
    }

    /// <summary>
    /// Configure AutoMapper services manually 
    /// </summary>
    public static IServiceCollection AddAutoMapperServices(this IServiceCollection services)
    {
        // Try the correct way for AutoMapper 12.0.1
        services.AddSingleton<IMapper>(serviceProvider =>
        {
            // Create the configuration expression first
            var configExpression = new MapperConfigurationExpression();
            configExpression.AddProfile<MappingProfile>();

            // Pass the configuration expression to the constructor
            var config = new MapperConfiguration(configExpression);
            return new Mapper(config);
        });

        return services;
    }

    /// <summary>
    /// Configure HTTP client services
    /// </summary>
    public static IServiceCollection AddHttpClientServices(this IServiceCollection services)
    {
        services.AddHttpClient<IDataVerseAPIService, DataVerseAPIService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "SXG-EvalPlatform-API/1.0");
        });

        return services;
    }

    /// <summary>
    /// Configure Service Bus client services
    /// </summary>
    public static IServiceCollection AddServiceBus(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAzureClients(clients =>
        {
            var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";

            Azure.Core.TokenCredential credential = CommonUtils.GetTokenCredential(environment);

            _ = clients.AddServiceBusClientWithNamespace(configuration.GetSection("ServiceBus")
                .GetValue<string>("EventBusConnection"))
                .WithCredential(credential);
            _ = clients.ConfigureDefaults(configuration.GetSection("AzureDefaults"));
        });
        _ = services.AddScoped<IMessagePublisher, MessagePublisher>();


        return services;
    }

    /// <summary>
    /// Configure business services (Storage, Request Handlers, etc.)
    /// </summary>
    public static IServiceCollection AddBusinessServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IConfigHelper, ConfigHelper>();

        // Storage services
        services.AddScoped<IAzureBlobStorageService, AzureBlobStorageService>();
        services.AddScoped<IAzureQueueStorageService, AzureQueueStorageService>();
        services.AddScoped<IDataVerseAPIService, DataVerseAPIService>();

        // Table services
        services.AddScoped<IMetricsConfigTableService, MetricsConfigTableService>();
        services.AddScoped<IDataSetTableService, DataSetTableService>();
        services.AddScoped<IEvalRunTableService, EvalRunTableService>();

        // Request handlers
        services.AddScoped<IMetricsConfigurationRequestHandler, MetricsConfigurationRequestHandler>();
        services.AddScoped<IDataSetRequestHandler, DataSetRequestHandler>();
        services.AddScoped<IEvalRunRequestHandler, EvalRunRequestHandler>();
        services.AddScoped<IEvaluationResultRequestHandler, EvaluationResultRequestHandler>();
        services.AddScoped<IEvalArtifactsRequestHandler, EvalArtifactsRequestHandler>();

        // Validators
        services.AddScoped<IEntityValidators, EntityValidators>();

        // Caller Identification Service
        services.AddScoped<ICallerIdentificationService, CallerIdentificationService>();

        // Cache services - IConfigHelper is now registered, so this will work
        services.AddCacheServices();

        return services;
    }
}