using Sxg.EvalPlatform.API.Storage.Services;
using System.Collections.Concurrent;

namespace SXG.EvalPlatform.API.IntegrationTests.Infrastructure
{
    /// <summary>
    /// In-memory implementation of IAzureBlobStorageService for integration testing
    /// </summary>
    public class InMemoryAzureBlobStorageService : IAzureBlobStorageService
    {
        private readonly ConcurrentDictionary<string, string> _blobs = new();

        public InMemoryAzureBlobStorageService()
        {
            // Seed with default configuration blob
            var defaultConfigKey = GetBlobKey("platform-configurations", "default-metric-configuration.json");
            var defaultConfig = """
            {
                "configurationName": "Default Configuration",
                "environmentName": "Production",
                "description": "Default metrics configuration for testing",
                "metricsConfiguration": [
                    {
                        "metricName": "BLEU",
                        "threshold": 0.7
                    },
                    {
                        "metricName": "ROUGE",
                        "threshold": 0.65
                    }
                ]
            }
            """;
            _blobs.TryAdd(defaultConfigKey, defaultConfig);
        }

        private string GetBlobKey(string containerName, string blobName)
        {
            return $"{containerName}/{blobName}";
        }

        public async Task<string?> ReadBlobContentAsync(string containerName, string blobName)
        {
            await Task.Yield();
            
            var key = GetBlobKey(containerName, blobName);
            _blobs.TryGetValue(key, out var content);
            return content;
        }

        public async Task<bool> WriteBlobContentAsync(string containerName, string blobName, string content)
        {
            await Task.Yield();
            
            var key = GetBlobKey(containerName, blobName);
            _blobs.AddOrUpdate(key, content, (k, oldValue) => content);
            return true;
        }

        public async Task<bool> BlobExistsAsync(string containerName, string blobName)
        {
            await Task.Yield();
            
            var key = GetBlobKey(containerName, blobName);
            return _blobs.ContainsKey(key);
        }

        public async Task<bool> DeleteBlobAsync(string containerName, string blobName)
        {
            await Task.Yield();
            
            var key = GetBlobKey(containerName, blobName);
            return _blobs.TryRemove(key, out _);
        }

        public async Task<List<string>> ListBlobsAsync(string containerName, string prefix)
        {
            await Task.Yield();
            
            var containerPrefix = $"{containerName}/";
            var fullPrefix = $"{containerPrefix}{prefix}";
            
            var matchingBlobs = _blobs.Keys
                .Where(key => key.StartsWith(fullPrefix))
                .Select(key => key.Substring(containerPrefix.Length))
                .ToList();
                
            return matchingBlobs;
        }
    }
}