namespace Sxg.EvalPlatform.API.UnitTests.RequestHandlerTests
{
    /// <summary>
    /// Constants used across request handler unit tests to avoid magic strings and ensure consistency.
    /// </summary>
    public static class TestConstants
    {
        /// <summary>
        /// User-related test constants for authentication and audit scenarios.
        /// </summary>
        public static class Users
        {
            public const string DefaultEmail = "test@example.com";
            public const string DefaultUserId = "test-user-id";
            public const string DelegatedEmail = "delegated@example.com";
            public const string DelegatedUserId = "delegated-user-id";
            public const string UnknownEmail = "unknown";
            public const string SystemUser = "System";
        }

        /// <summary>
        /// Application-related test constants for service principal scenarios.
        /// </summary>
        public static class Applications
        {
            public const string DefaultApp = "TestApp";
            public const string ServiceApp = "TestServiceApp";
            public const string DelegatedApp = "DelegatedApp";
            public const string UnknownApp = "unknown";
        }

        /// <summary>
        /// Agent-related test constants.
        /// </summary>
        public static class Agents
        {
            public const string DefaultAgentId = "agent-123";
            public const string AgentWithSpaces = "agent with spaces";
            public const string NonExistentAgentId = "agent-nonexistent";
            public const string DefaultAgentSchemaName = "TestAgent";
        }

        /// <summary>
        /// Environment-related test constants.
        /// </summary>
        public static class Environments
        {
            public const string Dev = "dev";
            public const string Prod = "prod";
            public const string Test = "test";
            public const string All = "All";
        }

        /// <summary>
        /// Dataset-related test constants.
        /// </summary>
        public static class Datasets
        {
            public const string DefaultName = "Test Dataset";
            public const string GoldenType = "Golden";
            public const string SyntheticType = "Synthetic";
        }

        /// <summary>
        /// Metrics configuration-related test constants.
        /// </summary>
        public static class MetricsConfigs
        {
            public const string DefaultName = "Test Config";
            public const string DefaultMetricsName = "default-metrics.json";
        }

        /// <summary>
        /// Response status constants used in DTOs and responses.
        /// </summary>
        public static class ResponseStatus
        {
            public const string Created = "created";
            public const string Updated = "updated";
            public const string Error = "error";
            public const string Success = "success";
            public const string NotFound = "not_found";
            public const string Failed = "failed";
        }

        /// <summary>
        /// Evaluation run status constants.
        /// </summary>
        public static class EvalRunStatus
        {
            public const string RequestSubmitted = "RequestSubmitted";
            public const string EnrichingDataset = "EnrichingDataset";
            public const string DatasetEnrichmentCompleted = "DatasetEnrichmentCompleted";
            public const string EvalRunStarted = "EvalRunStarted";
            public const string EvalRunCompleted = "EvalRunCompleted";
            public const string EvalRunFailed = "EvalRunFailed";
        }

        /// <summary>
        /// Azure container name constants.
        /// </summary>
        public static class Containers
        {
            public const string PlatformConfigs = "platform-configs";
            public const string DefaultAgentContainer = "agent123";
        }

        /// <summary>
        /// Folder name constants for blob storage.
        /// </summary>
        public static class Folders
        {
            public const string Datasets = "datasets";
            public const string MetricsConfigs = "metrics-configs";
            public const string EvalResults = "eval-results";
        }

        /// <summary>
        /// Blob path constants.
        /// </summary>
        public static class BlobPaths
        {
            public const string DefaultMetricsConfig = "default-metrics.json";
        }

        /// <summary>
        /// Error message constants for testing exception scenarios.
        /// </summary>
        public static class ErrorMessages
        {
            public const string DatabaseError = "Database error";
            public const string DatabaseConnectionFailed = "Database connection failed";
            public const string BlobStorageError = "Blob storage error";
            public const string BlobReadError = "Blob read error";
            public const string BlobWriteError = "Blob write error";
            public const string BlobDeleteFailed = "Blob delete failed";
            public const string CallerServiceError = "Caller service error";
            public const string ServiceUnavailable = "Service unavailable";
            public const string InvalidJson = "{ invalid json }";
            public const string DataVerseApiError = "API Error";
        }

        /// <summary>
        /// Test data constants for queries, responses, etc.
        /// </summary>
        public static class TestData
        {
            public const string DefaultQuery = "What is AI?";
            public const string DefaultGroundTruth = "Artificial Intelligence";
            public const string DefaultActualResponse = "AI is Artificial Intelligence";
            public const string DefaultContext = "Technology context";
            
            public const string Query1 = "Q1";
            public const string Query2 = "Q2";
            public const string Query3 = "Q3";
            
            public const string GroundTruth1 = "GT1";
            public const string GroundTruth2 = "GT2";
            public const string GroundTruth3 = "GT3";
            
            public const string ConversationId = "conv-123";
            public const string CopilotConversationId = "copilot-456";
        }

        /// <summary>
        /// Metrics-related test constants.
        /// </summary>
        public static class Metrics
        {
            public const string Accuracy = "Accuracy";
            public const string Precision = "Precision";
            public const string Recall = "Recall";
            public const double DefaultThreshold = 0.85;
        }

        /// <summary>
        /// HTTP status code constants for testing.
        /// </summary>
        public static class HttpStatusCodes
        {
            public const int Ok = 200;
            public const int Created = 201;
            public const int BadRequest = 400;
            public const int NotFound = 404;
            public const int InternalServerError = 500;
        }

        /// <summary>
        /// Category names for metrics configuration.
        /// </summary>
        public static class Categories
        {
            public const string Quality = "Quality";
            public const string Performance = "Performance";
            public const string Safety = "Safety";
        }
    }
}
