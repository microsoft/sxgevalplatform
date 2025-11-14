# Redis Cache with Azure AD (Managed Identity) - Complete Setup Guide

## Overview

This guide provides step-by-step instructions for configuring Azure Redis Cache to use **Azure AD (Managed Identity) authentication** instead of access keys.

---

## ? Prerequisites

1. **Azure Redis Cache** (Standard or Premium tier)
2. **App Service with Managed Identity enabled**
3. **Azure CLI** installed and logged in
4. **Appropriate Azure permissions** to assign roles

---

## ?? Step-by-Step Setup

### **Step 1: Enable Azure AD Authentication on Redis Cache**

Azure Cache for Redis must have **Azure Entra ID (Azure AD) authentication enabled**.

#### **Option A: Via Azure Portal**

1. Go to Azure Portal ? Your Redis Cache (`sxgagenteval`)
2. Navigate to **Settings** ? **Authentication**
3. Select **Azure AD authentication**
4. Click **Enable Azure AD authentication**
5. Click **Save**

#### **Option B: Via Azure CLI**

```powershell
# Enable Azure AD authentication on Redis cache
az redis update `
    --name sxgagenteval `
 --resource-group rg-sxg-agent-evaluation-platform `
    --enable-non-ssl-port false `
    --aad-enabled true
```

**Verify**:
```powershell
az redis show --name sxgagenteval --resource-group rg-sxg-agent-evaluation-platform --query "enableNonSslPort,aadEnabled"
```

**Expected output**:
```json
{
  "enableNonSslPort": false,
  "aadEnabled": true
}
```

---

### **Step 2: Enable Managed Identity on App Service**

#### **Check if Managed Identity is already enabled**:

```powershell
az webapp identity show `
    --name sxgevalapippe `
    --resource-group rg-sxg-agent-evaluation-platform
```

#### **Enable System-Assigned Managed Identity** (if not enabled):

```powershell
az webapp identity assign `
    --name sxgevalapippe `
    --resource-group rg-sxg-agent-evaluation-platform
```

#### **Get the Managed Identity Principal ID**:

```powershell
$principalId = az webapp identity show `
    --name sxgevalapippe `
    --resource-group rg-sxg-agent-evaluation-platform `
    --query principalId -o tsv

Write-Host "Managed Identity Principal ID: $principalId"
```

**Copy this Principal ID** - you'll need it for Step 3.

---

### **Step 3: Grant Managed Identity Access to Redis Cache**

The Managed Identity needs the **"Redis Cache Contributor"** role on the Redis cache.

#### **Get Redis Cache Resource ID**:

```powershell
$redisId = az redis show `
 --name sxgagenteval `
    --resource-group rg-sxg-agent-evaluation-platform `
    --query id -o tsv

Write-Host "Redis Cache Resource ID: $redisId"
```

#### **Assign "Redis Cache Contributor" Role**:

```powershell
az role assignment create `
    --assignee $principalId `
    --role "Redis Cache Contributor" `
    --scope $redisId
```

**Verify role assignment**:

```powershell
az role assignment list `
    --assignee $principalId `
    --scope $redisId `
    --query "[].{Role:roleDefinitionName, Scope:scope}" -o table
```

**Expected output**:
```
Role   Scope
--------------------------  --------------------------------------------------------
Redis Cache Contributor     /subscriptions/.../providers/Microsoft.Cache/Redis/sxgagenteval
```

---

### **Step 4: Configure Firewall Rules (If Needed)**

If your Redis cache has firewall rules enabled, you need to allow the App Service to connect.

#### **Option A: Allow Azure Services**

```powershell
# Allow all Azure services to access Redis
az redis firewall-rules create `
    --name AllowAzureServices `
    --resource-group rg-sxg-agent-evaluation-platform `
    --redis-name sxgagenteval `
    --start-ip 0.0.0.0 `
    --end-ip 0.0.0.0
```

#### **Option B: Add Specific App Service Outbound IPs**

```powershell
# Get App Service outbound IPs
$outboundIps = az webapp show `
    --name sxgevalapippe `
    --resource-group rg-sxg-agent-evaluation-platform `
    --query "outboundIpAddresses" -o tsv

# Split and add each IP
$ipArray = $outboundIps -split ","
$counter = 1
foreach ($ip in $ipArray) {
    $ip = $ip.Trim()
    az redis firewall-rules create `
        --name "AppService-IP-$counter" `
        --resource-group rg-sxg-agent-evaluation-platform `
        --redis-name sxgagenteval `
        --start-ip $ip `
