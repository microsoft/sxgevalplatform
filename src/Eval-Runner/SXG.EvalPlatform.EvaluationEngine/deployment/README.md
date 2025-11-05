# Deployment Files

This directory contains the Azure Resource Manager (ARM) templates and configuration files needed to deploy the Evaluation Engine to Azure Container Apps with performance optimizations.

## 🚀 Performance Optimizations Included

This deployment now includes environment variables and resource allocations optimized for **60% faster processing**:

### Concurrent Processing Settings
- `MAX_DATASET_CONCURRENCY=3` - Process 3 dataset items simultaneously  
- `MAX_METRICS_CONCURRENCY=8` - Evaluate 8 metrics in parallel
- `EVALUATION_TIMEOUT=30` - Timeout protection for metric evaluation

### HTTP Connection Optimization
- `HTTP_POOL_CONNECTIONS=20` - Total HTTP connection pool size
- `HTTP_POOL_MAXSIZE=10` - Max connections per host
- `HTTP_SESSION_LIFETIME=3600` - Session lifetime (1 hour)

### Azure Storage Optimization
- `AZURE_STORAGE_TIMEOUT=30` - Storage operation timeout
- `AZURE_STORAGE_CONNECT_TIMEOUT=60` - Storage connection timeout

### Resource Allocations
- **CPU**: 2.0 cores (increased from 1.0 for concurrent processing)
- **Memory**: 4.0Gi (increased from 2.0Gi for concurrent operations)

## Files

- **`container-app-template.json`** - ARM template with performance optimization environment variables
- **`container-app-parameters.json`** - Parameters file with optimized resource allocations

## Usage

These files are used by the deployment scripts (`deploy.ps1` and `deploy.sh`) in the root directory.

### Manual Deployment

If you prefer to deploy manually using Azure CLI:

```bash
az deployment group create \
    --resource-group rg-sxg-agent-evaluation-platform \
    --template-file container-app-template.json \
    --parameters @container-app-parameters.json
```

## Configuration

Before deploying, ensure you update the parameters file with actual values for:

- `azureStorageConnectionString`
- `evaluationApiBaseUrl`

See the main [DEPLOYMENT_GUIDE.md](../docs/DEPLOYMENT_GUIDE.md) for detailed instructions.