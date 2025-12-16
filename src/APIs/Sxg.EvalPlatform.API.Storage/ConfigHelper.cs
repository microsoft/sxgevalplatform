using Microsoft.Extensions.Configuration;

namespace Sxg.EvalPlatform.API.Storage
{
    public class ConfigHelper : IConfigHelper
    {
        IConfiguration _configuration;

        public ConfigHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GetAzureStorageAccountName()
        {
            var accountName = _configuration["AzureStorage:AccountName"];
            if (string.IsNullOrEmpty(accountName))
            {
                throw new InvalidOperationException("Azure Storage connection string is not configured.");
            }
            return accountName;
        }

        public string GetPlatformConfigurationsContainer()
        {
            var containerName = _configuration["AzureStorage:PlatformConfigurationsContainer"];
            if (string.IsNullOrEmpty(containerName))
            {
                throw new InvalidOperationException("Azure Storage Platform Configurations container name is not configured.");
            }
            return containerName;
        }

        public string GetDefaultMetricsConfiguration()
        {
            var defaultMetricsConfig = _configuration["AzureStorage:DefaultMetricsConfiguration"];
            if (string.IsNullOrEmpty(defaultMetricsConfig))
            {
                throw new InvalidOperationException("Default Metrics Configuration is not configured.");
            }
            return defaultMetricsConfig;
        }

        public string GetDefaultConfigurationBlob()
        {
            var blobFileName = _configuration["AzureStorage:DefaultConfigurationBlob"];
            if (string.IsNullOrEmpty(blobFileName))
            {
                throw new InvalidOperationException("Azure Storage Default Configuration Blob name is not configured.");
            }
            return blobFileName;
        }

        public string GetMetricsConfigurationsTable()
        {
            var tableName = _configuration["AzureStorage:MetricsConfigurationsTable"];
            if (string.IsNullOrEmpty(tableName))
            {
                throw new InvalidOperationException("Azure Storage Metrics Configurations table name is not configured.");
            }
            return tableName;
        }

        public string GetDataSetsTable()
        {
            var tableName = _configuration["AzureStorage:DataSetsTable"];
            if (string.IsNullOrEmpty(tableName))
            {
                throw new InvalidOperationException("Azure Storage Data Sets table name is not configured.");
            }
            return tableName;
        }

        public string GetDataSetFolderName()
        {
            var folderName = _configuration["AzureStorage:DataSetFolderName"];
            if (string.IsNullOrEmpty(folderName))
            {
                throw new InvalidOperationException("Azure Storage Data Set folder name is not configured.");
            }
            return folderName;
        }

        public string GetDatasetsFolderName()
        {
            var folderName = _configuration["AzureStorage:DatasetsFolderName"] ?? "datasets";
            return folderName;
        }

        public string EvalResultsFolderName()
        {
            var folderName = _configuration["AzureStorage:EvalResultsFolderName"];
            if (string.IsNullOrEmpty(folderName))
            {
                throw new InvalidOperationException("Azure Storage Eval Results folder name is not configured.");
            }
            return folderName;
        }

        public string MetricsConfigurationsFolderName()
        {
            var folderName = _configuration["AzureStorage:MetricsConfigurationsFolderName"];
            if (string.IsNullOrEmpty(folderName))
            {
                throw new InvalidOperationException("Azure Storage Metrics Configurations folder name is not configured.");
            }
            return folderName;
        }

        public string AppInsightsConnectionString()
        {
            var appInsightsConnectionString = _configuration["Telemetry:AppInsightsConnectionString"];
            if (string.IsNullOrEmpty(appInsightsConnectionString))
            {
                throw new InvalidOperationException("Application Insights connection string is not configured.");
            }
            return appInsightsConnectionString;
        }

        public object GetMetricsConfigurationsFolderName()
        {
            var metricsConfigurationFolderName = _configuration["AzureStorage:MetricsConfigurationsFolderName"];
            if (string.IsNullOrEmpty(metricsConfigurationFolderName))
            {
                throw new InvalidOperationException("Application Insights connection string is not configured.");
            }
            return metricsConfigurationFolderName;
        }

