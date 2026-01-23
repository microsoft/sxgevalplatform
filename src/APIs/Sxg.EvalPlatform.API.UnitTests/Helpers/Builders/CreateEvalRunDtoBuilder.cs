using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SxgEvalPlatformApi.Models;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.API.UnitTests.Helpers.Builders
{
    /// <summary>
    /// Builder for creating CreateEvalRunDto test objects with fluent API.
    /// </summary>
    public class CreateEvalRunDtoBuilder
    {
        private string _agentId = TestConstants.Agents.DefaultAgentId;
        private Guid _dataSetId = Guid.NewGuid();
        private Guid _metricsConfigurationId = Guid.NewGuid();
        private string _type = "MCS";
        private string _environmentId = TestConstants.Environments.Dev;
        private string _agentSchemaName = TestConstants.Agents.DefaultAgentSchemaName;
        private string _evalRunName = "Test Eval Run";

        /// <summary>
        /// Sets the agent ID.
        /// </summary>
        public CreateEvalRunDtoBuilder WithAgentId(string agentId)
        {
            _agentId = agentId;
            return this;
        }

        /// <summary>
        /// Sets the dataset ID.
        /// </summary>
        public CreateEvalRunDtoBuilder WithDataSetId(Guid dataSetId)
        {
            _dataSetId = dataSetId;
            return this;
        }

        /// <summary>
        /// Sets the metrics configuration ID.
        /// </summary>
        public CreateEvalRunDtoBuilder WithMetricsConfigurationId(Guid metricsConfigurationId)
        {
            _metricsConfigurationId = metricsConfigurationId;
            return this;
        }

        /// <summary>
        /// Sets the type.
        /// </summary>
        public CreateEvalRunDtoBuilder WithType(string type)
        {
            _type = type;
            return this;
        }

        /// <summary>
        /// Sets the environment ID.
        /// </summary>
        public CreateEvalRunDtoBuilder WithEnvironmentId(string environmentId)
        {
            _environmentId = environmentId;
            return this;
        }

        /// <summary>
        /// Sets the agent schema name.
        /// </summary>
        public CreateEvalRunDtoBuilder WithAgentSchemaName(string agentSchemaName)
        {
            _agentSchemaName = agentSchemaName;
            return this;
        }

        /// <summary>
        /// Sets the eval run name.
        /// </summary>
        public CreateEvalRunDtoBuilder WithEvalRunName(string evalRunName)
        {
            _evalRunName = evalRunName;
            return this;
        }

        /// <summary>
        /// Builds the CreateEvalRunDto object.
        /// </summary>
        public CreateEvalRunDto Build()
        {
            return new CreateEvalRunDto
            {
                AgentId = _agentId,
                DataSetId = _dataSetId,
                MetricsConfigurationId = _metricsConfigurationId,
                Type = _type,
                EnvironmentId = _environmentId,
                AgentSchemaName = _agentSchemaName,
                EvalRunName = _evalRunName
            };
        }

        /// <summary>
        /// Creates a default valid CreateEvalRunDto for testing.
        /// </summary>
        public static CreateEvalRunDto CreateDefault() => new CreateEvalRunDtoBuilder().Build();

        /// <summary>
        /// Creates a CreateEvalRunDto with minimal valid data.
        /// </summary>
        public static CreateEvalRunDto CreateMinimal() => new CreateEvalRunDtoBuilder()
            .WithEvalRunName("Minimal Run")
            .Build();
    }
}
