using Azure.Identity;
using Microsoft.Extensions.Options;
using SxgEvalPlatformApi.Configuration;
using StackExchange.Redis;

namespace SxgEvalPlatformApi.Services.Cache;

public static class RedisConnectionFactory
{
    public static async Task<IConnectionMultiplexer> CreateConnectionAsync(IOptions<RedisConfiguration> config, ILogger logger)
    {
        var redisConfig = config.Value;
        
        logger.LogInformation("Connecting to Redis at {Hostname}", redisConfig.Hostname);

        var configurationOptions = new ConfigurationOptions
        {
            EndPoints = { $"{redisConfig.Hostname}:6380" },
            Ssl = true,
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
            AbortOnConnectFail = false,
            ConnectRetry = 3,
            ConnectTimeout = 30000,
            SyncTimeout = 30000,
            AsyncTimeout = 30000
        };

        // Configure authentication
        if (!string.IsNullOrEmpty(redisConfig.AccessKey))
        {
            // Use access key authentication
            configurationOptions.Password = redisConfig.AccessKey;
            logger.LogInformation("Using access key authentication");
        }
        else
        {
            // Use Azure AD authentication
            await ConfigureAzureAdAuthenticationAsync(configurationOptions, logger, redisConfig);
            logger.LogInformation("Using Azure AD authentication");
        }

        try
        {
            var connection = await ConnectionMultiplexer.ConnectAsync(configurationOptions);
            
            // Test the connection
            var database = connection.GetDatabase();
            await database.PingAsync();
            
            logger.LogInformation("Successfully connected to Redis");
            return connection;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to Redis at {Hostname}", redisConfig.Hostname);
            throw new InvalidOperationException($"Unable to connect to Redis at {redisConfig.Hostname}. Please check your configuration and network connectivity.", ex);
        }
    }

    private static async Task ConfigureAzureAdAuthenticationAsync(ConfigurationOptions configOptions, ILogger logger, RedisConfiguration redisConfig)
    {
        try
        {
            var credential = new DefaultAzureCredential();
            
            // Get access token for Azure Cache for Redis
            var tokenRequestContext = new Azure.Core.TokenRequestContext(new[] { "https://redis.azure.com/.default" });
            var token = await credential.GetTokenAsync(tokenRequestContext);
            
            // For Azure Redis Cache with Azure AD: Username = configured User, Password = Token
            configOptions.User = redisConfig.User ?? throw new InvalidOperationException("Redis User is not configured for Azure AD authentication");
            configOptions.Password = token.Token;
            
            // Removed the success log to reduce verbosity
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to configure Azure AD authentication. Ensure you're logged in with 'az login'");
            throw new InvalidOperationException("Azure AD authentication failed. Please run 'az login' to authenticate.", ex);
        }
    }
}