--end-ip $ip
    $counter++
}
```

---

### **Step 5: Repeat for All Environments**

Repeat Steps 2-4 for each environment:

#### **Development**:

```powershell
# Enable Managed Identity
az webapp identity assign --name sxgevalapidev --resource-group rg-sxg-agent-evaluation-platform

# Get Principal ID
$devPrincipalId = az webapp identity show --name sxgevalapidev --resource-group rg-sxg-agent-evaluation-platform --query principalId -o tsv

# Assign role
az role assignment create --assignee $devPrincipalId --role "Redis Cache Contributor" --scope $redisId
```

#### **PPE**:

```powershell
# Enable Managed Identity
az webapp identity assign --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform

# Get Principal ID
$ppePrincipalId = az webapp identity show --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform --query principalId -o tsv

# Assign role
az role assignment create --assignee $ppePrincipalId --role "Redis Cache Contributor" --scope $redisId
```

#### **Production** (separate Redis cache):

```powershell
# Get Production Redis ID
$prodRedisId = az redis show --name evalplatformcacheprod --resource-group rg-sxg-agent-evaluation-platform --query id -o tsv

# Enable Managed Identity
az webapp identity assign --name sxgevalapiprod --resource-group rg-sxg-agent-evaluation-platform

# Get Principal ID
$prodPrincipalId = az webapp identity show --name sxgevalapiprod --resource-group rg-sxg-agent-evaluation-platform --query principalId -o tsv

# Assign role
az role assignment create --assignee $prodPrincipalId --role "Redis Cache Contributor" --scope $prodRedisId
```

---

### **Step 6: Deploy Application**

Now deploy the application with the updated code that uses Managed Identity:

```powershell
cd "D:\Github-Projects\sxgevalplatform\src\APIs\SXG.EvalPlatform.API\deploy"

# Deploy to PPE
.\Deploy-To-Azure-PPE.ps1

# Deploy to Development
.\Deploy-To-Azure-Dev-NEW.ps1

# Deploy to Production
.\Deploy-To-Azure-Production.ps1
```

---

### **Step 7: Verify Connection**

#### **Check Application Insights for Errors**:

```kql
exceptions
| where cloud_RoleName contains "SXG-EvalPlatform-API"
| where timestamp > ago(30m)
| where outerMessage contains "Redis"
| project timestamp, cloud_RoleName, outerMessage, details
```

**Expected**: No Redis connection exceptions

#### **Check Application Logs for Success**:

```powershell
az webapp log tail --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform
```

**Expected logs**:
```
Successfully connected to Redis cache using Azure AD (Managed Identity) - Host: sxgagenteval.redis.cache.windows.net:6380
Redis connection verified. Ping latency: 5.2ms
Redis cache HIT for key: evalplatformcacheppe:metrics-config-default
```

#### **Check Application Insights for Success Logs**:

```kql
traces
| where cloud_RoleName contains "SXG-EvalPlatform-API"
| where timestamp > ago(30m)
| where message contains "Redis"
| where message contains "Managed Identity"
| project timestamp, cloud_RoleName, message
| order by timestamp desc
```

**Expected**:
```
Successfully connected to Redis cache using Azure AD (Managed Identity)
Using credential type: DefaultAzureCredential for environment: PPE
Redis connection verified. Ping latency: 5.2ms
```

---

## ?? Troubleshooting

### **Issue 1: "NOAUTH Authentication required"**

**Cause**: Azure AD authentication is not enabled on Redis cache

**Solution**:
```powershell
az redis update `
    --name sxgagenteval `
    --resource-group rg-sxg-agent-evaluation-platform `
    --aad-enabled true
```

---

### **Issue 2: "Authorization failed" or "Access denied"**

**Cause**: Managed Identity doesn't have the "Redis Cache Contributor" role

**Solution**: Verify and re-assign role:
```powershell
# Get Principal ID
$principalId = az webapp identity show --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform --query principalId -o tsv

# Get Redis ID
$redisId = az redis show --name sxgagenteval --resource-group rg-sxg-agent-evaluation-platform --query id -o tsv

# Assign role
az role assignment create --assignee $principalId --role "Redis Cache Contributor" --scope $redisId
```

**Verify**:
```powershell
az role assignment list --assignee $principalId --scope $redisId -o table
```

---

### **Issue 3: Connection timeout or "No connection available"**

