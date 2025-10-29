# SXG Evaluation Platform - Integration Tests

This directory contains **comprehensive in-memory integration tests** for the SXG Evaluation Platform API. All Docker dependencies have been removed per project requirement: **"we CAN NOT USE connection strings"**.

## 🎯 Current Implementation: Pure In-Memory Testing

### ✅ Active Test Infrastructure (`Infrastructure/`)
- **`InMemoryWebApplicationFactory.cs`** - Pure in-memory factory with zero external dependencies
- **`InMemoryMetricsConfigTableService.cs`** - In-memory table storage simulation
- **`InMemoryAzureBlobStorageService.cs`** - In-memory blob storage simulation  
- **`InMemoryIntegrationTestBase.cs`** - Base class for in-memory tests

### 📦 Archived Docker Implementation (`archive/docker-testcontainers/`)
All Docker/Testcontainers code has been moved to the archive folder. See `archive/docker-testcontainers/README.md` for details.

### 🧪 Active Test Suites (`Tests/`)

#### 1. Comprehensive EvalConfig Integration Tests (`ComprehensiveEvalConfigIntegrationTests.cs`)
**18 tests covering complete EvalConfig API:**
- ✅ CREATE configurations with validation and caching
- ✅ READ configurations by ID with cache optimization  
- ✅ UPDATE configurations with cache refresh
- ✅ DELETE configurations with cache cleanup
- ✅ LIST configurations with agent isolation
- ✅ DEFAULT configuration endpoint
- ✅ Agent isolation and multi-tenancy verification
- ✅ Concurrent operations (10+ simultaneous requests)
- ✅ Performance testing and load handling
- ✅ Edge cases and error conditions
- ✅ Cache behavior validation (TTL, refresh, performance)
- ✅ End-to-end workflow validation

#### 2. Quick In-Memory Tests (`QuickInMemoryEvalConfigTests.cs`)
- ✅ Basic CRUD operations validation
- ✅ Fast execution lightweight tests
- ✅ Result upload and processing
- ✅ Result retrieval by evaluation run
- ✅ Agent-based result queries
- ✅ Status updates and workflows
- ✅ Large result file handling

#### 4. Health Check Integration Tests (`HealthCheckIntegrationTests.cs`)
- ✅ Health endpoint validation
- ✅ Dependency health checks (Redis, Azure Storage)
- ✅ Performance monitoring
- ✅ Concurrent health check handling
- ✅ Error resilience testing

#### 5. Configuration Management Tests (`ConfigurationManagementIntegrationTests.cs`)
## 🚀 Key Benefits of In-Memory Approach

| Metric | Current Implementation |
|--------|----------------------|
| **External Dependencies** | ❌ None required |
| **Connection Strings** | ❌ Not needed |
| **Docker** | ❌ Not required |
| **Test Execution Time** | ⚡ ~10.5 seconds for 18 tests |
| **Setup Time** | ⚡ Instant |
| **Test Coverage** | ✅ Complete EvalConfig API |
| **Concurrent Testing** | ✅ Thread-safe |
| **Agent Isolation** | ✅ Verified |

## 📦 Docker Cleanup Complete

All Docker/Testcontainers dependencies have been **removed and archived**:

### Removed Package References
```xml
<!-- REMOVED - No longer needed -->
<PackageReference Include="Testcontainers" Version="3.6.0" />
<PackageReference Include="Testcontainers.Redis" Version="3.6.0" />
<PackageReference Include="Testcontainers.Azurite" Version="3.6.0" />
```

### Current Required Packages
```xml
<PackageReference Include="xunit" Version="2.4.2" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<!-- No Docker packages needed! -->
```

## Running the Tests

### Command Line (Recommended)
```bash
# Run all integration tests (18 tests, ~10.5 seconds)
dotnet test

# Run specific test class
dotnet test --filter "ComprehensiveEvalConfigIntegrationTests"

# Run with detailed output
dotnet test --verbosity detailed

# Run with minimal output
dotnet test --verbosity minimal
```

### Visual Studio
1. Open the solution in Visual Studio
2. Build the solution (no Docker startup needed!)
3. Open Test Explorer (Test → Test Explorer)
4. Run tests directly from Test Explorer

