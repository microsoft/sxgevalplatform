# Direct Controller Tests - Quick Start Guide

## Overview
The `EvalConfigControllerDirectTests.cs` file contains unit tests that directly instantiate the `EvalConfigsController` with mocked dependencies. This approach tests controller logic in isolation without HTTP overhead.

## Running the Tests

### Run All Direct Tests
```powershell
dotnet test --filter "FullyQualifiedName~DirectTests"
```

### Run from Visual Studio
1. Open **Test Explorer** (Test ? Test Explorer)
2. Search for "DirectTests"
3. Right-click ? Run

### Run Specific Test
```powershell
# Run just the default configuration test
dotnet test --filter "FullyQualifiedName~WhenGetDefaultMetricsConfigurationMethodIsInvoked_ShouldReturnValidConfiguration"

# Run just the configuration by ID test
dotnet test --filter "FullyQualifiedName~WhenGetConfigurationsByMetricsConfigurationId_ShouldReturnValidConfiguration"

# Run just the invalid ID test
dotnet test --filter "FullyQualifiedName~WhenGetConfigurationsByInvalidId_ShouldReturnNotFound"
```

## Current Tests

### ? Test 1: Get Default Metrics Configuration
**Method**: `WhenGetDefaultMetricsConfigurationMethodIsInvoked_ShouldReturnValidConfiguration`

**What it tests:**
- Controller calls the request handler correctly
- Response has valid Version (semantic versioning)
- Response has valid LastUpdated timestamp
- Categories collection is not empty
- Each category has valid structure (name, display name, metrics)
- Each metric has valid structure (name, description, thresholds)
- Score ranges are valid (min ? max, threshold within range)
- Unique category and metric names

**Mocking:**
- `IMetricsConfigurationRequestHandler.GetDefaultMetricsConfigurationAsync()` returns a predefined configuration

### ? Test 2: Get Configuration By ID
**Method**: `WhenGetConfigurationsByMetricsConfigurationId_ShouldReturnValidConfiguration`

**What it tests:**
- Controller calls request handler with correct ID
- Response is OkObjectResult
- Configuration has expected metrics (coherence, groundedness)
- Metric thresholds are correct (0.75, 0.80)
- All metric names are unique
- All thresholds are within valid range (0-1)

**Mocking:**
- `IMetricsConfigurationRequestHandler.GetMetricsConfigurationByConfigurationIdAsync()` returns a predefined list of metrics

### ? Test 3: Get Configuration By Invalid ID
**Method**: `WhenGetConfigurationsByInvalidId_ShouldReturnNotFound`

**What it tests:**
- Controller returns NotFoundObjectResult when configuration doesn't exist
- Request handler is called with correct ID
- Proper error handling

**Mocking:**
- `IMetricsConfigurationRequestHandler.GetMetricsConfigurationByConfigurationIdAsync()` returns null

## Test Structure

### AAA Pattern (Arrange-Act-Assert)

```csharp
[Fact]
public async Task WhenGetDefaultMetricsConfigurationMethodIsInvoked_ShouldReturnValidConfiguration()
{
    // ARRANGE - Set up mocks and expected data
    var expectedConfig = new DefaultMetricsConfiguration { /* ... */ };
    _mockRequestHandler
     .Setup(x => x.GetDefaultMetricsConfigurationAsync())
    .ReturnsAsync(expectedConfig);

    // ACT - Call the controller method
    var result = await _controller.GetDefaultMetricsConfiguration();

    // ASSERT - Verify results and mock calls
 var okResult = result.Should().BeOfType<DefaultMetricsConfiguration>().Subject;
    okResult.Version.Should().NotBeNullOrWhiteSpace();
    
    // Verify mock was called exactly once
    _mockRequestHandler.Verify(
        x => x.GetDefaultMetricsConfigurationAsync(), 
        Times.Once);
}
```

## Adding New Tests

### Template for New Test

```csharp
[Fact]
public async Task WhenSomeAction_ShouldDoSomething()
{
    // Arrange
    var expectedData = /* create test data */;
    _mockRequestHandler
        .Setup(x => x.SomeMethod(It.IsAny<string>()))
        .ReturnsAsync(expectedData);

    // Act
    var result = await _controller.SomeAction();

    // Assert
    result.Should().NotBeNull();
    // Add your assertions...

    // Verify
    _mockRequestHandler.Verify(
        x => x.SomeMethod(It.IsAny<string>()), 
   Times.Once);
}
```

## Key Benefits of Direct Tests

