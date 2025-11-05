# Azure Storage Managed Identity Configuration

This application now supports **Managed Identity** authentication for Azure Storage services (Queue and Blob), which is more secure than using connection strings.

## Configuration Changes

### Before (Connection String)
```json
{
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "QueueName": "eval-processing-requests",
    "BlobContainerPrefix": "agent-"
  }
}
```

### After (Managed Identity - Recommended)
```json
{
  "AzureStorage": {
    "AccountName": "your-storage-account-name",
    "QueueName": "eval-processing-requests", 
    "BlobContainerPrefix": "agent-",
    "UseManagedIdentity": true,
    "ConnectionString": null
  }
}
```

### Fallback to Connection String (Local Development)
```json
{
  "AzureStorage": {
    "AccountName": "your-storage-account-name",
    "QueueName": "eval-processing-requests",
    "BlobContainerPrefix": "agent-",
    "UseManagedIdentity": false,
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=..."
  }
}
```

## Environment Variables

The following environment variables are supported:

- `AZURE_STORAGE_ACCOUNT_NAME` - Storage account name
- `AZURE_QUEUE_NAME` - Queue name 
- `AZURE_BLOB_CONTAINER_PREFIX` - Blob container prefix
- `AZURE_USE_MANAGED_IDENTITY` - "true" or "false"
- `AZURE_STORAGE_CONNECTION_STRING` - Connection string (fallback)

## Azure Configuration Requirements

### For Production (Managed Identity)

1. **Enable System-Assigned Managed Identity** on your Azure resource (App Service, Container Instance, etc.)

2. **Grant Storage Permissions** to the Managed Identity:
   ```bash
   # Storage Queue Data Contributor (for queue operations)
   az role assignment create \
     --role "Storage Queue Data Contributor" \
     --assignee-object-id <managed-identity-object-id> \
     --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.Storage/storageAccounts/<storage-account>

   # Storage Blob Data Contributor (for blob operations)  
   az role assignment create \
     --role "Storage Blob Data Contributor" \
     --assignee-object-id <managed-identity-object-id> \
     --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.Storage/storageAccounts/<storage-account>
   ```

### For Local Development

1. **Use Azure CLI login**:
   ```bash
   az login
   ```

2. **Or use connection string** by setting `UseManagedIdentity: false` in configuration

## Security Benefits

- ✅ **No secrets in configuration** - Managed Identity eliminates the need for connection strings
- ✅ **Automatic token rotation** - Azure handles credential lifecycle
- ✅ **Least privilege access** - Grant only required permissions
- ✅ **Centralized identity management** - Managed through Azure AD

## Migration Checklist

- [ ] Update `appsettings.json` with new format
- [ ] Set environment variables if using them
- [ ] Enable Managed Identity on Azure resource
- [ ] Grant appropriate storage permissions
- [ ] Test connectivity
- [ ] Remove old connection string references

## Troubleshooting

### Authentication Errors

1. **Check Managed Identity is enabled**:
   ```bash
   az webapp identity show --name <app-name> --resource-group <resource-group>
   ```

2. **Verify role assignments**:
   ```bash
   az role assignment list --assignee <managed-identity-object-id>
   ```

3. **Check storage account access**:
   ```bash
   az storage account show --name <storage-account> --resource-group <resource-group>
   ```

### Local Development Issues

1. **Ensure Azure CLI is logged in**:
   ```bash
   az account show
   ```

2. **Or use connection string fallback**:
   Set `UseManagedIdentity: false` in your local configuration