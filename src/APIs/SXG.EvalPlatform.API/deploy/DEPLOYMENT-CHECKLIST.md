# PPE Deployment - Quick Reference

## ?? Deploy to PPE Now

### Option 1: Automated Deployment (Recommended)

```powershell
# Navigate to deploy directory
cd "D:\Github-Projects\sxgevalplatform\src\APIs\SXG.EvalPlatform.API\deploy"

# Run deployment script
.\Deploy-To-Azure-PPE.ps1
```

### Option 2: Quick Deploy (One-Click)

```powershell
cd "D:\Github-Projects\sxgevalplatform\src\APIs\SXG.EvalPlatform.API\deploy"
.\QuickDeploy-PPE.ps1
```

## ?? What Gets Deployed

### Application Components
- ? .NET 8 API Application
- ? All Controllers and Request Handlers
- ? Refactored code (MetricsConfiguration, DataSet handlers)
- ? Integration and Direct tests (not deployed, for local use)

### Configuration Files
- ? `appsettings.json` (base configuration)
- ? `appsettings.PPE.json` (PPE overrides)
- ? All app settings configured in Azure App Service

### App Settings Configured

| Setting | Value |
|---------|-------|
| **Environment** | PPE |
| **Storage Account** | sxgagentevalppe |
| **Cache Provider** | Memory (100MB) |
| **Telemetry** | Application Insights Enabled |
| **API Version** | 1.0.0 |

## ? Verify Deployment

### 1. Check Health Endpoint
```powershell
curl https://sxgevalapippe.azurewebsites.net/api/v1/health
```

Expected Response:
```json
{
  "status": "Healthy",
  "version": "1.0.0",
"environment": "PPE"
}
```

### 2. Open Swagger UI
```
https://sxgevalapippe.azurewebsites.net/swagger
```

### 3. Test Default Configuration
```powershell
curl https://sxgevalapippe.azurewebsites.net/api/v1/eval/configurations/defaultconfiguration
```

### 4. View Application Logs
```powershell
az webapp log tail --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform
```

## ?? Target Environment Details

| Property | Value |
|----------|-------|
| **Environment** | PPE (Pre-Production) |
| **App Service** | sxgevalapippe |
| **Resource Group** | rg-sxg-agent-evaluation-platform |
| **Region** | East US |
| **Runtime** | .NET 8.0 |
| **URL** | https://sxgevalapippe.azurewebsites.net |

## ?? Recent Changes Included

### Refactored Components ?
1. **MetricsConfigurationRequestHandler**
   - Improved code organization
   - Extracted helper methods
   - Better error handling
   - Consistent caching strategy

2. **DataSetRequestHandler**
   - Upsert behavior (create or update)
   - Cleaner code structure
   - Better cache management

3. **Controllers**
   - POST and PUT methods refactored
   - Cleaner separation of concerns
   - Better validation

### New Features ?
1. **Integration Tests**
   - HTTP-based integration tests
 - Direct controller tests
   - Comprehensive test coverage

2. **Documentation**
   - Deployment guides
   - Testing approach comparisons
   - Refactoring summaries

## ?? Troubleshooting

### Deployment Fails

**Check Azure Login:**
```powershell
az account show
```

**Re-login if needed:**
```powershell
az login
```

### Build Fails

**Clean and rebuild:**
```powershell
cd "D:\Github-Projects\sxgevalplatform\src\APIs\SXG.EvalPlatform.API"
dotnet clean
dotnet restore
dotnet build --configuration Release
```

### App Won't Start

**View logs:**
```powershell
az webapp log tail --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform
```

**Check app settings:**
```powershell
az webapp config appsettings list --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform
```

### Storage Access Issues

**Verify storage account:**
```powershell
az storage account show --name sxgagentevalppe --resource-group rg-sxg-agent-evaluation-platform
```

## ?? Deployment Files

Located in: `SXG.EvalPlatform.API/deploy/`

| File | Purpose |
|------|---------|
| `Deploy-To-Azure-PPE.ps1` | Main deployment script |
| `QuickDeploy-PPE.ps1` | Quick one-click deployment |
| `PPE-Deployment-Guide.md` | Comprehensive deployment guide |
| `DEPLOYMENT-CHECKLIST.md` | This checklist |

## ?? Deployment Process

The script performs these steps:

1. ? **Verify Azure Login** - Check CLI authentication
2. ? **Verify Resources** - Ensure RG and App Service exist
3. ? **Build Application** - Compile .NET 8 project
4. ? **Publish Release** - Create deployment package
5. ? **Create ZIP** - Package for deployment
6. ? **Deploy to Azure** - Upload to App Service
7. ? **Configure Settings** - Set all app settings
8. ? **Cleanup** - Remove temporary files

## ?? Support

### Need Help?

1. **Check Deployment Guide**: `PPE-Deployment-Guide.md`
2. **View Logs**: Use `az webapp log tail`
3. **Check Portal**: https://portal.azure.com
4. **Contact**: DevOps team

## ?? Post-Deployment

After successful deployment:

1. ? **Test API endpoints** - Verify functionality
2. ? **Check Application Insights** - Monitor telemetry
3. ? **Run integration tests** - Validate behavior
4. ? **Update documentation** - Record deployment
5. ? **Notify team** - Share deployment status

## ?? Deployment History

| Date | Version | Deployed By | Notes |
|------|---------|-------------|-------|
| [Date] | 1.0.0 | [Your Name] | Initial PPE deployment with refactored code |

---

**Ready to Deploy?** Run: `.\Deploy-To-Azure-PPE.ps1`

**Questions?** See: `PPE-Deployment-Guide.md`
