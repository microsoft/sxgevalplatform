using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;

namespace Sxg.EvalPlatform.API.Storage.UnitTests.Services;

[Trait("Category", TestCategories.Unit)]
public class EvalRunTableServiceTests
{
    private readonly Mock<IConfigHelper> _mockConfigHelper;
    private readonly Mock<ILogger<EvalRunTableService>> _mockLogger;

    public EvalRunTableServiceTests()
    {
        _mockConfigHelper = new Mock<IConfigHelper>();
        _mockLogger = new Mock<ILogger<EvalRunTableService>>();

        // Setup default config helper responses
        _mockConfigHelper.Setup(x => x.GetAzureStorageAccountName()).Returns("teststorageaccount");
        _mockConfigHelper.Setup(x => x.GetEvalRunTableName()).Returns("EvalRun");
        _mockConfigHelper.Setup(x => x.GetASPNetCoreEnvironment()).Returns("Development");
        _mockConfigHelper.Setup(x => x.GetManagedIdentityClientId()).Returns(string.Empty);
    }

    [Fact]
    public void Constructor_WithEmptyStorageAccountName_ThrowsArgumentException()
    {
        // Arrange
        _mockConfigHelper.Setup(x => x.GetAzureStorageAccountName()).Returns(string.Empty);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            new EvalRunTableService(_mockConfigHelper.Object, _mockLogger.Object));
        
        Assert.Contains("Azure Storage account name is not configured", exception.Message);
    }

    [Fact(Skip = "Requires Azure authentication - should be moved to integration tests or properly mocked")]
    public void Constructor_WithValidConfig_CreatesInstance()
    {
        // Act
        var service = new EvalRunTableService(_mockConfigHelper.Object, _mockLogger.Object);

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullStorageAccountName_ThrowsArgumentException()
    {
        // Arrange
        _mockConfigHelper.Setup(x => x.GetAzureStorageAccountName()).Returns((string?)null);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new EvalRunTableService(_mockConfigHelper.Object, _mockLogger.Object));

        Assert.Contains("Azure Storage account name is not configured", exception.Message);
    }
}
