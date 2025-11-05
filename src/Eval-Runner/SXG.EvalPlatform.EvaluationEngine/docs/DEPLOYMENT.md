# Azure Container Apps Deployment Guide

This guide walks through deploying the SXG Evaluation Platform Evaluation Engine to Azure Container Apps.

## Prerequisites

- Azure CLI installed and configured
- Docker installed (for local testing)
- Azure Container Registry (ACR)
- Azure Storage Account with Queue and Blob services
- Resource Group and Container Apps Environment

## Step 1: Create Azure Resources

### 1.1 Create Resource Group (if not exists)
```bash
az group create --name sxg-eval-platform --location eastus
```

### 1.2 Create Container Apps Environment
```bash
az containerapp env create \
  --name sxg-eval-env \
  --resource-group sxg-eval-platform \
  --location eastus
```

### 1.3 Create Container Registry
```bash
az acr create \
  --name sxgevalregistry \
  --resource-group sxg-eval-platform \
  --sku Basic \
  --admin-enabled true
```

### 1.4 Create Storage Account
```bash
# Create storage account
az storage account create \
  --name sxgevalstorage \
  --resource-group sxg-eval-platform \
  --location eastus \
  --sku Standard_LRS

# Create queue
az storage queue create \
  --name eval-requests \
  --account-name sxgevalstorage

# Create blob container
az storage container create \
  --name eval-results \
  --account-name sxgevalstorage
```

## Step 2: Build and Push Container Image

### 2.1 Login to ACR
```bash
az acr login --name sxgevalregistry
```

### 2.2 Build and Push Image
```bash
# Build image
docker build -t sxgevalregistry.azurecr.io/eval-runner:latest .

# Push image
docker push sxgevalregistry.azurecr.io/eval-runner:latest
```

Or use ACR build:
```bash
az acr build \
  --registry sxgevalregistry \
  --image eval-runner:latest \
  .
```

## Step 3: Create Managed Identity

### 3.1 Create User-Assigned Managed Identity
```bash
az identity create \
  --name eval-runner-identity \
  --resource-group sxg-eval-platform
```

### 3.2 Get Identity Details
```bash
# Get principal ID and client ID
IDENTITY_ID=$(az identity show \
  --name eval-runner-identity \
  --resource-group sxg-eval-platform \
  --query id --output tsv)

PRINCIPAL_ID=$(az identity show \
  --name eval-runner-identity \
  --resource-group sxg-eval-platform \
  --query principalId --output tsv)

CLIENT_ID=$(az identity show \
  --name eval-runner-identity \
  --resource-group sxg-eval-platform \
  --query clientId --output tsv)
```

### 3.3 Assign Storage Permissions
```bash
# Get storage account resource ID
STORAGE_ID=$(az storage account show \
  --name sxgevalstorage \
  --resource-group sxg-eval-platform \
  --query id --output tsv)

# Assign Storage Queue Data Contributor role
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Storage Queue Data Contributor" \
  --scope $STORAGE_ID

# Assign Storage Blob Data Contributor role
az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Storage Blob Data Contributor" \
  --scope $STORAGE_ID
```

## Step 4: Deploy Container App

### 4.1 Create Container App
```bash
az containerapp create \
  --name eval-runner \
  --resource-group sxg-eval-platform \
  --environment sxg-eval-env \
  --image sxgevalregistry.azurecr.io/eval-runner:latest \
  --user-assigned $IDENTITY_ID \
  --registry-server sxgevalregistry.azurecr.io \
  --cpu 1.0 \
  --memory 2Gi \
  --min-replicas 1 \
  --max-replicas 5 \
  --env-vars \
    AZURE_CLIENT_ID=$CLIENT_ID \
    AZURE_QUEUE_ACCOUNT_NAME=sxgevalstorage \
    AZURE_QUEUE_NAME=eval-requests \
    AZURE_BLOB_ACCOUNT_NAME=sxgevalstorage \
    AZURE_BLOB_CONTAINER_NAME=eval-results \
    EVAL_CONFIG_ENDPOINT="https://your-api.com/api/eval/{evalRunId}/config" \
    STATUS_UPDATE_ENDPOINT="https://your-api.com/api/eval/{evalRunId}/status" \
    EVAL_API_KEY="your-eval-api-key" \
    STATUS_API_KEY="your-status-api-key"
```

### 4.2 Alternative: Using YAML Configuration
Create `container-app.yaml`:

