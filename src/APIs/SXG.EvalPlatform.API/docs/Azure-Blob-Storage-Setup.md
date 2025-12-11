# Azure Blob Storage Configuration for SXG Evaluation Platform

This document explains how to configure Azure Blob Storage with Managed Identity for the SXG Evaluation Platform API.

## Prerequisites

1. Azure Storage Account
2. Azure App Service or Azure Container Instance (for Managed Identity)
3. Proper RBAC permissions

## Setup Instructions

### 1. Create Azure Storage Account

```bash
# Create resource group (if not exists)
az group create --name sxg-eval-rg --location eastus

# Create storage account
az storage account create \
    --name sxgevalstorageaccount \
    --resource-group sxg-eval-rg \
    --location eastus \
    --sku Standard_LRS \
    --kind StorageV2
```

### 2. Create Container for Configuration Files

```bash
# Create container
az storage container create \
    --name eval-configurations \
    --account-name sxgevalstorageaccount \
    --auth-mode login
```

### 3. Upload Default Configuration

Upload the `sample-data/default-metric-configuration.json` file to the blob container:

```bash
# Upload default configuration
az storage blob upload \
    --account-name sxgevalstorageaccount \
    --container-name eval-configurations \
    --name default-metric-configuration.json \
    --file sample-data/default-metric-configuration.json \
    --auth-mode login
```

### 4. Configure Managed Identity

#### For Azure App Service:
1. Enable System Assigned Managed Identity in the App Service
2. Assign Storage Blob Data Reader role to the managed identity

```bash
# Get App Service principal ID
PRINCIPAL_ID=$(az webapp identity show --name your-app-name --resource-group sxg-eval-rg --query principalId -o tsv)

# Get Storage Account ID
STORAGE_ID=$(az storage account show --name sxgevalstorageaccount --resource-group sxg-eval-rg --query id -o tsv)

# Assign Storage Blob Data Reader role
az role assignment create \
    --assignee $PRINCIPAL_ID \
    --role "Storage Blob Data Reader" \
    --scope $STORAGE_ID
```

#### For Local Development:
Use Azure CLI authentication:

```bash
# Login to Azure CLI
az login

# Set default subscription (if needed)
az account set --subscription "your-subscription-id"
```

### 5. Update Application Configuration

Update `appsettings.json` with your storage account details:

```json
{
  "AzureStorage": {
    "AccountName": "sxgevalstorageaccount",
    "ConfigurationContainer": "eval-configurations",
    "DefaultConfigurationBlob": "default-metric-configuration.json"
  }
}
```

For production, use environment variables or Azure Key Vault:

```bash
# Environment variables
export AzureStorage__AccountName="sxgevalstorageaccount"
export AzureStorage__ConfigurationContainer="eval-configurations"
export AzureStorage__DefaultConfigurationBlob="default-metric-configuration.json"
```

## API Usage

Once configured, the API endpoint will read from Azure Blob Storage:

```http
GET /api/v1/eval/configurations
```

## Troubleshooting

### Common Issues:

1. **Authentication Errors**
   - Ensure Managed Identity is enabled
   - Verify RBAC permissions
   - Check storage account name in configuration

2. **Blob Not Found**
   - Verify container and blob names
   - Check if blob exists in storage account
   - Ensure proper file upload

3. **Network Issues**
   - Check storage account firewall settings
   - Verify VNet integration if applicable

### Logging

The application logs Azure Blob Storage operations. Check logs for detailed error information:

```bash
# View application logs (Azure App Service)
az webapp log tail --name your-app-name --resource-group sxg-eval-rg
```

## Security Best Practices

1. Use Managed Identity instead of connection strings
2. Limit RBAC permissions to minimum required
3. Enable storage account firewall when possible
4. Use private endpoints for production environments
5. Enable storage account logging and monitoring

## Development vs Production

### Development:
- Use Azure CLI authentication
- Local appsettings.json configuration
- Public storage account access

### Production:
- Use System Assigned Managed Identity
- Environment variables or Key Vault
- Private endpoints and firewall rules
- Storage account access logging enabled

## Cost Optimization

- Use appropriate storage tier (Hot, Cool, Archive)
- Enable lifecycle management policies
- Monitor storage usage and costs
- Consider data retention policies