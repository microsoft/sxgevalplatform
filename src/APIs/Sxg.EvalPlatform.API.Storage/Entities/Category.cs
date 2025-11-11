using System.Text.Json.Serialization;

namespace Sxg.EvalPlatform.API.Storage.Entities
{
    public class Category
    {
        [JsonPropertyName("categoryName")]
        public string CategoryName { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("metrics")]
        public List<Metric> Metrics { get; set; }
    }

    

}
