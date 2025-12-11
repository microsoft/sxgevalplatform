using System.Text.Json.Serialization;

namespace Sxg.EvalPlatform.API.Storage.Entities
{
    public class Metric
    {
        [JsonPropertyName("metricName")]
        public string MetricName { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("defaultThreshold")]
        public double DefaultThreshold { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("isMandatory")]
        public bool IsMandatory { get; set; }

        [JsonPropertyName("scoreRange")]
        public ScoreRange ScoreRange { get; set; }

    }

    public struct ScoreRange
    {
        [JsonPropertyName("min")]
        public double Min { get; set; }
        [JsonPropertyName("max")]
        public double Max { get; set; }
    }


}
