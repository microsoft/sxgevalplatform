using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SXG.EvalPlatform.API.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests using in-memory implementations
/// </summary>
public abstract class InMemoryIntegrationTestBase : IClassFixture<InMemoryWebApplicationFactory>
{
    protected readonly HttpClient Client;
    protected readonly InMemoryWebApplicationFactory Factory;

    protected InMemoryIntegrationTestBase(InMemoryWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    /// <summary>
    /// Helper method to create JSON content for HTTP requests
    /// </summary>
    protected static StringContent CreateJsonContent<T>(T data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    /// <summary>
    /// Helper method to deserialize HTTP response content
    /// </summary>
    protected static async Task<T?> DeserializeResponseAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        });
    }
}