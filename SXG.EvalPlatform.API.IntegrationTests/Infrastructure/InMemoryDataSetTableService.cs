using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using System.Collections.Concurrent;

namespace SXG.EvalPlatform.API.IntegrationTests.Infrastructure
{
    /// <summary>
    /// In-memory implementation of IDataSetTableService for integration testing
    /// </summary>
    public class InMemoryDataSetTableService : IDataSetTableService
    {
        private readonly ConcurrentDictionary<string, DataSetTableEntity> _datasets = new();

        private string GetKey(string agentId, string datasetId)
        {
            return $"{agentId}|{datasetId}";
        }

        public async Task<DataSetTableEntity> SaveDataSetAsync(DataSetTableEntity entity)
        {
            await Task.Yield();
            
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            // Ensure required fields are set
            if (string.IsNullOrEmpty(entity.DatasetId))
                entity.DatasetId = Guid.NewGuid().ToString();

            if (string.IsNullOrEmpty(entity.AgentId))
                throw new ArgumentException("AgentId is required", nameof(entity));

            // Update timestamps
            entity.LastUpdatedOn = DateTime.UtcNow;
            if (entity.CreatedOn == default)
                entity.CreatedOn = DateTime.UtcNow;

            var key = GetKey(entity.AgentId, entity.DatasetId);
            _datasets.AddOrUpdate(key, entity, (k, existing) => entity);
            
            return entity;
        }

        public async Task<DataSetTableEntity?> GetDataSetAsync(string agentId, string datasetId)
        {
            await Task.Yield();
            
            if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(datasetId))
                return null;

            var key = GetKey(agentId, datasetId);
            _datasets.TryGetValue(key, out var dataset);
            return dataset;
        }

        public async Task<DataSetTableEntity?> GetDataSetByIdAsync(string datasetId)
        {
            await Task.Yield();
            
            if (string.IsNullOrEmpty(datasetId))
                return null;

            return _datasets.Values.FirstOrDefault(d => d.DatasetId == datasetId);
        }

        public async Task<List<DataSetTableEntity>> GetAllDataSetsByAgentIdAsync(string agentId)
        {
            await Task.Yield();
            
            if (string.IsNullOrEmpty(agentId))
                return new List<DataSetTableEntity>();

            return _datasets.Values
                .Where(d => d.AgentId == agentId)
                .OrderBy(d => d.CreatedOn)
                .ToList();
        }

        public async Task<List<DataSetTableEntity>> GetAllDataSetsByAgentIdAndTypeAsync(string agentId, string datasetType)
        {
            await Task.Yield();
            
            if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(datasetType))
                return new List<DataSetTableEntity>();

            return _datasets.Values
                .Where(d => d.AgentId == agentId && 
                           string.Equals(d.DatasetType, datasetType, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.CreatedOn)
                .ToList();
        }

        public async Task<List<DataSetTableEntity>> GetDataSetsByDatasetNameAsync(string agentId, string datasetName)
        {
            await Task.Yield();
            
            if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(datasetName))
                return new List<DataSetTableEntity>();

            return _datasets.Values
                .Where(d => d.AgentId == agentId && 
                           string.Equals(d.DatasetName, datasetName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.CreatedOn)
                .ToList();
        }

        public async Task<DataSetTableEntity?> GetDataSetByDatasetNameAndTypeAsync(string agentId, string datasetName, string datasetType)
        {
            await Task.Yield();
            
            if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(datasetName) || string.IsNullOrEmpty(datasetType))
                return null;

            return _datasets.Values
                .FirstOrDefault(d => d.AgentId == agentId && 
                                   string.Equals(d.DatasetName, datasetName, StringComparison.OrdinalIgnoreCase) &&
                                   string.Equals(d.DatasetType, datasetType, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> DataSetExistsAsync(string agentId, string datasetId)
        {
            await Task.Yield();
            
            if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(datasetId))
                return false;

            var key = GetKey(agentId, datasetId);
            return _datasets.ContainsKey(key);
        }

        public async Task<bool> DeleteDataSetAsync(string agentId, string datasetId)
        {
            await Task.Yield();
            
            if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(datasetId))
                return false;

            var key = GetKey(agentId, datasetId);
            return _datasets.TryRemove(key, out _);
        }

        public async Task<int> DeleteAllDataSetsByAgentIdAsync(string agentId)
        {
            await Task.Yield();
            
            if (string.IsNullOrEmpty(agentId))
                return 0;

            var keysToRemove = _datasets.Keys
                .Where(key => key.StartsWith($"{agentId}|"))
                .ToList();

            var deletedCount = 0;
            foreach (var key in keysToRemove)
            {
                if (_datasets.TryRemove(key, out _))
                    deletedCount++;
            }

            return deletedCount;
        }

        public async Task<DataSetTableEntity?> UpdateDataSetMetadataAsync(string agentId, string datasetId, Action<DataSetTableEntity> updateAction)
        {
            await Task.Yield();
            
            if (string.IsNullOrEmpty(agentId) || string.IsNullOrEmpty(datasetId) || updateAction == null)
                return null;

            var key = GetKey(agentId, datasetId);
            if (!_datasets.TryGetValue(key, out var entity))
                return null;

            // Apply the update action
            updateAction(entity);
            entity.LastUpdatedOn = DateTime.UtcNow;

            _datasets.TryUpdate(key, entity, entity);
            return entity;
        }

        // Test helper methods
        public void Clear()
        {
            _datasets.Clear();
        }

        public int Count => _datasets.Count;

        public void SeedTestData()
        {
            // Add some seed data for testing
            var testDatasets = new[]
            {
                new DataSetTableEntity
                {
                    AgentId = "test-agent-1",
                    DatasetId = "dataset-1",
                    DatasetName = "Test Dataset 1",
                    DatasetType = "Synthetic",
                    BlobFilePath = "test-dataset-1.json",
                    ContainerName = "eval-datasets",
                    CreatedBy = "test-user",
                    CreatedOn = DateTime.UtcNow.AddDays(-5),
                    LastUpdatedBy = "test-user",
                    LastUpdatedOn = DateTime.UtcNow.AddDays(-5)
                },
                new DataSetTableEntity
                {
                    AgentId = "test-agent-1",
                    DatasetId = "dataset-2",
                    DatasetName = "Test Dataset 2",
                    DatasetType = "Golden",
                    BlobFilePath = "test-dataset-2.json",
                    ContainerName = "eval-datasets",
                    CreatedBy = "test-user",
                    CreatedOn = DateTime.UtcNow.AddDays(-3),
                    LastUpdatedBy = "test-user",
                    LastUpdatedOn = DateTime.UtcNow.AddDays(-3)
                }
            };

            foreach (var dataset in testDatasets)
            {
                var key = GetKey(dataset.AgentId, dataset.DatasetId);
                _datasets.TryAdd(key, dataset);
            }
        }
    }
}