```yaml
location: eastus
resourceGroup: sxg-eval-platform
type: Microsoft.App/containerApps
name: eval-runner
properties:
  environmentId: /subscriptions/{subscription-id}/resourceGroups/sxg-eval-platform/providers/Microsoft.App/managedEnvironments/sxg-eval-env
  configuration:
    registries:
      - server: sxgevalregistry.azurecr.io
        identity: /subscriptions/{subscription-id}/resourceGroups/sxg-eval-platform/providers/Microsoft.ManagedIdentity/userAssignedIdentities/eval-runner-identity
  template:
    containers:
      - name: eval-runner
        image: sxgevalregistry.azurecr.io/eval-runner:latest
        resources:
          cpu: 1.0
          memory: 2Gi
        env:
          - name: AZURE_CLIENT_ID
            value: "{client-id}"
          - name: AZURE_QUEUE_ACCOUNT_NAME
            value: "sxgevalstorage"
          - name: AZURE_QUEUE_NAME
            value: "eval-requests"
          - name: AZURE_BLOB_ACCOUNT_NAME
            value: "sxgevalstorage"
          - name: AZURE_BLOB_CONTAINER_NAME
            value: "eval-results"
          - name: EVAL_CONFIG_ENDPOINT
            value: "https://your-api.com/api/eval/{evalRunId}/config"
          - name: STATUS_UPDATE_ENDPOINT
            value: "https://your-api.com/api/eval/{evalRunId}/status"
          - name: EVAL_API_KEY
            secretRef: eval-api-key
          - name: STATUS_API_KEY
            secretRef: status-api-key
    scale:
      minReplicas: 1
      maxReplicas: 5
      rules:
        - name: queue-scaling
          azureQueue:
            queueName: eval-requests
            queueLength: 10
            auth:
              - secretRef: storage-connection-string
                triggerParameter: connection
  identity:
    type: UserAssigned
    userAssignedIdentities:
      "/subscriptions/{subscription-id}/resourceGroups/sxg-eval-platform/providers/Microsoft.ManagedIdentity/userAssignedIdentities/eval-runner-identity": {}
```

Deploy with:
```bash
az containerapp create --resource-group sxg-eval-platform --yaml container-app.yaml
```

## Step 5: Configure Secrets (Recommended)

### 5.1 Create Secrets for API Keys
```bash
# Add secrets to container app
az containerapp secret set \
  --name eval-runner \
  --resource-group sxg-eval-platform \
  --secrets eval-api-key="your-eval-api-key" status-api-key="your-status-api-key"

# Update environment variables to use secrets
az containerapp update \
  --name eval-runner \
  --resource-group sxg-eval-platform \
  --set-env-vars EVAL_API_KEY=secretref:eval-api-key STATUS_API_KEY=secretref:status-api-key
```

## Step 6: Configure Scaling (Optional)

### 6.1 Queue-based Scaling
```bash
az containerapp update \
  --name eval-runner \
  --resource-group sxg-eval-platform \
  --scale-rule-name queue-rule \
  --scale-rule-type azure-queue \
  --scale-rule-metadata queueName=eval-requests queueLength=5 \
  --scale-rule-auth "connection=storage-connection-string"
```

## Step 7: Monitoring and Logging

### 7.1 Enable Application Insights
```bash
# Create Application Insights
az monitor app-insights component create \
  --app eval-runner-insights \
  --location eastus \
  --resource-group sxg-eval-platform

# Get instrumentation key
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
  --app eval-runner-insights \
  --resource-group sxg-eval-platform \
  --query instrumentationKey --output tsv)

# Update container app with instrumentation key
az containerapp update \
  --name eval-runner \
  --resource-group sxg-eval-platform \
  --set-env-vars APPLICATIONINSIGHTS_INSTRUMENTATIONKEY=$INSTRUMENTATION_KEY
```

### 7.2 View Logs
```bash
# Stream logs
az containerapp logs show \
  --name eval-runner \
  --resource-group sxg-eval-platform \
  --follow

# Query logs
az monitor log-analytics query \
  --workspace {workspace-id} \
  --analytics-query "ContainerAppConsoleLogs_CL | where ContainerName_s == 'eval-runner' | order by TimeGenerated desc"
```

## Step 8: Testing the Deployment

### 8.1 Test Queue Message
```bash
# Send test message to queue
az storage message put \
  --queue-name eval-requests \
  --content '{"evalRunId": "test-123", "timestamp": "2024-01-01T00:00:00Z"}' \
  --account-name sxgevalstorage
```

### 8.2 Check Container App Status
```bash
az containerapp show \
  --name eval-runner \
  --resource-group sxg-eval-platform \
  --query "properties.runningStatus"
```

## Troubleshooting

### Common Issues

