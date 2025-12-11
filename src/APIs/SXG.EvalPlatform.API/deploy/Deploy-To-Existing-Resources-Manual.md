# Deploy to Existing Azure Resources - Manual Steps

This guide provides step-by-step manual deployment instructions for the existing Azure resources.

## Existing Resources
- **Resource Group**: `rg-sxg-agent-evaluation-platform`
- **Web App**: `EVAL-API`
- **App Service Plan**: `agent-eval-platform-appservice-plan` (P0v3)
- **Location**: East US
- **Storage Account**: `sxgagenteval` (existing)
- **Managed Identity**: ✅ Already configured
- **RBAC Permissions**: ✅ Already configured

## Quick Deployment Commands

### 1. Login to Azure (if not already logged in)
```bash
az login
```

### 2. Set Application Settings
```bash
az webapp config appsettings set \
  --name EVAL-API \
  --resource-group rg-sxg-agent-evaluation-platform \
  --settings \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "AzureStorage__AccountName=sxgagenteval" \
    "AzureStorage__ConfigurationContainer=eval-configurations" \
    "AzureStorage__DefaultConfigurationBlob=default-metric-configuration.json" \
    "Logging__LogLevel__Default=Information" \
    "ApiSettings__Version=1.0.0" \
    "ApiSettings__Environment=Production"
```

### 3. Build and Publish Application
```bash
# Navigate to project directory
cd "src\Sxg-Eval-Platform-Api"

# Restore packages
dotnet restore

# Build application
dotnet build -c Release --no-restore

# Publish application
dotnet publish -c Release -o .\publish --no-restore

# Create ZIP package
Compress-Archive -Path ".\publish\*" -DestinationPath ".\deploy.zip" -Force
```

### 4. Deploy to Azure
```bash
az webapp deploy \
  --name EVAL-API \
  --resource-group rg-sxg-agent-evaluation-platform \
  --src-path .\deploy.zip \
  --type zip
```

### 5. Upload Sample Configuration (Optional)
```bash
az storage blob upload \
  --account-name sxgagenteval \
  --container-name eval-configurations \
  --name default-metric-configuration.json \
  --file sample-data\default-metric-configuration.json \
  --auth-mode login \
  --overwrite
```

### 6. Restart Web App
```bash
az webapp restart \
  --name EVAL-API \
  --resource-group rg-sxg-agent-evaluation-platform
```

### 7. Test Deployment
```bash
# Get the app URL
$appUrl = az webapp show \
  --name EVAL-API \
  --resource-group rg-sxg-agent-evaluation-platform \
  --query defaultHostName -o tsv

echo "Application URL: https://$appUrl"
echo "Health Check: https://$appUrl/api/v1/health"
echo "Swagger UI: https://$appUrl"

# Test health endpoint
curl -X GET "https://$appUrl/api/v1/health"
```

## Expected URLs After Deployment
- **Main URL**: `https://eval-api.azurewebsites.net`
- **Swagger UI**: `https://eval-api.azurewebsites.net`
- **Health Check**: `https://eval-api.azurewebsites.net/api/v1/health`
- **Default Config**: `https://eval-api.azurewebsites.net/api/v1/eval/configurations`

## Verification Steps

### 1. Test Health Endpoint
```bash
curl -X GET "https://eval-api.azurewebsites.net/api/v1/health"
```
Expected response:
```json
{
  "Status": "Healthy",
  "Timestamp": "2025-10-07T...",
  "Version": "1.0.0",
  "Environment": "Production"
}
```

### 2. Test Default Configuration Endpoint
```bash
curl -X GET "https://eval-api.azurewebsites.net/api/v1/eval/configurations"
```
Should return the default metric configuration from Azure Blob Storage.

### 3. Test Swagger UI
Open `https://eval-api.azurewebsites.net` in your browser to access the interactive API documentation.

## Troubleshooting

### If deployment fails:
1. **Check permissions**: Ensure you have Contributor access to the resource group
2. **Verify resources**: Confirm all resource names are correct
3. **Check logs**: `az webapp log tail --name EVAL-API --resource-group rg-sxg-agent-evaluation-platform`

### If blob storage access fails:
1. **Verify Managed Identity**: Ensure it's enabled on the Web App
2. **Check RBAC**: Verify "Storage Blob Data Reader" role is assigned
3. **Test manually**: Use Azure Portal to verify blob access

### If app doesn't start:
1. **Check app settings**: Verify all required configuration is present
2. **Review logs**: Check Application Insights or Web App logs
3. **Restart app**: `az webapp restart --name EVAL-API --resource-group rg-sxg-agent-evaluation-platform`

## Monitoring and Logs

### Stream logs in real-time:
```bash
az webapp log tail --name EVAL-API --resource-group rg-sxg-agent-evaluation-platform
```

### Download logs:
```bash
az webapp log download --name EVAL-API --resource-group rg-sxg-agent-evaluation-platform
```

### Enable detailed logging:
```bash
az webapp log config --name EVAL-API --resource-group rg-sxg-agent-evaluation-platform \
  --application-logging filesystem --level information
```