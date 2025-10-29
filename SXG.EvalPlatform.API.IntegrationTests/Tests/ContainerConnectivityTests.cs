using Azure.Data.Tables;
using Azure.Storage.Blobs;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;
using SXG.EvalPlatform.API.IntegrationTests.Infrastructure;
using System.Linq;

namespace SXG.EvalPlatform.API.IntegrationTests.Tests;

/// <summary>
/// Tests to verify connectivity to containerized dependencies (Azurite and Redis)
/// before running integration tests
/// </summary>
[Collection("Integration Tests")]
public class ContainerConnectivityTests : IClassFixture<ContainerizedWebApplicationFactory>
{
    private readonly ContainerizedWebApplicationFactory _factory;

    public ContainerConnectivityTests(ContainerizedWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Redis_ShouldBeAccessible()
    {
        // Arrange
        var database = _factory.GetRedisDatabase();

        // Act - Test basic Redis operations
        var key = "test-connectivity-key";
        var value = "test-value";

        await database.StringSetAsync(key, value);
        var retrievedValue = await database.StringGetAsync(key);

        // Assert
        retrievedValue.Should().Be(value);
        
        // Cleanup
        await database.KeyDeleteAsync(key);
    }

    [Fact]
    public async Task AzuriteBlob_ShouldBeAccessible()
    {
        // Arrange - Create BlobServiceClient using connection string
        var blobServiceClient = new BlobServiceClient(_factory.AzuriteConnectionString);

        // Act - Test basic blob operations
        var containerName = "test-container";
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        
        await containerClient.CreateIfNotExistsAsync();
        var exists = await containerClient.ExistsAsync();

        // Assert
        exists.Value.Should().BeTrue();

        // Cleanup
        await containerClient.DeleteIfExistsAsync();
    }

    [Fact]
    public async Task AzuriteTable_ShouldBeAccessible()
    {
        // Arrange - Create TableServiceClient using connection string
        var tableServiceClient = new TableServiceClient(_factory.AzuriteConnectionString);

        // Act - Test basic table operations
        var tableName = "TestConnectivityTable";
        var tableClient = tableServiceClient.GetTableClient(tableName);
        
        await tableClient.CreateIfNotExistsAsync();
        
        // Try to query the table - should not throw
        var queryResult = tableClient.QueryAsync<TableEntity>();
        var hasAnyItems = false;
        await foreach (var item in queryResult)
        {
            hasAnyItems = true;
            break; // Just check if we can query, don't need to enumerate all
        }

        // Assert - Table should exist and be queryable (empty is fine)
        tableClient.Should().NotBeNull();

        // Cleanup
        await tableClient.DeleteAsync();
    }

    [Fact]
    public void ConnectionStrings_ShouldBeValid()
    {
        // Assert
        _factory.AzuriteConnectionString.Should().NotBeNullOrEmpty();
        _factory.AzuriteConnectionString.Should().Contain("devstoreaccount1");
        _factory.AzuriteConnectionString.Should().Contain("DefaultEndpointsProtocol=http");
        
        _factory.RedisConnectionString.Should().NotBeNullOrEmpty();
        _factory.RedisConnectionString.Should().MatchRegex(@"127\.0\.0\.1:\d+");
    }
}