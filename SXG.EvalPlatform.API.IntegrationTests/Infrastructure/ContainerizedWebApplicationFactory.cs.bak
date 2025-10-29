using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.Azurite;
using Testcontainers.Redis;
using DotNet.Testcontainers.Builders;
using Xunit;
using SxgEvalPlatformApi.Configuration;
using StackExchange.Redis;
using System.Linq;
using Moq;
using System.Linq;

namespace SXG.EvalPlatform.API.IntegrationTests.Infrastructure;

/// <summary>
/// Enhanced WebApplicationFactory with containerized Azure Storage (Azurite) and Redis
/// </summary>
public class ContainerizedWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private AzuriteContainer? _azuriteContainer;
    private RedisContainer? _redisContainer;
    private IConnectionMultiplexer? _redisConnection;

    public string AzuriteConnectionString { get; private set; } = string.Empty;
    public string RedisConnectionString { get; private set; } = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear existing configuration sources to ensure our test config takes precedence
            config.Sources.Clear();
            
            // Override with test-specific configuration
            var testConfig = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=true;",
                ["AzureStorage:ConnectionString"] = AzuriteConnectionString,
                ["AzureStorage:AccountName"] = "devstoreaccount1", 
                ["AzureStorage:DataSetFolderName"] = "test-datasets",
                ["AzureStorage:DatasetsFolderName"] = "test-datasets",
                ["AzureStorage:EvalResultsFolderName"] = "test-eval-results",
                ["AzureStorage:MetricsConfigurationsFolderName"] = "test-metrics-configurations",
                ["AzureStorage:PlatformConfigurationsContainer"] = "test-platform-configurations",
                ["AzureStorage:MetricsConfigurationsTable"] = "TestMetricsConfigurationsTable",
                ["AzureStorage:DataSetsTable"] = "TestDataSetsTable",
                ["AzureStorage:EvalRunTable"] = "TestEvalRunsTable",
                ["Redis:Hostname"] = RedisConnectionString,
                ["Redis:User"] = null,
                ["Redis:Cache:KeyPrefix"] = "test-sxg-eval",
                ["Redis:Cache:DefaultTtlMinutes"] = "60",
                ["Redis:Cache:MaxMemoryMB"] = "100",
                ["Redis:Cache:EnableCompression"] = "false",
                ["Environment"] = "Testing",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning"
            };

            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove real Redis connection
            services.RemoveAll<IConnectionMultiplexer>();
            
            // Remove startup validation services to prevent Azure connection validation
            services.RemoveAll<SxgEvalPlatformApi.Services.IStartupConnectionValidator>();
            
            // Remove the startup validation hosted service manually
            var hostedServiceDescriptor = services.FirstOrDefault(d => 
                d.ImplementationType == typeof(SxgEvalPlatformApi.Services.StartupValidationHostedService));
            if (hostedServiceDescriptor != null)
            {
                services.Remove(hostedServiceDescriptor);
            }
            
            // Add test Redis connection directly  
            services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
            {
                if (_redisConnection == null)
                    throw new InvalidOperationException("Redis connection not initialized");
                return _redisConnection;
            });

            // Replace Azure Storage services with simple mocks for testing
            // (We'll use mocks instead of real Azurite implementations for now)
            services.RemoveAll<Sxg.EvalPlatform.API.Storage.Services.IMetricsConfigTableService>();
            services.RemoveAll<Sxg.EvalPlatform.API.Storage.Services.IAzureBlobStorageService>();
            services.RemoveAll<Sxg.EvalPlatform.API.Storage.Services.IDataSetTableService>();
            services.RemoveAll<Sxg.EvalPlatform.API.Storage.Services.IEvalRunTableService>();

            // Add mock implementations that return success without actual storage
            services.AddSingleton<Sxg.EvalPlatform.API.Storage.Services.IMetricsConfigTableService>(serviceProvider =>
            {
                var mock = new Mock<Sxg.EvalPlatform.API.Storage.Services.IMetricsConfigTableService>();
                
                // Mock the main method used in CreateConfiguration
                mock.Setup(x => x.GetAllMetricsConfigurations(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync(new List<Sxg.EvalPlatform.API.Storage.TableEntities.MetricsConfigurationTableEntity>());
                
                mock.Setup(x => x.SaveMetricsConfigurationAsync(It.IsAny<Sxg.EvalPlatform.API.Storage.TableEntities.MetricsConfigurationTableEntity>()))
                    .ReturnsAsync((Sxg.EvalPlatform.API.Storage.TableEntities.MetricsConfigurationTableEntity entity) => entity);
                
                return mock.Object;
            });

            services.AddSingleton<Sxg.EvalPlatform.API.Storage.Services.IAzureBlobStorageService>(serviceProvider =>
            {
                var mock = new Mock<Sxg.EvalPlatform.API.Storage.Services.IAzureBlobStorageService>();
                // Return mock that doesn't fail
                return mock.Object;
            });

            services.AddSingleton<Sxg.EvalPlatform.API.Storage.Services.IDataSetTableService>(serviceProvider =>
            {
                var mock = new Mock<Sxg.EvalPlatform.API.Storage.Services.IDataSetTableService>();
                return mock.Object;
            });

            services.AddSingleton<Sxg.EvalPlatform.API.Storage.Services.IEvalRunTableService>(serviceProvider =>
            {
                var mock = new Mock<Sxg.EvalPlatform.API.Storage.Services.IEvalRunTableService>();
                return mock.Object;
            });
            
            // Override logging to reduce noise in tests
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            });

            // Update Redis configuration to match test container
            services.Configure<SxgEvalPlatformApi.Configuration.RedisConfiguration>(opt =>
            {
                opt.Hostname = RedisConnectionString;
                opt.User = null;
                opt.Cache = new SxgEvalPlatformApi.Configuration.CacheConfiguration
                {
                    KeyPrefix = "test-sxg-eval",
                    DefaultTtlMinutes = 60,
                    MaxMemoryMB = 100,
                    EnableCompression = false
                };
            });
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        // Start Azurite container for Azure Storage emulation
        _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .WithPortBinding(10000, true) // Blob service
            .WithPortBinding(10001, true) // Queue service
            .WithPortBinding(10002, true) // Table service
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(10000))
            .WithCleanUp(true)
            .Build();

        await _azuriteContainer.StartAsync();

        // Build Azurite connection string
        var blobPort = _azuriteContainer.GetMappedPublicPort(10000);
        var queuePort = _azuriteContainer.GetMappedPublicPort(10001);
        var tablePort = _azuriteContainer.GetMappedPublicPort(10002);

        AzuriteConnectionString = $"DefaultEndpointsProtocol=http;" +
                                $"AccountName=devstoreaccount1;" +
                                $"AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
                                $"BlobEndpoint=http://127.0.0.1:{blobPort}/devstoreaccount1;" +
                                $"QueueEndpoint=http://127.0.0.1:{queuePort}/devstoreaccount1;" +
                                $"TableEndpoint=http://127.0.0.1:{tablePort}/devstoreaccount1;";

        // Start Redis container
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
            .WithCleanUp(true)
            .Build();

        await _redisContainer.StartAsync();

        var redisPort = _redisContainer.GetMappedPublicPort(6379);
        RedisConnectionString = $"127.0.0.1:{redisPort}";

        // Verify Redis connection
        _redisConnection = ConnectionMultiplexer.Connect(RedisConnectionString);
        await _redisConnection.GetDatabase().PingAsync();
    }

    public new async Task DisposeAsync()
    {
        _redisConnection?.Dispose();
        
        if (_azuriteContainer != null)
        {
            await _azuriteContainer.DisposeAsync();
        }
        
        if (_redisContainer != null)
        {
            await _redisContainer.DisposeAsync();
        }
    }

    /// <summary>
    /// Get Redis database for test verification
    /// </summary>
    public IDatabase GetRedisDatabase()
    {
        if (_redisConnection == null)
            throw new InvalidOperationException("Redis connection not initialized");
        
        return _redisConnection.GetDatabase();
    }
}

/// <summary>
/// Extension methods for service collection manipulation
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection RemoveAll<T>(this IServiceCollection services)
    {
        var servicesToRemove = services.Where(x => x.ServiceType == typeof(T)).ToList();
        foreach (var service in servicesToRemove)
        {
            services.Remove(service);
        }
        return services;
    }
}