### ? Advantages
- **Fast execution** - No HTTP overhead, no middleware
- **Focused testing** - Tests controller logic only
- **Easy debugging** - Step through controller code directly
- **Complete control** - Full control over all dependencies via mocks
- **Edge cases** - Easy to test error scenarios and edge cases

### ?? Limitations
- **No routing tests** - Can't verify URL paths work
- **No middleware** - Authentication, CORS, etc. not tested
- **No model binding** - [FromQuery], [FromRoute] not tested
- **No serialization** - JSON issues not caught
- **No integration bugs** - HTTP pipeline issues missed

## Common Moq Patterns

### Setup Method to Return Value
```csharp
_mockHandler.Setup(x => x.GetAsync(id)).ReturnsAsync(result);
```

### Setup Method to Throw Exception
```csharp
_mockHandler.Setup(x => x.GetAsync(id)).ThrowsAsync(new Exception("Error"));
```

### Verify Method Was Called
```csharp
_mockHandler.Verify(x => x.GetAsync(id), Times.Once);
```

### Verify Method Was Never Called
```csharp
_mockHandler.Verify(x => x.DeleteAsync(id), Times.Never);
```

### Match Any Parameter
```csharp
_mockHandler.Setup(x => x.GetAsync(It.IsAny<string>())).ReturnsAsync(result);
```

### Match Specific Parameter
```csharp
_mockHandler.Setup(x => x.GetAsync(It.Is<string>(s => s == "specific-id"))).ReturnsAsync(result);
```

## FluentAssertions Patterns

### Type Assertions
```csharp
result.Should().BeOfType<OkObjectResult>();
result.Result.Should().BeOfType<NotFoundObjectResult>();
```

### Null Checks
```csharp
result.Should().NotBeNull();
result.Should().BeNull();
```

### String Assertions
```csharp
value.Should().NotBeNullOrWhiteSpace();
value.Should().MatchRegex(@"^\d+\.\d+$");
```

### Collection Assertions
```csharp
list.Should().NotBeEmpty();
list.Should().HaveCount(2);
list.Should().OnlyHaveUniqueItems();
list.Should().Contain(x => x.Name == "test");
```

### Numeric Assertions
```csharp
value.Should().BeGreaterThanOrEqualTo(0);
value.Should().BeLessThanOrEqualTo(1.0);
value.Should().BeInRange(0, 1);
```

## Troubleshooting

### Test Not Found
- Make sure the test class is `public`
- Verify `[Fact]` attribute is present
- Rebuild the solution: `dotnet build`
- Refresh Test Explorer

### Mock Not Working
- Verify setup matches method signature exactly
- Check that method is `virtual` or interface method
- Use `It.IsAny<T>()` to match any parameter
- Check mock setup order (setup before calling)

### Assertion Failed
- Check expected vs actual values in test output
- Use `.Which` to get more details: `result.Should().BeOfType<OkObjectResult>().Which`
- Add custom assertion messages for clarity

## Next Steps

### To add more tests for EvalConfigsController:

1. **Create Configuration** (POST)
```csharp
[Fact]
public async Task CreateConfiguration_ShouldReturnCreated_WhenValid()
{
    // Test the CreateConfiguration endpoint
}
```

2. **Update Configuration** (PUT)
```csharp
[Fact]
public async Task UpdateConfiguration_ShouldReturnOk_WhenExists()
{
    // Test the UpdateConfiguration endpoint
}
```

3. **Delete Configuration** (DELETE)
```csharp
[Fact]
public async Task DeleteConfiguration_ShouldReturnOk_WhenExists()
{
    // Test the DeleteConfiguration endpoint
}
```

4. **Get Configurations by Agent**
```csharp
[Fact]
public async Task GetConfigurationsByAgentId_ShouldReturnList_WhenAgentHasConfigs()
{
    // Test getting all configs for an agent
}
```

5. **Error Scenarios**
```csharp
[Fact]
public async Task GetConfiguration_ShouldReturn500_WhenHandlerThrows()
{
    // Test error handling
}
```

## References

- **Moq Documentation**: https://github.com/moq/moq4
- **FluentAssertions Documentation**: https://fluentassertions.com/
- **xUnit Documentation**: https://xunit.net/
- **Test Project Location**: `Sxg.EvalPlatform.API.IntegrationTests/EvalConfigControllerDirectTests.cs`

## Summary

Your direct controller tests are **ready to run**! They provide:
- ? Fast, focused testing of controller logic
- ? Easy to debug and maintain
- ? Great for testing edge cases and error scenarios
- ? No deployment or infrastructure needed

**Just run**: `dotnet test --filter "FullyQualifiedName~DirectTests"`
