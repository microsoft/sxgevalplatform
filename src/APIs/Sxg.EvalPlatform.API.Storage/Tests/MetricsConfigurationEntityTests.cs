using Sxg.EvalPlatform.API.Storage.TableEntities;
using Sxg.EvalPlatform.API.Storage.Helpers;

namespace Sxg.EvalPlatform.API.Storage.Tests
{
    /// <summary>
    /// Simple test examples to verify automatic key setting behavior
    /// </summary>
    public static class MetricsConfigurationEntityTests
    {
        /// <summary>
        /// Test that demonstrates automatic key setting in constructor
        /// </summary>
        public static void TestConstructorBehavior()
        {
            // Arrange & Act
            var entity = new MetricsConfigurationTableEntity();

            // Verify
            Console.WriteLine($"Constructor Test:");
            Console.WriteLine($"ConfigurationId: {entity.ConfigurationId}");
            Console.WriteLine($"RowKey: {entity.RowKey}");
            Console.WriteLine($"ConfigurationId == RowKey: {entity.ConfigurationId == entity.RowKey}");
            Console.WriteLine($"ConfigurationId is valid GUID: {MetricsConfigurationEntityHelper.IsValidGuid(entity.ConfigurationId)}");
            Console.WriteLine();
        }

        /// <summary>
        /// Test that demonstrates automatic PartitionKey setting when AgentId is set
        /// </summary>
        public static void TestAgentIdPartitionKeyBehavior()
        {
            // Arrange
            var entity = new MetricsConfigurationTableEntity();
            var testAgentId = "test-agent-001";

            // Act
            entity.AgentId = testAgentId;

            // Verify
            Console.WriteLine($"AgentId Property Test:");
            Console.WriteLine($"AgentId: {entity.AgentId}");
            Console.WriteLine($"PartitionKey: {entity.PartitionKey}");
            Console.WriteLine($"AgentId == PartitionKey: {entity.AgentId == entity.PartitionKey}");
            Console.WriteLine();
        }

        /// <summary>
        /// Test that demonstrates automatic RowKey setting when ConfigurationId is set
        /// </summary>
        public static void TestConfigurationIdRowKeyBehavior()
        {
            // Arrange
            var entity = new MetricsConfigurationTableEntity();
            var testConfigId = Guid.NewGuid().ToString();

            // Act
            entity.ConfigurationId = testConfigId;

            // Verify
            Console.WriteLine($"ConfigurationId Property Test:");
            Console.WriteLine($"ConfigurationId: {entity.ConfigurationId}");
            Console.WriteLine($"RowKey: {entity.RowKey}");
            Console.WriteLine($"ConfigurationId == RowKey: {entity.ConfigurationId == entity.RowKey}");
            Console.WriteLine();
        }

        /// <summary>
        /// Test helper method creation with automatic key setting
        /// </summary>
        public static void TestHelperMethodBehavior()
        {
            // Arrange & Act
            var entity = MetricsConfigurationEntityHelper.CreateEntity(
                agentId: "agent-002",
                configurationName: "test-config",
                environmentName: "development"
            );

            // Verify
            Console.WriteLine($"Helper Method Test:");
            Console.WriteLine($"AgentId: {entity.AgentId}");
            Console.WriteLine($"PartitionKey: {entity.PartitionKey}");
            Console.WriteLine($"ConfigurationId: {entity.ConfigurationId}");
            Console.WriteLine($"RowKey: {entity.RowKey}");
            Console.WriteLine($"AgentId == PartitionKey: {entity.AgentId == entity.PartitionKey}");
            Console.WriteLine($"ConfigurationId == RowKey: {entity.ConfigurationId == entity.RowKey}");
            Console.WriteLine($"Keys are valid: {MetricsConfigurationEntityHelper.ValidateKeys(entity)}");
            Console.WriteLine();
        }

        /// <summary>
        /// Test helper method creation with specific configuration ID
        /// </summary>
        public static void TestHelperMethodWithSpecificId()
        {
            // Arrange
            var specificId = "12345678-1234-1234-1234-123456789012";

            // Act
            var entity = MetricsConfigurationEntityHelper.CreateEntity(
                agentId: "agent-003",
                configurationName: "specific-config",
                environmentName: "production",
                configurationId: specificId
            );

            // Verify
            Console.WriteLine($"Helper Method with Specific ID Test:");
            Console.WriteLine($"AgentId: {entity.AgentId}");
            Console.WriteLine($"PartitionKey: {entity.PartitionKey}");
            Console.WriteLine($"ConfigurationId: {entity.ConfigurationId}");
            Console.WriteLine($"RowKey: {entity.RowKey}");
            Console.WriteLine($"ConfigurationId == specificId: {entity.ConfigurationId == specificId}");
            Console.WriteLine($"AgentId == PartitionKey: {entity.AgentId == entity.PartitionKey}");
            Console.WriteLine($"ConfigurationId == RowKey: {entity.ConfigurationId == entity.RowKey}");
            Console.WriteLine($"Keys are valid: {MetricsConfigurationEntityHelper.ValidateKeys(entity)}");
            Console.WriteLine();
        }

        /// <summary>
        /// Run all tests to verify automatic key setting behavior
        /// </summary>
        public static void RunAllTests()
        {
            Console.WriteLine("=== MetricsConfigurationEntity Automatic Key Setting Tests ===");
            Console.WriteLine();

            TestConstructorBehavior();
            TestAgentIdPartitionKeyBehavior();
            TestConfigurationIdRowKeyBehavior();
            TestHelperMethodBehavior();
            TestHelperMethodWithSpecificId();

            Console.WriteLine("=== All Tests Completed ===");
        }
    }
}