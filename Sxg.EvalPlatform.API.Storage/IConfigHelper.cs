namespace Sxg.EvalPlatform.API.Storage
{
    public interface IConfigHelper
    {
        string AppInsightsConnectionString();
        string EvalResultsFolderName();
        string GetAzureStorageAccountName();
        string GetDataSetFolderName();
        string GetDataSetsTable();
        string GetDefaultConfigurationBlob();
        string GetDefaultMetricsConfiguration();
        object GetMetricsConfigurationsFolderName();
        string GetMetricsConfigurationsTable();
        string GetPlatformConfigurationsContainer();
        string MetricsConfigurationsFolderName();
    }
}