### Prerequisites: NONE! 🎉
- ❌ No Docker required
- ❌ No external services needed  
- ❌ No connection strings to configure
- ✅ Just run `dotnet test` and you're done!
4. Run all tests or specific test suites

### VS Code
1. Install the .NET Test Explorer extension
2. Open the workspace
3. Use the Test Explorer panel to run tests

## Test Configuration

### Settings (`appsettings.Test.json`)
```json
{
  "AzureStorage": {
    "ConnectionString": "UseDevelopmentStorage=true"
  },
  "Redis": {
    "Hostname": "localhost",
    "Ssl": false
  }
}
```

## Test Features

### Containerized Dependencies
- Automatic Redis and Azurite container management
- Isolated test environment
- No external dependencies required

### Comprehensive Coverage
- **API Endpoints** - All major endpoints tested
- **Data Persistence** - Azure Table and Blob Storage validation
- **Caching** - Redis cache behavior verification
- **Performance** - Response time and concurrency testing
- **Error Handling** - Validation and error scenario testing

### Test Data Management
- Automatic test data creation and cleanup
- Isolated test scenarios
- Reusable test helper methods

## Performance Benchmarks

### Response Time SLAs
- **Average Response**: < 100ms
- **95th Percentile**: < 200ms
- **Maximum Response**: < 500ms

### Concurrency Targets
- **50 concurrent requests**: < 10 seconds total
- **Health checks under load**: < 5 seconds
- **Large file uploads**: < 30 seconds

### Cache Effectiveness
- **Cache hit ratio**: > 80% for repeated requests
- **Cache refresh**: < 50ms after expiration
- **Memory usage**: Within configured limits

## Debugging Tests

### Common Issues
1. **Container startup failures** - Ensure Docker is running
2. **Port conflicts** - Close other services using test ports
3. **Test data conflicts** - Check test isolation and cleanup

### Logging
```csharp
// Enable detailed logging in tests
builder.ConfigureLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Debug);
    logging.AddConsole();
});
```

### Manual Container Management
```bash
# List running containers
docker ps

# View container logs
docker logs <container-id>

# Stop all test containers
docker stop $(docker ps -q --filter ancestor=redis:7-alpine)
docker stop $(docker ps -q --filter ancestor=mcr.microsoft.com/azure-storage/azurite)
```

## Test Data

### Sample Evaluation Run
```json
{
  "agentId": "test-agent-001",
  "dataSetId": "guid",
  "metricsConfigurationId": "guid",
  "type": "TestAgent",
  "environmentId": "guid",
  "agentSchemaName": "test-schema"
}
```

### Sample Dataset Content
```json
{
  "questions": [
    {
      "id": "q1",
      "question": "Test question 1?",
      "expectedAnswer": "Test answer 1",
      "category": "test",
      "difficulty": "easy"
    }
  ]
}
```

## CI/CD Integration

### GitHub Actions Example
```yaml
- name: Run Integration Tests
  run: |
    dotnet test SXG.EvalPlatform.API.IntegrationTests.csproj \
      --configuration Release \
      --logger trx \
      --collect:"XPlat Code Coverage"
```

### Azure DevOps Example
```yaml
- task: DotNetCoreCLI@2
  displayName: 'Run Integration Tests'
  inputs:
    command: 'test'
    projects: 'SXG.EvalPlatform.API.IntegrationTests.csproj'
    arguments: '--configuration Release --collect:"XPlat Code Coverage"'
```

## Metrics and Monitoring

The integration tests validate:
- ✅ API response times and throughput
- ✅ Database query performance
- ✅ Cache hit rates and efficiency
- ✅ Memory usage patterns
- ✅ Error rates and handling
- ✅ Dependency health and availability

## Contributing

When adding new tests:
1. Follow the existing test patterns and naming conventions
2. Use the `IntegrationTestBase` class for common functionality
3. Include performance assertions where appropriate
4. Add proper test data cleanup
5. Update this documentation for new test suites

## Troubleshooting

### Test Failures
- Check container logs for dependency issues
- Verify test isolation and data cleanup
- Review timing-sensitive assertions for flakiness

### Performance Issues
- Monitor container resource usage
- Check for memory leaks in test data
- Validate cache configuration and behavior