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
    }
}
