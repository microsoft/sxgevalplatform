# API Endpoints Affected by Recent Changes

## Overview

The recent changes primarily affect the **Evaluation Run** related endpoints and **Evaluation Results** endpoints. Here's a comprehensive breakdown of which endpoints are impacted and how.

---

## 🔄 **Directly Affected Endpoints**

### **EvalRunController** - `/api/v1/eval/runs`

#### ✅ **POST /api/v1/eval/runs** - Create Evaluation Run
- **Impact**: ✅ **Response Schema Changed**
- **What Changed**: 
  - Removed `blobFilePath` and `containerName` from response
  - Container names now automatically lowercase
  - Blob path structure changed to folder-based (`evalresults/{evalRunId}/`)

**Before Response**:
```json
{
  "evalRunId": "12345-guid",
  "agentId": "A001",
  "status": "Queued",
  "blobFilePath": "evaluations/12345-guid.json",  // ❌ Removed
  "containerName": "A001"                         // ❌ Removed
}
```

**After Response**:
```json
{
  "evalRunId": "12345-guid", 
  "agentId": "A001",
  "status": "Queued"
  // ✅ Internal blob details no longer exposed
}
```

#### ✅ **GET /api/v1/eval/runs/{evalRunId}** - Get Evaluation Run
- **Impact**: ✅ **Response Schema Changed**
- **What Changed**: Same as POST - removed internal blob storage fields

#### ✅ **PUT /api/v1/eval/runs/{evalRunId}** - Update Evaluation Run Status  
- **Impact**: ✅ **Request Schema Simplified** (from previous changes)
- **What Changed**: Request only requires `status` field, no longer needs `evalRunId` or `agentId`

---

### **EvalResultController** - `/api/v1/eval/results`

#### ✅ **POST /api/v1/eval/results** - Save Evaluation Results
- **Impact**: ✅ **Internal Behavior Changed**
- **What Changed**:
  - Now uses folder structure for blob storage (`evalresults/{evalRunId}/{fileName}`)
  - Container names automatically lowercase
  - Better support for multiple result files per evaluation

#### ✅ **GET /api/v1/eval/results/{evalRunId}** - Get Evaluation Results
- **Impact**: ✅ **Internal Behavior Changed**  
- **What Changed**:
  - Now looks for results in folder structure
  - Searches for `results.json` as main file in folder
  - Container names handled with lowercase

#### ✅ **GET /api/v1/eval/results/agent/{agentId}** - Get Eval Runs by Agent
- **Impact**: ✅ **Response Schema Changed**
- **What Changed**: Returns `EvalRunDto` objects without internal blob fields

#### ✅ **GET /api/v1/eval/results/agent/{agentId}/daterange** - Get Results by Date Range  
- **Impact**: ✅ **Internal Behavior Changed**
- **What Changed**: Uses updated blob storage paths and structure

---

## 🔧 **Indirectly Affected Endpoints**

### **EvalConfigController** - `/api/v1/eval/configurations`
- **Impact**: ⚠️ **No Direct Impact** 
- **Notes**: These endpoints are not directly affected but may interact with evaluation runs

### **EvalDatasetController** - `/api/v1/eval/datasets`
- **Impact**: ⚠️ **No Direct Impact**
- **Notes**: Dataset endpoints are not directly affected

### **HealthController** - `/api/v1/health`
- **Impact**: ❌ **No Impact**
- **Notes**: Health endpoints remain unchanged

---

## 📊 **Impact Summary**

### **Breaking Changes** ❌
- **None** - All changes are backward compatible from API contract perspective
- Removed fields were internal implementation details not part of public API contract

### **Response Schema Changes** ✅
**Affected Response Models**:
- `EvalRunDto` - Removed `blobFilePath` and `containerName` properties

### **Internal Behavior Changes** 🔧
**Blob Storage Changes**:
- Container names: `A001` → `a001` (automatic lowercase)
- File paths: `evaluations/{evalRunId}.json` → `evalresults/{evalRunId}/`
- Support for multiple files per evaluation run

---

## 🔄 **Migration Impact**

### **For API Consumers**
✅ **No Action Required**:
- Request formats remain the same
- Response contracts are cleaner (removed internal fields)
- All existing integrations will continue to work

### **For Internal Services**
⚠️ **Internal Changes**:
- Services now use `GetEvalRunEntityByIdAsync()` for internal blob storage details
- Blob storage paths changed to folder structure
- Container names automatically normalized to lowercase

---

## 📋 **Endpoint Mapping**

| Endpoint | Method | Route | Impact Level | Change Type |
|----------|--------|-------|--------------|-------------|
| Create Eval Run | POST | `/api/v1/eval/runs` | ✅ High | Response Schema |
| Get Eval Run | GET | `/api/v1/eval/runs/{id}` | ✅ High | Response Schema |  
| Update Eval Run | PUT | `/api/v1/eval/runs/{id}` | ✅ Medium | Internal Logic |
| Save Results | POST | `/api/v1/eval/results` | ✅ Medium | Internal Behavior |
| Get Results | GET | `/api/v1/eval/results/{id}` | ✅ Medium | Internal Behavior |
| Get Runs by Agent | GET | `/api/v1/eval/results/agent/{id}` | ✅ Medium | Response Schema |
| Get Results by Date | GET | `/api/v1/eval/results/agent/{id}/daterange` | ✅ Low | Internal Behavior |

---

## 🧪 **Testing Recommendations**

### **API Contract Testing**
1. Verify `EvalRunDto` responses no longer contain `blobFilePath` or `containerName`
2. Confirm all evaluation run CRUD operations work as expected
3. Test blob storage operations with new folder structure

### **Integration Testing**  
1. Test file upload/download with new blob paths
2. Verify container name case handling
3. Test multiple file support per evaluation run

### **Regression Testing**
1. Ensure existing client integrations still work
2. Verify backward compatibility for older evaluation runs
3. Test error handling remains consistent

---

## 🎯 **Key Benefits**

1. **Security**: Internal storage details no longer exposed
2. **Scalability**: Folder structure supports multiple files per evaluation  
3. **Compliance**: Container names properly formatted for Azure Blob Storage
4. **Maintainability**: Clear separation between public API and internal implementation

The changes improve the API design while maintaining backward compatibility for all consumer-facing contracts.