namespace Sxg.EvalPlatform.API.Storage.Configuration
{
    /// <summary>
    /// Configuration options for cache settings
    /// </summary>
    public class CacheOptions
    {
        /// <summary>
        /// Cache provider type: "Memory" or "Redis"
        /// </summary>
        public string Provider { get; set; } = "Memory";

        /// <summary>
        /// Default cache expiration time in minutes
        /// </summary>
        public int DefaultExpirationMinutes { get; set; } = 30;

        /// <summary>
        /// Redis-specific configuration
        /// </summary>
        public RedisOptions Redis { get; set; } = new();

        /// <summary>
        /// Memory cache specific configuration
        /// </summary>
        public MemoryCacheOptions Memory { get; set; } = new();
    }

    /// <summary>
    /// Redis cache configuration options
    /// </summary>
    public class RedisOptions
    {
        /// <summary>
        /// Redis cache endpoint (for managed identity authentication)
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Redis cache instance name (optional)
        /// </summary>
        public string? InstanceName { get; set; }

        /// <summary>
        /// Use managed identity for authentication
        /// </summary>
        public bool UseManagedIdentity { get; set; } = true;

        /// <summary>
        /// Connection timeout in seconds
        /// </summary>
        public int ConnectTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Command timeout in seconds
        /// </summary>
        public int CommandTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Enable SSL connection
        /// </summary>
        public bool UseSsl { get; set; } = true;

        /// <summary>
        /// Retry policy settings
        /// </summary>
        public RedisRetryOptions Retry { get; set; } = new();
    }

    /// <summary>
    /// Memory cache configuration options
    /// </summary>
    public class MemoryCacheOptions
    {
        /// <summary>
        /// Maximum memory cache size in MB (0 = unlimited)
        /// </summary>
        public int SizeLimitMB { get; set; } = 500;

        /// <summary>
        /// Compaction percentage (0.0 to 1.0)
        /// </summary>
        public double CompactionPercentage { get; set; } = 0.25;

        /// <summary>
        /// Scan frequency for expired items cleanup in seconds
        /// </summary>
        public int ExpirationScanFrequencySeconds { get; set; } = 60;
    }

    /// <summary>
    /// Redis retry policy options
    /// </summary>
    public class RedisRetryOptions
    {
        /// <summary>
        /// Enable retry policy
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum retry attempts
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Base delay between retries in milliseconds
        /// </summary>
        public int BaseDelayMs { get; set; } = 1000;

        /// <summary>
        /// Maximum delay between retries in milliseconds
        /// </summary>
        public int MaxDelayMs { get; set; } = 5000;
    }
}