# Deploy SXG Evaluation Platform to Azure

This guide walks you through deploying the SXG Evaluation Platform API to Azure App Service with Managed Identity.

## Prerequisites

1. Azure CLI installed and logged in
2. Azure subscription with appropriate permissions
3. Resource group created
4. Azure Storage Account with blob container

## Deployment Steps

### 1. Create Azure Resources

```bash
# Set variables
RESOURCE_GROUP="sxg-eval-rg"
APP_NAME="sxg-eval-platform-api"
LOCATION="eastus"
STORAGE_ACCOUNT="sxgagenteval"
CONTAINER_NAME="eval-configurations"

# Create Resource Group (if not exists)
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create App Service Plan
az appservice plan create \
  --name "${APP_NAME}-plan" \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku B1 \
  --is-linux

# Create Web App
az webapp create \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --plan "${APP_NAME}-plan" \
  --runtime "DOTNET|8.0"
```

### 2. Configure Managed Identity

```bash
# Enable System Assigned Managed Identity
az webapp identity assign \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP

# Get the principal ID
PRINCIPAL_ID=$(az webapp identity show \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query principalId -o tsv)

echo "Principal ID: $PRINCIPAL_ID"
```

### 3. Grant Storage Permissions

```bash
# Get Storage Account ID
STORAGE_ID=$(az storage account show \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --query id -o tsv)

# Assign Storage Blob Data Reader role
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Storage Blob Data Reader" \
  --scope $STORAGE_ID

# Verify role assignment
az role assignment list \
  --assignee $PRINCIPAL_ID \
  --scope $STORAGE_ID
```

### 4. Configure Application Settings

```bash
# Set application settings
az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "AzureStorage__AccountName=$STORAGE_ACCOUNT" \
    "AzureStorage__ConfigurationContainer=$CONTAINER_NAME" \
    "AzureStorage__DefaultConfigurationBlob=default-metric-configuration.json" \
    "Logging__LogLevel__Default=Information" \
    "ApiSettings__Version=1.0.0" \
    "ApiSettings__Environment=Production"
```

### 5. Deploy Application

#### Option A: Deploy from Local (Recommended for development)

```bash
# Build and publish the application
cd "D:\Projects\sxg-eval-platform\src\Sxg-Eval-Platform-Api"
dotnet publish -c Release -o ./publish

# Deploy using ZIP
az webapp deploy \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --src-path ./publish.zip \
  --type zip
```

#### Option B: Deploy from GitHub (Recommended for production)

```bash
# Configure GitHub deployment
az webapp deployment source config \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --repo-url https://github.com/microsoft/sxg-eval-platform \
  --branch main \
  --manual-integration
```

### 6. Upload Configuration to Blob Storage

```bash
# Upload default configuration file
az storage blob upload \
  --account-name $STORAGE_ACCOUNT \
  --container-name $CONTAINER_NAME \
  --name default-metric-configuration.json \
  --file sample-data/default-metric-configuration.json \
  --auth-mode login
```

### 7. Verify Deployment

```bash
# Get the app URL
APP_URL=$(az webapp show \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query defaultHostName -o tsv)

echo "Application URL: https://$APP_URL"
echo "Swagger UI: https://$APP_URL"
echo "Health Check: https://$APP_URL/api/v1/health"
echo "Default Config: https://$APP_URL/api/v1/eval/configurations"

# Test the health endpoint
curl -X GET "https://$APP_URL/api/v1/health"
```

## Post-Deployment Configuration

### 1. Configure Custom Domain (Optional)

```bash
# Add custom domain
az webapp config hostname add \
  --webapp-name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --hostname your-domain.com

# Configure SSL certificate
az webapp config ssl bind \
  --certificate-thumbprint <thumbprint> \
  --ssl-type SNI \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP
```

### 2. Configure Scaling (Optional)

```bash
# Configure auto-scaling
az monitor autoscale create \
  --resource-group $RESOURCE_GROUP \
  --resource $APP_NAME \
  --resource-type Microsoft.Web/sites \
  --name "${APP_NAME}-autoscale" \
  --min-count 1 \
  --max-count 3 \
  --count 1

# Add scale-out rule (CPU > 70%)
az monitor autoscale rule create \
  --resource-group $RESOURCE_GROUP \
  --autoscale-name "${APP_NAME}-autoscale" \
  --scale out 1 \
  --condition "Percentage CPU > 70 avg 5m"

# Add scale-in rule (CPU < 30%)
az monitor autoscale rule create \
  --resource-group $RESOURCE_GROUP \
  --autoscale-name "${APP_NAME}-autoscale" \
  --scale in 1 \
  --condition "Percentage CPU < 30 avg 5m"
```

### 3. Configure Monitoring

```bash
# Enable Application Insights
az monitor app-insights component create \
  --app "${APP_NAME}-insights" \
  --location $LOCATION \
  --resource-group $RESOURCE_GROUP \
  --application-type web

# Get instrumentation key
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
  --app "${APP_NAME}-insights" \
  --resource-group $RESOURCE_GROUP \
  --query instrumentationKey -o tsv)

# Configure App Insights in the web app
az webapp config appsettings set \
  --name $APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --settings "APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=$INSTRUMENTATION_KEY"
```

## Troubleshooting

### Common Issues:

1. **Managed Identity Authentication Fails**
   - Verify Managed Identity is enabled
   - Check RBAC role assignments
   - Ensure storage account allows Managed Identity access

2. **Blob Storage Access Denied**
   - Verify container and blob names in configuration
   - Check storage account firewall settings
   - Ensure proper permissions are assigned

3. **Application Startup Errors**
   - Check application logs: `az webapp log tail --name $APP_NAME --resource-group $RESOURCE_GROUP`
   - Verify all required app settings are configured
   - Check for missing dependencies

### Viewing Logs:

```bash
# Stream application logs
az webapp log tail --name $APP_NAME --resource-group $RESOURCE_GROUP

# Download log files
az webapp log download --name $APP_NAME --resource-group $RESOURCE_GROUP

# Configure logging
az webapp log config --name $APP_NAME --resource-group $RESOURCE_GROUP \
  --application-logging filesystem --level information
```

## Security Considerations

1. **Use Managed Identity** instead of connection strings
2. **Enable HTTPS only** for production
3. **Configure CORS** appropriately for your client applications
4. **Use Azure Key Vault** for sensitive configuration
5. **Enable Web Application Firewall** if using Application Gateway
6. **Regular security updates** and vulnerability scanning

## Cost Optimization

1. **Use appropriate App Service Plan** based on load
2. **Configure auto-scaling** to handle traffic spikes
3. **Monitor usage** with Azure Cost Management
4. **Use staging slots** for blue-green deployments
5. **Consider Azure Container Instances** for lower-cost scenarios