using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using System.Collections.Concurrent;

namespace SXG.EvalPlatform.API.IntegrationTests.Infrastructure
{
    /// <summary>
    /// In-memory implementation of IEvalRunTableService for integration testing
    /// </summary>
    public class InMemoryEvalRunTableService : IEvalRunTableService
    {
        private readonly ConcurrentDictionary<string, EvalRunTableEntity> _evalRuns = new();

        private string GetKey(string agentId, Guid evalRunId)
        {
            return $"{agentId}|{evalRunId}";
        }

        public async Task<EvalRunTableEntity> CreateEvalRunAsync(EvalRunTableEntity entity)
        {
            await Task.Yield();
            
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            // Ensure required fields are set
            if (entity.EvalRunId == Guid.Empty)
                entity.EvalRunId = Guid.NewGuid();

            if (string.IsNullOrEmpty(entity.AgentId))
                throw new ArgumentException("AgentId is required", nameof(entity));

            // Update timestamps
            entity.LastUpdatedOn = DateTime.UtcNow;
            if (entity.StartedDatetime == default)
                entity.StartedDatetime = DateTime.UtcNow;

            var key = GetKey(entity.AgentId, entity.EvalRunId);
            _evalRuns.AddOrUpdate(key, entity, (k, existing) => entity);
            
            return entity;
        }

        public async Task<EvalRunTableEntity?> GetEvalRunByIdAsync(Guid evalRunId)
        {
            await Task.Yield();
            
            return _evalRuns.Values.FirstOrDefault(e => e.EvalRunId == evalRunId);
        }

        public async Task<EvalRunTableEntity?> GetEvalRunByIdAsync(string agentId, Guid evalRunId)
        {
            await Task.Yield();
            
            var key = GetKey(agentId, evalRunId);
            _evalRuns.TryGetValue(key, out var entity);
            return entity;
        }

        public async Task<EvalRunTableEntity?> UpdateEvalRunStatusAsync(string agentId, Guid evalRunId, string status, string? lastUpdatedBy = null)
        {
            await Task.Yield();
            
            var key = GetKey(agentId, evalRunId);
            if (_evalRuns.TryGetValue(key, out var entity))
            {
                entity.Status = status;
                entity.LastUpdatedOn = DateTime.UtcNow;
                entity.LastUpdatedBy = lastUpdatedBy ?? "System";
                
                // Set completion datetime if status is Completed or Failed
                if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    entity.CompletedDatetime = DateTime.UtcNow;
                }

                _evalRuns.AddOrUpdate(key, entity, (k, existing) => entity);
                return entity;
            }
            
            return null;
        }

        public async Task<List<EvalRunTableEntity>> GetEvalRunsByAgentIdAsync(string agentId)
        {
            await Task.Yield();
            
            if (string.IsNullOrEmpty(agentId))
                return new List<EvalRunTableEntity>();

            return _evalRuns.Values
                .Where(e => e.AgentId == agentId)
                .OrderByDescending(e => e.StartedDatetime)
                .ToList();
        }

        public async Task<EvalRunTableEntity> UpdateEvalRunAsync(EvalRunTableEntity entity)
        {
            await Task.Yield();
            
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            entity.LastUpdatedOn = DateTime.UtcNow;

            var key = GetKey(entity.AgentId, entity.EvalRunId);
            _evalRuns.AddOrUpdate(key, entity, (k, existing) => entity);
            
            return entity;
        }

        public async Task DeleteEvalRunAsync(string agentId, Guid evalRunId)
        {
            await Task.Yield();
            
            var key = GetKey(agentId, evalRunId);
            _evalRuns.TryRemove(key, out _);
        }

        public async Task<List<EvalRunTableEntity>> GetEvalRunsByStatusAsync(string status)
        {
            await Task.Yield();
            
            if (string.IsNullOrEmpty(status))
                return new List<EvalRunTableEntity>();

            return _evalRuns.Values
                .Where(e => e.Status == status)
                .OrderByDescending(e => e.StartedDatetime)
                .ToList();
        }

        public async Task<List<EvalRunTableEntity>> GetEvalRunsByDateRangeAsync(string agentId, DateTime startDate, DateTime endDate)
        {
            await Task.Yield();
            
            if (string.IsNullOrEmpty(agentId))
                return new List<EvalRunTableEntity>();

            return _evalRuns.Values
                .Where(e => e.AgentId == agentId && 
                           e.StartedDatetime >= startDate && 
                           e.StartedDatetime <= endDate)
                .OrderByDescending(e => e.StartedDatetime)
                .ToList();
        }

        /// <summary>
        /// Clear all data - useful for test cleanup
        /// </summary>
        public void Clear()
        {
            _evalRuns.Clear();
        }
    }
}