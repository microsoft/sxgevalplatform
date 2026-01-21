using Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests;
using SxgEvalPlatformApi.Models.Dtos;

namespace Sxg.EvalPlatform.API.UnitTests.Helpers.Builders
{
    /// <summary>
    /// Builder for creating SelectedMetricsConfigurationDto test objects with fluent API.
    /// </summary>
    public class SelectedMetricsConfigurationDtoBuilder
    {
        private string _metricName = TestConstants.Metrics.Accuracy;
        private double _threshold = TestConstants.Metrics.DefaultThreshold;

        /// <summary>
        /// Sets the metric name.
        /// </summary>
        public SelectedMetricsConfigurationDtoBuilder WithMetricName(string metricName)
        {
            _metricName = metricName;
            return this;
        }

        /// <summary>
        /// Sets the threshold.
        /// </summary>
        public SelectedMetricsConfigurationDtoBuilder WithThreshold(double threshold)
        {
            _threshold = threshold;
            return this;
        }

        /// <summary>
        /// Builds the SelectedMetricsConfigurationDto object.
        /// </summary>
        public SelectedMetricsConfigurationDto Build()
        {
            return new SelectedMetricsConfigurationDto
            {
                MetricName = _metricName,
                Threshold = _threshold
            };
        }

        /// <summary>
        /// Creates a default SelectedMetricsConfigurationDto for testing.
        /// </summary>
        public static SelectedMetricsConfigurationDto CreateDefault() =>
            new SelectedMetricsConfigurationDtoBuilder().Build();

        /// <summary>
        /// Creates a SelectedMetricsConfigurationDto for accuracy metric.
        /// </summary>
        public static SelectedMetricsConfigurationDto CreateForAccuracy() =>
            new SelectedMetricsConfigurationDtoBuilder()
                .WithMetricName(TestConstants.Metrics.Accuracy)
                .WithThreshold(0.80)
                .Build();

        /// <summary>
        /// Creates a SelectedMetricsConfigurationDto for precision metric.
        /// </summary>
        public static SelectedMetricsConfigurationDto CreateForPrecision() =>
            new SelectedMetricsConfigurationDtoBuilder()
                .WithMetricName(TestConstants.Metrics.Precision)
                .WithThreshold(0.90)
                .Build();

        /// <summary>
        /// Creates a SelectedMetricsConfigurationDto for recall metric.
        /// </summary>
        public static SelectedMetricsConfigurationDto CreateForRecall() =>
            new SelectedMetricsConfigurationDtoBuilder()
                .WithMetricName(TestConstants.Metrics.Recall)
                .WithThreshold(0.75)
                .Build();
    }
}
