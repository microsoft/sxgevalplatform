# Sxg.EvalPlatform.API.Storage.UnitTests

Comprehensive unit tests for the `Sxg.EvalPlatform.API.Storage` project.

## Overview

This test project provides comprehensive unit test coverage for all classes in the Storage layer, including:
- Azure Storage Services (Blob, Table, Queue)
- Cache Managers (Memory, Redis, NoCache)
- Configuration Helpers
- Entity Validators
- Helper Classes
- Table Entities

## Test Structure

```
Sxg.EvalPlatform.API.Storage.UnitTests/
??? ConfigurationTests/
?   ??? ConfigHelperTests.cs
??? Services/
?   ??? CacheManagerTests.cs (existing)
?   ??? NoCacheManagerTests.cs
?   ??? AzureQueueStorageServiceTests.cs
?   ??? [Additional service tests to be added]
??? ValidatorTests/
?   ??? EntityValidatorsTests.cs
??? HelperTests/
?   ??? DataSetTableEntityHelperTests.cs
?   ??? [Additional helper tests to be added]
??? Utilities/
?   ??? TestUtilities.cs (existing)
??? TestCategories.cs
??? TestConstants.cs
```

## Test Categories

Tests are organized using xUnit traits for easy filtering:

- **Unit**: All unit tests
- **Integration**: Integration tests (separate project)
- **Service**: Service layer tests
- **Storage**: Azure Storage tests
- **TableStorage**: Table Storage specific tests
- **BlobStorage**: Blob Storage specific tests
- **QueueStorage**: Queue Storage specific tests
- **Cache**: Caching functionality tests
- **Validation**: Validator tests
- **Configuration**: Configuration helper tests
- **Helper**: Helper class tests
- **HappyPath**: Successful scenario tests
- **ErrorHandling**: Error and exception handling tests
- **Security**: Security-related tests

## Running Tests

### Run all tests
```bash
dotnet test
```

### Run specific category
```bash
dotnet test --filter "Category=Service"
dotnet test --filter "Category=Cache"
dotnet test --filter "Category=Configuration"
```

### Run multiple categories
```bash
dotnet test --filter "Category=Unit&Category=Service"
dotnet test --filter "Category=Storage|Category=Cache"
```

### Run with coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Test Patterns and Practices

### Naming Conventions
- Test class: `{ClassName}Tests`
- Test method: `{MethodName}_{Scenario}_{ExpectedBehavior}`

Examples:
- `ConfigHelper_GetAzureStorageAccountName_WithValidConfiguration_ReturnsAccountName`
- `NoCacheManager_GetAsync_WithAnyKey_ReturnsNull`

### Test Structure (AAA Pattern)
```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var service = CreateService();
    var input = CreateTestInput();

    // Act
    var result = await service.MethodAsync(input);

    // Assert
    result.Should().NotBeNull();
    result.Property.Should().Be(expectedValue);
}
```

### Mocking Strategy
- Use Moq for creating test doubles
- Setup default mocks in test constructor
- Override specific behaviors in individual tests
- Verify interactions when testing behavior

### FluentAssertions
All assertions use FluentAssertions for better readability:
```csharp
result.Should().NotBeNull();
result.Should().Be(expected);
result.Should().BeOfType<ExpectedType>();
result.Should().Contain("expected text");
```

## Test Coverage Goals

- **Line Coverage**: > 80%
- **Branch Coverage**: > 75%
- **Critical Paths**: 100%

### Priority Areas
1. Service layer business logic
2. Configuration and validation
3. Error handling and exception scenarios
4. Cache operations and invalidation
5. Azure Storage interactions

## Dependencies

- xUnit (test framework)
- Moq (mocking framework)
- FluentAssertions (assertion library)
- Microsoft.Extensions.Logging (logging abstractions)
- Azure SDK libraries (for service testing)

## Common Test Scenarios

### Configuration Tests
- Valid configuration retrieval
- Missing configuration handling
- Default value behavior
- Type conversion
- Environment-specific settings

### Cache Tests
- Get/Set operations
- Expiration handling
- Cache miss scenarios
- Concurrent access
- Statistics and monitoring

### Storage Service Tests
- CRUD operations
- Error handling
- Retry logic
- Authentication failures
- Concurrent operations

### Validator Tests
- Valid input acceptance
- Invalid input rejection
- Null/empty handling
- Cross-entity validation

### Helper Tests
- Entity creation
- Key generation
- Filter building
- Path construction
- Validation logic

## Integration with CI/CD

These unit tests are designed to:
- Run quickly (< 30 seconds total)
- Run in isolation without external dependencies
- Provide fast feedback in PR checks
- Be deterministic and reliable

Integration tests requiring actual Azure resources are in:
- `Sxg.EvalPlatform.API.Storage.IntegrationTests`

## Contributing

When adding new tests:
1. Follow existing naming conventions
2. Add appropriate test categories
3. Include happy path and error scenarios
4. Test edge cases and boundary conditions
5. Document complex test scenarios
6. Keep tests focused and independent
7. Mock external dependencies

## Recent Updates

### DataSetTableServiceTests.cs (January 2025)
- ? **Completed**: 31 comprehensive unit tests covering all cache operations
- **Test Coverage**: Cache hit/miss scenarios, cache key validation, error handling, cache invalidation
- **Key Features Tested**:
  - All read operations with cache integration
  - Cache key format validation for all query patterns
  - Write operations with TableClient integration boundaries
  - Cache invalidation logic for updates and deletes
  - Error handling and resilience patterns
- **Notes**: Tests use mocked cache manager to test cache behavior without requiring TableClient mocking

## Future Enhancements

Additional test files to be created or enhanced:
- `AzureBlobStorageServiceTests.cs` (needs enhancement)
- `MetricsConfigTableServiceTests.cs` (needs enhancement)
- `EvalRunTableServiceTests.cs` (needs enhancement)
- `MemoryCacheManagerTests.cs` (enhance existing)
- `RedisCacheManagerTests.cs` (needs enhancement)
- `MetricsConfigurationEntityHelperTests.cs` (existing, may need enhancement)
- Entity tests for TableEntities (partial coverage exists)

## References

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [Azure SDK for .NET](https://docs.microsoft.com/en-us/dotnet/azure/)
