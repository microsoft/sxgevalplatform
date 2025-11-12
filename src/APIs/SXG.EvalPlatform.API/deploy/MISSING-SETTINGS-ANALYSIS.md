# Missing App Settings - Analysis and Fix

## Overview
This document identifies the missing app settings in the PPE deployment and provides solutions.

## Missing Settings Identified

### ? Previously Missing Settings

| Category | Setting Name | Value for PPE | Critical? |
|----------|--------------|---------------|-----------|
| **DataVerse API** | `DataVerseAPI__DatasetEnrichmentRequestAPIEndPoint` | https://sxg-eval-dev.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun | ?? YES |
| **DataVerse API** | `DataVerseAPI__Scope` | https://sxg-eval-dev.crm.dynamics.com/.default | ?? YES |
| **Telemetry** | `Telemetry__AppInsightsConnectionString` | [Application Insights Connection String] | ?? YES |
| **Logging** | `Logging__LogLevel__Default` | Information | ?? No |
| **Logging** | `Logging__LogLevel__Microsoft.AspNetCore` | Warning | ?? No |
| **Logging** | `Logging__LogLevel__Microsoft.ApplicationInsights` | Warning | ?? No |
| **Logging** | `Logging__ApplicationInsights__LogLevel__Default` | Information | ?? No |
| **Logging** | `Logging__ApplicationInsights__LogLevel__Microsoft` | Warning | ?? No |
| **Cache - Redis** | `Cache__Redis__Endpoint` | evalplatformcacheppe.redis.cache.windows.net:6380 | ?? No* |
| **Cache - Redis** | `Cache__Redis__InstanceName` | evalplatformcacheppe | ?? No* |
| **Cache - Redis** | `Cache__Redis__UseManagedIdentity` | true | ?? No* |
| **Cache - Redis** | `Cache__Redis__ConnectTimeoutSeconds` | 5 | ?? No* |
| **Cache - Redis** | `Cache__Redis__CommandTimeoutSeconds` | 3 | ?? No* |
| **Cache - Redis** | `Cache__Redis__UseSsl` | true | ?? No* |
| **Cache - Redis** | `Cache__Redis__Retry__Enabled` | true | ?? No* |
| **Cache - Redis** | `Cache__Redis__Retry__MaxRetryAttempts` | 2 | ?? No* |
| **Cache - Redis** | `Cache__Redis__Retry__BaseDelayMs` | 500 | ?? No* |
| **Cache - Redis** | `Cache__Redis__Retry__MaxDelayMs` | 2000 | ?? No* |
| **Storage** | `AzureStorage__EvalResultsFolderName` | evaluation-results | ?? YES |

**\*Note**: Redis settings not critical since `Cache__Provider=Memory` is configured. However, they're needed if switching to Redis in the future.

## Impact Analysis

### Critical Missing Settings (Immediate Action Required)

#### 1. DataVerse API Configuration ??
**Missing**:
- `DataVerseAPI__DatasetEnrichmentRequestAPIEndPoint`
- `DataVerseAPI__Scope`

**Impact**:
- Dataset enrichment requests will fail
- Integration with DataVerse won't work
- Any features dependent on DataVerse API will throw errors

**Affected Features**:
- Dataset enrichment workflows
- Evaluation run submissions to DataVerse

#### 2. Telemetry Configuration ??
**Missing**:
- `Telemetry__AppInsightsConnectionString`

**Impact**:
- Application Insights telemetry may not work correctly
- Monitoring and diagnostics data may be incomplete
- Performance tracking impacted

#### 3. Storage Folder Names ??
**Issue**:
- Current: `AzureStorage__EvalResultsFolderName=eval-results`
- Should be: `AzureStorage__EvalResultsFolderName=evaluation-results`

**Impact**:
- Results may be stored in wrong location
- Existing results may not be found
- Inconsistency with appsettings.json

### Non-Critical Missing Settings (Nice to Have)

#### 1. Logging Configuration ??
Settings present in appsettings but not in environment variables. The app will use defaults from appsettings.json files, so not critical.

#### 2. Redis Cache Configuration ??
Not critical since Memory cache is configured as the provider. However, these should be present for:
- Future migration to Redis
- Configuration completeness
- Matching appsettings.json structure

## Solutions Provided

### ? Solution 1: Update Deployment Script (Permanent Fix)

**File**: `Deploy-To-Azure-PPE.ps1`

**Status**: ? **UPDATED**

The deployment script now includes **ALL** settings from appsettings.json and appsettings.PPE.json:

```powershell
$appSettings = @(
    # ... 58 total settings including:
    
    # DataVerse API
    "DataVerseAPI__DatasetEnrichmentRequestAPIEndPoint=...",
    "DataVerseAPI__Scope=...",
    
 # Telemetry
    "Telemetry__AppInsightsConnectionString=...",
    
    # Complete Redis configuration
    "Cache__Redis__Endpoint=...",
    # ... all Redis settings
    
    # Complete Logging configuration
    "Logging__LogLevel__Default=...",
    # ... all Logging settings
)
```

### ? Solution 2: Standalone Update Script (Quick Fix)

**File**: `Update-AppSettings-PPE.ps1`

**Status**: ? **CREATED**

**Purpose**: Update app settings WITHOUT redeploying the application

**Usage**:
```powershell
cd "D:\Github-Projects\sxgevalplatform\src\APIs\SXG.EvalPlatform.API\deploy"
.\Update-AppSettings-PPE.ps1
```

**Benefits**:
- No downtime during update
- Fast execution (~30 seconds)
- Verifies settings before and after
- Shows key settings configured

## How to Apply the Fix

### Option 1: Update Settings Now (Recommended)

