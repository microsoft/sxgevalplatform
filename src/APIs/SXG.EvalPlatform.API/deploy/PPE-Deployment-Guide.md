# Deployment to PPE Environment - Step-by-Step Guide

## Overview
This guide walks you through deploying the SXG Evaluation Platform API to the **PPE (Pre-Production)** environment.

**Target Details:**
- **App Service Name**: `sxgevalapippe`
- **Resource Group**: `rg-sxg-agent-evaluation-platform`
- **Environment**: PPE
- **Storage Account**: `sxgagentevalppe`

## Prerequisites

### 1. Azure CLI Setup
Ensure you're logged in to Azure CLI:
```powershell
# Login to Azure
az login

# Verify current subscription
az account show

# Set subscription if needed
az account set --subscription <subscription-id>
```

### 2. Verify Access
You need the following permissions:
- Contributor access to resource group `rg-sxg-agent-evaluation-platform`
- Access to storage account `sxgagentevalppe`

### 3. .NET 8 SDK
Ensure .NET 8 SDK is installed:
```powershell
dotnet --version  # Should show 8.0.x
```

## Deployment Steps

### Option 1: Using the Automated Script (Recommended)

#### Step 1: Navigate to Deploy Directory
```powershell
cd "D:\Github-Projects\sxgevalplatform\src\APIs\SXG.EvalPlatform.API\deploy"
```

#### Step 2: Run PPE Deployment Script
```powershell
.\Deploy-To-Azure-PPE.ps1
```

The script will:
1. ? Verify Azure login and resource group
2. ? Check/create App Service Plan
3. ? Check/create App Service (sxgevalapippe)
4. ? Configure PPE-specific app settings
5. ? Build the application (.NET 8)
6. ? Publish to release folder
7. ? Create deployment package
8. ? Deploy to Azure App Service
9. ? Deploy appsettings.PPE.json configuration

**Expected Output:**
```
Starting Azure deployment for SXG Evaluation Platform API - PPE Environment...
Target App Service: sxgevalapippe
Using subscription: <your-subscription>
Resource group verified: rg-sxg-agent-evaluation-platform
App Service Plan already exists: asp-sxg-eval-platform
App Service already exists: sxgevalapippe
Configuring App Settings for PPE...
Building application...
Build successful
Publishing application...
Publish successful
Deploying to Azure App Service...
Deployment completed successfully!

PPE API URL: https://sxgevalapippe.azurewebsites.net
Swagger UI: https://sxgevalapippe.azurewebsites.net/swagger
```

### Option 2: Manual Deployment Steps

If you prefer manual control or the script fails:

#### Step 1: Build the Application
```powershell
cd "D:\Github-Projects\sxgevalplatform\src\APIs\SXG.EvalPlatform.API"

# Clean build
dotnet clean
dotnet build --configuration Release
```

#### Step 2: Publish the Application
```powershell
# Publish to local folder
dotnet publish --configuration Release --output ./publish
```

#### Step 3: Create Deployment Package
```powershell
# Create zip file
Compress-Archive -Path "./publish/*" -DestinationPath "./deploy-ppe.zip" -Force
```

#### Step 4: Deploy to Azure
```powershell
az webapp deploy `
    --name sxgevalapippe `
    --resource-group rg-sxg-agent-evaluation-platform `
    --src-path ./deploy-ppe.zip `
    --type zip `
    --async false
```

#### Step 5: Configure App Settings for PPE
```powershell
az webapp config appsettings set `
    --name sxgevalapippe `
    --resource-group rg-sxg-agent-evaluation-platform `
    --settings `
        "ASPNETCORE_ENVIRONMENT=PPE" `
        "AzureStorage__AccountName=sxgagentevalppe" `
        "ApiSettings__Environment=PPE" `
        "ApiSettings__Version=1.0.0" `
   "AzureStorage__DataSetFolderName=datasets" `
     "AzureStorage__DatasetsFolderName=datasets" `
   "AzureStorage__EvalResultsFolderName=eval-results" `
      "AzureStorage__MetricsConfigurationsFolderName=metrics-configurations" `
        "AzureStorage__PlatformConfigurationsContainer=platform-configurations" `
        "AzureStorage__DefaultMetricsConfiguration=default-metric-configuration.json" `
  "AzureStorage__MetricsConfigurationsTable=MetricsConfigurationsTable" `
        "AzureStorage__DataSetsTable=DataSetsTable" `
        "AzureStorage__EvalRunsTable=EvalRunsTable" `
        "AzureStorage__DatasetEnrichmentRequestsQueueName=dataset-enrichment-requests" `
        "AzureStorage__EvalProcessingRequestsQueueName=eval-processing-requests" `
   "Cache__Provider=Memory" `
  "Cache__DefaultExpirationMinutes=60" `
   "OpenTelemetry__ServiceName=SXG-EvalPlatform-API" `
        "OpenTelemetry__ServiceVersion=1.0.0" `
    "OpenTelemetry__EnableApplicationInsights=true"
```

