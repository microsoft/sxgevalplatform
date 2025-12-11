# SXG Evaluation Platform - Developer Onboarding Guide

**Welcome to the SXG Evaluation Platform Team!** ??

This guide will help you understand the architecture, set up your development environment, and start contributing to the project.

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Architecture](#architecture)
3. [Technology Stack](#technology-stack)
4. [Development Environment Setup](#development-environment-setup)
5. [Project Structure](#project-structure)
6. [Core Concepts](#core-concepts)
7. [Development Workflow](#development-workflow)
8. [Testing Strategy](#testing-strategy)
9. [Deployment](#deployment)
10. [Coding Standards](#coding-standards)
11. [Troubleshooting](#troubleshooting)
12. [Resources](#resources)

---

## Project Overview

### What is SXG Evaluation Platform?

The SXG Evaluation Platform is a comprehensive .NET 8 API service that enables organizations to evaluate AI agents through:

- **Metrics Configuration Management**: Define custom evaluation metrics with weights and thresholds
- **Dataset Management**: Store and version golden (reference) and synthetic (generated) datasets
- **Evaluation Execution**: Run evaluations and track progress through multiple states
- **Results Storage**: Persist evaluation results with flexible JSON schemas
- **Health Monitoring**: Comprehensive dependency health checks and telemetry

### Business Domain

- **Primary Users**: AI/ML teams evaluating agent performance
- **Use Cases**:
  - Regression testing for AI agents
  - A/B testing between agent versions
  - Continuous evaluation in CI/CD pipelines
  - Performance benchmarking

### Key Features

? **Multi-tenant Architecture**: Agent-based data isolation  
? **Flexible Schema**: JSON-based storage for custom metrics and results  
? **Azure-Native**: Built on Azure Table Storage, Blob Storage, and Redis Cache  
? **Observable**: OpenTelemetry and Application Insights integration  
? **RESTful API**: Clean, predictable endpoint structure  
? **Feature Flags**: Runtime configuration for gradual rollout

---

## Architecture

### High-Level Architecture

```
???????????????????????????????????????????????????????????????
?            API Layer (Controllers)           ?
?  ????????????  ????????????  ????????????  ????????????  ?
?  ? EvalRuns ?  ? Datasets ?  ?  Config  ?  ?  Health  ?  ?
?  ????????????  ????????????  ????????????  ?????????????
???????????????????????????????????????????????????????????????
      ?
           ?
???????????????????????????????????????????????????????????????
?    Business Logic (Request Handlers)       ?
?  ?????????????????  ????????????????  ????????????????   ?
?  ? EvalRunRequest?  ? DataSetReq ?  ? MetricsReq   ?   ?
?  ?   Handler     ?  ?  Handler     ?  ?  Handler     ? ?
?  ?????????????????  ????????????????  ????????????????   ?
???????????????????????????????????????????????????????????????
     ?
       ?
???????????????????????????????????????????????????????????????
?      Storage Layer (Services)   ?
?  ????????????????  ?????????????  ????????????????        ?
?  ? Table Storage?  ?   Blob    ?  ?     Queue    ?        ?
?  ?   Service    ?  ?  Service  ?  ?   Service    ?  ?
?  ????????????????  ?????????????  ????????????????        ?
???????????????????????????????????????????????????????????????
  ?
    ?
???????????????????????????????????????????????????????????????
?           Azure Services                 ?
?  ????????????????  ?????????????  ?????????????       ?
?  ? Table Storage?  ?Blob Storage?  ?   Redis   ?   ?
?  ?(Metadata)    ?  ? (Datasets/?  ?   Cache   ?     ?
?  ?       ?  ?  Results)  ?  ?           ??
?  ????????????????  ?????????????  ?????????????  ?
???????????????????????????????????????????????????????????????
```

### Architecture Layers

#### 1. **API Layer** (`Controllers/`)
- **Responsibility**: HTTP request/response handling, routing, model validation
- **Pattern**: Thin controllers that delegate to request handlers
- **Key Files**:
  - `EvalConfigsController.cs`: Metrics configuration CRUD
  - `EvalDatasetsController.cs`: Dataset management
  - `EvalRunsController.*.cs`: Evaluation run operations (split into partial classes)
  - `HealthController.cs`: Health checks and monitoring
  - `BaseController.cs`: Shared controller logic

#### 2. **Business Logic Layer** (`RequestHandlers/`)
- **Responsibility**: Business rules, validation, orchestration
- **Pattern**: Handler per domain aggregate
- **Key Files**:
  - `EvalRunRequestHandler.cs`: Evaluation run business logic
  - `DataSetRequestHandler.cs`: Dataset operations
  - `MetricsConfigurationRequestHandler.cs`: Configuration management
  - `EvaluationResultRequestHandler.cs`: Results processing

#### 3. **Storage Layer** (`Sxg.EvalPlatform.API.Storage/Services/`)
- **Responsibility**: Data access, Azure service integration
- **Pattern**: Service per storage type
- **Key Files**:
  - `DataSetTableService.cs`: Table storage for dataset metadata
  - `MetricsConfigTableService.cs`: Table storage for configurations
  - `AzureBlobStorageService.cs`: Blob storage for datasets/results
  - `AzureQueueStorageService.cs`: Queue storage for async processing
  - `RedisCacheManager.cs`: Distributed caching
  - `MemoryCacheManager.cs`: In-memory caching

#### 4. **Common Layer** (`SXG.EvalPlatform.Common/`)
- **Responsibility**: Shared utilities, constants, exceptions
- **Key Files**:
  - `CommonConstants.cs`: Application-wide constants
  - `CommonUtils.cs`: Utility functions
  - `Exceptions/`: Custom exception types

### Design Patterns

#### Request Handler Pattern
```csharp
public interface IEvalRunRequestHandler
{
    Task<EvalRunDto> CreateEvalRunAsync(CreateEvalRunDto createDto);
  Task<EvalRunDto> GetEvalRunByIdAsync(Guid evalRunId);
    Task<List<EvalRunDto>> GetEvalRunsByAgentIdAsync(string agentId);
}

public class EvalRunRequestHandler : IEvalRunRequestHandler
{
    private readonly IDataSetTableService _dataSetTableService;
    private readonly ILogger<EvalRunRequestHandler> _logger;
    
    // Handler orchestrates multiple services
    public async Task<EvalRunDto> CreateEvalRunAsync(CreateEvalRunDto createDto)
    {
  // 1. Validate input
        // 2. Check prerequisites (dataset exists, config exists)
        // 3. Create entity
        // 4. Return DTO
    }
}
```

#### Repository Pattern (Table/Blob Services)
```csharp
public interface IDataSetTableService
{
    Task<DatasetEntity> GetDatasetByIdAsync(string datasetId);
    Task<List<DatasetEntity>> GetDatasetsByAgentIdAsync(string agentId);
    Task UpsertDatasetAsync(DatasetEntity entity);
}
```

#### Cache-Aside Pattern
```csharp
public async Task<T> GetWithCacheAsync<T>(string key, Func<Task<T>> factory)
{
    // Try cache first
    var cached = await _cacheManager.GetAsync<T>(key);
    if (cached != null) return cached;
    
    // Fetch from source
    var value = await factory();
    
    // Store in cache
    await _cacheManager.SetAsync(key, value, TimeSpan.FromMinutes(30));
    
    return value;
}
```

---

## Technology Stack

### Core Framework
- **.NET 8.0**: Latest LTS version
- **ASP.NET Core**: Web API framework
- **C# 12.0**: Language version

### Azure Services
- **Azure Table Storage**: Metadata storage (configurations, datasets, eval runs)
- **Azure Blob Storage**: Large file storage (datasets, results)
- **Azure Queue Storage**: Async processing queues
- **Azure Redis Cache**: Distributed caching
- **Azure Active Directory**: Authentication
- **Application Insights**: Telemetry and monitoring

### Libraries & Packages
```xml
<PackageReference Include="Azure.Data.Tables" Version="12.8.0" />
<PackageReference Include="Azure.Storage.Blobs" Version="12.19.0" />
<PackageReference Include="Azure.Storage.Queues" Version="12.17.0" />
<PackageReference Include="Azure.Identity" Version="1.10.4" />
<PackageReference Include="StackExchange.Redis" Version="2.7.10" />
<PackageReference Include="OpenTelemetry" Version="1.7.0" />
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.7.0" />
<PackageReference Include="Azure.Monitor.OpenTelemetry.Exporter" Version="1.2.0" />
<PackageReference Include="AutoMapper" Version="12.0.1" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
```

### Development Tools
- **Visual Studio 2022** or **VS Code** with C# extension
- **Azure Storage Explorer**: Browse Azure storage
- **Postman** or **REST Client**: API testing
- **Azure CLI**: Deployment and management

---

## Development Environment Setup

### Prerequisites

1. **.NET 8.0 SDK**
   ```bash
   # Download from: https://dotnet.microsoft.com/download/dotnet/8.0
   dotnet --version  # Verify: 8.0.x
   ```

2. **Visual Studio 2022** (or VS Code)
   - Workloads: ASP.NET and web development, Azure development
   - Extensions: C# Dev Kit, Azure Account

3. **Azure CLI**
   ```bash
   # Windows (winget)
   winget install Microsoft.AzureCLI
   
   # macOS (Homebrew)
   brew install azure-cli
   
   # Verify
   az --version
   az login
   ```

4. **Git**
 ```bash
   git --version
   ```

### Clone Repository

```bash
cd D:\Github-Projects
git clone https://github.com/microsoft/sxgevalplatform.git
cd sxgevalplatform
```

### Configure Azure Resources

#### Option 1: Use Existing Development Environment

Contact your team lead to get:
- Storage account name
- Redis cache endpoint
- Application Insights connection string

#### Option 2: Set Up Personal Dev Resources

```bash
# Create resource group
az group create --name rg-sxgeval-dev-yourname --location eastus

# Create storage account
az storage account create \
  --name sxgevalstyourname \
  --resource-group rg-sxgeval-dev-yourname \
  --location eastus \
  --sku Standard_LRS

# Create Redis cache (optional - can use Memory cache for local dev)
az redis create \
  --name sxgeval-redis-yourname \
  --resource-group rg-sxgeval-dev-yourname \
  --location eastus \
  --sku Basic \
  --vm-size c0
```

### Configure appsettings.Local.json

Create `SXG.EvalPlatform.API\appsettings.Local.json`:

```json
{
  "ApiSettings": {
    "Environment": "Local"
  },
  "AzureStorage": {
    "AccountName": "sxgevalstyourname"
  },
  "Cache": {
  "Provider": "Memory",  // Use "Redis" if you set up Redis
    "DefaultExpirationMinutes": 30
  },
  "DataVerseAPI": {
    "DatasetEnrichmentRequestAPIEndPoint": "https://sxg-eval-dev.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun",
    "Scope": "https://sxg-eval-dev.crm.dynamics.com/.default"
  },
  "FeatureFlags": {
    "EnableDataCaching": true
  },
  "Telemetry": {
    "AppInsightsConnectionString": ""  // Leave empty for local dev
  }
}
```

### Set Environment Variables

For local development with Azure Storage:

```powershell
# PowerShell
$env:ASPNETCORE_ENVIRONMENT = "Local"
$env:AZURE_STORAGE_ACCOUNT_NAME = "sxgevalstyourname"

# Or use Managed Identity locally
az login
```

### Build and Run

```bash
cd src/APIs/SXG.EvalPlatform.API
dotnet restore
dotnet build
dotnet run
```

Application will start at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger: `https://localhost:5001/swagger`

### Verify Setup

```bash
# Check health
curl https://localhost:5001/api/v1/health

# Check detailed health
curl https://localhost:5001/api/v1/health/detailed
```

Expected response:
```json
{
  "status": "Healthy",
  "environment": "Local",
  "dependencies": [...]
}
```

---

## Project Structure

### Solution Structure

```
sxgevalplatform/
??? src/
?   ??? APIs/
?       ??? SXG.EvalPlatform.API/           # Main API project
?       ??? Sxg.EvalPlatform.API.Storage/   # Storage layer
?       ??? SXG.EvalPlatform.Common/        # Shared utilities
?     ??? Sxg.EvalPlatform.API.UnitTests/
?       ??? Sxg.EvalPlatform.API.IntegrationTests/
?       ??? Sxg.EvalPlatform.API.Storage.UnitTests/
?       ??? Sxg.EvalPlatform.API.Storage.IntegrationTests/
??? docs/         # Documentation
??? deploy/      # Deployment scripts
??? README.md
```

### SXG.EvalPlatform.API Project

```
SXG.EvalPlatform.API/
??? Controllers/
?   ??? BaseController.cs      # Base class with common functionality
?   ??? EvalConfigsController.cs   # Metrics configuration endpoints
?   ??? EvalDatasetsController.cs       # Dataset endpoints
?   ??? EvalRunsController.cs       # Main eval runs controller
?   ??? EvalRunsController.EvalRuns.cs  # Eval run CRUD operations
?   ??? EvalRunsController.Status.cs    # Status update operations
? ??? EvalRunsController.Results.cs   # Results operations
?   ??? EvalRunsController.EnrichedDataset.cs  # Enriched dataset ops
?   ??? HealthController.cs        # Health check endpoints
??? RequestHandlers/
?   ??? Interfaces/
?   ?   ??? IEvalRunRequestHandler.cs
?   ?   ??? IDataSetRequestHandler.cs
?   ?   ??? IMetricsConfigurationRequestHandler.cs
?   ?   ??? IEvaluationResultRequestHandler.cs
?   ??? EvalRunRequestHandler.cs
?   ??? DataSetRequestHandler.cs
?   ??? MetricsConfigurationRequestHandler.cs
?   ??? EvaluationResultRequestHandler.cs
??? Models/
?   ??? EvalRunModels.cs       # Eval run DTOs
?   ??? EvalDataset.cs # Dataset DTOs
? ??? MetricsConfiguration.cs    # Configuration DTOs
?   ??? EvaluationResultModels.cs      # Result DTOs
?   ??? Dtos/
?  ??? ErrorResponseDto.cs
?       ??? CreateConfigurationRequestDto.cs
?    ??? ...
??? Services/
?   ??? IOpenTelemetryService.cs
?   ??? OpenTelemetryService.cs
?   ??? CloudRoleNameTelemetryInitializer.cs
??? Middleware/
?   ??? TelemetryMiddleware.cs         # Request/response telemetry
??? Extensions/
?   ??? ServiceCollectionExtensions.cs # DI registration
?   ??? ConfigurationExtensions.cs  # Configuration helpers
??? Common/
?   ??? Result.cs # Result pattern
??? SwaggerFilters/
?   ??? JsonElementSchemaFilter.cs     # Swagger customization
??? Program.cs # Application entry point
??? AutoMapperProfile.cs    # Object mapping configuration
??? appsettings.json # Base configuration
??? appsettings.Development.json       # Dev environment config
??? appsettings.PPE.json               # PPE environment config
??? appsettings.Production.json        # Production environment config
??? appsettings.Local.json             # Local developer config (gitignored)
```

### Storage Project

```
Sxg.EvalPlatform.API.Storage/
??? Services/
?   ??? Interfaces/
?   ?   ??? IAzureBlobStorageService.cs
?   ?   ??? IAzureTableStorageService.cs
?   ?   ??? ICacheManager.cs
?   ?   ??? ...
?   ??? AzureBlobStorageService.cs
? ??? AzureTableStorageService.cs
???? DataSetTableService.cs
?   ??? MetricsConfigTableService.cs
?   ??? RedisCacheManager.cs
?   ??? MemoryCacheManager.cs
?   ??? NoCacheManager.cs      # Null object pattern
??? Entities/
?   ??? DatasetEntity.cs             # Table entities
? ??? MetricsConfigurationEntity.cs
?   ??? EvalRunEntity.cs
??? Extensions/
?   ??? CacheServiceExtensions.cs      # Cache DI registration
??? ConfigHelper.cs         # Configuration helper
??? IConfigHelper.cs
```

---

## Core Concepts

### 1. Evaluation Workflow

```
???????????????
? 1. Create   ?
? Config      ?
???????????????
       ?
       ?
???????????????
? 2. Upload   ?
? Dataset     ?
???????????????
       ?
       ?
???????????????
? 3. Create   ?
? Eval Run  ?
? (Queued)    ?
???????????????
       ?
     ?
???????????????
? 4. Start  ?
? Evaluation  ?
? (Running)   ?
???????????????
       ?
   ?
???????????????
? 5. Save     ?
? Enriched    ?
? Dataset     ?
???????????????
       ?
       ?
???????????????
? 6. Save     ?
? Results ?
???????????????
       ?
       ?
???????????????
? 7. Mark     ?
? Completed   ?
???????????????
```

### 2. Data Partitioning Strategy

#### Table Storage Partitioning
- **Partition Key**: `AgentId` (enables efficient agent-based queries)
- **Row Key**: `{EntityType}Id` (e.g., `EvalRunId`, `DatasetId`)
- **Benefits**:
  - Fast agent-scoped queries
  - Automatic load distribution
  - Natural tenant isolation

#### Blob Storage Organization
```
Container: {agent-id-lowercase}
??? datasets/
?   ??? {dataset-id}/
?   ?   ??? dataset.json
??? evaluation-results/
?   ??? {eval-run-id}/
?   ?   ??? results.json
?   ?   ??? detailed-metrics.json
?   ?   ??? enriched-dataset.json
??? metrics-configurations/
    ??? {config-id}/
        ??? configuration.json
```

### 3. Caching Strategy

#### Cache Layers
1. **Memory Cache** (for single instance or local dev)
2. **Redis Cache** (for distributed scenarios)
3. **No Cache** (for testing or when caching is disabled)

#### Cache Key Convention
```
Format: {Type}:{Identifier}:{SubType}
Examples:
- Config:agent-001:Default
- Dataset:550e8400-e29b-41d4-a716-446655440000
- EvalRun:agent-001:Latest
```

#### Cache Invalidation
- **TTL-based**: Default 30-60 minutes
- **Event-based**: Invalidate on create/update/delete
- **Pattern-based**: Invalidate related keys (e.g., all agent configs)

### 4. Feature Flags

Enable/disable features at runtime via configuration:

```json
{
  "FeatureFlags": {
    "EnableDataCaching": true,
    "EnableAsyncProcessing": false
  }
}
```

Usage in code:
```csharp
var isCachingEnabled = _configHelper.IsDataCachingEnabled();
if (isCachingEnabled)
{
    // Use cache
}
else
{
    // Direct database access
}
```

### 5. Status State Machine

```
[Queued] ???????????????????????
   ?        ?
   ?   ?
[Running] ??????????????????    ?
   ?       ?    ?
   ??????? [Completed] ?????????? (Terminal state)
   ?
   ??????? [Failed]       (Terminal state)
```

**Rules**:
- Terminal states (`Completed`, `Failed`) are immutable
- Status updates are case-insensitive
- Invalid transitions are rejected with 400 Bad Request

---

## Development Workflow

### 1. Branch Strategy

```
main (protected)
??? feature/evalrunnerintegration (current development)
??? feature/your-feature-name
??? bugfix/issue-123
??? hotfix/critical-fix
```

**Branch Naming**:
- `feature/` - New features
- `bugfix/` - Bug fixes
- `hotfix/` - Production hotfixes
- `chore/` - Maintenance tasks

### 2. Development Cycle

#### Create Feature Branch
```bash
git checkout feature/evalrunnerintegration
git pull origin feature/evalrunnerintegration
git checkout -b feature/my-new-feature
```

#### Make Changes
```bash
# Edit files
code Controllers/NewFeatureController.cs

# Build and test
dotnet build
dotnet test

# Run locally
dotnet run --project SXG.EvalPlatform.API
```

#### Test Your Changes
```bash
# Unit tests
dotnet test Sxg.EvalPlatform.API.UnitTests

# Integration tests
dotnet test Sxg.EvalPlatform.API.IntegrationTests

# Manual testing via Swagger
# https://localhost:5001/swagger
```

#### Commit and Push
```bash
git add .
git commit -m "feat: Add new evaluation metric type"
git push origin feature/my-new-feature
```

#### Create Pull Request
1. Go to GitHub repository
2. Create PR from `feature/my-new-feature` to `feature/evalrunnerintegration`
3. Fill in PR template
4. Request reviewers
5. Address feedback

### 3. Commit Message Convention

Format: `<type>(<scope>): <subject>`

**Types**:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `style`: Formatting (no code change)
- `refactor`: Code restructuring
- `test`: Add/update tests
- `chore`: Maintenance

**Examples**:
```bash
feat(datasets): Add support for CSV dataset upload
fix(evalruns): Correct status validation logic
docs(api): Update API consumer guide
refactor(cache): Extract cache key generation to utility
test(configs): Add integration tests for configuration CRUD
```

### 4. Code Review Checklist

Before submitting PR, ensure:

- [ ] Code builds without warnings
- [ ] All tests pass
- [ ] New features have unit tests
- [ ] Integration tests added/updated if needed
- [ ] XML documentation added to public APIs
- [ ] Swagger annotations updated
- [ ] Error handling implemented
- [ ] Logging added for important operations
- [ ] Configuration changes documented
- [ ] Database migrations created (if applicable)
- [ ] No hardcoded secrets or connection strings
- [ ] Code follows project conventions

---

## Testing Strategy

### Test Pyramid

```
        ?????????????????
        ?  Manual Tests ?  (Exploratory, UAT)
        ?????????????????
      ?????????????????????
? Integration Tests ?  (API, Database, Redis)
      ?????????????????????
   ??????????????????????????
   ?     Unit Tests    ?  (Business logic, Utilities)
   ??????????????????????????
```

### Unit Tests

**Location**: `Sxg.EvalPlatform.API.UnitTests/`

**Framework**: xUnit, Moq

**Example**:
```csharp
public class EvalRunRequestHandlerTests
{
    private readonly Mock<IDataSetTableService> _mockDataSetService;
    private readonly Mock<ILogger<EvalRunRequestHandler>> _mockLogger;
    private readonly EvalRunRequestHandler _handler;

    public EvalRunRequestHandlerTests()
    {
        _mockDataSetService = new Mock<IDataSetTableService>();
        _mockLogger = new Mock<ILogger<EvalRunRequestHandler>>();
  _handler = new EvalRunRequestHandler(
            _mockDataSetService.Object,
         _mockLogger.Object
        );
    }

    [Fact]
    public async Task CreateEvalRunAsync_ValidInput_ReturnsEvalRunDto()
    {
        // Arrange
      var createDto = new CreateEvalRunDto
      {
     AgentId = "agent-001",
       EvalRunName = "Test Run",
    DataSetId = Guid.NewGuid(),
            MetricsConfigurationId = Guid.NewGuid(),
            Type = "Automated",
 EnvironmentId = "Development",
   AgentSchemaName = "TestSchema"
        };

        // Act
     var result = await _handler.CreateEvalRunAsync(createDto);

        // Assert
     Assert.NotNull(result);
        Assert.Equal(createDto.AgentId, result.AgentId);
        Assert.Equal("Queued", result.Status);
    }

    [Fact]
    public async Task CreateEvalRunAsync_DatasetNotFound_ThrowsException()
    {
        // Arrange
 var createDto = new CreateEvalRunDto { /* ... */ };
        _mockDataSetService
            .Setup(x => x.GetDatasetByIdAsync(It.IsAny<string>()))
         .ReturnsAsync((DatasetEntity)null);

     // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
       () => _handler.CreateEvalRunAsync(createDto)
        );
    }
}
```

**Run Unit Tests**:
```bash
cd Sxg.EvalPlatform.API.UnitTests
dotnet test
```

### Integration Tests

**Location**: `Sxg.EvalPlatform.API.IntegrationTests/`

**Framework**: xUnit, WebApplicationFactory

**Example**:
```csharp
public class EvalRunsControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EvalRunsControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
 _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateEvalRun_ValidData_Returns201()
    {
        // Arrange
        var createDto = new CreateEvalRunDto
  {
      AgentId = "test-agent",
   EvalRunName = "Integration Test Run",
     DataSetId = Guid.NewGuid(),
        MetricsConfigurationId = Guid.NewGuid(),
            Type = "Automated",
            EnvironmentId = "Test",
    AgentSchemaName = "TestSchema"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
 "/api/v1/eval/runs",
            createDto
        );

   // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
 var result = await response.Content.ReadFromJsonAsync<EvalRunDto>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.EvalRunId);
    }
}
```

**Run Integration Tests**:
```bash
cd Sxg.EvalPlatform.API.IntegrationTests
dotnet test
```

### Manual Testing

#### Using Swagger UI
1. Run application: `dotnet run`
2. Open browser: `https://localhost:5001/swagger`
3. Test endpoints interactively

#### Using Postman
1. Import Postman collection from `docs/postman/`
2. Set environment variables (base URL, token)
3. Execute requests

#### Using cURL
```bash
# Health check
curl https://localhost:5001/api/v1/health

# Create configuration
curl -X POST https://localhost:5001/api/v1/eval/configurations \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "test-agent",
    "configurationName": "Test Config",
    "selectedMetrics": [...]
  }'
```

---

## Deployment

### Deployment Environments

| Environment | URL | Purpose |
|-------------|-----|---------|
| Local | localhost:5001 | Developer workstation |
| Development | sxgevalapidev.azurewebsites.net | Integration testing |
| PPE | sxgevalapippe.azurewebsites.net | Pre-production validation |
| Production | sxgevalapiproduction.azurewebsites.net | Live service |

### Deployment Scripts

**Location**: `SXG.EvalPlatform.API/deploy/`

#### Deploy to Development
```powershell
cd SXG.EvalPlatform.API\deploy
.\Deploy-To-Azure-Dev-AUTOMATED.ps1
```

**What it does**:
1. Stops App Service
2. Deploys appsettings (including `ASPNETCORE_ENVIRONMENT=Development`)
3. Builds application (Release mode)
4. Publishes to Azure
5. Starts App Service
6. Runs health checks

#### Deploy to PPE
```powershell
.\Deploy-To-Azure-PPE-AUTOMATED.ps1
```

#### Deploy to Production
```powershell
.\Deploy-To-Azure-Production-AUTOMATED.ps1
```

### Deployment Checklist

Before deploying:

- [ ] All tests pass locally
- [ ] Code reviewed and approved
- [ ] Feature flags configured appropriately
- [ ] Database migrations applied (if any)
- [ ] Configuration verified for target environment
- [ ] Rollback plan prepared
- [ ] Stakeholders notified

After deployment:

- [ ] Health endpoint returns "Healthy"
- [ ] Swagger UI loads correctly
- [ ] Smoke tests pass
- [ ] Application Insights shows telemetry
- [ ] No error spikes in logs
- [ ] Performance metrics within acceptable range

### Rollback Procedure

If deployment fails:

1. **Check Health**:
   ```bash
   curl https://sxgevalapidev.azurewebsites.net/api/v1/health/detailed
   ```

2. **Review Logs**:
   ```bash
   az webapp log tail --name sxgevalapidev --resource-group rg-sxg-agent-evaluation-platform
   ```

3. **Rollback** (if needed):
   ```powershell
   # Redeploy previous version
   git checkout <previous-commit>
   .\Deploy-To-Azure-Dev-AUTOMATED.ps1
   ```

4. **Notify Team**: Post in Teams channel about rollback

---

## Coding Standards

### C# Style Guide

#### Naming Conventions
```csharp
// Classes: PascalCase
public class EvalRunRequestHandler { }

// Interfaces: IPascalCase
public interface IEvalRunRequestHandler { }

// Public members: PascalCase
public string AgentId { get; set; }

// Private fields: _camelCase
private readonly ILogger<EvalRunRequestHandler> _logger;

// Local variables: camelCase
var evalRunId = Guid.NewGuid();

// Constants: PascalCase
public const string DefaultEnvironment = "Development";
```

#### File Organization
```csharp
// 1. Using statements (grouped and sorted)
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// 2. Namespace
namespace SxgEvalPlatformApi.Controllers;

// 3. Class with XML documentation
/// <summary>
/// Handles evaluation run operations
/// </summary>
public class EvalRunsController : BaseController
{
    // 4. Private fields
    private readonly IEvalRunRequestHandler _handler;
    private readonly ILogger<EvalRunsController> _logger;

    // 5. Constructor
    public EvalRunsController(
        IEvalRunRequestHandler handler,
    ILogger<EvalRunsController> logger)
    {
      _handler = handler;
        _logger = logger;
    }

    // 6. Public methods
    [HttpGet("{id}")]
    public async Task<ActionResult<EvalRunDto>> GetEvalRun(Guid id)
    {
        // Implementation
 }

    // 7. Private methods
    private void ValidateInput(CreateEvalRunDto dto)
    {
        // Implementation
    }
}
```

### Error Handling Pattern

```csharp
[HttpPost]
public async Task<ActionResult<EvalRunDto>> CreateEvalRun(CreateEvalRunDto createDto)
{
    using var activity = _telemetryService?.StartActivity("CreateEvalRun");
  var stopwatch = Stopwatch.StartNew();

    try
    {
        // Add telemetry
        activity?.SetTag("agentId", createDto.AgentId);

 // Validate
 if (!ModelState.IsValid)
        {
            activity?.SetTag("validation.failed", true);
        return CreateValidationErrorResponse<EvalRunDto>();
        }

        // Business logic
        _logger.LogInformation("Creating eval run for agent: {AgentId}", createDto.AgentId);
        var result = await _handler.CreateEvalRunAsync(createDto);

        // Success telemetry
    activity?.SetTag("success", true);
        activity?.SetTag("evalRunId", result.EvalRunId);

        stopwatch.Stop();
        _logger.LogInformation(
        "Created eval run {EvalRunId} in {Duration}ms",
     result.EvalRunId,
            stopwatch.ElapsedMilliseconds
        );

        return CreatedAtAction(nameof(GetEvalRun), new { id = result.EvalRunId }, result);
    }
    catch (ValidationException ex)
    {
      activity?.SetTag("error.type", "ValidationException");
   _logger.LogWarning(ex, "Validation failed");
        return CreateErrorResponse<EvalRunDto>(ex.Message, 400);
    }
    catch (RequestFailedException ex)
    {
 activity?.SetTag("error.type", "AzureRequestFailed");
        _logger.LogError(ex, "Azure error");
        return HandleAzureException<EvalRunDto>(ex, "Failed to create eval run");
    }
    catch (Exception ex)
    {
        activity?.SetTag("error.type", ex.GetType().Name);
     _logger.LogError(ex, "Unexpected error");
        return CreateErrorResponse<EvalRunDto>("Internal server error", 500);
    }
    finally
    {
        stopwatch.Stop();
        activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
    }
}
```

### Logging Best Practices

```csharp
// Good: Structured logging with parameters
_logger.LogInformation(
    "Creating eval run for agent: {AgentId}, dataset: {DatasetId}",
  agentId,
    datasetId
);

// Bad: String concatenation
_logger.LogInformation($"Creating eval run for agent: {agentId}");

// Good: Log levels
_logger.LogDebug("Detailed diagnostic info");      // Development only
_logger.LogInformation("Normal operation");         // Important milestones
_logger.LogWarning("Recoverable issue occurred");   // Non-critical issues
_logger.LogError(ex, "Error processing request");  // Errors with exceptions
_logger.LogCritical(ex, "System failure");         // Critical failures

// Good: Include context
_logger.LogError(
    ex,
    "Failed to create eval run. AgentId: {AgentId}, DatasetId: {DatasetId}",
    agentId,
 datasetId
);
```

### Async/Await Guidelines

```csharp
// Good: Async all the way down
public async Task<EvalRunDto> CreateEvalRunAsync(CreateEvalRunDto dto)
{
  var entity = await _tableService.CreateEntityAsync(dto);
    return _mapper.Map<EvalRunDto>(entity);
}

// Bad: Blocking on async code
public EvalRunDto CreateEvalRun(CreateEvalRunDto dto)
{
    var entity = _tableService.CreateEntityAsync(dto).Result; // Don't do this!
    return _mapper.Map<EvalRunDto>(entity);
}

// Good: ConfigureAwait for library code
public async Task<string> GetDataAsync()
{
    var data = await _httpClient.GetStringAsync(url).ConfigureAwait(false);
    return data;
}
```

### Dependency Injection

```csharp
// Good: Constructor injection
public class EvalRunRequestHandler : IEvalRunRequestHandler
{
    private readonly IDataSetTableService _dataSetService;
    private readonly ILogger<EvalRunRequestHandler> _logger;

    public EvalRunRequestHandler(
 IDataSetTableService dataSetService,
     ILogger<EvalRunRequestHandler> logger)
    {
    _dataSetService = dataSetService ?? throw new ArgumentNullException(nameof(dataSetService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}

// Register in Program.cs (via ServiceCollectionExtensions)
builder.Services.AddScoped<IEvalRunRequestHandler, EvalRunRequestHandler>();
```

---

## Troubleshooting

### Common Issues

#### 1. Azure Storage Connection Fails

**Symptoms**:
```
Azure.RequestFailedException: Server failed to authenticate the request
```

**Solutions**:
```bash
# Check if logged in
az account show

# Login if needed
az login

# Verify storage account access
az storage account show --name sxgagentevaldev
```

#### 2. Redis Connection Timeout

**Symptoms**:
```
StackExchange.Redis.RedisTimeoutException: Timeout performing GET
```

**Solutions**:
1. Check `appsettings.json`:
   ```json
   {
     "Cache": {
       "Provider": "Memory"  // Use memory cache for local dev
     }
   }
   ```

2. Verify Redis managed identity:
```powershell
   .\Verify-Redis-ManagedIdentity.ps1 -AppServiceName sxgevalapidev
   ```

3. Fix Redis access:
   ```powershell
   .\Fix-Redis-ManagedIdentity-Access.ps1 -AppServiceName sxgevalapidev
   ```

#### 3. Build Errors After Pull

**Symptoms**:
```
error CS0246: The type or namespace name 'X' could not be found
```

**Solutions**:
```bash
# Clean and restore
dotnet clean
dotnet restore
dotnet build
```

#### 4. Tests Fail Locally

**Symptoms**:
```
System.InvalidOperationException: Unable to resolve service for type 'IConfiguration'
```

**Solutions**:
1. Ensure `appsettings.Local.json` exists
2. Check test project references
3. Verify test fixtures are properly configured

#### 5. Swagger Not Loading

**Symptoms**:
- 404 on `/swagger`
- Blank swagger page

**Solutions**:
1. Check `Program.cs` has Swagger configuration
2. Verify app is running in correct environment
3. Clear browser cache
4. Check for XML documentation file generation

### Debugging Tips

#### Enable Detailed Logging
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
```

#### View Application Logs
```powershell
# Azure App Service logs
az webapp log tail --name sxgevalapidev --resource-group rg-sxg-agent-evaluation-platform

# Local development logs
# Check console output
```

#### Debug in Visual Studio
1. Set breakpoints in code
2. Press F5 to start debugging
3. Use Immediate Window for expressions
4. Check Locals/Autos windows for variable values

#### Use Health Endpoint for Diagnostics
```bash
# Detailed health with dependency status
curl https://localhost:5001/api/v1/health/detailed | jq .
```

---

## Resources

### Documentation

- **API Consumer Guide**: `docs/API-Consumer-Guide.md`
- **Deployment Guide**: `docs/Development-Deployment-Review-Summary.md`
- **Feature Flags**: `docs/FeatureFlags-Deployment-Configuration.md`
- **Redis Setup**: `SXG.EvalPlatform.API/deploy/REDIS-MANAGED-IDENTITY-SETUP.md`

### Internal Links

- **Swagger UI (Dev)**: https://sxgevalapidev.azurewebsites.net/swagger
- **Azure Portal**: https://portal.azure.com
- **GitHub Repository**: https://github.com/microsoft/sxgevalplatform

### External Resources

- **.NET 8 Documentation**: https://learn.microsoft.com/en-us/dotnet/
- **ASP.NET Core**: https://learn.microsoft.com/en-us/aspnet/core/
- **Azure Storage**: https://learn.microsoft.com/en-us/azure/storage/
- **Redis**: https://redis.io/documentation
- **OpenTelemetry**: https://opentelemetry.io/docs/

### Team Contacts

- **Tech Lead**: [Name] - [email]
- **DevOps**: [Name] - [email]
- **Architecture**: [Name] - [email]

### Getting Help

1. **Check Documentation**: Start with this guide and API documentation
2. **Search GitHub Issues**: Someone may have had the same problem
3. **Ask in Teams**: Post in SXG Evaluation Platform channel
4. **Create GitHub Issue**: For bugs or feature requests
5. **Schedule 1:1**: Reach out to team lead for complex issues

---

## Next Steps

Now that you're onboarded, here's what to do next:

### Week 1: Getting Familiar
- [ ] Set up development environment
- [ ] Run application locally
- [ ] Explore Swagger UI
- [ ] Run all tests
- [ ] Review codebase structure
- [ ] Read architecture documentation

### Week 2: First Contribution
- [ ] Pick a "good first issue" from GitHub
- [ ] Create feature branch
- [ ] Implement solution
- [ ] Add tests
- [ ] Submit PR
- [ ] Address review feedback

### Week 3: Deep Dive
- [ ] Review storage layer implementation
- [ ] Understand caching strategy
- [ ] Learn deployment process
- [ ] Contribute to documentation
- [ ] Pair program with team member

### Ongoing
- [ ] Attend sprint planning
- [ ] Participate in code reviews
- [ ] Share knowledge in team meetings
- [ ] Suggest improvements
- [ ] Mentor new team members

---

## Conclusion

Welcome to the team! This guide is a living document—if you find gaps or have suggestions, please submit a PR to improve it for future team members.

Remember:
- **Ask questions**: No question is too small
- **Share knowledge**: Document what you learn
- **Collaborate**: Code reviews are learning opportunities
- **Iterate**: Continuous improvement is our culture

Happy coding! ??

---

**Last Updated**: January 15, 2024  
**Document Version**: 1.0.0  
**Maintained By**: SXG Evaluation Platform Team

---

© 2024 Microsoft Corporation. All rights reserved.