        public string GetDatasetEnrichmentRequestsQueueName()
        {
            var queueName = _configuration["AzureStorage:DatasetEnrichmentRequestsQueueName"];
            if (string.IsNullOrEmpty(queueName))
            {
                throw new InvalidOperationException("Azure Storage Dataset Enrichment Requests queue name is not configured.");
            }
            return queueName;
        }

        public string GetEvalProcessingRequestsQueueName()
        {
            var queueName = _configuration["AzureStorage:EvalProcessingRequestsQueueName"];
            if (string.IsNullOrEmpty(queueName))
            {
                throw new InvalidOperationException("Azure Storage Eval Processing Requests queue name is not configured.");
            }
            return queueName;
        }

        public string GetDatasetEnrichmentRequestAPIEndPoint()
        {
            var endpoint = _configuration["DataVerseAPI:DatasetEnrichmentRequestAPIEndPoint"];
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new InvalidOperationException("DataVerse API Endpoint is not configured.");
            }
            return endpoint;
        }

        public string GetDataVerseAPIScope()
        {
            var scope = _configuration["DataVerseAPI:Scope"];
            if (string.IsNullOrEmpty(scope))
            {
                throw new InvalidOperationException("DataVerse API scope is not configured.");
            }
            return scope;
        }

        // Cache configuration methods
        public string GetCacheProvider()
        {
            var provider = _configuration["Cache:Provider"] ?? "Memory";
            return provider;
        }

        public string? GetRedisCacheEndpoint()
        {
            return _configuration["Cache:Redis:Endpoint"];
        }

        public TimeSpan GetDefaultCacheExpiration()
        {
            var expirationMinutes = _configuration.GetValue<int>("Cache:DefaultExpirationMinutes", 30);
            return TimeSpan.FromMinutes(expirationMinutes);
        }

        public bool IsDistributedCacheEnabled()
        {
            return string.Equals(GetCacheProvider(), "Redis", StringComparison.OrdinalIgnoreCase);
        }

        public string GetASPNetCoreEnvironment()
        {
            // Try to get from environment variable first (Azure App Settings)
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"];
            
            // Fallback to default if not set
            return environment ?? "Production";
        }

        public string GetEvalRunTableName()
        {
            var tableName = _configuration["AzureStorage:EvalRunsTable"];
            if (string.IsNullOrEmpty(tableName))
            {
                throw new InvalidOperationException("Azure Storage Eval Runs table name is not configured.");
            }
            return tableName;
        }

        public bool GetEnablePublishingEvalResultsToDataPlatform()
        {
            return _configuration.GetValue<bool>("FeatureFlags:EnablePublishingEvalResultsToDataPlatform", true);
        }

        /// <summary>
        /// Determines if caching is enabled based on the cache provider setting
        /// Returns true if provider is "Memory" or "Redis", false if "None" or "Disabled"
        /// </summary>
        /// <returns>True if caching is enabled, false otherwise</returns>
        public bool IsCachingEnabled()
        {
            var provider = GetCacheProvider().ToLowerInvariant();
            return provider != "none" && provider != "disabled" && provider != "";
        }

        /// <summary>
        /// Legacy method for backward compatibility - now uses cache provider setting
        /// </summary>
        /// <returns>True if caching is enabled based on provider, false otherwise</returns>
        [Obsolete("Use IsCachingEnabled() instead. This method now delegates to cache provider setting.")]
        public bool IsDataCachingEnabled()
        {
            return IsCachingEnabled();
        }

        /// <summary>
        /// Gets a strongly-typed configuration section
        /// </summary>
        /// <typeparam name="T">The type to bind the configuration section to</typeparam>
        /// <param name="sectionName">The name of the configuration section</param>
        /// <returns>The bound configuration object</returns>
        public T GetConfigurationSection<T>(string sectionName) where T : class, new()
        {
            var configObject = new T();
            _configuration.GetSection(sectionName).Bind(configObject);
            return configObject;
        }
    }
}
