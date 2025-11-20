# Azure Infrastructure Setup Scripts

This folder contains scripts to provision and configure Azure resources for the SXG Evaluation Platform.

## Scripts Overview

### 1. `provision-azure-resources.ps1`
**Purpose**: Provisions all required Azure resources for the evaluation platform.

**Resources Created**:
- ✅ Resource Group
- ✅ Container Apps Environment
- ✅ Container Registry (ACR)
- ✅ Storage Account (with evaluation queue)
- ✅ Azure OpenAI/Cognitive Services Account
- ✅ Azure AI Foundry Project
- ✅ Application Insights
- ✅ Container App (with system-assigned managed identity)

### 2. `setup-managed-identity-permissions.ps1`
**Purpose**: Configures managed identity permissions for secretless authentication.

**Permissions Configured**:
- ✅ Storage Queue Data Contributor
- ✅ Storage Blob Data Contributor
- ✅ AcrPull (Container Registry)
- ✅ Cognitive Services OpenAI User
- ✅ AzureML Data Scientist
- ✅ Monitoring Metrics Publisher
- ✅ Resource Group Reader

## Usage Examples

### Basic Setup (Development Environment)
```powershell
# 1. Provision all resources
.\scripts\provision-azure-resources.ps1 -ResourceGroupName "rg-eval-platform-dev" -ContainerAppName "eval-app-dev"

# 2. Setup permissions
.\scripts\setup-managed-identity-permissions.ps1 -ResourceGroupName "rg-eval-platform-dev" -ContainerAppName "eval-app-dev"

# 3. Deploy application
.\deploy-bulletproof.ps1 -Environment "Development"
```

### Production Setup with Custom Names
```powershell
# 1. Provision resources with specific names
.\scripts\provision-azure-resources.ps1 `
    -ResourceGroupName "rg-eval-platform-prod" `
    -ContainerAppName "eval-app-prod" `
    -Environment "Production" `
    -Location "westus2" `
    -StorageAccountName "evalstorageproduction" `
    -RegistryName "evalregprod" `
    -OpenAIAccountName "evalopenaiprod"

# 2. Setup permissions with resource discovery
.\scripts\setup-managed-identity-permissions.ps1 -ResourceGroupName "rg-eval-platform-prod" -ContainerAppName "eval-app-prod"
```

### Minimal Setup (Skip AI Resources)
```powershell
# 1. Provision core resources only
.\scripts\provision-azure-resources.ps1 `
    -ResourceGroupName "rg-eval-platform-minimal" `
    -ContainerAppName "eval-app-minimal" `
    -SkipAI

# 2. Setup permissions (skip AI)
.\scripts\setup-managed-identity-permissions.ps1 `
    -ResourceGroupName "rg-eval-platform-minimal" `
    -ContainerAppName "eval-app-minimal" `
    -SkipAI
```

## Prerequisites

1. **Azure CLI**: Install from https://docs.microsoft.com/en-us/cli/azure/install-azure-cli
2. **Azure Login**: Run `az login`
3. **Permissions**: Owner or Contributor + User Access Administrator roles
4. **PowerShell**: Windows PowerShell 5.1 or PowerShell Core 7+

## Resource Naming Conventions

The scripts follow Azure naming conventions and automatically generate unique names:

| Resource Type | Naming Pattern | Example |
|---------------|----------------|---------|
| Resource Group | User-defined | `rg-eval-platform-dev` |
| Container App | User-defined | `eval-app-dev` |
| Container Registry | `evalreg{env}{timestamp}` | `evalregdev20241120` |
| Storage Account | `evalstore{env}{timestamp}` | `evalstoredev20241120` |
| Azure OpenAI | `evalopenai{env}{timestamp}` | `evalopenaiprod20241120` |
| AI Project | `evalaiproject{env}` | `evalaiprojectdev` |
| App Insights | `evalinsights{env}` | `evalinsightsprod` |

## Security Features

### Secretless Authentication
- ✅ **No API Keys**: Azure OpenAI access via managed identity
- ✅ **No Connection Strings**: Storage access via managed identity
- ✅ **No Secrets**: All authentication through Azure RBAC
- ✅ **Least Privilege**: Minimum required permissions only
- ✅ **Auto-Rotation**: Azure manages identity tokens automatically

### RBAC Permissions Applied
| Service | Role | Purpose |
|---------|------|---------|
| Storage Account | Storage Queue Data Contributor | Queue read/write operations |
| Storage Account | Storage Blob Data Contributor | Blob read/write operations |
| Container Registry | AcrPull | Pull container images |
| Azure OpenAI | Cognitive Services OpenAI User | Model inference access |
| Azure OpenAI | Cognitive Services User | General AI services |
| AI Foundry Project | AzureML Data Scientist | AI services access |
| Application Insights | Monitoring Metrics Publisher | Telemetry publishing |
| Resource Group | Reader | Resource discovery |

## Troubleshooting

### Common Issues

1. **Permission Denied**
   ```
   Solution: Ensure you have Owner or User Access Administrator role
   ```

2. **Resource Name Already Exists**
   ```
   Solution: Use custom names with -StorageAccountName, -RegistryName parameters
   ```

3. **Azure OpenAI Not Available**
   ```
   Solution: Use -SkipAI flag or create in a different region
   ```

4. **Managed Identity Not Found**
   ```
   Solution: Scripts auto-create managed identity, wait a few moments for propagation
   ```

### Verification Commands

```powershell
# Check container app status
az containerapp show --name "your-app-name" --resource-group "your-rg" --query properties.runningStatus

# Get managed identity principal ID
az containerapp show --name "your-app-name" --resource-group "your-rg" --query identity.principalId

# List all permissions
az role assignment list --assignee "principal-id" --output table

# View container app logs
az containerapp logs show --name "your-app-name" --resource-group "your-rg" --follow
```

## Integration with Deployment

After running these scripts, update your `appsettings.json` with the created resource names:

```json
{
  "AzureStorage": {
    "AccountName": "evalstoredev20241120",
    "UseManagedIdentity": true
  },
  "AzureOpenAI": {
    "Endpoint": "https://evalopenaidev20241120.openai.azure.com/",
    "UseManagedIdentity": true
  }
}
```

Then run the deployment script:
```powershell
.\deploy-bulletproof.ps1 -Environment "Development"
```

## Support

For issues or questions:
1. Check the troubleshooting section above
2. Verify Azure CLI authentication: `az account show`
3. Check Azure service status and regional availability
4. Review Azure subscription quotas and limits