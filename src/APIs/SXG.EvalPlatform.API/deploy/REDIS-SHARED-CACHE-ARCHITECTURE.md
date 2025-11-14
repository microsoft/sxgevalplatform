# Redis Endpoint Change - Shared Cache Architecture

## Overview

The Redis cache architecture has been updated to use a **shared Redis cache** for Development and PPE environments, while Production maintains its own dedicated cache.

---

## ?? Changes Made

### **Previous Architecture**

| Environment | Redis Cache Name | Endpoint |
|-------------|-----------------|----------|
| Development | `evalplatformcachedev` | `evalplatformcachedev.redis.cache.windows.net:6380` |
| PPE | `evalplatformcacheppe` | `evalplatformcacheppe.redis.cache.windows.net:6380` |
| Production | `evalplatformcacheprod` | `evalplatformcacheprod.redis.cache.windows.net:6380` |

**Cost**: 3 separate Redis cache instances

---

### **New Architecture** ?

| Environment | Redis Cache Name | Endpoint | Instance Prefix |
|-------------|-----------------|----------|-----------------|
| Development | `sxgagenteval` (shared) | `sxgagenteval.redis.cache.windows.net:6380` | `evalplatformcachedev` |
| PPE | `sxgagenteval` (shared) | `sxgagenteval.redis.cache.windows.net:6380` | `evalplatformcacheppe` |
| Production | `evalplatformcacheprod` (dedicated) | `evalplatformcacheprod.redis.cache.windows.net:6380` | `evalplatformcacheprod` |

**Cost**: 2 Redis cache instances (shared + dedicated)  
**Savings**: ~33% reduction in Redis costs

---

## ? Benefits of Shared Cache

### **1. Cost Optimization** ??
- **Before**: 3 Redis cache instances
- **After**: 2 Redis cache instances (1 shared, 1 dedicated)
- **Savings**: Approximately 33% reduction in Redis cache costs

### **2. Logical Separation** ??
- Despite sharing the same Redis instance, Dev and PPE data are **logically separated** using different **instance name prefixes**
- Dev uses prefix: `evalplatformcachedev`
- PPE uses prefix: `evalplatformcacheppe`
- Cache keys are prefixed with instance name, preventing collisions

**Example**:
```
Dev cache key:   evalplatformcachedev:metrics-config-abc123
PPE cache key:   evalplatformcacheppe:metrics-config-abc123
```

### **3. Simplified Management** ???
- Fewer Redis instances to monitor
- Reduced firewall rules
- Fewer access key rotations

### **4. Resource Efficiency** ?
- Better resource utilization (shared instance more efficient than two underutilized instances)
- Shared connection pooling benefits

---

## ?? Files Updated

### **appsettings Files**

1. ? **appsettings.Development.json**
   - Redis Endpoint: `sxgagenteval.redis.cache.windows.net:6380`
   - Instance Name: `evalplatformcachedev`

2. ? **appsettings.PPE.json**
   - Redis Endpoint: `sxgagenteval.redis.cache.windows.net:6380`
   - Instance Name: `evalplatformcacheppe`

3. ? **appsettings.Production.json**
   - Redis Endpoint: `evalplatformcacheprod.redis.cache.windows.net:6380` (no change)
   - Instance Name: `evalplatformcacheprod`

### **Deployment Scripts**

1. ? **Deploy-To-Azure-Dev-NEW.ps1**
   - Updated `Cache__Redis__Endpoint` to `sxgagenteval.redis.cache.windows.net:6380`

2. ? **Deploy-To-Azure-PPE.ps1**
   - Updated `Cache__Redis__Endpoint` to `sxgagenteval.redis.cache.windows.net:6380`

3. ? **Deploy-To-Azure-Production.ps1**
   - No changes (still uses dedicated `evalplatformcacheprod`)

### **Verification & Helper Scripts**

1. ? **Verify-Cache-Configuration.ps1**
   - Updated to check for shared cache (`sxgagenteval`)
   - Validates correct endpoints for Dev and PPE

2. ? **Get-Redis-Access-Keys.ps1**
   - Updated to retrieve key for `sxgagenteval` once (shared between Dev and PPE)
   - Shows that Dev and PPE use the same Redis cache

