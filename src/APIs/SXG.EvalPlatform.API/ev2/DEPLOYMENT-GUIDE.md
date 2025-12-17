# SXG Evaluation Platform API - EV2 Production Deployment Guide

## Overview

This guide provides comprehensive instructions for deploying the SXG Evaluation Platform API to Production using Express V2 (EV2) with zero-downtime deployment strategy through Azure Traffic Manager.

## Architecture

### Regions
- **East US 2** (Primary): `sxgevalapiproduseast2.azurewebsites.net`
- **West US 2** (Secondary): `sxgevalapiproduswest2.azurewebsites.net`

### Shared Resources
- **Storage Account**: `stevalplatformprod` (East US 2)
- **Service Bus**: `sxgevalframework-produseast2` (East US 2)
- **Redis Cache**: `evalplatformcacheprod.redis.cache.windows.net`
- **Traffic Manager**: `sxgevalapiprod.trafficmanager.net`

### Deployment Strategy
1. **Sequential Deployment** - One region at a time
2. **Traffic Manager Integration** - Remove endpoint before deployment
3. **Health Validation** - Comprehensive checks before enabling endpoint
4. **Baking Period** - 30-minute monitoring between regions
5. **Zero Downtime** - One region always serves traffic

## Prerequisites

### 1. Azure Resources Setup

Ensure the following resources exist:

#### East US 2
- Resource Group: `EvalApiRg-UsEast2`
- App Service: `sxgevalapiproduseast2`
- App Service Plan: Premium or higher tier

#### West US 2
- Resource Group: `EvalApiRg-UsWest2`
- App Service: `sxgevalapiproduswest2`
- App Service Plan: Premium or higher tier

#### Common (East US 2)
- Resource Group: `EvalCommonRg-useast2`
- Storage Account: `stevalplatformprod`
- Service Bus Namespace: `sxgevalframework-produseast2`
- Traffic Manager Profile: `sxgevalapiprod`

### 2. Azure DevOps Setup

#### Service Connection
Create Azure Resource Manager service connection:
- Name: `OneVoice-AdminPortal-Dev`
- Subscription: Your Azure subscription
- Grant access to all pipelines

#### Environments
Create deployment environments with approvals:
```
Production-EastUS2
  - Approvers: Team leads
  - Checks: Manual approval + business hours gate

Production-WestUS2
  - Approvers: Team leads
  - Checks: Manual approval + business hours gate
```

### 3. Traffic Manager Configuration

Run the setup script to configure Traffic Manager:

```powershell
cd ev2
.\Setup-TrafficManager.ps1
```

This creates:
- Traffic Manager profile with Performance routing
- Health probe: `/api/v1/health` (HTTPS, port 443)
- Two endpoints: `eastus2-endpoint` and `westus2-endpoint`
- 30-second TTL for fast failover

Verify setup:
```powershell
az network traffic-manager endpoint list `
  --profile-name sxgevalapiprod `
  --resource-group EvalCommonRg-useast2 `
  --type azureEndpoints `
  --query '[].{name:name, status:endpointStatus, monitor:endpointMonitorStatus}' `
  -o table
