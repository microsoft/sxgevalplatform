using System.Text.Json.Serialization;

namespace Sxg.EvalPlatform.API.Storage.Entities
{

    public class DefaultMetricsConfiguration
    {
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; }

        [JsonPropertyName("categories")]
        public List<Category> Categories { get; set; }
    }

    

}
