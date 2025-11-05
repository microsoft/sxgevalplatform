# Azure AI Foundry Managed Identity Setup Guide

## Overview

This guide explains how to configure Azure AI Foundry with managed identity authentication for the SXG Evaluation Platform.

## ✅ What's Already Configured

The application now supports managed identity authentication for:

1. **Azure OpenAI Services** - For model-based evaluators (coherence, similarity, fluency, etc.)
2. **Azure AI Foundry Project** - For safety evaluators and content filtering
3. **Azure Storage** - Already using managed identity for queue and blob operations

## 🔧 Code Changes Made

### 1. Updated `azure_ai_config.py`

```python
# OLD - API Key Authentication
self._model_config = AzureOpenAIModelConfiguration(
    azure_endpoint=openai_config.endpoint,
    api_key=openai_config.api_key,  # ❌ API Key
    azure_deployment=openai_config.deployment_name,
    api_version=openai_config.api_version,
)

# NEW - Managed Identity Authentication  
self._model_config = AzureOpenAIModelConfiguration(
    azure_endpoint=base_endpoint,
    azure_ad_token_provider=self._credential,  # ✅ Managed Identity
    azure_deployment=openai_config.deployment_name,
    api_version=openai_config.api_version,
)
```

### 2. Azure AI Foundry Project Integration

```python
self._azure_ai_project = {
    "subscription_id": ai_config.subscription_id,
    "resource_group_name": ai_config.resource_group_name, 
    "project_name": ai_config.project_name,
    "credential": self._credential  # ✅ Managed Identity for project access
}
```

## 🔑 Required Azure RBAC Permissions

For managed identity to work, assign these roles to your managed identity:

### 1. Azure OpenAI Permissions
```bash
# Cognitive Services OpenAI User
az role assignment create \
  --assignee <managed-identity-principal-id> \
  --role "Cognitive Services OpenAI User" \
  --scope "/subscriptions/d2ef7484-d847-4ca9-88be-d2d9f2a8a50f/resourceGroups/rg-sxg-agent-evaluation-platform/providers/Microsoft.CognitiveServices/accounts/evalplatform"
```

### 2. Azure AI Foundry Project Permissions  
```bash
# Azure AI Developer (for project access)
az role assignment create \
  --assignee <managed-identity-principal-id> \
  --role "Azure AI Developer" \
  --scope "/subscriptions/d2ef7484-d847-4ca9-88be-d2d9f2a8a50f/resourceGroups/rg-sxg-agent-evaluation-platform/providers/Microsoft.CognitiveServices/accounts/evalplatform/projects/evalplatformproject"
```

### 3. Storage Permissions (Already Configured)
```bash
# Storage Queue Data Contributor & Storage Blob Data Contributor  
# (These should already be assigned for queue/blob operations)
```

## 🧪 Testing Managed Identity

Run this test to verify managed identity is working:

```python
# Test script
python -c "
from eval_runner.azure_ai_config import azure_ai_config
from eval_runner.models.eval_models import DatasetItem

# Test configuration
print('Testing managed identity configuration...')
model_config = azure_ai_config.model_config
project_config = azure_ai_config.azure_ai_project
print('✅ Configuration loaded successfully')

# Test Azure OpenAI access
test_item = DatasetItem(
    prompt='What is AI?',
    ground_truth='AI is artificial intelligence',
    actual_response='AI refers to artificial intelligence technology',
    context=[]
)

from eval_runner.metrics.simple_interface import registry
similarity_metric = registry.get_all_metrics_flat().get('similarity')
if similarity_metric:
    result = similarity_metric.evaluate(test_item)
    print(f'✅ Similarity evaluation: {result.score}')
else:
    print('❌ Similarity metric not available')
"
```

## 🔍 Expected Behavior

### ✅ Success Indicators
- Configuration loads without API key errors
- Metrics return actual scores instead of "Model config validation failed"
- Logs show "Configured Azure OpenAI with managed identity"

### ❌ Failure Indicators  
- `PermissionDeniedError: Error code: 403` with "AuthenticationTypeDisabled"
- `Model config validation failed` errors
- `Unauthorized` or `Forbidden` errors

## 🚀 Benefits of Managed Identity

1. **🔒 Enhanced Security**: No hardcoded API keys in configuration
2. **🔄 Automatic Token Refresh**: Azure handles token lifecycle
3. **📊 Better Auditing**: Identity-based access tracking  
4. **🛡️ Principle of Least Privilege**: Granular RBAC permissions
5. **☁️ Cloud-Native**: Integrates seamlessly with Azure services

## 📝 Configuration Files

### `appsettings.json` Changes

You can remove the API key from configuration:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://evalplatform.cognitiveservices.azure.com/openai/deployments/gpt-4.1/chat/completions?api-version=2025-01-01-preview",
    // "ApiKey": "no-longer-needed",  // ✅ Can remove this line
    "DeploymentName": "gpt-4.1",
    "ApiVersion": "2025-01-01-preview"
  }
}
```

## 🔧 Troubleshooting

### Issue: "Model config validation failed"
**Solution**: Ensure RBAC permissions are correctly assigned to managed identity

### Issue: "PermissionDeniedError: AuthenticationTypeDisabled"  
**Solution**: This error confirms managed identity is being used (good!), just need proper permissions

### Issue: Cannot access Azure AI Foundry project
**Solution**: Add "Azure AI Developer" role to managed identity at project scope

## 🎯 Next Steps

1. ✅ Code updated for managed identity
2. 🔄 **Assign RBAC permissions** (required next step)
3. 🧪 Test evaluations with managed identity
4. 📊 Monitor authentication success in Azure logs
5. 🗑️ Remove API keys from configuration once confirmed working

The managed identity configuration is now ready - you just need to assign the proper RBAC permissions!