**Cause**: Firewall blocking the connection

**Solution**: Add App Service IPs to Redis firewall rules (see Step 4)

---

### **Issue 4: "Credential not found" in local development**

**Cause**: Local development uses `AzureCliCredential`, but Azure CLI not logged in

**Solution**:
```powershell
az login
az account set --subscription "<your-subscription>"
```

For local development, you can also use:
- Visual Studio credential
- Environment variables with service principal
- `azd auth login` if using Azure Developer CLI

---

## ?? Verify Role Assignments

### **Check All App Services**:

```powershell
# Development
az role assignment list `
    --assignee $(az webapp identity show --name sxgevalapidev --resource-group rg-sxg-agent-evaluation-platform --query principalId -o tsv) `
    --query "[?contains(scope, 'Redis')].{App:'sxgevalapidev', Role:roleDefinitionName, Redis:scope}" -o table

# PPE
az role assignment list `
    --assignee $(az webapp identity show --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform --query principalId -o tsv) `
    --query "[?contains(scope, 'Redis')].{App:'sxgevalapippe', Role:roleDefinitionName, Redis:scope}" -o table

# Production
az role assignment list `
    --assignee $(az webapp identity show --name sxgevalapiprod --resource-group rg-sxg-agent-evaluation-platform --query principalId -o tsv) `
    --query "[?contains(scope, 'Redis')].{App:'sxgevalapiprod', Role:roleDefinitionName, Redis:scope}" -o table
```

---

## ?? Configuration Summary

### **What Changed in Code**

**File**: `Sxg.EvalPlatform.API.Storage/Extensions/CacheServiceExtensions.cs`

**Key Changes**:
1. ? Uses `ConfigureForAzureWithTokenCredentialAsync()` for Azure AD authentication
2. ? Gets credential from `CommonUtils.GetTokenCredential(environment)`
3. ? No fallback to Memory Cache - Redis must work
4. ? Comprehensive logging for troubleshooting
5. ? Connection verification with ping test

### **Deployment Scripts Updated**

**All scripts now use**:
```powershell
"Cache__Redis__Endpoint=sxgagenteval.redis.cache.windows.net:6380",
"Cache__Redis__UseManagedIdentity=true",
```

**No access keys in configuration** - pure Managed Identity approach.

---

## ? Success Criteria

After setup, verify:

- [ ] Azure AD authentication enabled on Redis cache (`aadEnabled: true`)
- [ ] Managed Identity enabled on all App Services
- [ ] "Redis Cache Contributor" role assigned to all Managed Identities
- [ ] Firewall rules allow App Service IPs (if needed)
- [ ] Application logs show successful Redis connection with Managed Identity
- [ ] No `NOAUTH` or authentication errors in Application Insights
- [ ] Cache operations (GET/SET) work correctly
- [ ] Ping latency is reasonable (<100ms)

---

## ?? Benefits of Managed Identity

1. **Zero Secrets** ??
   - No access keys in configuration
   - No secrets to rotate
   - No risk of leaked credentials

2. **Zero-Trust Architecture** ???
   - Identity-based access control
   - Centralized access management
   - Audit trail in Azure AD

3. **Automatic Key Rotation** ??
   - Azure manages token lifecycle
   - No manual rotation needed
   - Seamless renewal

4. **Simplified Operations** ??
   - No key management
   - Role-based access control (RBAC)
   - Easy to revoke access

---

## ?? Quick Command Reference

```powershell
# Enable Azure AD on Redis
az redis update --name sxgagenteval --resource-group rg-sxg-agent-evaluation-platform --aad-enabled true

# Enable Managed Identity on App Service
az webapp identity assign --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform

# Get Principal ID
$principalId = az webapp identity show --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform --query principalId -o tsv

# Get Redis Resource ID
$redisId = az redis show --name sxgagenteval --resource-group rg-sxg-agent-evaluation-platform --query id -o tsv

# Assign Role
az role assignment create --assignee $principalId --role "Redis Cache Contributor" --scope $redisId

# Verify Role
az role assignment list --assignee $principalId --scope $redisId -o table

# Deploy
cd "D:\Github-Projects\sxgevalplatform\src\APIs\SXG.EvalPlatform.API\deploy"
.\Deploy-To-Azure-PPE.ps1
```

---

**Your Redis Cache is now configured with Azure AD (Managed Identity) authentication!** ?

**No more access keys, no more NOAUTH errors, pure zero-trust architecture.** ??
