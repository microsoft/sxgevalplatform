using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SxgEvalPlatformApi.Models.Dtos;

namespace Sxg.EvalPlatform.API.UnitTests.Helpers.Builders
{
    /// <summary>
    /// Builder for creating CreateConfigurationRequestDto test objects with fluent API.
    /// </summary>
    public class CreateConfigurationRequestDtoBuilder
    {
        private string _agentId = TestConstants.Agents.DefaultAgentId;
        private string _configurationName = TestConstants.MetricsConfigs.DefaultName;
        private string _environmentName = TestConstants.Environments.Prod;
        private string? _description;
        private IList<SelectedMetricsConfigurationDto> _metricsConfiguration = new List<SelectedMetricsConfigurationDto>
        {
            SelectedMetricsConfigurationDtoBuilder.CreateDefault()
        };

        /// <summary>
        /// Sets the agent ID.
        /// </summary>
        public CreateConfigurationRequestDtoBuilder WithAgentId(string agentId)
        {
            _agentId = agentId;
            return this;
        }

        /// <summary>
        /// Sets the configuration name.
        /// </summary>
        public CreateConfigurationRequestDtoBuilder WithConfigurationName(string configurationName)
        {
            _configurationName = configurationName;
            return this;
        }

        /// <summary>
        /// Sets the environment name.
        /// </summary>
        public CreateConfigurationRequestDtoBuilder WithEnvironmentName(string environmentName)
        {
            _environmentName = environmentName;
            return this;
        }

        /// <summary>
        /// Sets the description.
        /// </summary>
        public CreateConfigurationRequestDtoBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }

        /// <summary>
        /// Sets the metrics configuration list.
        /// </summary>
        public CreateConfigurationRequestDtoBuilder WithMetricsConfiguration(IList<SelectedMetricsConfigurationDto> metricsConfiguration)
        {
            _metricsConfiguration = metricsConfiguration;
            return this;
        }

        /// <summary>
        /// Adds a single metric configuration.
        /// </summary>
        public CreateConfigurationRequestDtoBuilder AddMetricConfiguration(SelectedMetricsConfigurationDto metricConfig)
        {
            _metricsConfiguration.Add(metricConfig);
            return this;
        }

        /// <summary>
        /// Sets empty metrics configuration (for validation testing).
        /// </summary>
        public CreateConfigurationRequestDtoBuilder WithEmptyMetricsConfiguration()
        {
            _metricsConfiguration = new List<SelectedMetricsConfigurationDto>();
            return this;
        }

        /// <summary>
        /// Builds the CreateConfigurationRequestDto object.
        /// </summary>
        public CreateConfigurationRequestDto Build()
        {
            return new CreateConfigurationRequestDto
            {
                AgentId = _agentId,
                ConfigurationName = _configurationName,
                EnvironmentName = _environmentName,
                Description = _description,
                MetricsConfiguration = _metricsConfiguration
            };
        }

        /// <summary>
        /// Creates a default valid CreateConfigurationRequestDto for testing.
        /// </summary>
        public static CreateConfigurationRequestDto CreateDefault() =>
            new CreateConfigurationRequestDtoBuilder().Build();

        /// <summary>
        /// Creates a CreateConfigurationRequestDto with multiple metrics.
        /// </summary>
        public static CreateConfigurationRequestDto CreateWithMultipleMetrics() =>
            new CreateConfigurationRequestDtoBuilder()
                .WithEmptyMetricsConfiguration()
                .AddMetricConfiguration(SelectedMetricsConfigurationDtoBuilder.CreateForAccuracy())
                .AddMetricConfiguration(SelectedMetricsConfigurationDtoBuilder.CreateForPrecision())
                .AddMetricConfiguration(SelectedMetricsConfigurationDtoBuilder.CreateForRecall())
                .Build();

        /// <summary>
        /// Creates a CreateConfigurationRequestDto with description.
        /// </summary>
        public static CreateConfigurationRequestDto CreateWithDescription(string description) =>
            new CreateConfigurationRequestDtoBuilder()
                .WithDescription(description)
                .Build();
    }
}
