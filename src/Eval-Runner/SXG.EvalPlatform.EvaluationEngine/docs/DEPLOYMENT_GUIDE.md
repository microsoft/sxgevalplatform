# Azure Container Apps Deployment Guide

This guide provides step-by-step instructions for deploying the Evaluation Engine to Azure Container Apps.

## Prerequisites

Before deploying, ensure you have:

1. **Azure CLI** installed and configured
   ```bash
   az --version  # Check if installed
   az login      # Login to Azure
   ```

2. **Docker** installed and running
   ```bash
   docker --version  # Check if installed
   ```

3. **Required Azure Resources** (should already exist):
   - Resource Group: `rg-sxg-agent-evaluation-platform`
   - Container Registry: `evalplatformregistry`
   - Container App: `eval-framework-app`

## Configuration

### 1. Update Parameters File

Before deploying, you must update the parameters file with actual configuration values:

**File:** `deployment/container-app-parameters.json`

Replace the following placeholder values:

```json
{
  "azureStorageAccountName": {
    "value": "REPLACE_WITH_STORAGE_ACCOUNT_NAME"
  },
  "evaluationApiBaseUrl": {
    "value": "REPLACE_WITH_ACTUAL_API_BASE_URL"
  },
  "azureTenantId": {
    "value": "REPLACE_WITH_TENANT_ID"
  },
  "azureSubscriptionId": {
    "value": "REPLACE_WITH_SUBSCRIPTION_ID"
  }
}
```

**Required Configuration Values:**

1. **Azure Storage Account Name**
   - Get from Azure Portal → Storage Account → Properties
   - Format: `mystorageaccount` (just the account name, not the full URL)

2. **Evaluation API Base URL**
   - The base URL of your evaluation API service
   - Example: `https://your-api-service.azurewebsites.net`

3. **Azure Tenant ID**
   - Get from Azure Portal → Azure Active Directory → Properties
   - Format: `72f988bf-86f1-41af-91ab-2d7cd011db47`

4. **Azure Subscription ID**
   - Get from Azure Portal → Subscriptions
   - Format: `d2ef7484-d847-4ca9-88be-d2d9f2a8a50f`

### 2. Verify Application Insights

The Application Insights connection string is already configured in the parameters file:
```json
"applicationInsightsConnectionString": {
  "value": "InstrumentationKey=11aea68a-d038-460d-9ffc-5531760d8df7;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;..."
}
```

## Deployment Options

### Option 1: Full Deployment (Recommended)

Deploy everything in one command:

**Windows (PowerShell):**
```powershell
.\deploy.ps1
```

**Linux/macOS (Bash):**
```bash
./deploy.sh
```

### Option 2: Step-by-Step Deployment

If you prefer to control each step:

**Build Docker Image:**
```powershell
.\deploy.ps1 build
```

**Push to Container Registry:**
```powershell
.\deploy.ps1 push
```

**Deploy to Container Apps:**
```powershell
.\deploy.ps1 deploy
```

## Post-Deployment

### 1. Check Deployment Status

```powershell
.\deploy.ps1 status
```

This will show:
- Container App name and status
- Number of replicas running
- FQDN (if applicable)

### 2. View Application Logs

```powershell
.\deploy.ps1 logs
```

### 3. Monitor Health

The container app includes health check endpoints:

- **Liveness Probe:** `http://container:8080/health`
- **Readiness Probe:** `http://container:8080/ready`

### 4. Monitor in Azure Portal

1. Go to Azure Portal
2. Navigate to Resource Group: `rg-sxg-agent-evaluation-platform`
3. Click on Container App: `eval-framework-app`
4. Monitor:
   - Revision management
   - Metrics and logs
   - Console logs

## Configuration Details

### Container App Configuration

- **Minimum Replicas:** 3 (as requested)
- **Maximum Replicas:** 10 (auto-scaling)
- **CPU:** 1.0 cores per instance
- **Memory:** 2.0 GB per instance
- **Port:** 8080 (for health checks)

### Scaling Rules

The container app is configured with:

1. **Queue-based scaling:**
   - Scales based on Azure Service Bus queue length
   - Triggers when queue has more than 5 messages

2. **CPU-based scaling:**
   - Scales when concurrent requests exceed 10

### Environment Variables

The following environment variables are automatically configured:

- `APPLICATIONINSIGHTS_CONNECTION_STRING`: Application Insights connection
- `AZURE_STORAGE_ACCOUNT_NAME`: Azure Storage account name
- `AZURE_QUEUE_NAME`: Azure Storage queue name
- `AZURE_USE_MANAGED_IDENTITY`: Enable managed identity (set to "true")
- `AZURE_TENANT_ID`: Azure Active Directory tenant ID
- `AZURE_SUBSCRIPTION_ID`: Azure subscription ID
- `EVALUATION_API_BASE_URL`: Evaluation API base URL
- `AZURE_OPENAI_ENDPOINT`: Azure OpenAI endpoint
- `AZURE_OPENAI_API_VERSION`: Azure OpenAI API version
- `AZURE_OPENAI_DEPLOYMENT_NAME`: Azure OpenAI model deployment name
- `AZURE_OPENAI_USE_MANAGED_IDENTITY`: Enable managed identity for OpenAI (set to "true")
- `PYTHONPATH`: Python module path
- `PYTHON_UNBUFFERED`: Python output buffering
- `LOG_LEVEL`: Application log level

## Troubleshooting

### Common Issues

1. **Parameters file contains placeholders:**
   - Error: "Parameters file contains placeholder values"
   - Solution: Update `deployment/container-app-parameters.json` with actual values

2. **Docker build fails:**
   - Check Docker is running
   - Verify Dockerfile syntax
   - Check network connectivity

3. **ACR push fails:**
   - Verify you're logged into Azure: `az login`
   - Check ACR permissions: `az acr show --name evalplatformregistry`

4. **Container app deployment fails:**
   - Check ARM template syntax
   - Verify resource group and container app exist
   - Check Azure permissions

### Useful Commands

**Check Azure login:**
```bash
az account show
```

**List container registries:**
```bash
az acr list --resource-group rg-sxg-agent-evaluation-platform
```

**Check container app status:**
```bash
az containerapp show --name eval-framework-app --resource-group rg-sxg-agent-evaluation-platform
```

**View container app logs:**
```bash
az containerapp logs show --name eval-framework-app --resource-group rg-sxg-agent-evaluation-platform --tail 50
```

### Health Check Endpoints

Test health endpoints locally during development:

```bash
# Liveness check
curl http://localhost:8080/health

# Readiness check  
curl http://localhost:8080/ready
```

Expected responses:
- **200 OK:** Service is healthy/ready
- **503 Service Unavailable:** Service is not ready (readiness only)

## Security Considerations

1. **Managed Identity Authentication:**
   - Container app uses system-assigned managed identity
   - No connection strings or API keys stored in the application
   - Automatic token management and refresh

2. **Required RBAC Permissions:**
   After deployment, assign the following roles to the Container App's managed identity:
   
   **Azure Storage:**
   ```bash
   # Get the Container App's managed identity principal ID
   PRINCIPAL_ID=$(az containerapp show --name eval-framework-app --resource-group rg-sxg-agent-evaluation-platform --query identity.principalId -o tsv)
   
   # Assign Storage Queue Data Contributor role
   az role assignment create \
     --assignee $PRINCIPAL_ID \
     --role "Storage Queue Data Contributor" \
     --scope "/subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/rg-sxg-agent-evaluation-platform/providers/Microsoft.Storage/storageAccounts/YOUR_STORAGE_ACCOUNT"
   
   # Assign Storage Blob Data Contributor role
   az role assignment create \
     --assignee $PRINCIPAL_ID \
     --role "Storage Blob Data Contributor" \
     --scope "/subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/rg-sxg-agent-evaluation-platform/providers/Microsoft.Storage/storageAccounts/YOUR_STORAGE_ACCOUNT"
   ```
   
   **Azure OpenAI:**
   ```bash
   # Assign Cognitive Services OpenAI User role
   az role assignment create \
     --assignee $PRINCIPAL_ID \
     --role "Cognitive Services OpenAI User" \
     --scope "/subscriptions/YOUR_SUBSCRIPTION_ID/resourceGroups/rg-sxg-agent-evaluation-platform/providers/Microsoft.CognitiveServices/accounts/evalplatform"
   ```

3. **Network Security:**
   - Container app ingress is configured as internal-only
   - Not exposed to public internet
   - All Azure service communication uses secure HTTPS

## Monitoring and Logging

### Application Insights

- All application telemetry is sent to Application Insights
- Use Azure Portal to view:
  - Application performance
  - Error rates and exceptions
  - Custom metrics and traces

### Container App Logs

- View real-time logs in Azure Portal
- Use Azure CLI for command-line log viewing
- Configure log retention as needed

## Support

For deployment issues:
1. Check this deployment guide
2. Review Azure Container Apps documentation
3. Check Application Insights for runtime errors
4. Contact the development team with specific error messages