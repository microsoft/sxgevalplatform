namespace Sxg.EvalPlatform.API.UnitTests
{
    /// <summary>
    /// Constants for xUnit test categories/traits.
    /// Used to filter and organize test execution.
    /// </summary>
    /// <remarks>
    /// Usage in tests:
    /// <code>
    /// [Trait("Category", TestCategories.Unit)]
    /// [Trait("Category", TestCategories.Controller)]
    /// public class MyControllerUnitTests { }
    /// </code>
    /// 
    /// Run specific categories:
    /// <code>
    /// dotnet test --filter "Category=Controller"
    /// dotnet test --filter "Category=Unit&amp;Category=Controller"
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
        /// Controller tests - Tests for API controllers.
        /// </summary>
        public const string Controller = "Controller";

        /// <summary>
        /// Request handler tests - Tests for business logic handlers.
        /// </summary>
        public const string RequestHandler = "RequestHandler";

        /// <summary>
        /// Middleware tests - Tests for ASP.NET Core middleware.
        /// </summary>
        public const string Middleware = "Middleware";

        /// <summary>
        /// Service tests - Tests for service layer components.
        /// </summary>
        public const string Service = "Service";

        /// <summary>
        /// Validation tests - Tests for DTO validation logic.
        /// </summary>
        public const string Validation = "Validation";

        /// <summary>
        /// Authentication/Authorization tests - Tests for security features.
        /// </summary>
        public const string Security = "Security";

        /// <summary>
        /// Telemetry/Logging tests - Tests for observability features.
        /// </summary>
        public const string Telemetry = "Telemetry";

        /// <summary>
        /// Performance tests - Tests that verify performance characteristics.
        /// </summary>
        public const string Performance = "Performance";

        /// <summary>
        /// Smoke tests - Quick tests to verify basic functionality.
        /// </summary>
        public const string Smoke = "Smoke";

        /// <summary>
        /// Happy path tests - Tests for expected/successful scenarios.
        /// </summary>
        public const string HappyPath = "HappyPath";

        /// <summary>
        /// Error handling tests - Tests for exception and error scenarios.
        /// </summary>
        public const string ErrorHandling = "ErrorHandling";
    }
}