Use the standalone script to update settings immediately:

```powershell
# Navigate to deploy directory
cd "D:\Github-Projects\sxgevalplatform\src\APIs\SXG.EvalPlatform.API\deploy"

# Run update script
.\Update-AppSettings-PPE.ps1
```

**What happens**:
1. ? Connects to Azure
2. ? Verifies App Service exists
3. ? Shows current settings count
4. ? Applies all 58 settings
5. ? Verifies key settings
6. ?? App may restart (brief downtime)

**Duration**: ~30-60 seconds

### Option 2: Full Redeployment

Use this if you also want to deploy code changes:

```powershell
cd "D:\Github-Projects\sxgevalplatform\src\APIs\SXG.EvalPlatform.API\deploy"
.\Deploy-To-Azure-PPE.ps1
```

**What happens**:
1. ? Builds application
2. ? Creates deployment package
3. ? Deploys to Azure
4. ? Configures ALL app settings
5. ?? App restarts (brief downtime)

**Duration**: ~3-5 minutes

## Verification Steps

After applying the fix, verify the settings:

### 1. Check via Azure Portal
```
https://portal.azure.com
? sxgevalapippe
? Configuration
? Application Settings
```

### 2. Check via Azure CLI
```powershell
az webapp config appsettings list `
    --name sxgevalapippe `
    --resource-group rg-sxg-agent-evaluation-platform `
    --query "[].{Name:name, Value:value}" `
    --output table
```

### 3. Test DataVerse Integration
If your app has a DataVerse integration endpoint, test it:
```powershell
curl https://sxgevalapippe.azurewebsites.net/api/v1/[dataverse-endpoint]
```

### 4. Check Application Insights
Verify telemetry is flowing:
```
https://portal.azure.com
? Application Insights
? Live Metrics
```

## Settings Comparison Matrix

| Setting Category | In appsettings.json | In appsettings.PPE.json | In Old Deployment Script | In New Deployment Script |
|------------------|---------------------|-------------------------|--------------------------|--------------------------|
| API Settings | ? | ? | ? | ? |
| Azure Storage - Basic | ? | ? | ? | ? |
| Azure Storage - Queues | ? | ? | ? | ? |
| Cache - Memory | ? | ? | ? | ? |
| Cache - Redis | ? | ? | ? | ? |
| DataVerse API | ? | ? | ? | ? |
| Telemetry | ? | ? | ? | ? |
| OpenTelemetry | ? | ? | ? | ? |
| Logging | ? | ? | ? | ? |

## Complete Settings List (58 Total)

### 1. Environment (1)
- ASPNETCORE_ENVIRONMENT

### 2. API Settings (2)
- ApiSettings__Version
- ApiSettings__Environment

### 3. Azure Storage (11)
- AzureStorage__AccountName
- AzureStorage__DataSetFolderName
- AzureStorage__DatasetsFolderName
- AzureStorage__EvalResultsFolderName
- AzureStorage__MetricsConfigurationsFolderName
- AzureStorage__PlatformConfigurationsContainer
- AzureStorage__DefaultMetricsConfiguration
- AzureStorage__MetricsConfigurationsTable
- AzureStorage__DataSetsTable
- AzureStorage__EvalRunsTable
- AzureStorage__DatasetEnrichmentRequestsQueueName
- AzureStorage__EvalProcessingRequestsQueueName

### 4. Cache - Memory (5)
- Cache__Provider
- Cache__DefaultExpirationMinutes
- Cache__Memory__SizeLimitMB
- Cache__Memory__CompactionPercentage
- Cache__Memory__ExpirationScanFrequencySeconds

### 5. Cache - Redis (10)
- Cache__Redis__Endpoint
- Cache__Redis__InstanceName
- Cache__Redis__UseManagedIdentity
- Cache__Redis__ConnectTimeoutSeconds
- Cache__Redis__CommandTimeoutSeconds
- Cache__Redis__UseSsl
- Cache__Redis__Retry__Enabled
- Cache__Redis__Retry__MaxRetryAttempts
- Cache__Redis__Retry__BaseDelayMs
- Cache__Redis__Retry__MaxDelayMs

### 6. DataVerse API (2) ?? **CRITICAL**
- DataVerseAPI__DatasetEnrichmentRequestAPIEndPoint
- DataVerseAPI__Scope

### 7. Telemetry (1) ?? **CRITICAL**
- Telemetry__AppInsightsConnectionString

### 8. OpenTelemetry (7)
- OpenTelemetry__ServiceName
- OpenTelemetry__ServiceVersion
- OpenTelemetry__EnableConsoleExporter
- OpenTelemetry__EnableApplicationInsights
- OpenTelemetry__SamplingRatio
- OpenTelemetry__MaxExportBatchSize
- OpenTelemetry__ExportTimeoutMilliseconds

### 9. Logging (5)
- Logging__LogLevel__Default
- Logging__LogLevel__Microsoft.AspNetCore
- Logging__LogLevel__Microsoft.ApplicationInsights
- Logging__ApplicationInsights__LogLevel__Default
- Logging__ApplicationInsights__LogLevel__Microsoft

## Recommendation

### Immediate Action (Today) ?
```powershell
# Update app settings immediately
.\Update-AppSettings-PPE.ps1
```

### Future Deployments ?
The updated `Deploy-To-Azure-PPE.ps1` script now includes all settings automatically.

### Monitoring
- Check Application Insights for any errors related to missing configuration
- Monitor logs for DataVerse API integration issues
- Verify evaluation runs complete successfully

---

**Status**: ? Fixed
**Updated**: [Current Date]
**Scripts**: Deploy-To-Azure-PPE.ps1, Update-AppSettings-PPE.ps1
