# Quick Start Guide - Production Deployment

## Pre-Flight Checklist

- [ ] Code merged to main
- [ ] Tests passing
- [ ] PPE validation complete
- [ ] Change approval obtained
- [ ] Team notified

## Quick Deploy

1. **Setup Traffic Manager** (One-time)
   ```powershell
   cd ev2
   .\Setup-TrafficManager.ps1
   ```

2. **Run Pipeline**
   - Azure DevOps → Pipelines → `SXG-EvalPlatform-API-Production`
   - Click "Run pipeline"
   - Approve each stage when prompted

3. **Monitor**
   - East US 2 deployment: ~15-20 min
   - Baking period: 30 min
   - West US 2 deployment: ~15-20 min
   - **Total time**: ~75-90 minutes

## Quick Health Check

```powershell
# Via Traffic Manager
Invoke-RestMethod "https://sxgevalapiprod.trafficmanager.net/api/v1/health"

# Direct endpoints
Invoke-RestMethod "https://sxgevalapiproduseast2.azurewebsites.net/api/v1/health/detailed"
Invoke-RestMethod "https://sxgevalapiproduswest2.azurewebsites.net/api/v1/health/detailed"
```

## Quick Rollback

### Disable problematic region
```powershell
az network traffic-manager endpoint update `
  --name "eastus2-endpoint" `
  --profile-name "sxgevalapiprod" `
  --resource-group "EvalCommonRg-useast2" `
  --type azureEndpoints `
  --endpoint-status Disabled
```

### Manual deployment of previous version
```powershell
cd ev2
.\Deploy-Regional-With-TrafficManager.ps1 `
  -Region "EastUS2" `
  -ResourceGroupName "EvalApiRg-UsEast2" `
  -AppServiceName "sxgevalapiproduseast2" `
  -StorageAccountName "stevalplatformprod" `
  -TrafficManagerProfileName "sxgevalapiprod" `
  -TrafficManagerResourceGroup "EvalCommonRg-useast2" `
  -TrafficManagerEndpointName "eastus2-endpoint"
```

## Key URLs

- **Production**: https://sxgevalapiprod.trafficmanager.net
- **East US 2**: https://sxgevalapiproduseast2.azurewebsites.net
- **West US 2**: https://sxgevalapiproduswest2.azurewebsites.net
- **Swagger**: https://sxgevalapiprod.trafficmanager.net/swagger

## Emergency Contacts

- **Team**: sxgevalvteam@microsoft.com
- **Service Tree**: https://microsoftservicetree.com/services/409ab2c3-dafd-4ee4-b158-b405c578bbcd

## Common Issues

| Issue | Quick Fix |
|-------|-----------|
| Endpoint offline | Check health endpoint returns 200 OK |
| Settings wrong | Verify `ASPNETCORE_ENVIRONMENT=Production` |
| Deployment timeout | Increase timeout in pipeline YAML |
| Health check fails | Check Application Insights for errors |

---

For detailed instructions, see [DEPLOYMENT-GUIDE.md](DEPLOYMENT-GUIDE.md)
