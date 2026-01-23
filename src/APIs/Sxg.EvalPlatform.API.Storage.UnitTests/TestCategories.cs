namespace Sxg.EvalPlatform.API.Storage.UnitTests
{
    /// <summary>
    /// Constants for xUnit test categories/traits for Storage layer tests.
    /// Used to filter and organize test execution.
    /// </summary>
    /// <remarks>
    /// Usage in tests:
    /// <code>
    /// [Trait("Category", TestCategories.Unit)]
    /// [Trait("Category", TestCategories.Service)]
    /// public class MyServiceUnitTests { }
    /// </code>
    /// 
    /// Run specific categories:
    /// <code>
    /// dotnet test --filter "Category=Service"
    /// dotnet test --filter "Category=Unit&amp;Category=Service"
    /// </code>
    /// </remarks>
    public static class TestCategories
    {
        /// <summary>
        /// Unit tests - Tests that run in isolation with mocked dependencies.
        /// </summary>
        public const string Unit = "Unit";

        /// <summary>
        /// Integration tests - Tests that interact with external systems.
        /// </summary>
        public const string Integration = "Integration";

        /// <summary>
        /// Service tests - Tests for service layer components.
        /// </summary>
        public const string Service = "Service";

        /// <summary>
        /// Storage tests - Tests for Azure Storage operations.
        /// </summary>
        public const string Storage = "Storage";

        /// <summary>
        /// Table Storage tests - Tests for Azure Table Storage operations.
        /// </summary>
        public const string TableStorage = "TableStorage";

        /// <summary>
        /// Blob Storage tests - Tests for Azure Blob Storage operations.
        /// </summary>
        public const string BlobStorage = "BlobStorage";

        /// <summary>
        /// Queue Storage tests - Tests for Azure Queue Storage operations.
        /// </summary>
        public const string QueueStorage = "QueueStorage";

        /// <summary>
        /// Cache tests - Tests for caching functionality.
        /// </summary>
        public const string Cache = "Cache";

        /// <summary>
        /// Validation tests - Tests for validation logic.
        /// </summary>
        public const string Validation = "Validation";

        /// <summary>
        /// Configuration tests - Tests for configuration helpers.
        /// </summary>
        public const string Configuration = "Configuration";

        /// <summary>
        /// Helper tests - Tests for helper classes.
        /// </summary>
        public const string Helper = "Helper";

        /// <summary>
        /// Happy path tests - Tests for expected/successful scenarios.
        /// </summary>
        public const string HappyPath = "HappyPath";

        /// <summary>
        /// Error handling tests - Tests for exception and error scenarios.
        /// </summary>
        public const string ErrorHandling = "ErrorHandling";

        /// <summary>
        /// Security tests - Tests for security-related functionality.
        /// </summary>
        public const string Security = "Security";
    }
}
