using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SxgEvalPlatformApi.Models;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.API.UnitTests.Helpers.Builders
{
    /// <summary>
    /// Builder for creating EvalRunDto test objects with fluent API.
    /// </summary>
    public class EvalRunDtoBuilder
    {
        private Guid _evalRunId = Guid.NewGuid();
        private string _metricsConfigurationId = Guid.NewGuid().ToString();
        private string _dataSetId = Guid.NewGuid().ToString();
        private string _agentId = TestConstants.Agents.DefaultAgentId;
        private string _status = CommonConstants.EvalRunStatus.RequestSubmitted;
        private string? _lastUpdatedBy = TestConstants.Users.DefaultEmail;
        private DateTime? _lastUpdatedOn = DateTime.UtcNow;
        private DateTime? _startedDatetime;
        private DateTime? _completedDatetime;
        private string _evalRunName = "Test Eval Run";
        private string _dataSetName = TestConstants.Datasets.DefaultName;
        private string _metricsConfigurationName = TestConstants.MetricsConfigs.DefaultName;

        /// <summary>
        /// Sets the eval run ID.
        /// </summary>
        public EvalRunDtoBuilder WithEvalRunId(Guid evalRunId)
        {
            _evalRunId = evalRunId;
            return this;
        }

        /// <summary>
        /// Sets the metrics configuration ID.
        /// </summary>
        public EvalRunDtoBuilder WithMetricsConfigurationId(string metricsConfigurationId)
        {
            _metricsConfigurationId = metricsConfigurationId;
            return this;
        }

        /// <summary>
        /// Sets the dataset ID.
        /// </summary>
        public EvalRunDtoBuilder WithDataSetId(string dataSetId)
        {
            _dataSetId = dataSetId;
            return this;
        }

        /// <summary>
        /// Sets the agent ID.
        /// </summary>
        public EvalRunDtoBuilder WithAgentId(string agentId)
        {
            _agentId = agentId;
            return this;
        }

        /// <summary>
        /// Sets the status.
        /// </summary>
        public EvalRunDtoBuilder WithStatus(string status)
        {
            _status = status;
            return this;
        }

        /// <summary>
        /// Sets the last updated by field.
        /// </summary>
        public EvalRunDtoBuilder WithLastUpdatedBy(string lastUpdatedBy)
        {
            _lastUpdatedBy = lastUpdatedBy;
            return this;
        }

        /// <summary>
        /// Sets the last updated on timestamp.
        /// </summary>
        public EvalRunDtoBuilder WithLastUpdatedOn(DateTime lastUpdatedOn)
        {
            _lastUpdatedOn = lastUpdatedOn;
            return this;
        }

        /// <summary>
        /// Sets the started datetime.
        /// </summary>
        public EvalRunDtoBuilder WithStartedDatetime(DateTime startedDatetime)
        {
            _startedDatetime = startedDatetime;
            return this;
        }

        /// <summary>
        /// Sets the completed datetime.
        /// </summary>
        public EvalRunDtoBuilder WithCompletedDatetime(DateTime completedDatetime)
        {
            _completedDatetime = completedDatetime;
            return this;
        }

        /// <summary>
        /// Sets the eval run name.
        /// </summary>
        public EvalRunDtoBuilder WithEvalRunName(string evalRunName)
        {
            _evalRunName = evalRunName;
            return this;
        }

        /// <summary>
        /// Sets the dataset name.
        /// </summary>
        public EvalRunDtoBuilder WithDataSetName(string dataSetName)
        {
            _dataSetName = dataSetName;
            return this;
        }

        /// <summary>
        /// Sets the metrics configuration name.
        /// </summary>
        public EvalRunDtoBuilder WithMetricsConfigurationName(string metricsConfigurationName)
        {
            _metricsConfigurationName = metricsConfigurationName;
            return this;
        }

        /// <summary>
        /// Sets status to completed with timestamps.
        /// </summary>
        public EvalRunDtoBuilder AsCompleted()
        {
            _status = CommonConstants.EvalRunStatus.EvalRunCompleted;
            _startedDatetime = DateTime.UtcNow.AddMinutes(-30);
            _completedDatetime = DateTime.UtcNow;
            return this;
        }

        /// <summary>
        /// Sets status to in progress.
        /// </summary>
        public EvalRunDtoBuilder AsInProgress()
        {
            _status = CommonConstants.EvalRunStatus.EvalRunStarted;
            _startedDatetime = DateTime.UtcNow.AddMinutes(-10);
            _completedDatetime = null;
            return this;
        }

        /// <summary>
        /// Sets status to failed.
        /// </summary>
        public EvalRunDtoBuilder AsFailed()
        {
            _status = CommonConstants.EvalRunStatus.EvalRunFailed;
            _startedDatetime = DateTime.UtcNow.AddMinutes(-5);
            _completedDatetime = null;
            return this;
        }

        /// <summary>
        /// Builds the EvalRunDto object.
        /// </summary>
        public EvalRunDto Build()
        {
            return new EvalRunDto
            {
                EvalRunId = _evalRunId,
                MetricsConfigurationId = _metricsConfigurationId,
                DataSetId = _dataSetId,
                AgentId = _agentId,
                Status = _status,
                LastUpdatedBy = _lastUpdatedBy,
                LastUpdatedOn = _lastUpdatedOn,
                StartedDatetime = _startedDatetime,
                CompletedDatetime = _completedDatetime,
                EvalRunName = _evalRunName,
                DataSetName = _dataSetName,
                MetricsConfigurationName = _metricsConfigurationName
            };
        }

        /// <summary>
        /// Creates a default EvalRunDto for testing.
        /// </summary>
        public static EvalRunDto CreateDefault() => new EvalRunDtoBuilder().Build();

        /// <summary>
        /// Creates a completed EvalRunDto.
        /// </summary>
        public static EvalRunDto CreateCompleted() => new EvalRunDtoBuilder()
            .AsCompleted()
            .Build();

        /// <summary>
        /// Creates an in-progress EvalRunDto.
        /// </summary>
        public static EvalRunDto CreateInProgress() => new EvalRunDtoBuilder()
            .AsInProgress()
            .Build();

        /// <summary>
        /// Creates a failed EvalRunDto.
        /// </summary>
        public static EvalRunDto CreateFailed() => new EvalRunDtoBuilder()
            .AsFailed()
            .Build();
    }
}