## Post-Deployment Verification

### 1. Check Deployment Status
```powershell
# View deployment logs
az webapp log tail --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform

# Check app status
az webapp show --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform --query state
```

### 2. Test API Endpoints

#### Health Check
```powershell
curl https://sxgevalapippe.azurewebsites.net/api/v1/health
```

Expected response:
```json
{
  "status": "Healthy",
  "version": "1.0.0",
  "environment": "PPE"
}
```

#### Swagger UI
Open in browser:
```
https://sxgevalapippe.azurewebsites.net/swagger
```

#### Test Default Configuration
```powershell
curl https://sxgevalapippe.azurewebsites.net/api/v1/eval/configurations/defaultconfiguration
```

### 3. Verify App Settings
```powershell
az webapp config appsettings list `
    --name sxgevalapippe `
    --resource-group rg-sxg-agent-evaluation-platform `
    --output table
```

Verify these key settings:
- `ASPNETCORE_ENVIRONMENT` = PPE
- `AzureStorage__AccountName` = sxgagentevalppe
- `ApiSettings__Environment` = PPE

## Configuration Files Deployed

The deployment includes:

1. **appsettings.json** - Base configuration
2. **appsettings.PPE.json** - PPE-specific overrides
3. **Application DLLs** - Compiled .NET 8 assemblies
4. **Dependencies** - All NuGet packages

PPE-specific configuration automatically overrides base settings when `ASPNETCORE_ENVIRONMENT=PPE`.

## Rollback Procedure

If deployment fails or issues occur:

### Option 1: Redeploy Previous Version
```powershell
# View deployment history
az webapp deployment list --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform

# Redeploy specific deployment
az webapp deployment source sync `
    --name sxgevalapippe `
    --resource-group rg-sxg-agent-evaluation-platform
```

### Option 2: Use Deployment Slots (if configured)
```powershell
# Swap back to previous slot
az webapp deployment slot swap `
  --name sxgevalapippe `
  --resource-group rg-sxg-agent-evaluation-platform `
  --slot staging `
    --target-slot production
```

## Troubleshooting

### Issue: Build Fails
**Solution:**
```powershell
# Clean and restore
dotnet clean
dotnet restore
dotnet build --configuration Release
```

### Issue: Deployment Package Too Large
**Solution:**
```powershell
# Exclude unnecessary files
dotnet publish --configuration Release --output ./publish --no-self-contained
```

### Issue: Application Won't Start
**Solution:**
1. Check logs: `az webapp log tail --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform`
2. Verify .NET 8 runtime: `az webapp config show --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform`
3. Check app settings match expected configuration

### Issue: Storage Access Denied
**Solution:**
```powershell
# Verify storage account exists and is accessible
az storage account show --name sxgagentevalppe --resource-group rg-sxg-agent-evaluation-platform

# Check managed identity is enabled
az webapp identity show --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform
```

## Monitoring

### View Application Logs
```powershell
# Real-time log streaming
az webapp log tail `
    --name sxgevalapippe `
    --resource-group rg-sxg-agent-evaluation-platform

# Download logs
az webapp log download `
    --name sxgevalapippe `
    --resource-group rg-sxg-agent-evaluation-platform `
    --log-file ppe-logs.zip
```

### Application Insights
- Dashboard: https://portal.azure.com ? Application Insights
- Query logs and telemetry
- Set up alerts for errors

## Environment-Specific Settings

### PPE vs DEV vs Production

| Setting | DEV | PPE | Production |
|---------|-----|-----|------------|
| Storage Account | sxgagentevaldev | sxgagentevalppe | sxgagentevalprod |
| App Service | sxgevalapidev | sxgevalapippe | sxgevalapiprod |
| Environment Variable | DEV | PPE | Production |
| Cache Provider | Memory | Memory | Redis |
| Telemetry | Console | App Insights | App Insights |

## Next Steps After Deployment

1. ? **Smoke Test**: Run basic API tests
2. ? **Integration Test**: Test with dependent services
3. ? **Performance Test**: Verify response times
4. ? **Monitoring Setup**: Configure alerts
5. ? **Documentation**: Update deployment history

## Support

For issues or questions:
- Check deployment logs
- Review Azure Portal diagnostics
- Contact DevOps team
- Refer to main documentation: `Azure-Deployment-Guide.md`

---

**Deployment Date**: [Current Date]
**Deployed By**: [Your Name]
**Version**: 1.0.0
**Environment**: PPE
