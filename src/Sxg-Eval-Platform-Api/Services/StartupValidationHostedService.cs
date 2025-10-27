using Microsoft.Extensions.Hosting;

namespace SxgEvalPlatformApi.Services;

public class StartupValidationHostedService : IHostedService
{
    private readonly IStartupConnectionValidator _connectionValidator;
    private readonly ILogger<StartupValidationHostedService> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    public StartupValidationHostedService(
        IStartupConnectionValidator connectionValidator,
        ILogger<StartupValidationHostedService> logger,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        _connectionValidator = connectionValidator;
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting application with connection validation...");
            
            // Validate all connections before the application fully starts
            await _connectionValidator.ValidateConnectionsAsync();
            
            _logger.LogInformation("Application startup validation completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Application startup validation failed - stopping application");
            
            // Stop the application if validation fails
            _hostApplicationLifetime.StopApplication();
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Application shutting down...");
        return Task.CompletedTask;
    }
}