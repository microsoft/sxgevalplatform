using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.API.Storage.Services
{


    /// <summary>
    /// Service for Azure Blob Storage operations with caching support
    /// </summary>
    public class AzureBlobStorageService : IAzureBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<AzureBlobStorageService> _logger;
        private readonly ICacheManager _cacheManager;
        private readonly string _accountName;
        IConfigHelper _configHelper; 

        public AzureBlobStorageService(IConfigHelper configHelper, ICacheManager cacheManager, ILogger<AzureBlobStorageService> logger)
        {
            _logger = logger;

            _accountName = configHelper.GetAzureStorageAccountName();
            _cacheManager = cacheManager;
            _configHelper = configHelper;

            if (string.IsNullOrEmpty(_accountName))
            {
                throw new ArgumentException("Azure Storage account name is not configured");
            }

            var blobUri = $"https://{_accountName}.blob.core.windows.net";

            var environment = configHelper.GetASPNetCoreEnvironment();
            TokenCredential credential = CommonUtils.GetTokenCredential(environment);

            // Use DefaultAzureCredential for managed identity
            _blobServiceClient = new BlobServiceClient(new Uri(blobUri), credential);
            _logger.LogInformation("Azure Blob Storage service initialized with managed identity for account: {AccountName}", _accountName);
        }

        /// <inheritdoc />
        public async Task<string?> ReadBlobContentAsync(string containerName, string blobName)
        {
            var cacheKey = GenerateBlobCacheKey(containerName, blobName);

            try
            {
                _logger.LogDebug("Reading blob content from container: {ContainerName}, blob: {BlobName}",
                   CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));

                // Try to get from cache first
                var cachedContent = await _cacheManager.GetAsync<BlobContentCache>(cacheKey);
                if (cachedContent != null)
                {
                    _logger.LogDebug("Cache HIT for blob {ContainerName}/{BlobName}", CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));
                    return cachedContent.Content;
                }

                _logger.LogDebug("Cache MISS for blob {ContainerName}/{BlobName}", CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));

                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToLower());
                var blobClient = containerClient.GetBlobClient(blobName);

                if (!await blobClient.ExistsAsync())
                {
                    _logger.LogWarning("Blob not found: {ContainerName}/{BlobName}", CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));
                    return null;
                }

                var response = await blobClient.DownloadContentAsync();
                var content = response.Value.Content.ToString();

                // Cache the content
                await _cacheManager.SetAsync(cacheKey, new BlobContentCache { Content = content }, _configHelper.GetDefaultCacheExpiration());

                _logger.LogInformation("Successfully read and cached blob content from {ContainerName}/{BlobName}",
                  CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));

                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read blob content from {ContainerName}/{BlobName}",
                  CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));

                // Invalidate cache on error
                await _cacheManager.RemoveAsync(cacheKey);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> WriteBlobContentAsync(string containerName, string blobName, string content)
        {
            var cacheKey = GenerateBlobCacheKey(containerName, blobName);
            var listCacheKey = GenerateListCacheKey(containerName, GetBlobPrefix(blobName));

            try
            {
                _logger.LogInformation("Writing blob content to container: {ContainerName}, blob: {BlobName}",
                       CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));

                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToLower());

                // Create container if it doesn't exist
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                var blobClient = containerClient.GetBlobClient(blobName);

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                await blobClient.UploadAsync(stream, overwrite: true);

                // Update cache with new content
                await _cacheManager.SetAsync(cacheKey, new BlobContentCache { Content = content }, _configHelper.GetDefaultCacheExpiration());

                // Invalidate list cache for this container/prefix since a new blob was added or updated
                await InvalidateListCache(containerName, blobName);

                _logger.LogInformation("Successfully wrote and cached blob content to {ContainerName}/{BlobName}",
                  CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write blob content to {ContainerName}/{BlobName}",
                            CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));

                // Invalidate cache on error
                await _cacheManager.RemoveAsync(cacheKey);
                await InvalidateListCache(containerName, blobName);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> BlobExistsAsync(string containerName, string blobName)
        {
            var cacheKey = GenerateBlobExistsCacheKey(containerName, blobName);

            try
            {
                // Try to get from cache first
                var cachedExists = await _cacheManager.GetAsync<BlobExistsCache>(cacheKey);
                if (cachedExists != null)
                {
                    _logger.LogDebug("Cache HIT for blob exists check {ContainerName}/{BlobName}", CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));
                    return cachedExists.Exists;
                }

                _logger.LogDebug("Cache MISS for blob exists check {ContainerName}/{BlobName}", CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));

                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToLower());
                var blobClient = containerClient.GetBlobClient(blobName);

                var response = await blobClient.ExistsAsync();
                var exists = response.Value;

                // Cache the existence check (shorter expiration for exists checks)
                await _cacheManager.SetAsync(cacheKey, new BlobExistsCache { Exists = exists }, TimeSpan.FromMinutes(5));

                _logger.LogDebug("Blob exists check cached for {ContainerName}/{BlobName}: {Exists}",
                   CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName), exists);

                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check blob existence for {ContainerName}/{BlobName}",
        CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));

                // Invalidate cache on error
                await _cacheManager.RemoveAsync(cacheKey);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteBlobAsync(string containerName, string blobName)
        {
            var cacheKey = GenerateBlobCacheKey(containerName, blobName);
            var existsCacheKey = GenerateBlobExistsCacheKey(containerName, blobName);

            try
            {
                _logger.LogInformation("Deleting blob from container: {ContainerName}, blob: {BlobName}",
                     CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));

                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToLower());
                var blobClient = containerClient.GetBlobClient(blobName);

                var response = await blobClient.DeleteIfExistsAsync();

                // Invalidate all related cache entries
                await _cacheManager.RemoveAsync(cacheKey);
                await _cacheManager.RemoveAsync(existsCacheKey);
                await InvalidateListCache(containerName, blobName);

                _logger.LogInformation("Successfully deleted blob and invalidated cache for {ContainerName}/{BlobName}",
       CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));

                return response.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete blob {ContainerName}/{BlobName}",
                    CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));

                // Ensure cache is invalidated even on error
                await _cacheManager.RemoveAsync(cacheKey);
                await _cacheManager.RemoveAsync(existsCacheKey);
                await InvalidateListCache(containerName, blobName);
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<List<string>> ListBlobsAsync(string containerName, string prefix)
        {
            var cacheKey = GenerateListCacheKey(containerName, prefix);

            try
            {
                _logger.LogDebug("Listing blobs in container: {ContainerName} with prefix: {Prefix}",
                     CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(prefix));

                // Try to get from cache first
                var cachedList = await _cacheManager.GetAsync<BlobListCache>(cacheKey);
                if (cachedList != null)
                {
                    _logger.LogDebug("Cache HIT for blob list {ContainerName}/{Prefix}, Count: {Count}",
                     CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(prefix), cachedList.BlobNames.Count);
                    return cachedList.BlobNames;
                }

                _logger.LogDebug("Cache MISS for blob list {ContainerName}/{Prefix}", CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(prefix));

                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName.ToLower());
                var blobNames = new List<string>();

                if (!await containerClient.ExistsAsync())
                {
                    _logger.LogWarning("Container does not exist: {ContainerName}", CommonUtils.SanitizeForLog(containerName));
                    return blobNames;
                }

                await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
                {
                    blobNames.Add(blobItem.Name);
                }

                // Cache the list (shorter expiration for lists as they change more frequently)
                await _cacheManager.SetAsync(cacheKey, new BlobListCache { BlobNames = blobNames }, TimeSpan.FromMinutes(10));

                _logger.LogInformation("Found and cached {Count} blobs with prefix {Prefix} in container {ContainerName}",
                        blobNames.Count, CommonUtils.SanitizeForLog(prefix), CommonUtils.SanitizeForLog(containerName));

                return blobNames;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list blobs in container {ContainerName} with prefix {Prefix}",
             CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(prefix));

                // Invalidate cache on error
                await _cacheManager.RemoveAsync(cacheKey);
                throw;
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Generate cache key for blob content
        /// Format: blob:{accountName}:{containerName}:{blobName}
        /// </summary>
        private string GenerateBlobCacheKey(string containerName, string blobName)
        {
            return $"blob:{_accountName}:{containerName.ToLower()}:{blobName}";
        }

        /// <summary>
        /// Generate cache key for blob exists check
        /// Format: blob-exists:{accountName}:{containerName}:{blobName}
        /// </summary>
        private string GenerateBlobExistsCacheKey(string containerName, string blobName)
        {
            return $"blob-exists:{_accountName}:{containerName.ToLower()}:{blobName}";
        }

        /// <summary>
        /// Generate cache key for blob list
        /// Format: blob-list:{accountName}:{containerName}:{prefix}
        /// </summary>
        private string GenerateListCacheKey(string containerName, string prefix)
        {
            var normalizedPrefix = string.IsNullOrEmpty(prefix) ? "all" : prefix;
            return $"blob-list:{_accountName}:{containerName.ToLower()}:{normalizedPrefix}";
        }

        /// <summary>
        /// Extract prefix from blob name (folder path)
        /// Example: "datasets/test.json" -> "datasets"
        /// </summary>
        private string GetBlobPrefix(string blobName)
        {
            var lastSlashIndex = blobName.LastIndexOf('/');
            return lastSlashIndex > 0 ? blobName.Substring(0, lastSlashIndex) : string.Empty;
        }

        /// <summary>
        /// Invalidate list cache for container and blob prefix
        /// When a blob is written or deleted, we need to invalidate the list cache
        /// </summary>
        private async Task InvalidateListCache(string containerName, string blobName)
        {
            try
            {
                // Invalidate cache for the specific prefix
                var prefix = GetBlobPrefix(blobName);
                var listCacheKey = GenerateListCacheKey(containerName, prefix);
                await _cacheManager.RemoveAsync(listCacheKey);

                // Also invalidate the "all" blobs list (no prefix)
                var allBlobsKey = GenerateListCacheKey(containerName, string.Empty);
                await _cacheManager.RemoveAsync(allBlobsKey);

                _logger.LogDebug("Invalidated list cache for container {ContainerName}, prefix: {Prefix}",
               CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(prefix));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate list cache for {ContainerName}/{BlobName}",
             CommonUtils.SanitizeForLog(containerName), CommonUtils.SanitizeForLog(blobName));
                // Don't throw - cache invalidation failure shouldn't break the operation
            }
        }

        #endregion

        #region Cache Models

        /// <summary>
        /// Cache model for blob content
        /// </summary>
        private class BlobContentCache
        {
            public string Content { get; set; } = string.Empty;
        }

        /// <summary>
        /// Cache model for blob existence check
        /// </summary>
        private class BlobExistsCache
        {
            public bool Exists { get; set; }
        }

        /// <summary>
        /// Cache model for blob list
        /// </summary>
        private class BlobListCache
        {
            public List<string> BlobNames { get; set; } = new List<string>();
        }

        #endregion
    }
}