3. ? **CACHE-REDIS-CONFIGURATION-SUMMARY.md**
   - Updated documentation to reflect shared cache architecture

---

## ?? Infrastructure Requirements

### **Redis Cache Instances Needed**

#### **1. Shared Cache for Dev + PPE**

**Name**: `sxgagenteval`

**Create if not exists**:
```powershell
az redis create `
    --name sxgagenteval `
    --resource-group rg-sxg-agent-evaluation-platform `
    --location eastus `
    --sku Standard `
    --vm-size C1 `
    --enable-non-ssl-port false
```

**Purpose**: Shared cache for Development and PPE environments

**Cost**: ~$75/month (Standard C1)

---

#### **2. Dedicated Cache for Production**

**Name**: `evalplatformcacheprod`

**Create if not exists**:
```powershell
az redis create `
    --name evalplatformcacheprod `
    --resource-group rg-sxg-agent-evaluation-platform `
    --location eastus `
    --sku Premium `
    --vm-size P1 `
 --enable-non-ssl-port false `
    --zones 1 2 3
```

**Purpose**: Dedicated cache for Production (high availability)

**Cost**: ~$250/month (Premium P1 with zone redundancy)

---

### **Old Caches to Delete** (Optional)

If the following caches exist, they can be **deleted** to save costs:

```powershell
# Delete old Dev cache (if exists)
az redis delete --name evalplatformcachedev --resource-group rg-sxg-agent-evaluation-platform

# Delete old PPE cache (if exists)
az redis delete --name evalplatformcacheppe --resource-group rg-sxg-agent-evaluation-platform
```

**Caution**: Ensure new shared cache (`sxgagenteval`) is working before deleting old caches.

---

## ?? Deployment Steps

### **Step 1: Verify Shared Redis Cache Exists**

```powershell
az redis show --name sxgagenteval --resource-group rg-sxg-agent-evaluation-platform
```

If it doesn't exist, create it (see Infrastructure Requirements above).

---

### **Step 2: Get Access Key (If Using Access Keys)**

```powershell
cd "D:\Github-Projects\sxgevalplatform\src\APIs\SXG.EvalPlatform.API\deploy"
.\Get-Redis-Access-Keys.ps1
```

This will retrieve the access key for `sxgagenteval` (used by both Dev and PPE).

---

### **Step 3: Update Deployment Scripts (If Using Access Keys)**

**For Development** (`Deploy-To-Azure-Dev-NEW.ps1`):
```powershell
"Cache__Redis__Endpoint=sxgagenteval.redis.cache.windows.net:6380,password=YOUR_KEY_HERE,ssl=True,abortConnect=False",
"Cache__Redis__UseManagedIdentity=false",
```

**For PPE** (`Deploy-To-Azure-PPE.ps1`):
```powershell
"Cache__Redis__Endpoint=sxgagenteval.redis.cache.windows.net:6380,password=YOUR_KEY_HERE,ssl=True,abortConnect=False",
"Cache__Redis__UseManagedIdentity=false",
```

**Note**: Both use the **same Redis cache** but different **instance name prefixes** for logical separation.

---

### **Step 4: Deploy**

```powershell
# Development
.\Deploy-To-Azure-Dev-NEW.ps1

# PPE
.\Deploy-To-Azure-PPE.ps1

# Production (no changes)
.\Deploy-To-Azure-Production.ps1
```

---

### **Step 5: Verify**

```powershell
# Run verification script
.\Verify-Cache-Configuration.ps1
```

**Expected Output**:
```
Development: sxgagenteval.redis.cache.windows.net:6380 ?
PPE: sxgagenteval.redis.cache.windows.net:6380 ?
Production: evalplatformcacheprod.redis.cache.windows.net:6380 ?
```

---

## ? Verification Checklist

After deployment:

- [ ] **Development** connects to `sxgagenteval` with prefix `evalplatformcachedev`
- [ ] **PPE** connects to `sxgagenteval` with prefix `evalplatformcacheppe`
- [ ] **Production** connects to `evalplatformcacheprod` with prefix `evalplatformcacheprod`
- [ ] No Redis connection exceptions in Application Insights
- [ ] Cache HIT/MISS logs show correct instance prefix
- [ ] Dev and PPE cache data are logically separated (different prefixes)

---

## ?? Testing Cache Separation

### **Test 1: Verify Different Prefixes**

```powershell
# Call Dev API
curl https://sxgevalapidev.azurewebsites.net/api/v1/eval/configurations/defaultconfiguration

