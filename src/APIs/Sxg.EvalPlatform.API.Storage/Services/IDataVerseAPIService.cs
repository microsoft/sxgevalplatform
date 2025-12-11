using System.Text.Json.Serialization;

namespace Sxg.EvalPlatform.API.Storage.Services
{
    /// <summary>
    /// Interface for DataVerse API operations
    /// </summary>
    public interface IDataVerseAPIService
    {
        /// <summary>
        /// Post evaluation run data to DataVerse API
        /// </summary>
        /// <param name="evalRunData">Evaluation run data to post</param>
        /// <returns>Response from DataVerse API</returns>
        Task<DataVerseApiResponse> PostEvalRunAsync(DataVerseApiRequest evalRunData); 
                
    }

    /// <summary>
    /// Response model for DataVerse API calls
    /// </summary>
    public class DataVerseApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ResponseContent { get; set; }
        public int StatusCode { get; set; }
    }

    public class DataVerseApiRequest
    {
        [JsonPropertyName("evalRunId")]
        public string EvalRunId { get; set; }
        [JsonPropertyName("agentId")]
        public string AgentId { get; set; } = string.Empty;
        [JsonPropertyName("environmentId")]
        public string EnvironmentId { get; set; }
        [JsonPropertyName("agentSchemaName")]
        public string AgentSchemaName { get; set; } = string.Empty;

        [JsonPropertyName("datasetId")]
        public string DatasetId { get; set; } = string.Empty;
    }
}