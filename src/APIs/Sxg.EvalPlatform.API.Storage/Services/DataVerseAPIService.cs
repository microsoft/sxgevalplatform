using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.API.Storage.Services
{
    /// <summary>
    /// Service for DataVerse API operations
    /// </summary>
    public class DataVerseAPIService : IDataVerseAPIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigHelper _configHelper;
        private readonly ILogger<DataVerseAPIService> _logger;

        public DataVerseAPIService(
            HttpClient httpClient, 
            IConfigHelper configHelper, 
            ILogger<DataVerseAPIService> logger)
        {
            _httpClient = httpClient;
            _configHelper = configHelper;
            _logger = logger;
        }

        /// <summary>
        /// Get authentication token for DataVerse API
        /// </summary>
        private async Task<string> GetAccessTokenAsync()
        {
            try
            {
                var scope = _configHelper.GetDataVerseAPIScope();
                var environment = _configHelper.GetASPNetCoreEnvironment();
                var credential = CommonUtils.GetTokenCredential(environment);

                var tokenRequestContext = new TokenRequestContext(new[] { scope });
                var token = await credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);

                return token.Token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get access token for DataVerse API");
                throw;
            }
        }

        /// <summary>
        /// Post evaluation run data to DataVerse API
        /// </summary>
        public async Task<DataVerseApiResponse> PostEvalRunAsync(DataVerseApiRequest evalRunData)
        {
            try
            {
                var endpoint = _configHelper.GetDatasetEnrichmentRequestAPIEndPoint();
                _logger.LogInformation("Posting evaluation run data to DataVerse API endpoint: {Endpoint}", endpoint);

                // Get access token
                var accessToken = await GetAccessTokenAsync();

                // Serialize the data
                var jsonContent = JsonSerializer.Serialize(evalRunData, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                // Prepare the request
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Add required headers for DataVerse
                request.Headers.Add("OData-MaxVersion", "4.0");
                request.Headers.Add("OData-Version", "4.0");
                request.Headers.Add("Accept", "application/json");

                _logger.LogInformation("Sending POST request to DataVerse API with payload: {JsonContent}", jsonContent);

                // Make the request
                var response = await _httpClient.SendAsync(request);

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully posted data to DataVerse API. Status: {StatusCode}", response.StatusCode);
                    return new DataVerseApiResponse
                    {
                        Success = true,
                        Message = "Successfully posted data to DataVerse API",
                        ResponseContent = responseContent,
                        StatusCode = (int)response.StatusCode
                    };
                }
                else
                {
                    _logger.LogError("Failed to post data to DataVerse API. Status: {StatusCode}, Response: {ResponseContent}", 
                        response.StatusCode, responseContent);
                    return new DataVerseApiResponse
                    {
                        Success = false,
                        Message = $"DataVerse API call failed with status {response.StatusCode}",
                        ResponseContent = responseContent,
                        StatusCode = (int)response.StatusCode
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while posting data to DataVerse API");
                return new DataVerseApiResponse
                {
                    Success = false,
                    Message = $"Exception occurred: {ex.Message}",
                    ResponseContent = null,
                    StatusCode = 0
                };
            }
        }

        /// <summary>
        /// Post dataset enrichment request to DataVerse API
        /// </summary>
        //public async Task<DataVerseApiResponse> PostDatasetEnrichmentRequestAsync(object enrichmentRequest)
        //{
        //    // Use the same implementation as PostEvalRunAsync since they both post to the same endpoint
        //    // but you can customize this if needed for different data structures
        //    return await PostEvalRunAsync(enrichmentRequest);
        //}
    }
}