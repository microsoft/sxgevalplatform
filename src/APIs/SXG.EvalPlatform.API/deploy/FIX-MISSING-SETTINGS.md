# Fix Missing App Settings - Quick Action Guide

## ?? Issue Identified
Several critical app settings from `appsettings.json` were missing from the PPE deployment, including:
- ? `DataVerseAPI__DatasetEnrichmentRequestAPIEndPoint`
- ? `DataVerseAPI__Scope`
- ? `Telemetry__AppInsightsConnectionString`
- ? Redis cache configuration (10 settings)
- ? Logging configuration (5 settings)
- ? Wrong value for `EvalResultsFolderName` (eval-results vs evaluation-results)

## ? Solution Ready

### Option 1: Update Settings Only (Fast - Recommended)

**Use this to fix settings immediately without redeploying code**

```powershell
# Navigate to deploy directory
cd "D:\Github-Projects\sxgevalplatform\src\APIs\SXG.EvalPlatform.API\deploy"

# Run the update script
.\Update-AppSettings-PPE.ps1
```

**What it does**:
- ? Updates ALL 58 app settings
- ? Includes all missing DataVerse, Telemetry, and Redis settings
- ? Shows before/after comparison
- ?? Takes ~30 seconds
- ?? May cause brief app restart

### Option 2: Full Redeployment (If you also need to deploy code)

```powershell
cd "D:\Github-Projects\sxgevalplatform\src\APIs\SXG.EvalPlatform.API\deploy"
.\Deploy-To-Azure-PPE.ps1
```

**What it does**:
- ? Builds and deploys application
- ? Configures ALL 58 app settings
- ?? Takes ~3-5 minutes

## ?? Complete Settings Now Included

### Critical Settings Added (Previously Missing)

#### DataVerse API ??
```
DataVerseAPI__DatasetEnrichmentRequestAPIEndPoint = https://sxg-eval-dev.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun
DataVerseAPI__Scope = https://sxg-eval-dev.crm.dynamics.com/.default
```

#### Telemetry ??
```
Telemetry__AppInsightsConnectionString = [Application Insights Connection String]
```

#### Redis Cache Configuration (10 settings)
```
Cache__Redis__Endpoint = evalplatformcacheppe.redis.cache.windows.net:6380
Cache__Redis__InstanceName = evalplatformcacheppe
Cache__Redis__UseManagedIdentity = true
Cache__Redis__ConnectTimeoutSeconds = 5
Cache__Redis__CommandTimeoutSeconds = 3
Cache__Redis__UseSsl = true
Cache__Redis__Retry__Enabled = true
Cache__Redis__Retry__MaxRetryAttempts = 2
Cache__Redis__Retry__BaseDelayMs = 500
Cache__Redis__Retry__MaxDelayMs = 2000
```

#### Logging Configuration (5 settings)
```
Logging__LogLevel__Default = Information
Logging__LogLevel__Microsoft.AspNetCore = Warning
Logging__LogLevel__Microsoft.ApplicationInsights = Warning
Logging__ApplicationInsights__LogLevel__Default = Information
Logging__ApplicationInsights__LogLevel__Microsoft = Warning
```

#### Storage Correction
```
AzureStorage__EvalResultsFolderName = evaluation-results  # Was: eval-results
```

## ?? Total Settings: 58

| Category | Count |
|----------|-------|
| Environment | 1 |
| API Settings | 2 |
| Azure Storage | 11 |
| Cache - Memory | 5 |
| Cache - Redis | 10 |
| DataVerse API | 2 |
| Telemetry | 1 |
| OpenTelemetry | 7 |
| Logging | 5 |
| **TOTAL** | **58** |

## ? Verification After Update

### 1. Check Key Settings
```powershell
az webapp config appsettings list `
    --name sxgevalapippe `
    --resource-group rg-sxg-agent-evaluation-platform `
    --query "[?contains(name, 'DataVerseAPI')].{Name:name, Value:value}" `
    --output table
```

### 2. Test API Health
```powershell
curl https://sxgevalapippe.azurewebsites.net/api/v1/health
```

### 3. View in Portal
```
https://portal.azure.com
? sxgevalapippe
? Configuration
? Application Settings
```

## ?? Files Updated/Created

| File | Status | Purpose |
|------|--------|---------|
| `Deploy-To-Azure-PPE.ps1` | ? Updated | Full deployment with ALL settings |
| `Update-AppSettings-PPE.ps1` | ? New | Update settings only (no code deployment) |
| `MISSING-SETTINGS-ANALYSIS.md` | ? New | Detailed analysis of missing settings |
| `FIX-MISSING-SETTINGS.md` | ? New | This quick action guide |

## ?? Recommended Action

**Run this NOW to fix the missing settings:**

```powershell
cd "D:\Github-Projects\sxgevalplatform\src\APIs\SXG.EvalPlatform.API\deploy"
.\Update-AppSettings-PPE.ps1
```

**Duration**: 30-60 seconds
**Downtime**: Minimal (app may restart)
**Impact**: All settings will be correct

---

**Status**: ? Scripts Ready
**Date**: [Current Date]
**Environment**: PPE (sxgevalapippe)
