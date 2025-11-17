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

        // Feature flags
        bool IsDataCachingEnabled();

        // Configuration helper for binding sections (needed for CacheOptions)
        T GetConfigurationSection<T>(string sectionName) where T : class, new();
    }
}