```

## Pipeline Structure

### File Organization

```
.
├── azure-pipelines-production.yml    # Main pipeline definition
├── ev2/
│   ├── ServiceModel.json             # EV2 service model
│   ├── RolloutSpec.json              # EV2 rollout specification
│   ├── AppServiceDeployment.json     # ARM template stub
│   ├── Parameters.EastUS2.json       # East US 2 parameters
│   ├── Parameters.WestUS2.json       # West US 2 parameters
│   ├── Deploy-Regional-With-TrafficManager.ps1  # Deployment script
│   └── Setup-TrafficManager.ps1      # TM configuration script
├── appsettings.json                  # Base configuration
└── appsettings.Production.json       # Production overrides
```

### Pipeline Stages

1. **Build**
   - Restore NuGet packages
   - Build solution (.NET 8.0)
   - Run unit tests
   - Publish application
   - Create deployment package
   - Publish artifacts

2. **Deploy East US 2**
   - Disable Traffic Manager endpoint
   - Stop App Service
   - Deploy app settings
   - Build and publish code
   - Deploy to App Service
   - Start App Service
   - Health checks (5 minutes)
   - Enable Traffic Manager endpoint
   - Verify via Traffic Manager

3. **Baking Period**
   - 30-minute wait
   - Monitor East US 2 in production

4. **Deploy West US 2**
   - Same steps as East US 2
   - Sequential execution

5. **Post-Deployment Validation**
   - Validate Traffic Manager configuration
   - Check both endpoint health
   - Final smoke tests

## Deployment Process

### Step 1: Pre-Deployment Checklist

- [ ] All code changes merged to main branch
- [ ] Code review completed
- [ ] Unit tests passing
- [ ] Integration tests validated in PPE
- [ ] Change approval obtained
- [ ] Team notified of deployment window
- [ ] Monitoring dashboard open

### Step 2: Trigger Pipeline

1. Navigate to Azure DevOps pipelines
2. Select `SXG-EvalPlatform-API-Production`
3. Click "Run pipeline"
4. Review parameters and confirm

### Step 3: Monitor Build Stage

Monitor build output for:
- ✓ NuGet restore success
- ✓ Build compilation success
- ✓ Unit test results
- ✓ Artifact publication

### Step 4: Approve East US 2 Deployment

1. Pipeline will pause at `Production-EastUS2` environment
2. Review build artifacts and test results
3. Approve deployment when ready

### Step 5: Monitor East US 2 Deployment

Watch for:
- Traffic Manager endpoint disabled
- App Service stopped
- App settings deployed
- Application deployed
- Health checks passing (5 attempts max)
- Traffic Manager endpoint re-enabled

**Expected Duration**: 15-20 minutes

### Step 6: Baking Period

- Pipeline automatically waits 30 minutes
- Monitor East US 2 metrics:
  - Response times
  - Error rates
  - Health endpoint status
  - Traffic Manager health
  - Application Insights logs

**Roll back if**: Error rate > 1% or critical issues detected

### Step 7: Approve West US 2 Deployment

1. Review East US 2 metrics
2. Confirm no issues during baking
3. Approve West US 2 deployment

### Step 8: Monitor West US 2 Deployment

Same monitoring as East US 2:
- Deployment steps complete
- Health checks passing
- Traffic Manager endpoint online

### Step 9: Post-Deployment Validation

Pipeline automatically validates:
- Both Traffic Manager endpoints online
- Health checks via Traffic Manager URL
- Both regions responding correctly

### Step 10: Final Verification

Manually verify:

```powershell
# Check Traffic Manager
$tmUrl = "https://sxgevalapiprod.trafficmanager.net"
Invoke-RestMethod "$tmUrl/api/v1/health"

# Check East US 2 directly
$eastUrl = "https://sxgevalapiproduseast2.azurewebsites.net"
Invoke-RestMethod "$eastUrl/api/v1/health/detailed"

# Check West US 2 directly
$westUrl = "https://sxgevalapiproduswest2.azurewebsites.net"
Invoke-RestMethod "$westUrl/api/v1/health/detailed"
```

## Rollback Procedures

### During East US 2 Deployment

If deployment fails or health checks fail:

1. Pipeline automatically enables Traffic Manager endpoint
2. East US 2 serves previous version
3. No user impact (West US 2 continues serving)

### After East US 2 Success, Before West US 2

If issues found during baking:

```powershell
# Rollback East US 2
cd deploy
.\Deploy-To-Azure-Dev-AUTOMATED.ps1 `
  -ResourceGroupName "EvalApiRg-UsEast2" `
  -AppName "sxgevalapiproduseast2" `
  -StorageAccountName "stevalplatformprod"
```

### After Both Regions Deployed

If critical issues found:

1. Disable problematic endpoint:
```powershell
az network traffic-manager endpoint update `
  --name "eastus2-endpoint" `
  --profile-name "sxgevalapiprod" `
  --resource-group "EvalCommonRg-useast2" `
  --type azureEndpoints `
  --endpoint-status Disabled
```

2. Deploy previous version:
```powershell
# Run deployment script with previous build
.\Deploy-Regional-With-TrafficManager.ps1 `
  -Region "EastUS2" `
  -ResourceGroupName "EvalApiRg-UsEast2" `
  -AppServiceName "sxgevalapiproduseast2" `
  -StorageAccountName "stevalplatformprod" `
  -TrafficManagerProfileName "sxgevalapiprod" `
  -TrafficManagerResourceGroup "EvalCommonRg-useast2" `
  -TrafficManagerEndpointName "eastus2-endpoint"
```

3. Re-enable endpoint after validation

## Monitoring

### Application Insights

Monitor these queries:

