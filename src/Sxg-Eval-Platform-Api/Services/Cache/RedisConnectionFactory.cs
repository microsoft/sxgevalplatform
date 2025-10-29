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
            ConnectRetry = 5,
            ConnectTimeout = 60000, // Increased to 60 seconds
            SyncTimeout = 60000,    // Increased to 60 seconds
            AsyncTimeout = 60000,   // Increased to 60 seconds
            KeepAlive = 60,
            ReconnectRetryPolicy = new ExponentialRetry(5000) // 5 second base delay with exponential backoff
        };

        // Configure Entra ID authentication (only method supported)
        await ConfigureAzureAdAuthenticationAsync(configurationOptions, logger, redisConfig);
        logger.LogInformation("Using Azure AD authentication");

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
            
            // For Azure Cache for Redis with Entra ID authentication:
            // - If User is specified, use it as the username with the token as password
            // - If no User is specified, try using the token directly
            if (!string.IsNullOrEmpty(redisConfig.User))
            {
                configOptions.User = redisConfig.User;
                configOptions.Password = token.Token;
                logger.LogInformation("Configured Azure AD authentication for user: {User}", redisConfig.User);
            }
            else
            {
                // Try token-only authentication
                configOptions.Password = token.Token;
                logger.LogInformation("Configured Azure AD authentication with token-only");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to configure Azure AD authentication. Ensure you're logged in with 'az login' and have access to the Redis instance");
            throw new InvalidOperationException("Azure AD authentication failed. Please run 'az login' to authenticate and ensure you have proper permissions.", ex);
        }
    }
}