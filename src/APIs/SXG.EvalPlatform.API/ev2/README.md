# EV2 Production Deployment Artifacts

This directory contains all artifacts required for Express V2 (EV2) based production deployment of the SXG Evaluation Platform API.

## Contents

### Pipeline & Configuration
- **azure-pipelines-production.yml** - Main Azure DevOps pipeline definition
- **ServiceModel.json** - EV2 service model defining Azure resources
- **RolloutSpec.json** - EV2 rollout specification with orchestration steps
- **version.txt** - Current version for EV2 deployment tracking

### Parameters
- **Parameters.EastUS2.json** - East US 2 region configuration
- **Parameters.WestUS2.json** - West US 2 region configuration
- **AppServiceDeployment.json** - ARM template for App Service deployment

### Scripts
- **Deploy-Regional-With-TrafficManager.ps1** - Main deployment script with Traffic Manager integration
- **Setup-TrafficManager.ps1** - One-time Traffic Manager configuration script

### Documentation
- **DEPLOYMENT-GUIDE.md** - Comprehensive deployment guide with procedures and troubleshooting
- **QUICK-START.md** - Quick reference for common deployment tasks

## Quick Start

1. **First Time Setup**
   ```powershell
   .\Setup-TrafficManager.ps1
   ```

2. **Deploy to Production**
   - Use Azure DevOps pipeline: `azure-pipelines-production.yml`
   - Or run manually:
   ```powershell
   .\Deploy-Regional-With-TrafficManager.ps1 -Region "EastUS2" -ResourceGroupName "EvalApiRg-UsEast2" ...
   ```

3. **Read the Docs**
   - Full guide: [DEPLOYMENT-GUIDE.md](DEPLOYMENT-GUIDE.md)
   - Quick reference: [QUICK-START.md](QUICK-START.md)

## Architecture

### Multi-Region Setup
```
                    ┌─────────────────────┐
                    │  Traffic Manager    │
                    │  (Performance)      │
                    └──────────┬──────────┘
                               │
                ┌──────────────┴──────────────┐
                │                             │
        ┌───────▼────────┐          ┌────────▼────────┐
        │   East US 2    │          │   West US 2     │
        │  (Primary)     │          │  (Secondary)    │
        └───────┬────────┘          └────────┬────────┘
                │                             │
                │    ┌─────────────────┐      │
                └────►  Shared Storage │◄─────┘
                     │  Service Bus    │
                     │  Redis Cache    │
                     └─────────────────┘
```

### Deployment Flow
1. **Build** → Compile, test, package
2. **East US 2** → Disable TM → Deploy → Health Check → Enable TM
3. **Baking** → 30-minute monitoring
4. **West US 2** → Disable TM → Deploy → Health Check → Enable TM
5. **Validation** → Final verification

## Resources

### Production Endpoints
- Traffic Manager: `sxgevalapiprod.trafficmanager.net`
- East US 2: `sxgevalapiproduseast2.azurewebsites.net`
- West US 2: `sxgevalapiproduswest2.azurewebsites.net`

### Resource Groups
- East US 2: `EvalApiRg-UsEast2`
- West US 2: `EvalApiRg-UsWest2`
- Common: `EvalCommonRg-useast2`

### Shared Resources
- Storage: `stevalplatformprod`
- Service Bus: `sxgevalframework-produseast2`
- Redis: `evalplatformcacheprod.redis.cache.windows.net`

## Key Features

✓ **Zero-downtime deployment** - One region always serving traffic  
✓ **Traffic Manager integration** - Automatic endpoint management  
✓ **Health validation** - Comprehensive checks before enabling  
✓ **Sequential rollout** - Safe deployment across regions  
✓ **Automated rollback** - Fail-safe mechanisms  
✓ **Baking period** - 30-minute monitoring between regions  

## Support

- **Team**: sxgevalvteam@microsoft.com
- **Service Tree**: [409ab2c3-dafd-4ee4-b158-b405c578bbcd](https://microsoftservicetree.com/services/409ab2c3-dafd-4ee4-b158-b405c578bbcd)

## Version

Current Version: **1.0.0**

---

Last Updated: December 17, 2025