# Call PPE API
curl https://sxgevalapippe.azurewebsites.net/api/v1/eval/configurations/defaultconfiguration

# Check logs - should show different cache keys
az webapp log tail --name sxgevalapidev --resource-group rg-sxg-agent-evaluation-platform | Select-String "cache"
az webapp log tail --name sxgevalapippe --resource-group rg-sxg-agent-evaluation-platform | Select-String "cache"
```

**Expected**:
```
Dev:  Redis cache key: evalplatformcachedev:metrics-config-default
PPE:  Redis cache key: evalplatformcacheppe:metrics-config-default
```

### **Test 2: Verify No Cross-Environment Cache Pollution**

1. Clear cache in Dev
2. Call API in Dev (cache MISS, then cache SET)
3. Call same API in PPE
4. **Expected**: Cache MISS in PPE (because prefix is different)

This confirms Dev and PPE caches are logically separated despite sharing the same Redis instance.

---

## ?? Cost Comparison

| Configuration | Redis Instances | Monthly Cost (Estimate) | Annual Cost |
|--------------|----------------|------------------------|-------------|
| **Old (3 separate)** | evalplatformcachedev (C1)<br>evalplatformcacheppe (C1)<br>evalplatformcacheprod (P1) | $75<br>$75<br>$250<br>**Total: $400/mo** | **$4,800/yr** |
| **New (1 shared + 1 dedicated)** | sxgagenteval (C1, shared)<br>evalplatformcacheprod (P1) | $75<br>$250<br>**Total: $325/mo** | **$3,900/yr** |
| **Savings** | | **$75/mo (19%)** | **$900/yr** |

---

## ?? Important Notes

### **1. Cache Key Collisions**

**Q**: Won't Dev and PPE cache keys collide since they share the same Redis instance?

**A**: No, because they use **different instance name prefixes**:
- Dev keys: `evalplatformcachedev:*`
- PPE keys: `evalplatformcacheppe:*`

Redis sees these as completely different keys.

### **2. Performance Impact**

**Q**: Will Dev and PPE impact each other's performance?

**A**: Minimal impact because:
- Both Dev and PPE are **non-production** environments with lower traffic
- Redis Standard C1 can handle **thousands of operations per second**
- Connection pooling is efficient

If performance becomes an issue, upgrade to C2 or higher.

### **3. Production Isolation**

**Q**: Why doesn't Production share the cache?

**A**: Production uses a **dedicated Premium P1** cache with:
- **Zone redundancy** (high availability)
- **Higher performance** (Premium tier)
- **Complete isolation** from non-production environments
- **Better SLA** (99.95% vs 99.9%)

---

## ?? Summary

### **What Changed**

| Item | Old Value | New Value |
|------|-----------|-----------|
| **Dev Redis Endpoint** | `evalplatformcachedev.redis.cache.windows.net:6380` | `sxgagenteval.redis.cache.windows.net:6380` |
| **PPE Redis Endpoint** | `evalplatformcacheppe.redis.cache.windows.net:6380` | `sxgagenteval.redis.cache.windows.net:6380` |
| **Prod Redis Endpoint** | `evalplatformcacheprod.redis.cache.windows.net:6380` | No change |

### **Files Updated**

- ? appsettings.Development.json
- ? appsettings.PPE.json
- ? Deploy-To-Azure-Dev-NEW.ps1
- ? Deploy-To-Azure-PPE.ps1
- ? Verify-Cache-Configuration.ps1
- ? Get-Redis-Access-Keys.ps1
- ? CACHE-REDIS-CONFIGURATION-SUMMARY.md

### **Action Required**

1. ? Ensure `sxgagenteval` Redis cache exists
2. ? Get access key (if using access key authentication)
3. ? Update deployment scripts with access key (if needed)
4. ? Deploy to Dev and PPE
5. ? Verify cache connectivity and logical separation

---

**The deployment scripts have been updated to reflect the shared Redis cache architecture!** ?
