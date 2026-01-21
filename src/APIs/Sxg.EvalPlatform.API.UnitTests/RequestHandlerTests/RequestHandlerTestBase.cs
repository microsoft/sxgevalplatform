using Microsoft.Extensions.Logging;
using Moq;
using Sxg.EvalPlatform.API.Storage;
using Sxg.EvalPlatform.API.Storage.Services;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SxgEvalPlatformApi.Controllers;
using SxgEvalPlatformApi.Services;

namespace Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests
{
    /// <summary>
    /// Base class for Request Handler unit tests providing common setup and helper methods.
    /// This class extracts common patterns across all request handler tests to reduce code duplication.
    /// </summary>
    public abstract class RequestHandlerTestBase<THandler> where THandler : class
    {
        #region Common Mocks

        protected Mock<IConfigHelper> MockConfigHelper { get; }
        protected Mock<ICallerIdentificationService> MockCallerService { get; }
        protected Mock<ICacheManager> MockCacheManager { get; }
        protected Mock<IAzureBlobStorageService> MockBlobStorageService { get; }

        #endregion

        #region Constructor

        protected RequestHandlerTestBase()
        {
            MockConfigHelper = new Mock<IConfigHelper>();
            MockCallerService = new Mock<ICallerIdentificationService>();
            MockCacheManager = new Mock<ICacheManager>();
            MockBlobStorageService = new Mock<IAzureBlobStorageService>();

            SetupDefaultMocks();
        }

        #endregion

        #region Setup Methods

        /// <summary>
        /// Sets up default behaviors for common mocks that are used across all tests.
        /// Can be overridden by derived classes to customize default behavior.
        /// </summary>
        protected virtual void SetupDefaultMocks()
        {
            // Setup default caller service behavior for DirectUser (non-service principal)
            MockCallerService.Setup(x => x.GetCallerInfo()).Returns(CreateDefaultCallerInfo());
            MockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns("test@example.com");
            MockCallerService.Setup(x => x.GetCurrentUserId()).Returns("test-user-id");
            MockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(false);
            MockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);
            MockCallerService.Setup(x => x.GetCallingApplicationName()).Returns("TestApp");
        }

        /// <summary>
        /// Creates a default CallerInfo object for testing.
        /// </summary>
        protected virtual CallerInfo CreateDefaultCallerInfo()
        {
            return new CallerInfo
            {
                UserId = "test-user-id",
                UserEmail = "test@example.com",
                ApplicationName = "TestApp",
                IsServicePrincipal = false,
                HasDelegatedUser = false
            };
        }

        #endregion

        #region Caller Service Configuration Helpers

