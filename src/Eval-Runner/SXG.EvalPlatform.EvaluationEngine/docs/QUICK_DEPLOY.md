# Quick Deployment Reference

## 🚀 Quick Start

1. **Update Configuration**
   ```powershell
   # Edit deployment/container-app-parameters.json
   # Replace REPLACE_WITH_ACTUAL_* values
   ```

2. **Validate Configuration**
   ```powershell
   .\validate-config.ps1
   ```

3. **Deploy**
   ```powershell
   .\deploy.ps1
   ```

## 📋 Required Configuration Values

Update these in `deployment/container-app-parameters.json`:

| Parameter | Description | Example |
|-----------|-------------|---------|
| `azureStorageAccountName` | Azure Storage account name | `mystorageaccount` |
| `evaluationApiBaseUrl` | Base URL for evaluation API | `https://your-api.azurewebsites.net` |
| `azureTenantId` | Azure AD tenant ID | `72f988bf-86f1-41af-91ab-2d7cd011db47` |
| `azureSubscriptionId` | Azure subscription ID | `d2ef7484-d847-4ca9-88be-d2d9f2a8a50f` |

## 🔧 Common Commands

| Action | Command |
|--------|---------|
| Full deployment | `.\deploy.ps1` |
| Build only | `.\deploy.ps1 build` |
| Push only | `.\deploy.ps1 push` |
| Check status | `.\deploy.ps1 status` |
| View logs | `.\deploy.ps1 logs` |
| Validate config | `.\validate-config.ps1` |

## 🎯 Deployment Details

- **Resource Group:** `rg-sxg-agent-evaluation-platform`
- **Container App:** `eval-framework-app`
- **Container Registry:** `evalplatformregistry`
- **Minimum Replicas:** 3
- **Health Check Port:** 8080

## 📊 Health Endpoints

- **Liveness:** `http://container:8080/health`
- **Readiness:** `http://container:8080/ready`

## 🆘 Troubleshooting

1. **Placeholder values error:** Update parameters file with actual Azure resource details
2. **Docker not found:** Install Docker Desktop
3. **Azure login error:** Run `az login`
4. **ACR push fails:** Check permissions
5. **Managed identity access denied:** Assign required RBAC roles (see deployment guide)

## 🔐 Post-Deployment: Assign Permissions

After deployment, assign these roles to the Container App's managed identity:
- **Storage Queue Data Contributor** (on storage account)  
- **Storage Blob Data Contributor** (on storage account)
- **Cognitive Services OpenAI User** (on Azure OpenAI resource)

See [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md) for detailed instructions.