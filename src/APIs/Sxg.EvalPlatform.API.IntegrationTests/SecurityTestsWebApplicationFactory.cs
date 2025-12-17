using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Moq;
using SxgEvalPlatformApi.Services;
using Sxg.EvalPlatform.API.Storage.Services;
using Azure.Messaging.ServiceBus;
using SxgEvalPlatformApi.RequestHandlers;
using Sxg.EvalPlatform.API.Storage.Entities;

namespace Sxg.EvalPlatform.API.IntegrationTests;

/// <summary>
/// Custom WebApplicationFactory for security tests that bypasses S2S authentication
/// and mocks all Azure services to avoid actual cloud dependencies
/// </summary>
public class SecurityTestsWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Set Test environment
            context.HostingEnvironment.EnvironmentName = "Test";
            
            // Add test configuration
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FeatureFlags:EnableAuthentication"] = "false", // Disable authentication for tests
                ["ConnectionStrings:DefaultConnection"] = "test-connection",
                ["AzureStorage:AccountName"] = "testaccount",
                ["AzureStorage:ContainerName"] = "testcontainer",
                ["ServiceBus:EventBusConnection"] = "test-servicebus"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove real authentication and add test authentication
            var authDescriptors = services
                .Where(d => d.ServiceType == typeof(IAuthenticationService) ||
                           d.ServiceType.Name.Contains("Authentication"))
                .ToList();
            
            foreach (var descriptor in authDescriptors)
            {
                services.Remove(descriptor);
            }

            // Add test authentication scheme
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
                options.DefaultScheme = "Test";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                "Test", options => { });

            // Configure authorization to allow all requests
            services.AddAuthorization(options =>
            {
                options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                    .RequireAssertion(_ => true)
                    .Build();
            });

            // Mock CallerIdentificationService
            var mockCallerService = new Mock<ICallerIdentificationService>();
            mockCallerService.Setup(x => x.GetCurrentUserId()).Returns("test-user-id");
            mockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns("test@example.com");
            mockCallerService.Setup(x => x.GetCurrentTenantId()).Returns("test-tenant-id");
            mockCallerService.Setup(x => x.GetCallingApplicationId()).Returns("test-app-id");
            mockCallerService.Setup(x => x.GetCallingApplicationName()).Returns("test-app");
            mockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(false);
            mockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);
            mockCallerService.Setup(x => x.GetCallerDescription()).Returns("Test User (test@example.com)");

            RemoveAndReplace<ICallerIdentificationService>(services, mockCallerService.Object);

            // Mock OpenTelemetryService
            var mockOtelService = new Mock<IOpenTelemetryService>();
            RemoveAndReplace<IOpenTelemetryService>(services, mockOtelService.Object, ServiceLifetime.Singleton);

            // Mock Azure Storage Services (they would try to connect to real Azure)
            var mockBlobService = new Mock<IAzureBlobStorageService>();
            mockBlobService.Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            mockBlobService.Setup(x => x.BlobExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);
            RemoveAndReplace<IAzureBlobStorageService>(services, mockBlobService.Object);

            var mockQueueService = new Mock<IAzureQueueStorageService>();
            mockQueueService.Setup(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            RemoveAndReplace<IAzureQueueStorageService>(services, mockQueueService.Object);

            var mockDataVerseService = new Mock<IDataVerseAPIService>();
            mockDataVerseService.Setup(x => x.PostEvalRunAsync(It.IsAny<DataVerseApiRequest>()))
                .ReturnsAsync(new DataVerseApiResponse { Success = true, StatusCode = 200 });
            RemoveAndReplace<IDataVerseAPIService>(services, mockDataVerseService.Object);

            // Mock Table Services (they would try to connect to Azure Table Storage)
            var mockMetricsConfigTable = new Mock<IMetricsConfigTableService>();
            RemoveAndReplace<IMetricsConfigTableService>(services, mockMetricsConfigTable.Object);

            var mockDataSetTable = new Mock<IDataSetTableService>();
            RemoveAndReplace<IDataSetTableService>(services, mockDataSetTable.Object);

            var mockEvalRunTable = new Mock<IEvalRunTableService>();
            RemoveAndReplace<IEvalRunTableService>(services, mockEvalRunTable.Object);

            // Mock Service Bus Client and Message Publisher
            var mockServiceBusClient = new Mock<ServiceBusClient>();
            RemoveAndReplace<ServiceBusClient>(services, mockServiceBusClient.Object);

            var mockMessagePublisher = new Mock<IMessagePublisher>();
            mockMessagePublisher.Setup(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            RemoveAndReplace<IMessagePublisher>(services, mockMessagePublisher.Object);

            // Mock IMetricsConfigurationRequestHandler to return sample data
            var mockMetricsConfigHandler = new Mock<IMetricsConfigurationRequestHandler>();
            mockMetricsConfigHandler.Setup(x => x.GetDefaultMetricsConfigurationAsync())
                .ReturnsAsync(new DefaultMetricsConfiguration
                {
                    Version = "1.0.0",
                    LastUpdated = DateTime.UtcNow,
                    Categories = new List<Category>
                    {
                        new Category
                        {
                            CategoryName = "Quality",
                            DisplayName = "Quality Metrics",
                            Description = "Quality-related metrics",
                            Metrics = new List<Metric>
                            {
                                new Metric
                                {
                                    MetricName = "coherence",
                                    DisplayName = "Coherence",
                                    Description = "Measures coherence of responses",
                                    DefaultThreshold = 0.7,
                                    ScoreRange = new ScoreRange { Min = 0, Max = 1 },
                                    Enabled = true,
                                    IsMandatory = false
                                }
                            }
                        }
                    }
                });
            
            mockMetricsConfigHandler.Setup(x => x.GetMetricsConfigurationByConfigurationIdAsync(It.IsAny<string>()))
                .ReturnsAsync((string _) => null); // Return null for non-existent configs
            
            RemoveAndReplace<IMetricsConfigurationRequestHandler>(services, mockMetricsConfigHandler.Object);
        });

        builder.UseEnvironment("Test");
    }

    /// <summary>
    /// Helper method to remove existing service and add mock
    /// </summary>
    private void RemoveAndReplace<TService>(IServiceCollection services, TService mockInstance, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TService : class
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }
        
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                services.AddSingleton(_ => mockInstance);
                break;
            case ServiceLifetime.Transient:
                services.AddTransient(_ => mockInstance);
                break;
            default:
                services.AddScoped(_ => mockInstance);
                break;
        }
    }
}

/// <summary>
/// Test authentication handler that always succeeds
/// </summary>
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

#if NET8_0_OR_GREATER
    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }
#endif

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, "test@example.com"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim("tid", "test-tenant-id"),
            new Claim("oid", "test-object-id"),
            new Claim("appid", "test-app-id"),
            new Claim("scope", "eval.read eval.write")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        var result = AuthenticateResult.Success(ticket);
        return Task.FromResult(result);
    }
}