```kusto
// Error rate
requests
| where timestamp > ago(1h)
| summarize 
    Total = count(),
    Failures = countif(success == false),
    ErrorRate = round(100.0 * countif(success == false) / count(), 2)
| project ErrorRate, Failures, Total

// Response time
requests
| where timestamp > ago(1h)
| summarize 
    p50 = percentile(duration, 50),
    p95 = percentile(duration, 95),
    p99 = percentile(duration, 99)
| project p50, p95, p99

// Exceptions
exceptions
| where timestamp > ago(1h)
| summarize count() by type, outerMessage
| order by count_ desc
```

### Traffic Manager Health

```powershell
# Monitor endpoint health
az network traffic-manager endpoint list `
  --profile-name sxgevalapiprod `
  --resource-group EvalCommonRg-useast2 `
  --type azureEndpoints `
  --query '[].{Name:name, Status:endpointStatus, Monitor:endpointMonitorStatus, Priority:priority}' `
  -o table
```

### App Service Logs

```powershell
# East US 2 logs
az webapp log tail `
  --name sxgevalapiproduseast2 `
  --resource-group EvalApiRg-UsEast2

# West US 2 logs
az webapp log tail `
  --name sxgevalapiproduswest2 `
  --resource-group EvalApiRg-UsWest2
```

## Troubleshooting

### Health Check Failures

**Symptom**: Health endpoint returns unhealthy

**Investigation**:
1. Check specific dependency failure in detailed health response
2. Review Application Insights for errors
3. Check app service logs
4. Verify app settings configuration

**Common Issues**:
- Redis cache connection timeout → Check managed identity
- Storage account access denied → Verify RBAC roles
- Service Bus connection failed → Check namespace access
- DataVerse API unreachable → Verify network/firewall

### Traffic Manager Endpoint Offline

**Symptom**: Endpoint shows "Degraded" or "Offline"

**Investigation**:
```powershell
az network traffic-manager endpoint show `
  --name eastus2-endpoint `
  --profile-name sxgevalapiprod `
  --resource-group EvalCommonRg-useast2 `
  --type azureEndpoints
```

**Solutions**:
- Verify health probe path responds with 200 OK
- Check App Service is running
- Verify no firewall blocking Traffic Manager probes
- Review health endpoint implementation

### Deployment Timeout

**Symptom**: Deployment step times out

**Solutions**:
1. Increase timeout in pipeline (default: 600 seconds)
2. Check App Service deployment logs
3. Verify package size (should be < 500 MB)
4. Check App Service plan SKU (should be P1V2 or higher)

### App Settings Not Applied

**Symptom**: Application using wrong configuration

**Investigation**:
```powershell
az webapp config appsettings list `
  --name sxgevalapiproduseast2 `
  --resource-group EvalApiRg-UsEast2 `
  --query "[?name=='ASPNETCORE_ENVIRONMENT']"
```

**Solutions**:
- Verify appsettings.Production.json exists
- Check deployment script app settings merge
- Review App Service configuration in portal
- Restart App Service to reload configuration

## Best Practices

1. **Always deploy during low-traffic windows**
   - Recommended: Tuesday-Thursday, 10 AM - 2 PM PST
   - Avoid: Fridays, weekends, holidays, month-end

2. **Monitor actively during deployment**
   - Watch Application Insights live metrics
   - Keep Traffic Manager health view open
   - Have rollback plan ready

3. **Validate thoroughly in PPE first**
   - Run same deployment process in PPE
   - Verify all features work
   - Load test if major changes

4. **Communicate proactively**
   - Notify team before deployment
   - Update status during deployment
   - Send summary after completion

5. **Document issues and resolutions**
   - Update troubleshooting section
   - Share learnings with team
   - Improve automation based on issues

## Support Contacts

- **Team Email**: sxgevalvteam@microsoft.com
- **Service Tree**: https://microsoftservicetree.com/services/409ab2c3-dafd-4ee4-b158-b405c578bbcd

## Related Documentation

- [Azure Traffic Manager Documentation](https://docs.microsoft.com/azure/traffic-manager/)
- [Express V2 (EV2) Documentation](https://ev2docs.azure.net/)
- [App Service Deployment Best Practices](https://docs.microsoft.com/azure/app-service/deploy-best-practices)
- [Zero-Downtime Deployment Strategies](https://docs.microsoft.com/azure/architecture/guide/deployment/zero-downtime)

---

**Version**: 1.0.0  
**Last Updated**: December 17, 2025  
**Owner**: SXG Evaluation Platform Team
