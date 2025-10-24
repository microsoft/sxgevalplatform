using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Services.Cache;
using System.Text.Json;
using Sxg.EvalPlatform.API.Storage.TableEntities;

namespace SxgEvalPlatformApi.Cache
{
    /// <summary>
    /// Redis cache service implementation for dataset operations
    /// Provides optimized caching with proper namespacing for datasets
    /// </summary>
    public class EvalDatasetCacheService : IEvalDatasetCache
    {
        private readonly IRedisCache _redisCache;
        private readonly ILogger<EvalDatasetCacheService> _logger;

        // Cache key prefixes for dataset namespacing
        private const string DatasetMetadataPrefix = "evaldataset:metadata:";
        private const string DatasetContentPrefix = "evaldataset:content:";
        private const string AgentDatasetsPrefix = "evaldataset:agent:";

        public EvalDatasetCacheService(IRedisCache redisCache, ILogger<EvalDatasetCacheService> logger)
        {
            _redisCache = redisCache;
            _logger = logger;
        }

        #region Cache Key Builders

        private string BuildDatasetMetadataKey(string datasetId) => $"{DatasetMetadataPrefix}{datasetId}";
        private string BuildDatasetContentKey(string datasetId) => $"{DatasetContentPrefix}{datasetId}";
        private string BuildAgentDatasetsKey(string agentId) => $"{AgentDatasetsPrefix}{agentId}:list";

        #endregion

        #region Dataset Metadata Caching

        public async Task<bool> SetDatasetMetadataAsync(DatasetMetadataDto datasetMetadata, int ttlMinutes = 120)
        {
            try
            {
                var key = BuildDatasetMetadataKey(datasetMetadata.DatasetId);
                var success = await _redisCache.SetAsync(key, datasetMetadata, TimeSpan.FromMinutes(ttlMinutes));

                if (success)
                {
                    _logger.LogDebug("Cached dataset metadata for DatasetId: {DatasetId} with TTL: {TTL} minutes", 
                        datasetMetadata.DatasetId, ttlMinutes);
                }
                else
                {
                    _logger.LogWarning("Failed to cache dataset metadata for DatasetId: {DatasetId}", 
                        datasetMetadata.DatasetId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching dataset metadata for DatasetId: {DatasetId}", 
                    datasetMetadata.DatasetId);
                return false;
            }
        }

        public async Task<DatasetMetadataDto?> GetDatasetMetadataAsync(string datasetId)
        {
            try
            {
                var key = BuildDatasetMetadataKey(datasetId);
                var metadata = await _redisCache.GetAsync<DatasetMetadataDto>(key);

                if (metadata != null)
                {
                    _logger.LogDebug("Retrieved dataset metadata from cache for DatasetId: {DatasetId}", datasetId);
                }
                else
                {
                    _logger.LogDebug("Dataset metadata cache miss for DatasetId: {DatasetId}", datasetId);
                }

                return metadata;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dataset metadata from cache for DatasetId: {DatasetId}", datasetId);
                return null;
            }
        }

        #endregion

        #region Dataset Content Caching

        public async Task<bool> SetDatasetContentAsync(string datasetId, string content, int ttlMinutes = 120)
        {
            try
            {
                var key = BuildDatasetContentKey(datasetId);
                var success = await _redisCache.SetAsync(key, content, TimeSpan.FromMinutes(ttlMinutes));

                if (success)
                {
                    _logger.LogDebug("Cached dataset content for DatasetId: {DatasetId} with TTL: {TTL} minutes (Content size: {Size} chars)", 
                        datasetId, ttlMinutes, content?.Length ?? 0);
                }
                else
                {
                    _logger.LogWarning("Failed to cache dataset content for DatasetId: {DatasetId}", datasetId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching dataset content for DatasetId: {DatasetId}", datasetId);
                return false;
            }
        }

        public async Task<string?> GetDatasetContentAsync(string datasetId)
        {
            try
            {
                var key = BuildDatasetContentKey(datasetId);
                var content = await _redisCache.GetAsync<string>(key);

                if (content != null)
                {
                    _logger.LogDebug("Retrieved dataset content from cache for DatasetId: {DatasetId} (Content size: {Size} chars)", 
                        datasetId, content.Length);
                }
                else
                {
                    _logger.LogDebug("Dataset content cache miss for DatasetId: {DatasetId}", datasetId);
                }

                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dataset content from cache for DatasetId: {DatasetId}", datasetId);
                return null;
            }
        }

        #endregion

        #region Agent Dataset List Caching

        public async Task<bool> SetDatasetsByAgentAsync(string agentId, IList<DatasetMetadataDto> datasets, int ttlMinutes = 30)
        {
            try
            {
                var key = BuildAgentDatasetsKey(agentId);
                var success = await _redisCache.SetAsync(key, datasets.ToList(), TimeSpan.FromMinutes(ttlMinutes));

                if (success)
                {
                    _logger.LogDebug("Cached {Count} datasets for Agent: {AgentId} with TTL: {TTL} minutes", 
                        datasets.Count, agentId, ttlMinutes);
                }
                else
                {
                    _logger.LogWarning("Failed to cache datasets for Agent: {AgentId}", agentId);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching datasets for Agent: {AgentId}", agentId);
                return false;
            }
        }

        public async Task<IList<DatasetMetadataDto>?> GetDatasetsByAgentAsync(string agentId)
        {
            try
            {
                var key = BuildAgentDatasetsKey(agentId);
                var datasets = await _redisCache.GetAsync<List<DatasetMetadataDto>>(key);

                if (datasets != null)
                {
                    _logger.LogDebug("Retrieved {Count} datasets from cache for Agent: {AgentId}", 
                        datasets.Count, agentId);
                    return datasets;
                }
                else
                {
                    _logger.LogDebug("Dataset list cache miss for Agent: {AgentId}", agentId);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving datasets from cache for Agent: {AgentId}", agentId);
                return null;
            }
        }

        #endregion

        #region Cache Management

        public async Task<bool> RemoveDatasetAsync(string datasetId)
        {
            try
            {
                var metadataKey = BuildDatasetMetadataKey(datasetId);
                var contentKey = BuildDatasetContentKey(datasetId);

                var metadataRemoved = await _redisCache.RemoveAsync(metadataKey);
                var contentRemoved = await _redisCache.RemoveAsync(contentKey);

                if (metadataRemoved || contentRemoved)
                {
                    _logger.LogDebug("Removed dataset from cache - DatasetId: {DatasetId}, MetadataRemoved: {MetadataRemoved}, ContentRemoved: {ContentRemoved}", 
                        datasetId, metadataRemoved, contentRemoved);
                }

                return metadataRemoved || contentRemoved;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing dataset from cache for DatasetId: {DatasetId}", datasetId);
                return false;
            }
        }

        public async Task<bool> RemoveDatasetsByAgentAsync(string agentId)
        {
            try
            {
                var key = BuildAgentDatasetsKey(agentId);
                var removed = await _redisCache.RemoveAsync(key);

                if (removed)
                {
                    _logger.LogDebug("Removed dataset list from cache for Agent: {AgentId}", agentId);
                }

                return removed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing dataset list from cache for Agent: {AgentId}", agentId);
                return false;
            }
        }

        #endregion
    }
}