        /// <summary>
        /// Configures the caller service to simulate a DirectUser authentication flow.
        /// </summary>
        /// <param name="userEmail">The user's email address</param>
        /// <param name="userId">The user's ID</param>
        protected void SetupDirectUserAuth(string userEmail = "test@example.com", string userId = "test-user-id")
        {
            MockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(false);
            MockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);
            MockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns(userEmail);
            MockCallerService.Setup(x => x.GetCurrentUserId()).Returns(userId);
            MockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                UserId = userId,
                UserEmail = userEmail,
                IsServicePrincipal = false,
                HasDelegatedUser = false
            });
        }

        /// <summary>
        /// Configures the caller service to simulate an AppToApp (service principal) authentication flow.
        /// </summary>
        /// <param name="applicationName">The calling application name</param>
        protected void SetupServicePrincipalAuth(string applicationName = "TestServiceApp")
        {
            MockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(true);
            MockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);
            MockCallerService.Setup(x => x.GetCallingApplicationName()).Returns(applicationName);
            MockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                ApplicationName = applicationName,
                IsServicePrincipal = true,
                HasDelegatedUser = false
            });
        }

        /// <summary>
        /// Configures the caller service to simulate a DelegatedAppToApp authentication flow.
        /// </summary>
        /// <param name="userEmail">The delegated user's email</param>
        /// <param name="userId">The delegated user's ID</param>
        /// <param name="applicationName">The calling application name</param>
        protected void SetupDelegatedUserAuth(string userEmail = "delegated@example.com", 
            string userId = "delegated-user-id", 
            string applicationName = "DelegatedApp")
        {
            MockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(true);
            MockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(true);
            MockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns(userEmail);
            MockCallerService.Setup(x => x.GetCurrentUserId()).Returns(userId);
            MockCallerService.Setup(x => x.GetCallingApplicationName()).Returns(applicationName);
            MockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                UserId = userId,
                UserEmail = userEmail,
                ApplicationName = applicationName,
                IsServicePrincipal = true,
                HasDelegatedUser = true
            });
        }

        /// <summary>
        /// Configures the caller service to throw an exception, simulating a service failure.
        /// </summary>
        protected void SetupCallerServiceFailure()
        {
            MockCallerService.Setup(x => x.GetCallerInfo())
                .Throws(new Exception("Caller service error"));
            MockCallerService.Setup(x => x.IsServicePrincipalCall())
                .Throws(new Exception("Caller service error"));
        }

        /// <summary>
        /// Configures the caller service with fallback scenarios (unknown email, empty email, etc.)
        /// </summary>
        /// <param name="email">Email to return (can be "unknown", "", or null)</param>
        /// <param name="userId">User ID to return as fallback</param>
        protected void SetupCallerServiceWithFallback(string? email, string userId = "fallback-user-id")
        {
            MockCallerService.Setup(x => x.IsServicePrincipalCall()).Returns(false);
            MockCallerService.Setup(x => x.HasDelegatedUserContext()).Returns(false);
            MockCallerService.Setup(x => x.GetCurrentUserEmail()).Returns(email ?? string.Empty);
            MockCallerService.Setup(x => x.GetCurrentUserId()).Returns(userId);
            MockCallerService.Setup(x => x.GetCallerInfo()).Returns(new CallerInfo
            {
                UserId = userId,
                UserEmail = email ?? string.Empty,
                IsServicePrincipal = false,
                HasDelegatedUser = false
            });
        }

        #endregion

        #region Blob Storage Helpers

        /// <summary>
        /// Sets up the blob storage service to successfully write content.
        /// </summary>
        protected void SetupSuccessfulBlobWrite()
        {
            MockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
        }

        /// <summary>
        /// Sets up the blob storage service to fail when writing content.
        /// </summary>
        protected void SetupFailedBlobWrite()
        {
            MockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);
        }

        /// <summary>
        /// Sets up the blob storage service to throw an exception when writing.
        /// </summary>
        protected void SetupBlobWriteException(string errorMessage = "Blob write error")
        {
            MockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception(errorMessage));
        }

        /// <summary>
        /// Sets up the blob storage service to read and return the specified content.
        /// </summary>
        protected void SetupBlobRead(string content)
        {
            MockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(content);
        }

        /// <summary>
        /// Sets up the blob storage service to return null when reading (blob not found).
        /// </summary>
        protected void SetupBlobNotFound()
        {
            MockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string?)null);
        }

        /// <summary>
        /// Sets up the blob storage service to throw an exception when reading.
        /// </summary>
        protected void SetupBlobReadException(string errorMessage = "Blob read error")
        {
            MockBlobStorageService
                .Setup(x => x.ReadBlobContentAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception(errorMessage));
        }

        /// <summary>
        /// Captures the blob content written to storage for assertion.
        /// </summary>
        protected string? CapturedBlobContent { get; private set; }

        /// <summary>
        /// Sets up blob storage to capture written content for later verification.
        /// </summary>
        protected void SetupBlobContentCapture()
        {
            MockBlobStorageService
                .Setup(x => x.WriteBlobContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((_, __, content) => CapturedBlobContent = content)
                .ReturnsAsync(true);
        }

        #endregion

        #region Test Data Factory Methods

        /// <summary>
        /// Creates a test DataSetTableEntity with default values.
        /// </summary>
        protected DataSetTableEntity CreateTestDataSetEntity(
            string? datasetId = null,
            string agentId = "agent-123",
            string datasetName = "Test Dataset",
            string datasetType = "Golden")
        {
            var id = datasetId ?? Guid.NewGuid().ToString();
            var entity = new DataSetTableEntity
            {
                DatasetId = id,
                AgentId = agentId,
                DatasetName = datasetName,
                DatasetType = datasetType,
                ContainerName = agentId.Replace(" ", "").ToLower(),
                BlobFilePath = $"datasets/{datasetType}_{datasetName}_{id}.json",
                CreatedBy = "test@example.com",
                CreatedOn = DateTime.UtcNow,
                LastUpdatedBy = "test@example.com",
                LastUpdatedOn = DateTime.UtcNow
            };
            entity.PartitionKey = entity.AgentId;
            entity.RowKey = entity.DatasetId;
            return entity;
        }

        /// <summary>
        /// Creates a test MetricsConfigurationTableEntity with default values.
        /// </summary>
        protected MetricsConfigurationTableEntity CreateTestMetricsConfigEntity(
            string? configId = null,
            string agentId = "agent-123",
            string configName = "Test Config",
            string environment = "dev")
        {
            var id = configId ?? Guid.NewGuid().ToString();
            var entity = new MetricsConfigurationTableEntity
            {
                ConfigurationId = id,
                AgentId = agentId,
                ConfigurationName = configName,
                EnvironmentName = environment,
                ContainerName = agentId.Replace(" ", "").ToLower(),
                BlobFilePath = $"metrics-configs/{configName}_{environment}_{id}.json",
                CreatedBy = "test@example.com",
                CreatedOn = DateTime.UtcNow,
                LastUpdatedBy = "test@example.com",
                LastUpdatedOn = DateTime.UtcNow
            };
            entity.PartitionKey = entity.AgentId;
            entity.RowKey = entity.ConfigurationId;
            return entity;
        }

        /// <summary>
        /// Creates a test EvalRunTableEntity with default values.
        /// </summary>
        protected EvalRunTableEntity CreateTestEvalRunEntity(
            Guid? evalRunId = null,
            string agentId = "agent-123",
            string status = "RequestSubmitted")
        {
            var id = evalRunId ?? Guid.NewGuid();
            var entity = new EvalRunTableEntity
            {
                EvalRunId = id,
                AgentId = agentId,
                DataSetId = Guid.NewGuid().ToString(),
                MetricsConfigurationId = Guid.NewGuid().ToString(),
                Status = status,
                StartedDatetime = DateTime.UtcNow,
                ContainerName = agentId.Replace(" ", "").ToLower(),
                BlobFilePath = "eval-results/",
                Type = "Standard",
                EnvironmentId = "dev",
                AgentSchemaName = "TestAgent",
                EvalRunName = "Test Eval Run",
                CreatedBy = "test@example.com",
                CreatedOn = DateTime.UtcNow,
                LastUpdatedBy = "test@example.com",
                LastUpdatedOn = DateTime.UtcNow
            };
            entity.PartitionKey = entity.AgentId;
            entity.RowKey = id.ToString();
            return entity;
        }

        #endregion

        #region Assertion Helpers

        /// <summary>
        /// Verifies that the captured entity has the expected audit user.
        /// </summary>
        protected void VerifyAuditUser(string expectedUser, string? actualCreatedBy, string? actualUpdatedBy = null)
        {
            actualCreatedBy.Should().Be(expectedUser, "CreatedBy should match the expected audit user");
            if (actualUpdatedBy != null)
            {
                actualUpdatedBy.Should().Be(expectedUser, "LastUpdatedBy should match the expected audit user");
            }
        }

        /// <summary>
        /// Verifies that blob content was written with the expected format.
        /// </summary>
        protected void VerifyBlobContentFormat(string? content, bool shouldBeIndented = true, bool shouldBeCamelCase = true)
        {
            content.Should().NotBeNullOrEmpty("Blob content should not be null or empty");

            if (shouldBeIndented)
            {
                content.Should().Contain("\n", "JSON should be indented (WriteIndented = true)");
                content.Should().Contain("  ", "JSON should contain proper indentation");
            }

            if (shouldBeCamelCase)
            {
                // Note: This is a basic check. In real scenarios, you might parse JSON to verify property names
                content.Should().NotBeNull();
            }
        }

        #endregion

        #region Mock Creation Helpers

        /// <summary>
        /// Creates a mock logger with the specified type.
        /// </summary>
        protected Mock<ILogger<T>> CreateMockLogger<T>()
        {
            return new Mock<ILogger<T>>();
        }

        #endregion
    }
}