1. **Authentication Issues**
   - Verify managed identity has correct permissions
   - Check environment variables are set correctly

2. **Image Pull Issues**
   - Ensure ACR authentication is configured
   - Verify image exists in registry

3. **Scaling Issues**
   - Check queue scaling rules are configured
   - Monitor queue length and app replicas

### Debugging Commands

```bash
# Get app details
az containerapp show --name eval-runner --resource-group sxg-eval-platform

# Check revisions
az containerapp revision list --name eval-runner --resource-group sxg-eval-platform

# View environment variables
az containerapp show --name eval-runner --resource-group sxg-eval-platform --query "properties.template.containers[0].env"
```

## Security Best Practices

1. **Use Managed Identity**: Avoid storing connection strings
2. **Store Secrets Securely**: Use Container Apps secrets for API keys
3. **Network Isolation**: Configure VNet integration if needed
4. **RBAC**: Use least privilege principle for role assignments
5. **Monitor**: Set up alerts for failures and unusual activity

## Performance Optimization Configuration

The evaluation engine includes several performance optimizations that can be configured for your deployment:

### 10.1 Concurrent Processing Settings

Configure the following environment variables to optimize concurrent processing:

```bash
# Add to container app environment variables
az containerapp update \
  --name eval-runner \
  --resource-group sxg-eval-platform \
  --set-env-vars \
    MAX_DATASET_CONCURRENCY=3 \
    MAX_METRICS_CONCURRENCY=8 \
    EVALUATION_TIMEOUT=30 \
    HTTP_POOL_CONNECTIONS=20 \
    HTTP_POOL_MAXSIZE=10 \
    HTTP_SESSION_LIFETIME=3600
```

### 10.2 Performance Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `MAX_DATASET_CONCURRENCY` | 3 | Max concurrent dataset items processed |
| `MAX_METRICS_CONCURRENCY` | 8 | Max concurrent metrics evaluated per item |
| `EVALUATION_TIMEOUT` | 30 | Timeout (seconds) for metric evaluation |
| `HTTP_POOL_CONNECTIONS` | 20 | Total HTTP connection pool size |
| `HTTP_POOL_MAXSIZE` | 10 | Max connections per host |
| `HTTP_SESSION_LIFETIME` | 3600 | HTTP session lifetime (seconds) |
| `AZURE_STORAGE_TIMEOUT` | 30 | Azure Storage operation timeout |
| `AZURE_STORAGE_CONNECT_TIMEOUT` | 60 | Azure Storage connection timeout |

### 10.3 Resource Recommendations

Based on performance optimizations, consider these resource configurations:

#### Small Workload (< 100 items)
```bash
--cpu 1.0 \
--memory 2Gi \
--min-replicas 1 \
--max-replicas 3
```

#### Medium Workload (100-1000 items)
```bash
--cpu 2.0 \
--memory 4Gi \
--min-replicas 1 \
--max-replicas 5
```

#### Large Workload (> 1000 items)
```bash
--cpu 4.0 \
--memory 8Gi \
--min-replicas 2 \
--max-replicas 10
```

### 10.4 Monitoring Performance

Add Application Insights configuration for performance monitoring:

```bash
# Enable detailed telemetry
az containerapp update \
  --name eval-runner \
  --resource-group sxg-eval-platform \
  --set-env-vars \
    OTEL_EXPORTER_OTLP_ENDPOINT="https://your-app-insights-endpoint" \
    OTEL_SERVICE_NAME="eval-runner" \
    ENABLE_PERFORMANCE_LOGGING=true
```

## Scaling Considerations

1. **Queue Length**: Configure appropriate queue length triggers (recommended: 5-10 per replica)
2. **Resource Limits**: Set CPU/memory limits based on workload size (see recommendations above)
3. **Concurrent Processing**: Tune concurrency settings based on available resources
4. **Connection Pooling**: HTTP connection pool is optimized for concurrent operations
5. **Cost Optimization**: Use minimum replicas of 0 for cost savings during low usage
6. **Performance**: With optimizations, expect ~60% improvement in processing time

## Performance Testing

To validate your deployment performance:

```bash
# Send test workload
az storage message put \
  --queue-name eval-requests \
  --content '{"evalRunId": "perf-test-001", "timestamp": "2024-01-01T00:00:00Z"}' \
  --account-name sxgevalstorage

# Monitor processing time in logs
az containerapp logs show \
  --name eval-runner \
  --resource-group sxg-eval-platform \
  --follow | grep "Processing completed"
```

This completes the deployment of the SXG Evaluation Platform to Azure Container Apps with performance optimizations!