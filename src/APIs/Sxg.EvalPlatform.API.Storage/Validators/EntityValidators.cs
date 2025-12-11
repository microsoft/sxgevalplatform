using Microsoft.Extensions.Logging;
using Sxg.EvalPlatform.API.Storage.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sxg.EvalPlatform.API.Storage.Validators
{
    public class EntityValidators : IEntityValidators
    {
        private readonly IDataSetTableService _dataSetTableService;
        private readonly IMetricsConfigTableService _metricsConfigurationTableService;
        private readonly IEvalRunTableService _evalRunTableService;
        private readonly ILogger<EntityValidators> _logger;

        public EntityValidators(ILogger<EntityValidators> logger,
            IDataSetTableService dataSetTableService,
            IMetricsConfigTableService metricsConfigurationTableService,
            IEvalRunTableService evalRunTableService)
        {
            _logger = logger;
            _dataSetTableService = dataSetTableService;
            _metricsConfigurationTableService = metricsConfigurationTableService;
            _evalRunTableService = evalRunTableService;

        }
        public async Task<bool> IsValidDatasetId(string datasetId, string agentId = "")
        {
            if (string.IsNullOrWhiteSpace(datasetId))
            {
                return false;
            }

            try
            {
                var dataset = await _dataSetTableService.GetDataSetByIdAsync(datasetId);
                if (dataset != null && (string.IsNullOrEmpty(agentId) || agentId == dataset.AgentId))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Invalid DatasetId: {datasetId}");
            }

            return false;
        }


        public async Task<bool> IsValidMetricsConfigurationId(string metricsConfigurationId, string agentId = "")
        {
            if (string.IsNullOrWhiteSpace(metricsConfigurationId))
            {
                return false;
            }

            try
            {
                var metricsConfig = await _metricsConfigurationTableService.GetMetricsConfigurationByConfigurationIdAsync(metricsConfigurationId);
                if (metricsConfig != null && (string.IsNullOrEmpty(agentId) || agentId == metricsConfig.AgentId))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Invalid MetricsConfigurationId: {metricsConfigurationId}");
            }

            return false;
        }

        public async Task<bool> IsValidEvalRunId(Guid evalRunId)
        {
            if (evalRunId == Guid.Empty)
            {
                return false;
            }

            try
            {
                var evalRunTableEntity = await _evalRunTableService.GetEvalRunByIdAsync(evalRunId);
                if (evalRunTableEntity != null)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Invalid EvalRunId: {evalRunId}");
            }
            return true;
        }

        
    }
}
