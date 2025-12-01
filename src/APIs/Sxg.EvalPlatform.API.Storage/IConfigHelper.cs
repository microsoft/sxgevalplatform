namespace Sxg.EvalPlatform.API.Storage
{
    public interface IConfigHelper
    {
        string AppInsightsConnectionString();
        string EvalResultsFolderName();
        string GetAzureStorageAccountName();
        string GetDataSetFolderName();
        string GetDatasetsFolderName();
        string GetDataSetsTable();
        string GetDefaultConfigurationBlob();
        string GetDefaultMetricsConfiguration();
        object GetMetricsConfigurationsFolderName();
        string GetMetricsConfigurationsTable();
        string GetPlatformConfigurationsContainer();
        string MetricsConfigurationsFolderName();
        string GetDatasetEnrichmentRequestsQueueName();
        string GetEvalProcessingRequestsQueueName();
        string GetDatasetEnrichmentRequestAPIEndPoint();
        string GetDataVerseAPIScope();

        // Cache configuration methods
        string GetCacheProvider();
        string? GetRedisCacheEndpoint();
        TimeSpan GetDefaultCacheExpiration();
        bool IsDistributedCacheEnabled();
        string GetASPNetCoreEnvironment();
        string GetEvalRunTableName();

        /// <summary>
        /// Determines if caching is enabled based on the cache provider setting
        /// Returns true if provider is "Memory" or "Redis", false if "None" or "Disabled"
        /// </summary>
        bool IsCachingEnabled();

        // Configuration helper for binding sections (needed for CacheOptions)
        T GetConfigurationSection<T>(string sectionName) where T : class, new();
    }
}