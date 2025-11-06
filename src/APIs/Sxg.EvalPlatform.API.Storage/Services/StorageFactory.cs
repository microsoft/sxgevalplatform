using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sxg.EvalPlatform.API.Storage.Services;

public class StorageFactory
{
    private readonly IConfiguration _config;
    private readonly ILogger<AzureBlobStorageServiceV2> _logger;

    public StorageFactory(IConfiguration config, ILogger<AzureBlobStorageServiceV2> logger)
    {
        _config = config;
        _logger = logger;
    }

    public IStorageProviderService GetProvider(string providerName)
    {
        return providerName.ToLower() switch
        {
            "azureblobstorage" => new AzureBlobStorageServiceV2(_config, _logger),
            _ => throw new ArgumentException("Invalid storage provider")
        };
    }
}
