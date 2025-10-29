using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using System.Collections.Concurrent;

namespace SXG.EvalPlatform.API.IntegrationTests.Infrastructure
{
    /// <summary>
    /// In-memory implementation of IMetricsConfigTableService for integration testing
    /// </summary>
    public class InMemoryMetricsConfigTableService : IMetricsConfigTableService
    {
        private readonly ConcurrentDictionary<string, MetricsConfigurationTableEntity> _configurations = new();

        public async Task<IList<MetricsConfigurationTableEntity>> GetAllMetricsConfigurations(string agentId, string environmentName)
        {
            await Task.Yield(); // Make it properly async
            
            var results = _configurations.Values
                .Where(c => c.AgentId == agentId && 
                           (string.IsNullOrEmpty(environmentName) || c.EnvironmentName == environmentName))
                .ToList();
                
            return results;
        }

        public async Task<IList<MetricsConfigurationTableEntity>> GetAllMetricsConfigurations(string agentId, string configurationName, string environmentName)
        {
            await Task.Yield();
            
            var results = _configurations.Values
                .Where(c => c.AgentId == agentId && 
                           c.ConfigurationName == configurationName &&
                           (string.IsNullOrEmpty(environmentName) || c.EnvironmentName == environmentName))
                .ToList();
                
            return results;
        }

        public async Task<MetricsConfigurationTableEntity?> GetMetricsConfigurationByConfigurationIdAsync(string configurationId)
        {
            await Task.Yield();
            
            _configurations.TryGetValue(configurationId, out var config);
            return config;
        }

        public async Task<MetricsConfigurationTableEntity> SaveMetricsConfigurationAsync(MetricsConfigurationTableEntity entity)
        {
            await Task.Yield();
            
            // If no PartitionKey is set, generate one based on ConfigurationId
            if (string.IsNullOrEmpty(entity.PartitionKey))
            {
                entity.PartitionKey = entity.ConfigurationId;
            }
            
            // If no RowKey is set, use ConfigurationId
            if (string.IsNullOrEmpty(entity.RowKey))
            {
                entity.RowKey = entity.ConfigurationId;
            }
            
            _configurations.AddOrUpdate(entity.ConfigurationId, entity, (key, oldValue) => entity);
            return entity;
        }

        public async Task<bool> UpdateMetricsConfigurationAsync(MetricsConfigurationTableEntity entity)
        {
            await Task.Yield();
            
            if (_configurations.ContainsKey(entity.ConfigurationId))
            {
                _configurations.AddOrUpdate(entity.ConfigurationId, entity, (key, oldValue) => entity);
                return true;
            }
            return false;
        }

        public async Task<bool> DeleteMetricsConfigurationByIdAsync(string agentId, string configurationId)
        {
            await Task.Yield();
            
            return _configurations.TryRemove(configurationId, out _);
        }

        public async Task<bool> DeleteMetricsConfigurationAsync(string agentId, string configurationName, string environmentName)
        {
            await Task.Yield();
            
            var toRemove = _configurations.Values
                .Where(c => c.AgentId == agentId && 
                           c.ConfigurationName == configurationName &&
                           c.EnvironmentName == environmentName)
                .ToList();
                
            var removedAny = false;
            foreach (var config in toRemove)
            {
                if (_configurations.TryRemove(config.ConfigurationId, out _))
                {
                    removedAny = true;
                }
            }
            
            return removedAny;
        }
    }
}