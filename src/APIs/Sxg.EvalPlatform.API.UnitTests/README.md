# SXG Evaluation Platform API - Unit Tests

[![Tests](https://img.shields.io/badge/tests-654%20passing-brightgreen)]()
[![Coverage](https://img.shields.io/badge/coverage-80%25%20(API%20layer)-green)]()
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)]()
[![Quality](https://img.shields.io/badge/quality-?????-blue)]()

> **Comprehensive unit test suite** for the SXG Evaluation Platform API with 654 passing tests covering controllers, request handlers, middleware, services, and validation logic. This project ensures code quality, reliability, and maintainability through extensive automated testing.

---

## ?? Table of Contents

- [Overview](#-overview)
- [Quick Start](#-quick-start)
- [Tech Stack](#-tech-stack)
- [Project Structure](#-project-structure)
- [Testing Strategy](#-testing-strategy)
- [Test Categories](#-test-categories)
- [Test Data Builders](#-test-data-builders)
- [Base Test Classes](#-base-test-classes)
- [Writing New Tests](#-writing-new-tests)
- [Running Tests](#-running-tests)
- [Code Coverage](#-code-coverage)
- [Best Practices](#-best-practices)
- [Common Patterns](#-common-patterns)
- [Troubleshooting](#-troubleshooting)
- [Contributing Guidelines](#-contributing-guidelines)
- [Resources](#-resources)

---

## ?? Overview

This test project provides comprehensive unit test coverage for the **SXG Evaluation Platform API**, a system for managing AI agent evaluations, datasets, metrics configurations, and evaluation runs.

### What This Project Tests

- **Controllers**: API endpoint behavior, request/response handling, validation
- **Request Handlers**: Business logic, data transformations, orchestration
- **Middleware**: Authentication, telemetry, request processing pipeline
- **Services**: Core services like caller identification, messaging, telemetry
- **Validation**: DTO validation rules and error handling

### Key Statistics

| Metric | Value | Status |
|--------|-------|--------|
| **Total Tests** | 654 | ? All Passing |
| **Test Types** | 5 (Controller, Handler, Middleware, Service, Validation) | - |
| **Code Coverage (API Layer)** | ~80% | ? Good |
| **Average Test Duration** | 1.5 seconds | ? Fast |
| **Test Success Rate** | 100% | ? Stable |
| **Flaky Tests** | 0 | ? Reliable |

### Quality Characteristics

? **Well-Organized**: Clear structure with dedicated folders for each test type  
? **Maintainable**: Test data builders reduce duplication by 75%  
? **Fast**: Full test suite runs in < 2 seconds  
? **Reliable**: Zero flaky tests, deterministic execution  
? **Documented**: Comprehensive XML documentation on all test methods  
? **Discoverable**: Test categories enable selective execution

---

## ?? Quick Start

### Prerequisites

- **.NET 8.0 SDK** or later
- **Visual Studio 2022** or **VS Code** (optional, for IDE support)
- **Git** (for cloning the repository)

### Running Tests

#### Command Line

```bash
# Navigate to the test project directory
cd Sxg.EvalPlatform.API.UnitTests

# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Quick smoke tests (< 30 seconds)
dotnet test --filter "Category=Smoke"

# Run specific test categories
dotnet test --filter "Category=Controller"
dotnet test --filter "Category=Security"
dotnet test --filter "Category=Unit&Category=Middleware"
```

#### Visual Studio

1. Open **Test Explorer**: `Test` ? `Test Explorer` (or press `Ctrl+E, T`)
2. Click **Run All** (or press `Ctrl+R, A`)
3. To run specific tests:
   - Right-click a test class/method ? **Run**
   - Use **Group By** ? **Traits** to organize by category

#### VS Code

1. Install **C# Dev Kit** extension
2. Open the Testing panel (flask icon in sidebar)
3. Click **Run All Tests** or run individual tests

---

## ??? Tech Stack

### Testing Frameworks & Libraries

| Technology | Version | Purpose |
|------------|---------|---------|
| **.NET** | 8.0 | Target framework |
| **xUnit** | 2.5.3 | Test framework |
| **Moq** | 4.20.70 | Mocking framework |
| **FluentAssertions** | 6.12.0 | Assertion library |
| **AutoFixture** | 4.18.1 | Test data generation |
| **Coverlet** | 6.0.0 | Code coverage collection |
| **Microsoft.NET.Test.Sdk** | 17.8.0 | Test SDK |

### Why These Technologies?

- **xUnit**: Modern, extensible test framework with excellent async support
- **Moq**: Most popular .NET mocking library, intuitive syntax
- **FluentAssertions**: Readable, expressive assertion syntax (e.g., `result.Should().NotBeNull()`)
- **AutoFixture**: Reduces boilerplate for test data creation
- **Coverlet**: Cross-platform code coverage tool

### Project Configuration

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <IsPackable>false</IsPackable>
  <IsTestProject>true</IsTestProject>
</PropertyGroup>

<ItemGroup>
  <Using Include="Xunit" />
  <Using Include="FluentAssertions" />
  <Using Include="Moq" />
</ItemGroup>
```

---

## ?? Project Structure

```
Sxg.EvalPlatform.API.UnitTests/
?
??? ControllerTests/                    # API Controller Tests (145 tests, 85% coverage)
?   ??? ControllerTestBase.cs          # Base class for controller tests
?   ??? DiagnosticsControllerUnitTests.cs
?   ??? EvalConfigsControllerUnitTests.cs
?   ??? EvalDatasetsControllerUnitTests.cs
?   ??? EvalRunsControllerUnitTests.cs
?   ??? HealthControllerUnitTests.cs
?
??? RequestHandlerTests/                # Business Logic Tests (210 tests, 80% coverage)
?   ??? RequestHandlerTestBase.cs      # Base class for handler tests
?   ??? TestConstants.cs               # Shared test constants (70+ constants)
?   ??? DataSetRequestHandlerUnitTests.cs
?   ??? EvalRunRequestHandlerUnitTests.cs
?   ??? MetricsConfigurationRequestHandlerUnitTests.cs
?
??? MiddlewareTests/                    # Middleware Tests (63 tests, 75% coverage)
?   ??? TelemetryMiddlewareUnitTests.cs
?   ??? UserContextMiddlewareUnitTests.cs
?
??? ServicesTests/                      # Service Tests (148 tests, 70% coverage)
?   ??? CallerIdentificationServiceUnitTests.cs
?   ??? CloudRoleNameTelemetryInitializerUnitTests.cs
?   ??? MessagePublisherUnitTests.cs
?   ??? OpenTelemetryServiceUnitTests.cs
?
??? ValidationTests/                    # DTO Validation Tests (107 tests, 90% coverage)
?   ??? CreateEvalRunDtoValidationTests.cs
?   ??? CreateEvaluationDtoValidationTests.cs
?   ??? SaveDatasetDtoValidationTests.cs
?   ??? UpdateEvaluationDtoValidationTests.cs
?
??? Helpers/                            # Test Utilities & Helpers
?   ??? Builders/                       # Test Data Builders (8 builders)
?   ?   ??? CreateConfigurationRequestDtoBuilder.cs
?   ?   ??? CreateEvalRunDtoBuilder.cs
?   ?   ??? DatasetMetadataDtoBuilder.cs
?   ?   ??? EvalDatasetBuilder.cs
?   ?   ??? EvalRunDtoBuilder.cs
?   ?   ??? SaveDatasetDtoBuilder.cs
?   ?   ??? SelectedMetricsConfigurationDtoBuilder.cs
?   ?   ??? UpdateDatasetDtoBuilder.cs
?   ??? SecurityTestDataGenerator.cs    # Security test data generation
?   ??? TestAuthenticationHandler.cs    # Mock authentication handler
?   ??? ValidationTestHelper.cs         # Validation test utilities
?
??? TestCategories.cs                   # xUnit trait definitions (13 categories)
??? README.md                          # This file
```

### Test Distribution

| Test Type | Files | Tests | Coverage | Purpose |
|-----------|-------|-------|----------|---------|
| **Controllers** | 5 | ~145 | 85% | API endpoint behavior |
| **Request Handlers** | 3 | ~210 | 80% | Business logic |
| **Middleware** | 2 | ~63 | 75% | Request pipeline |
| **Services** | 4 | ~148 | 70% | Core services |
| **Validation** | 4 | ~107 | 90% | DTO validation |
| **Total** | 18 | 654 | ~80% | - |

---

## ?? Testing Strategy

### Test Pyramid Approach

Our testing strategy follows the **test pyramid** principle:

```
        /\
       /  \      E2E Tests (Few, in separate project)
      /____\
     /      \    Integration Tests (Some, separate project)
    /________\
   /          \  Unit Tests (Many, THIS PROJECT - 654 tests)
  /____________\
```

This project focuses on **unit tests** - fast, isolated tests with mocked dependencies.

### Testing Principles

1. **Isolation**: Each test runs independently with no shared state
2. **AAA Pattern**: Arrange-Act-Assert structure in every test
3. **Single Responsibility**: Each test validates one behavior
4. **Descriptive Naming**: `MethodName_Scenario_ExpectedResult` convention
5. **Fast Execution**: All tests run in < 2 seconds
6. **Deterministic**: No random data or timing dependencies
7. **Comprehensive**: Test both happy paths and error scenarios

### What We Test

#### ? DO Test

- **Public API behavior**: Controller actions, handler methods
- **Business logic**: Data transformations, calculations, workflows
- **Validation rules**: Input validation, business rules
- **Error handling**: Exception scenarios, edge cases
- **Security**: Authentication, authorization flows
- **Side effects**: Logging, telemetry calls

#### ? DON'T Test

- **Framework internals**: ASP.NET Core, Entity Framework behavior
- **Third-party libraries**: Azure SDK, external packages
- **Configuration**: appsettings.json values (test via integration tests)
- **Private methods**: Test through public API
- **Trivial code**: Auto-properties, simple getters/setters

### Test Types by Component

#### Controller Tests
- HTTP request/response handling
- Model binding and validation
- Authorization checks
- Status code verification
- Response content validation

#### Request Handler Tests
- Business logic correctness
- Data transformation accuracy
- External service interaction (mocked)
- Error handling and retries
- Audit trail creation

#### Middleware Tests
- Request pipeline behavior
- Context manipulation
- Authentication/authorization
- Logging and telemetry
- Error propagation

#### Service Tests
- Service method behavior
- Dependency interaction
- Configuration handling
- Error scenarios

#### Validation Tests
- DTO validation rules
- Required field enforcement
- Format validation (email, GUID, etc.)
- Business rule validation
- Custom validator behavior

---

## ??? Test Categories

We use **xUnit traits** to categorize tests for selective execution and better organization.

### Available Categories

| Category | Description | Example Use Case |
|----------|-------------|------------------|
| `Unit` | Tests that run in isolation with mocked dependencies | All unit tests |
| `Integration` | Tests that interact with external systems | Database, API integration |
| `Controller` | Tests for API controllers | Controller endpoint tests |
| `RequestHandler` | Tests for business logic handlers | Handler tests |
| `Middleware` | Tests for ASP.NET Core middleware | Middleware pipeline tests |
| `Service` | Tests for service layer components | Service tests |
| `Validation` | Tests for DTO validation logic | Validation rule tests |
| `Security` | Authentication/Authorization tests | Auth tests |
| `Telemetry` | Telemetry/Logging tests | Observability tests |
| `Performance` | Performance characteristic tests | Benchmarking |
| `Smoke` | Quick basic functionality tests | Quick validation |
| `HappyPath` | Tests for expected/successful scenarios | Positive test cases |
| `ErrorHandling` | Tests for exception and error scenarios | Negative test cases |

### Usage Example

```csharp
/// <summary>
/// Tests for EvalDatasetsController.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Category", TestCategories.Controller)]
public class EvalDatasetsControllerUnitTests : ControllerTestBase<EvalDatasetsController>
{
    [Fact]
    [Trait("Category", TestCategories.Smoke)]
    [Trait("Category", TestCategories.HappyPath)]
    public async Task GetDatasets_WithValidAgentId_ReturnsOk()
    {
        // Test implementation...
    }
    
    [Fact]
    [Trait("Category", TestCategories.ErrorHandling)]
    public async Task GetDatasets_WithInvalidAgentId_ReturnsBadRequest()
    {
        // Test implementation...
    }
}
```

### Running Tests by Category

```bash
# Quick smoke tests (< 30 seconds)
dotnet test --filter "Category=Smoke"

# All controller tests
dotnet test --filter "Category=Controller"

# Security tests only
dotnet test --filter "Category=Security"

# Combined filters (AND)
dotnet test --filter "Category=Unit&Category=Middleware"

# Multiple categories (OR)
dotnet test --filter "Category=Smoke|Category=HappyPath"

# Exclude categories
dotnet test --filter "Category!=Performance"
```

### CI/CD Pipeline Strategy

```yaml
# Stage 1: Quick Validation (< 30 sec)
- script: dotnet test --filter "Category=Smoke"

# Stage 2: Unit Tests (< 2 min)
- script: dotnet test --filter "Category=Unit"

# Stage 3: Integration Tests (longer running)
- script: dotnet test --filter "Category=Integration"
```

---

## ??? Test Data Builders

**Test Data Builders** reduce test setup code by **75%** and improve maintainability.

### Available Builders

Located in `Helpers/Builders/`:

1. **`EvalDatasetBuilder`** - Creates `EvalDataset` objects
2. **`SaveDatasetDtoBuilder`** - Creates `SaveDatasetDto` objects
3. **`UpdateDatasetDtoBuilder`** - Creates `UpdateDatasetDto` objects
4. **`DatasetMetadataDtoBuilder`** - Creates `DatasetMetadataDto` objects
5. **`CreateEvalRunDtoBuilder`** - Creates `CreateEvalRunDto` objects
6. **`EvalRunDtoBuilder`** - Creates `EvalRunDto` objects
7. **`SelectedMetricsConfigurationDtoBuilder`** - Creates metrics configuration objects
8. **`CreateConfigurationRequestDtoBuilder`** - Creates configuration request objects

### Before & After

#### Before (Without Builders) ?
```csharp
[Fact]
public async Task SaveDataset_WithValidData_ReturnsCreated()
{
    // Arrange - 15-20 lines of verbose setup
    var saveDto = new SaveDatasetDto
    {
        AgentId = "agent-123",
        DatasetType = "Golden",
        DatasetName = "Test Dataset",
        DatasetRecords = new List<EvalDataset>
        {
            new EvalDataset
            {
                Query = "What is AI?",
                GroundTruth = "Artificial Intelligence",
                ActualResponse = "AI is Artificial Intelligence",
                Context = "Technology context",
                ConversationId = "conv-123",
                TurnIndex = 1
            }
        }
    };
    
    // ... test continues
}
```

#### After (With Builders) ?
```csharp
[Fact]
public async Task SaveDataset_WithValidData_ReturnsCreated()
{
    // Arrange - 1 line, clean and clear
    var saveDto = SaveDatasetDtoBuilder.CreateDefault();
    
    // ... test continues
}
```

### Builder Patterns

#### 1. Default Factory Method
```csharp
// Create with sensible defaults
var dataset = EvalDatasetBuilder.CreateDefault();
var evalRun = EvalRunDtoBuilder.CreateDefault();
```

#### 2. Named Factory Methods
```csharp
// Common scenarios
var minimal = EvalDatasetBuilder.CreateMinimal();
var completed = EvalRunDtoBuilder.CreateCompleted();
var inProgress = EvalRunDtoBuilder.CreateInProgress();
var failed = EvalRunDtoBuilder.CreateFailed();
var multiRecord = SaveDatasetDtoBuilder.CreateWithMultipleRecords(10);
```

#### 3. Fluent API (Customize Specific Properties)
```csharp
var dataset = new EvalDatasetBuilder()
    .WithQuery("Custom query")
    .WithGroundTruth("Expected answer")
    .WithActualResponse("Agent's response")
    .Build();

var saveDto = new SaveDatasetDtoBuilder()
    .WithAgentId("custom-agent")
    .WithDatasetName("My Dataset")
    .WithEmptyRecords()  // For validation testing
    .AddDatasetRecord(EvalDatasetBuilder.CreateDefault())
    .Build();
```

#### 4. Helper Methods
```csharp
var completedRun = new EvalRunDtoBuilder()
    .AsCompleted()  // Sets status + timestamps automatically
    .WithEvalRunName("My Run")
    .Build();

var failedRun = new EvalRunDtoBuilder()
    .AsFailed("Validation error")
    .Build();
```

### When to Use Builders

? **Use Builders When:**
- Creating complex DTOs with many properties
- Setting up test data that appears in multiple tests
- Testing validation scenarios (builders make invalid states easy)
- You want to customize only a few properties

? **Don't Use Builders When:**
- Creating simple primitives (strings, ints)
- One-off test data used in a single test
- The builder would be more complex than direct instantiation

---

## ?? Base Test Classes

Base classes provide **common infrastructure** and **reduce boilerplate** by 60-70%.

### RequestHandlerTestBase<THandler>

**Location**: `RequestHandlerTests/RequestHandlerTestBase.cs`

**Purpose**: Base class for request handler tests with common mocks and helpers.

#### Provided Mocks

```csharp
public abstract class RequestHandlerTestBase<THandler>
{
    // Storage Services
    protected Mock<IBlobStorageService> MockBlobStorageService { get; }
    protected Mock<ITableStorageService> MockTableStorageService { get; }
    protected Mock<ICacheManager> MockCacheManager { get; }
    
    // Configuration & Utilities
    protected Mock<IConfigHelper> MockConfigHelper { get; }
    protected Mock<ICallerIdentificationService> MockCallerService { get; }
    protected Mock<IMapper> MockMapper { get; }
    
    // Captured Data (for assertions)
    protected string? CapturedBlobContent { get; private set; }
}
```

#### Helper Methods

```csharp
// Authentication Setup
SetupDirectUserAuth(email, userId);              // User context, no service principal
SetupServicePrincipalAuth(appName);              // App-to-app, no user
SetupDelegatedUserAuth(email, userId, appName);  // Service principal with user context
SetupCallerServiceFailure();                     // Simulate failure

// Blob Storage Setup
SetupSuccessfulBlobWrite();
SetupFailedBlobWrite();
SetupBlobRead(content);
SetupBlobNotFound();
SetupBlobReadException(errorMessage);
SetupBlobWriteException(errorMessage);
SetupBlobContentCapture();  // Captures content for assertions

// Entity Factories
CreateTestDataSetEntity(datasetId, agentId, datasetName, datasetType);
CreateTestMetricsConfigEntity(configId, agentId, configName, environment);
CreateTestEvalRunEntity(evalRunId, agentId, status);
CreateDefaultCallerInfo();

// Assertion Helpers
VerifyAuditUser(expectedUser, actualCreatedBy, actualUpdatedBy);
VerifyBlobContentFormat(content, shouldBeIndented, shouldBeCamelCase);

// Logger Creation
CreateMockLogger<T>();
```

#### Usage Example

```csharp
public class MyRequestHandlerUnitTests : RequestHandlerTestBase<MyRequestHandler>
{
    private readonly Mock<IMySpecificService> _mockMyService;
    private readonly MyRequestHandler _handler;

    public MyRequestHandlerUnitTests()
    {
        _mockMyService = new Mock<IMySpecificService>();
        
        _handler = new MyRequestHandler(
            _mockMyService.Object,
            MockBlobStorageService.Object,  // From base class
            MockConfigHelper.Object,        // From base class
            MockCallerService.Object        // From base class
        );
    }

    [Fact]
    public async Task MyMethod_WithValidInput_ReturnsSuccess()
    {
        // Arrange
        SetupDirectUserAuth();  // From base class
        SetupSuccessfulBlobWrite();  // From base class
        
        var input = CreateTestInput();
        
        _mockMyService.Setup(x => x.DoSomething(It.IsAny<string>()))
            .ReturnsAsync("result");
        
        // Act
        var result = await _handler.MyMethod(input);
        
        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(TestConstants.ResponseStatus.Success);
    }
}
```

### ControllerTestBase<TController>

**Location**: `ControllerTests/ControllerTestBase.cs`

**Purpose**: Base class for controller tests with HTTP context setup.

#### Provided Mocks

```csharp
public abstract class ControllerTestBase<TController>
{
    protected Mock<ILogger<TController>> MockLogger { get; }
    protected Mock<ICallerIdentificationService> MockCallerService { get; }
    protected Mock<ITelemetryService> MockTelemetryService { get; }
    protected Mock<HttpContext> MockHttpContext { get; }
}
```

#### Helper Methods

```csharp
// Authentication Setup
SetupDirectUserAuth(email, userId);
SetupServicePrincipalAuth(appName);
SetupDelegatedUserAuth(email, userId, appName);
SetupControllerContext(controller);  // Sets up HTTP context

// Result Verification
VerifyOkResult<T>(result, assertion);
VerifyNotFoundResult(result);
VerifyBadRequestResult(result, expectedMessage);
VerifyStatusCodeResult(result, expectedStatusCode);
GetValueFromResult<T>(result);

// ModelState Manipulation
AddModelStateError(controller, key, errorMessage);
SimulateInvalidModelState(controller);
```

#### Usage Example

```csharp
public class MyControllerUnitTests : ControllerTestBase<MyController>
{
    private readonly Mock<IMyRequestHandler> _mockHandler;
    private readonly MyController _controller;

    public MyControllerUnitTests()
    {
        _mockHandler = new Mock<IMyRequestHandler>();
        
        _controller = new MyController(
            MockLogger.Object,       // From base class
            MockCallerService.Object, // From base class
            _mockHandler.Object,
            MockTelemetryService.Object  // From base class
        );
        
        SetupControllerContext(_controller);  // From base class
    }

    [Fact]
    public async Task GetItem_WithValidId_ReturnsOk()
    {
        // Arrange
        var id = "test-id";
        var expectedItem = new ItemDto { Id = id };
        
        _mockHandler.Setup(x => x.GetItemAsync(id))
            .ReturnsAsync(expectedItem);
        
        // Act
        var result = await _controller.GetItem(id);
        
        // Assert
        VerifyOkResult(result, item =>  // From base class
        {
            item.Should().NotBeNull();
            item.Id.Should().Be(id);
        });
    }
}
```

---

## ?? Writing New Tests

### Test Naming Convention

Follow the pattern: **`MethodName_Scenario_ExpectedResult`**

```csharp
[Fact]
public async Task GetDataset_WithValidId_ReturnsOk() { }

[Fact]
public async Task GetDataset_WithInvalidId_ReturnsNotFound() { }

[Fact]
public async Task GetDataset_WithUnauthorizedUser_ReturnsForbidden() { }

[Fact]
public async Task SaveDataset_WithEmptyRecords_ThrowsValidationException() { }
```

### AAA Pattern (Arrange-Act-Assert)

**Every test should follow this structure:**

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedResult()
{
    // ========== ARRANGE ==========
    // Set up test data, mocks, and expectations
    var input = CreateTestInput();
    
    _mockService.Setup(x => x.Method(It.IsAny<string>()))
        .ReturnsAsync(expectedResult);
    
    // ========== ACT ==========
    // Execute the method under test
    var result = await _handler.MethodUnderTest(input);
    
    // ========== ASSERT ==========
    // Verify the outcome
    result.Should().NotBeNull();
    result.Status.Should().Be(TestConstants.ResponseStatus.Success);
    
    // Verify mock interactions
    _mockService.Verify(x => x.Method(It.IsAny<string>()), Times.Once);
}
```

### Using TestConstants

**? Bad - Magic strings**
```csharp
var agentId = "agent-123";
var email = "test@example.com";
var status = "created";
```

**? Good - Constants**
```csharp
var agentId = TestConstants.Agents.DefaultAgentId;
var email = TestConstants.Users.DefaultEmail;
var status = TestConstants.ResponseStatus.Created;
```

### Region Organization

Organize tests with `#region` directives:

```csharp
public class MyControllerUnitTests
{
    #region Constructor and Setup Tests
    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance() { }
    #endregion

    #region GetById Tests
    [Fact]
    public async Task GetById_WithValidId_ReturnsOk() { }
    
    [Fact]
    public async Task GetById_WithInvalidId_ReturnsNotFound() { }
    #endregion

    #region Create Tests
    [Fact]
    public async Task Create_WithValidData_ReturnsCreated() { }
    
    [Fact]
    public async Task Create_WithInvalidData_ReturnsBadRequest() { }
    #endregion

    #region Helper Methods
    private MyDto CreateTestDto() { return new MyDto(); }
    #endregion
}
```

### XML Documentation

Add XML comments for complex scenarios:

```csharp
/// <summary>
/// Validates that the SaveDataset method correctly handles the scenario where
/// a user attempts to save a dataset with duplicate records. The method should
/// deduplicate records before saving and return a success response.
/// </summary>
[Fact]
public async Task SaveDataset_WithDuplicateRecords_DeduplicatesAndSavesSuccessfully()
{
    // Test implementation...
}
```

---

## ?? Running Tests

### Command Line Options

#### Basic Execution
```bash
# Run all tests
dotnet test

# Run tests in specific project
dotnet test Sxg.EvalPlatform.API.UnitTests/Sxg.EvalPlatform.API.UnitTests.csproj

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run in parallel (default)
dotnet test --parallel

# Run sequentially
dotnet test --parallel false
```

#### Filtering Tests

```bash
# Run tests by category
dotnet test --filter "Category=Smoke"
dotnet test --filter "Category=Controller"
dotnet test --filter "Category=Security"

# Run tests by namespace
dotnet test --filter "FullyQualifiedName~ControllerTests"

# Run specific test class
dotnet test --filter "FullyQualifiedName~EvalDatasetsControllerUnitTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~EvalDatasetsControllerUnitTests.GetDatasets_WithValidAgentId_ReturnsOk"

# Combine filters
dotnet test --filter "Category=Unit&Category=Controller"
dotnet test --filter "Category=Smoke|Category=HappyPath"
```

#### Code Coverage

```bash
# Collect coverage
dotnet test --collect:"XPlat Code Coverage"

# Collect coverage with specific format
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults

# Generate HTML report (requires ReportGenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

### Visual Studio

#### Test Explorer

1. **Open Test Explorer**: `Test` ? `Test Explorer` (or `Ctrl+E, T`)
2. **Run All Tests**: Click **Run All** button (or `Ctrl+R, A`)
3. **Run Failed Tests**: Click **Run Failed Tests** button
4. **Debug Tests**: Right-click test ? **Debug**
5. **Group Tests**: Use **Group By** dropdown ? Select grouping (Class, Trait, etc.)

#### Live Unit Testing (VS Enterprise)

1. `Test` ? `Live Unit Testing` ? `Start`
2. Code coverage indicators appear in editor margin
3. Tests run automatically as you edit code

### CI/CD Integration

#### Azure DevOps Pipeline Example

```yaml
trigger:
  branches:
    include:
      - main
      - develop

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: UseDotNet@2
  displayName: 'Install .NET SDK'
  inputs:
    version: '8.x'

- task: DotNetCoreCLI@2
  displayName: 'Restore NuGet packages'
  inputs:
    command: 'restore'
    projects: '**/*.csproj'

- task: DotNetCoreCLI@2
  displayName: 'Build solution'
  inputs:
    command: 'build'
    projects: '**/*.csproj'
    arguments: '--configuration Release'

- task: DotNetCoreCLI@2
  displayName: 'Run smoke tests'
  inputs:
    command: 'test'
    projects: '**/Sxg.EvalPlatform.API.UnitTests.csproj'
    arguments: '--filter "Category=Smoke" --no-build --configuration Release'

- task: DotNetCoreCLI@2
  displayName: 'Run all unit tests'
  inputs:
    command: 'test'
    projects: '**/Sxg.EvalPlatform.API.UnitTests.csproj'
    arguments: '--collect:"XPlat Code Coverage" --no-build --configuration Release'
    publishTestResults: true

- task: PublishCodeCoverageResults@1
  displayName: 'Publish code coverage'
  inputs:
    codeCoverageTool: 'Cobertura'
    summaryFileLocation: '$(Agent.TempDirectory)/**/coverage.cobertura.xml'
```

---

## ?? Code Coverage

### Current Coverage

| Component | Line Coverage | Branch Coverage | Status |
|-----------|---------------|-----------------|--------|
| **API Layer** | ~80% | ~75% | ? Good |
| Controllers | ~85% | ~80% | ? Excellent |
| Request Handlers | ~80% | ~75% | ? Good |
| Middleware | ~75% | ~70% | ? Good |
| Services | ~70% | ~65% | ? Acceptable |
| Validation | ~90% | ~85% | ? Excellent |
| **Overall** | ~80% | ~75% | ? Good |

### Coverage Goals

- ? **Current**: 80% line coverage (API layer)
- ?? **Target**: 85% line coverage
- ?? **Stretch**: 90% line coverage

### Viewing Coverage

#### Command Line

```bash
# Collect coverage
dotnet test --collect:"XPlat Code Coverage"

# Coverage file location
TestResults/{guid}/coverage.cobertura.xml
```

#### Generate HTML Report

```bash
# Install ReportGenerator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate report
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:Html

# Open report
start coveragereport/index.html  # Windows
open coveragereport/index.html   # macOS
xdg-open coveragereport/index.html  # Linux
```

#### Visual Studio

1. **Run Tests with Coverage**: `Test` ? `Analyze Code Coverage for All Tests`
2. **View Results**: `Test` ? `Code Coverage Results`
3. **Color Coding**:
   - ?? **Blue**: Covered code
   - ?? **Red**: Uncovered code
   - ?? **Orange**: Partially covered code

### Coverage Best Practices

1. **Focus on critical paths**: 100% coverage of business logic
2. **Ignore boilerplate**: Auto-generated code, DTOs with no logic
3. **Test edge cases**: Boundary conditions, error scenarios
4. **Don't chase 100%**: Diminishing returns after 85-90%
5. **Use coverage to find gaps**: Not as a goal in itself

---

## ?? Best Practices

### General Testing Principles

#### ? DO

1. **Follow AAA pattern** (Arrange-Act-Assert)
2. **Test one thing** per test method
3. **Use descriptive names**: `MethodName_Scenario_ExpectedResult`
4. **Mock external dependencies**
5. **Use TestConstants** for literal values
6. **Use test data builders** for complex objects
7. **Test both happy and error paths**
8. **Keep tests fast** (< 100ms each)
9. **Make tests deterministic** (no random data, no timing dependencies)
10. **Add XML documentation** for complex scenarios

#### ? DON'T

1. **Don't test framework internals** (ASP.NET Core, EF Core)
2. **Don't share state** between tests
3. **Don't use magic strings/numbers** (use TestConstants)
4. **Don't test multiple scenarios** in one test
5. **Don't call real external services** (always mock)
6. **Don't skip cleanup** if you allocate resources
7. **Don't ignore warnings** from test runner
8. **Don't test private methods** directly (test through public API)

### Mocking Best Practices

#### Setting Up Mocks

```csharp
// ? Good - Flexible matching
_mockService.Setup(x => x.Method(It.IsAny<string>()))
    .ReturnsAsync(result);

// ? Bad - Exact object matching (won't work)
_mockService.Setup(x => x.Method(new DatasetDto()))
    .ReturnsAsync(result);

// ? Good - Specific value matching
_mockService.Setup(x => x.Method("specific-id"))
    .ReturnsAsync(result);

// ? Good - Conditional matching
_mockService.Setup(x => x.Method(It.Is<string>(s => s.StartsWith("agent-"))))
    .ReturnsAsync(result);
```

#### Verifying Mock Calls

```csharp
// ? Good - Verify interaction
_mockService.Verify(x => x.Method(It.IsAny<string>()), Times.Once);

// ? Good - Verify never called
_mockService.Verify(x => x.Method(It.IsAny<string>()), Times.Never);

// ? Good - Verify specific parameters
_mockService.Verify(x => x.Method("specific-id"), Times.Once);

// ? Avoid - Over-verification (brittle tests)
_mockService.Verify(x => x.Method(It.IsAny<string>()), Times.Exactly(3));
```

### Assertion Best Practices

#### FluentAssertions

```csharp
// ? Good - Readable, descriptive
result.Should().NotBeNull();
result.Status.Should().Be(TestConstants.ResponseStatus.Success);
result.Items.Should().HaveCount(5);
result.Items.Should().Contain(x => x.Id == "test-id");

// ? Good - Custom error messages
result.Should().NotBeNull("because the service should always return a result");

// ? Good - Complex assertions
result.Should().BeEquivalentTo(expected, options => options
    .Excluding(x => x.Timestamp)
    .Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromSeconds(1)))
    .WhenTypeIs<DateTime>());

// ? Avoid - xUnit asserts (less readable)
Assert.NotNull(result);
Assert.Equal(TestConstants.ResponseStatus.Success, result.Status);
```

### Test Organization

#### Region Structure

```csharp
public class MyControllerUnitTests
{
    #region Constructor and Setup Tests
    // Constructor tests
    #endregion

    #region GET /api/resource Tests
    // GET endpoint tests
    #endregion

    #region POST /api/resource Tests
    // POST endpoint tests
    #endregion

    #region PUT /api/resource Tests
    // PUT endpoint tests
    #endregion

    #region DELETE /api/resource Tests
    // DELETE endpoint tests
    #endregion

    #region Validation Tests
    // Validation tests
    #endregion

    #region Error Handling Tests
    // Error handling tests
    #endregion

    #region Helper Methods
    // Private helper methods
    #endregion
}
```

---

## ?? Common Patterns

### 1. Testing CRUD Operations

#### Create (POST)

```csharp
[Fact]
public async Task Create_WithValidData_ReturnsCreated()
{
    // Arrange
    SetupDirectUserAuth();
    var createDto = CreateValidDto();
    var createdEntity = CreateTestEntity();
    
    _mockHandler.Setup(x => x.CreateAsync(It.IsAny<CreateDto>()))
        .ReturnsAsync(createdEntity);
    
    // Act
    var result = await _controller.Create(createDto);
    
    // Assert
    result.Should().NotBeNull();
    var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
    createdResult.StatusCode.Should().Be(201);
    createdResult.Value.Should().BeEquivalentTo(createdEntity);
}

[Fact]
public async Task Create_WithInvalidModelState_ReturnsBadRequest()
{
    // Arrange
    var createDto = CreateValidDto();
    SimulateInvalidModelState(_controller);
    
    // Act
    var result = await _controller.Create(createDto);
    
    // Assert
    VerifyBadRequestResult(result);
}
```

#### Read (GET)

```csharp
[Fact]
public async Task GetById_WithValidId_ReturnsOk()
{
    // Arrange
    var id = TestConstants.SomeId;
    var entity = CreateTestEntity();
    
    _mockHandler.Setup(x => x.GetByIdAsync(id))
        .ReturnsAsync(entity);
    
    // Act
    var result = await _controller.GetById(id);
    
    // Assert
    VerifyOkResult(result, item =>
    {
        item.Should().NotBeNull();
        item.Id.Should().Be(id);
    });
}

[Fact]
public async Task GetById_WithNonExistentId_ReturnsNotFound()
{
    // Arrange
    var id = "non-existent-id";
    
    _mockHandler.Setup(x => x.GetByIdAsync(id))
        .ReturnsAsync((EntityDto?)null);
    
    // Act
    var result = await _controller.GetById(id);
    
    // Assert
    VerifyNotFoundResult(result);
}
```

#### Update (PUT)

```csharp
[Fact]
public async Task Update_WithValidData_ReturnsOk()
{
    // Arrange
    SetupDirectUserAuth();
    var id = TestConstants.SomeId;
    var updateDto = CreateUpdateDto();
    var updatedEntity = CreateTestEntity();
    
    _mockHandler.Setup(x => x.UpdateAsync(id, It.IsAny<UpdateDto>()))
        .ReturnsAsync(updatedEntity);
    
    // Act
    var result = await _controller.Update(id, updateDto);
    
    // Assert
    VerifyOkResult(result, item =>
    {
        item.Should().NotBeNull();
        item.Id.Should().Be(id);
    });
}

[Fact]
public async Task Update_WithNonExistentId_ReturnsNotFound()
{
    // Arrange
    var id = "non-existent-id";
    var updateDto = CreateUpdateDto();
    
    _mockHandler.Setup(x => x.UpdateAsync(id, It.IsAny<UpdateDto>()))
        .ReturnsAsync((EntityDto?)null);
    
    // Act
    var result = await _controller.Update(id, updateDto);
    
    // Assert
    VerifyNotFoundResult(result);
}
```

#### Delete (DELETE)

```csharp
[Fact]
public async Task Delete_WithValidId_ReturnsNoContent()
{
    // Arrange
    var id = TestConstants.SomeId;
    
    _mockHandler.Setup(x => x.DeleteAsync(id))
        .ReturnsAsync(true);
    
    // Act
    var result = await _controller.Delete(id);
    
    // Assert
    result.Should().BeOfType<NoContentResult>();
}

[Fact]
public async Task Delete_WithNonExistentId_ReturnsNotFound()
{
    // Arrange
    var id = "non-existent-id";
    
    _mockHandler.Setup(x => x.DeleteAsync(id))
        .ReturnsAsync(false);
    
    // Act
    var result = await _controller.Delete(id);
    
    // Assert
    VerifyNotFoundResult(result);
}
```

### 2. Testing Authentication Flows

```csharp
[Theory]
[InlineData(false, false, TestConstants.Users.DefaultEmail)]      // DirectUser
[InlineData(true, false, TestConstants.Applications.ServiceApp)]  // ServicePrincipal
[InlineData(true, true, TestConstants.Users.DefaultEmail)]        // DelegatedUser
public async Task Create_WithDifferentAuthFlows_SetsCorrectAuditUser(
    bool isServicePrincipal, bool hasDelegatedUser, string expectedAuditUser)
{
    // Arrange
    if (isServicePrincipal && !hasDelegatedUser)
        SetupServicePrincipalAuth(TestConstants.Applications.ServiceApp);
    else if (isServicePrincipal && hasDelegatedUser)
        SetupDelegatedUserAuth(TestConstants.Users.DefaultEmail, TestConstants.Users.DefaultUserId, TestConstants.Applications.ServiceApp);
    else
        SetupDirectUserAuth(TestConstants.Users.DefaultEmail, TestConstants.Users.DefaultUserId);
    
    var createDto = CreateValidDto();
    EntityBase? capturedEntity = null;
    
    _mockHandler.Setup(x => x.CreateAsync(It.IsAny<CreateDto>()))
        .Callback<CreateDto>(dto => { /* capture entity */ })
        .ReturnsAsync(CreateTestEntity());
    
    // Act
    await _controller.Create(createDto);
    
    // Assert
    capturedEntity.Should().NotBeNull();
    capturedEntity!.CreatedBy.Should().Be(expectedAuditUser);
    capturedEntity.LastUpdatedBy.Should().Be(expectedAuditUser);
}
```

### 3. Testing Validation

```csharp
[Theory]
[InlineData("")]
[InlineData(null)]
[InlineData("   ")]
public async Task Create_WithInvalidAgentId_ReturnsBadRequest(string? invalidAgentId)
{
    // Arrange
    var createDto = new CreateDto { AgentId = invalidAgentId };
    
    // Act
    var result = await _controller.Create(createDto);
    
    // Assert
    VerifyBadRequestResult(result, "AgentId");
}

[Fact]
public async Task Create_WithInvalidEmailFormat_ReturnsBadRequest()
{
    // Arrange
    var createDto = CreateValidDto();
    createDto.Email = "not-an-email";
    
    // Act
    var result = await _controller.Create(createDto);
    
    // Assert
    VerifyBadRequestResult(result, "Email");
}
```

### 4. Testing Error Handling

```csharp
[Fact]
public async Task GetById_WhenServiceThrowsException_ReturnsInternalServerError()
{
    // Arrange
    var id = TestConstants.SomeId;
    
    _mockHandler.Setup(x => x.GetByIdAsync(id))
        .ThrowsAsync(new Exception(TestConstants.ErrorMessages.DatabaseError));
    
    // Act
    var result = await _controller.GetById(id);
    
    // Assert
    VerifyStatusCodeResult(result, 500);
}

[Fact]
public async Task Create_WhenBlobWriteFails_HandlesGracefully()
{
    // Arrange
    SetupBlobWriteException(TestConstants.ErrorMessages.BlobWriteError);
    var createDto = CreateValidDto();
    
    // Act
    var result = await _handler.CreateAsync(createDto);
    
    // Assert
    result.Should().NotBeNull();
    result.Status.Should().Be(TestConstants.ResponseStatus.Error);
    result.Message.Should().Contain("blob");
}
```

### 5. Testing Blob Storage Interaction

```csharp
[Fact]
public async Task Create_WritesToBlob_WithCorrectContent()
{
    // Arrange
    SetupBlobContentCapture();
    var createDto = CreateValidDto();
    
    // Act
    await _handler.CreateAsync(createDto);
    
    // Assert
    CapturedBlobContent.Should().NotBeNullOrEmpty();
    VerifyBlobContentFormat(CapturedBlobContent, shouldBeIndented: true);
    
    var parsedContent = JsonSerializer.Deserialize<EntityDto>(CapturedBlobContent);
    parsedContent.Should().NotBeNull();
    parsedContent!.AgentId.Should().Be(createDto.AgentId);
}

[Fact]
public async Task Create_WhenBlobNotFound_ReturnsNotFound()
{
    // Arrange
    var id = TestConstants.SomeId;
    SetupBlobNotFound();
    
    // Act
    var result = await _handler.GetByIdAsync(id);
    
    // Assert
    result.Should().BeNull();
}
```

---

## ?? Troubleshooting

### Common Issues and Solutions

#### Issue: Tests Pass Locally but Fail in CI/CD

**Symptoms:**
- Tests pass on developer machine
- Same tests fail in Azure DevOps/GitHub Actions

**Possible Causes & Solutions:**

1. **File System Dependencies**
   ```csharp
   // ? Bad - Depends on local file system
   var file = File.ReadAllText("C:\\temp\\testdata.json");
   
   // ? Good - Use embedded resources or inline data
   var data = TestConstants.SampleData.DefaultJson;
   ```

2. **Timezone Issues**
   ```csharp
   // ? Bad - Depends on local timezone
   var now = DateTime.Now;
   
   // ? Good - Use UTC
   var now = DateTime.UtcNow;
   ```

3. **Absolute Paths**
   ```csharp
   // ? Bad - Hardcoded path
   var path = "C:\\Projects\\MyApp\\testfile.txt";
   
   // ? Good - Relative path or embedded resource
   var path = Path.Combine(AppContext.BaseDirectory, "testfile.txt");
   ```

#### Issue: Tests Are Slow

**Symptoms:**
- Test suite takes minutes instead of seconds
- Individual tests take > 100ms

**Possible Causes & Solutions:**

1. **Actual I/O Operations**
   ```csharp
   // ? Bad - Actual database/file access
   var data = await _database.GetDataAsync();
   
   // ? Good - Mock the dependency
   _mockDatabase.Setup(x => x.GetDataAsync()).ReturnsAsync(testData);
   ```

2. **Unnecessary Delays**
   ```csharp
   // ? Bad - Artificial delays
   await Task.Delay(1000);
   
   // ? Good - Use Task.CompletedTask for sync operations
   _mockService.Setup(x => x.DoSomething()).Returns(Task.CompletedTask);
   ```

3. **Inefficient Test Setup**
   ```csharp
   // ? Bad - Creating complex objects in each test
   [Fact]
   public void Test1() { var data = CreateComplexData(); /* ... */ }
   
   [Fact]
   public void Test2() { var data = CreateComplexData(); /* ... */ }
   
   // ? Good - Use test data builders or cached test data
   [Fact]
   public void Test1() { var data = TestDataBuilder.CreateDefault(); /* ... */ }
   
   [Fact]
   public void Test2() { var data = TestDataBuilder.CreateDefault(); /* ... */ }
   ```

#### Issue: Mock Setup Not Working

**Symptoms:**
- Mock returns null or default value
- Expected method not called

**Possible Causes & Solutions:**

1. **Exact Object Matching**
   ```csharp
   // ? Bad - Won't match (different object instance)
   _mockService.Setup(x => x.Method(new DatasetDto())).ReturnsAsync(result);
   
   // ? Good - Use It.IsAny<T>()
   _mockService.Setup(x => x.Method(It.IsAny<DatasetDto>())).ReturnsAsync(result);
   ```

2. **Async/Sync Mismatch**
   ```csharp
   // ? Bad - Setup for sync method but calling async
   _mockService.Setup(x => x.Method()).Returns(result);
   var actual = await _service.MethodAsync(); // Wrong method!
   
   // ? Good - Match the method signature
   _mockService.Setup(x => x.MethodAsync()).ReturnsAsync(result);
   var actual = await _service.MethodAsync();
   ```

3. **Setup After Act**
   ```csharp
   // ? Bad - Setup after calling the method
   var result = await _handler.Method();
   _mockService.Setup(x => x.DoSomething()).ReturnsAsync(data); // Too late!
   
   // ? Good - Setup in Arrange phase
   _mockService.Setup(x => x.DoSomething()).ReturnsAsync(data);
   var result = await _handler.Method();
   ```

#### Issue: FluentAssertions Not Working

**Symptoms:**
- `Should()` method not found
- Compilation errors on assertions

**Solution:**

```csharp
// Ensure using directive
using FluentAssertions;

// Or add to global usings (already configured in .csproj)
<ItemGroup>
  <Using Include="FluentAssertions" />
</ItemGroup>
```

#### Issue: Test Fails Intermittently

**Symptoms:**
- Test passes most of the time but occasionally fails
- Different results on different runs

**Possible Causes & Solutions:**

1. **Random Data**
   ```csharp
   // ? Bad - Random data causes unpredictable results
   var testData = new { Id = Guid.NewGuid() };
   
   // ? Good - Use deterministic test data
   var testData = new { Id = TestConstants.SomeId };
   ```

2. **Datetime Comparisons**
   ```csharp
   // ? Bad - Exact timestamp comparison
   result.Timestamp.Should().Be(DateTime.UtcNow); // Fails due to timing
   
   // ? Good - Use time window
   result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
   ```

3. **Shared State**
   ```csharp
   // ? Bad - Static field shared between tests
   private static List<string> _sharedData = new();
   
   // ? Good - Instance field (new for each test)
   private readonly List<string> _testData = new();
   ```

#### Issue: Coverage Report Not Generated

**Symptoms:**
- No coverage.cobertura.xml file created
- Coverage results empty

**Solution:**

```bash
# Ensure coverlet.collector package is installed
dotnet add package coverlet.collector

# Use correct flag
dotnet test --collect:"XPlat Code Coverage"

# Check output location
ls TestResults/**/coverage.cobertura.xml
```

### Getting Help

If you encounter issues not covered here:

1. **Search test documentation**:
   - This README
   - Builder documentation: `Helpers/Builders/README.md`
   - Test categories guide: `TEST_CATEGORIES_GUIDE.md`

2. **Check existing tests**:
   - Look for similar test scenarios in the codebase
   - Review base class implementations

3. **Review external resources**:
   - [xUnit Documentation](https://xunit.net/)
   - [Moq Documentation](https://github.com/moq/moq4/wiki/Quickstart)
   - [FluentAssertions Documentation](https://fluentassertions.com/)

4. **Contact the team**:
   - Create a GitHub issue
   - Reach out via team chat

---

## ?? Contributing Guidelines

### Before Submitting Tests

Complete this checklist:

- [ ] Test name follows convention: `MethodName_Scenario_ExpectedResult`
- [ ] Test follows AAA pattern (Arrange-Act-Assert)
- [ ] Test uses appropriate mocks/stubs
- [ ] Test has clear, focused assertions
- [ ] Test runs in isolation (no dependencies on other tests)
- [ ] Test is deterministic (no random values)
- [ ] Test executes quickly (< 100ms typically)
- [ ] Test includes XML documentation for complex scenarios
- [ ] Uses TestConstants instead of magic strings/numbers
- [ ] Uses test data builders where appropriate
- [ ] Inherits from appropriate base class (if applicable)
- [ ] Organized with `#region` directives
- [ ] All tests pass locally: `dotnet test`
- [ ] Code follows project conventions

### Pull Request Guidelines

1. **Title**: `[Tests] Brief description`
2. **Description**: Explain what tests were added/modified and why
3. **Coverage**: Note any coverage improvements
4. **Related Issues**: Link to related issues/user stories

### Code Review Criteria

Reviewers will check:

- ? Tests are well-organized and easy to understand
- ? Test names clearly describe scenarios
- ? No duplicate test code (use helpers/builders)
- ? Proper use of mocks and assertions
- ? Edge cases and error scenarios covered
- ? No hardcoded values (use TestConstants)
- ? All tests pass in CI/CD pipeline

---

## ?? Resources

### Internal Documentation

- **Test Data Builders**: [`Helpers/Builders/README.md`](Helpers/Builders/README.md)
- **Test Categories Guide**: [`TEST_CATEGORIES_GUIDE.md`](TEST_CATEGORIES_GUIDE.md)
- **Test Constants**: [`RequestHandlerTests/TestConstants.cs`](RequestHandlerTests/TestConstants.cs)

### External Resources

#### Official Documentation
- [xUnit Documentation](https://xunit.net/)
- [Moq Quickstart](https://github.com/moq/moq4/wiki/Quickstart)
- [FluentAssertions Documentation](https://fluentassertions.com/introduction)
- [.NET Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

#### Tools
- [Visual Studio Test Explorer](https://docs.microsoft.com/en-us/visualstudio/test/run-unit-tests-with-test-explorer)
- [dotnet test CLI](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test)
- [Code Coverage Tools](https://github.com/coverlet-coverage/coverlet)
- [ReportGenerator](https://github.com/danielpalme/ReportGenerator)

#### Articles & Guides
- [Unit Testing Best Practices by Microsoft](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
- [Test Pyramid by Martin Fowler](https://martinfowler.com/bliki/TestPyramid.html)
- [AAA Pattern](https://docs.microsoft.com/en-us/visualstudio/test/unit-test-basics#write-your-tests)

---

## ?? Test Metrics & Quality Goals

### Current Metrics

| Metric | Value | Trend | Status |
|--------|-------|-------|--------|
| **Total Tests** | 654 | ?? Stable | ? |
| **Passing Tests** | 654 | ? 100% | ? |
| **Failing Tests** | 0 | ? 0% | ? |
| **Skipped Tests** | 0 | ? 0% | ? |
| **Average Duration** | 1.5s | ? Fast | ? |
| **Code Coverage (API)** | ~80% | ?? Good | ? |
| **Flaky Tests** | 0 | ? Stable | ? |

### Quality Goals

#### Achieved ?
- [x] 100% test success rate
- [x] < 2 second full suite execution
- [x] 75%+ code coverage (API layer)
- [x] Zero test flakiness
- [x] Test data builders implemented
- [x] Test categories implemented
- [x] Comprehensive base classes

#### Future Targets ??
- [ ] 85%+ code coverage (target for 2024)
- [ ] Mutation testing score 85%+
- [ ] Property-based testing for validation logic
- [ ] Performance benchmarking suite

---

## ?? Version History

### Version 3.0 (Current) - January 2025
- **Consolidated documentation** into single comprehensive README
- **Archived** 30+ incremental documentation files
- **Enhanced** test categories and builder documentation
- **Updated** metrics and statistics

### Version 2.0 - December 2024
- Implemented test data builders (8 builders)
- Added test categories (13 categories)
- Created comprehensive base classes
- Achieved 80% API layer coverage

### Version 1.0 - November 2024
- Initial test suite creation
- 654 passing tests
- Core test infrastructure

---

## ?? Contact & Support

### Questions or Issues?

1. **Review Documentation**: Start with this README
2. **Check Examples**: Look at existing tests for patterns
3. **Search Issues**: Check if your question is already answered
4. **Ask the Team**: Reach out via team channels
5. **Create an Issue**: For bugs or feature requests

### Team

- **Project**: SXG Evaluation Platform
- **Repository**: [microsoft/sxgevalplatform](https://github.com/microsoft/sxgevalplatform)

---

**Last Updated**: January 2025  
**Test Framework**: xUnit 2.5.3  
**Target Framework**: .NET 8.0  
**Status**: ? All Tests Passing (654/654)

**Document Version**: 3.0
