namespace Sxg.EvalPlatform.API.Storage.UnitTests
{
    /// <summary>
    /// Centralized test constants for Storage unit tests.
    /// Provides reusable test data across different test classes.
    /// </summary>
    public static class TestConstants
    {
        /// <summary>
        /// Azure Storage constants
        /// </summary>
        public static class Storage
        {
            public const string AccountName = "teststorageaccount";
            public const string ContainerName = "testcontainer";
            public const string BlobName = "testblob.json";
            public const string BlobContent = "{\"test\":\"data\"}";
            public const string QueueName = "testqueue";
            public const string TableName = "TestTable";
        }

        /// <summary>
        /// Agent-related constants
        /// </summary>
        public static class Agents
        {
            public const string DefaultAgentId = "test-agent-123";
            public const string AgentId1 = "agent-001";
            public const string AgentId2 = "agent-002";
        }

        /// <summary>
        /// Dataset-related constants
        /// </summary>
        public static class Datasets
        {
            public const string DatasetId1 = "dataset-001";
            public const string DatasetId2 = "dataset-002";
            public const string DatasetName = "Test Dataset";
            public const string DatasetType = "TestType";
            public const string DatasetDescription = "Test dataset description";
        }

        /// <summary>
        /// Metrics configuration constants
        /// </summary>
        public static class MetricsConfigs
        {
            public const string ConfigId1 = "config-001";
            public const string ConfigId2 = "config-002";
            public const string ConfigName = "Test Config";
        }

        /// <summary>
        /// Eval run constants
        /// </summary>
        public static class EvalRuns
        {
            public static readonly Guid EvalRunId1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
            public static readonly Guid EvalRunId2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
            public const string EvalRunName = "Test Eval Run";
        }

        /// <summary>
        /// Configuration constants
        /// </summary>
        public static class Config
        {
            public const string LocalEnvironment = "Local";
            public const string ProductionEnvironment = "Production";
            public const string DevelopmentEnvironment = "Development";
            public const int DefaultCacheExpirationMinutes = 30;
        }

        /// <summary>
        /// Cache constants
        /// </summary>
        public static class Cache
        {
            public const string TestKey = "test-cache-key";
            public const string TestValue = "test-cache-value";
            public const int ShortExpirationSeconds = 1;
            public const int DefaultExpirationMinutes = 30;
        }

        /// <summary>
        /// Common test strings
        /// </summary>
        public static class TestStrings
        {
            public const string EmptyString = "";
            public const string WhitespaceString = "   ";
            public const string ValidJson = "{\"key\":\"value\"}";
            public const string InvalidJson = "{invalid json}";
        }
    }
}
