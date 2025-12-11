using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace Sxg.EvalPlatform.API.IntegrationTests
{
    /// <summary>
 /// Base class for integration tests providing common setup and utilities
    /// </summary>
    public class IntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>
    {
    protected readonly HttpClient Client;
     protected readonly WebApplicationFactory<Program> Factory;

        public IntegrationTestBase(WebApplicationFactory<Program> factory)
        {
            Factory = factory;
            Client = factory.CreateClient(new WebApplicationFactoryClientOptions
     {
         AllowAutoRedirect = false
            });
        }

   /// <summary>
        /// Helper method to make GET requests and deserialize JSON response
        /// </summary>
  protected async Task<T?> GetAsync<T>(string url)
        {
            var response = await Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
        }

      /// <summary>
 /// Helper method to make POST requests with JSON body
        /// </summary>
  protected async Task<HttpResponseMessage> PostAsync<T>(string url, T content)
        {
          return await Client.PostAsJsonAsync(url, content);
  }

        /// <summary>
        /// Helper method to make PUT requests with JSON body
        /// </summary>
        protected async Task<HttpResponseMessage> PutAsync<T>(string url, T content)
        {
            return await Client.PutAsJsonAsync(url, content);
        }

        /// <summary>
        /// Helper method to make DELETE requests
   /// </summary>
        protected async Task<HttpResponseMessage> DeleteAsync(string url)
        {
            return await Client.DeleteAsync(url);
        }
    }
}
