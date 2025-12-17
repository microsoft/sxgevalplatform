# Custom Domain Setup Script for Azure App Service
# Registers domain through Azure and configures with Traffic Manager

param(
    [Parameter(Mandatory=$false)]
    [string]$DomainName = "sxgevalplatformapi.com",
    
    [Parameter(Mandatory=$false)]
    [string]$EastUS2AppService = "sxgevalapiproduseast2",
    
    [Parameter(Mandatory=$false)]
    [string]$EastUS2ResourceGroup = "EvalApiRg-UsEast2",
    
    [Parameter(Mandatory=$false)]
    [string]$WestUS2AppService = "sxgevalapiproduswest2",
    
    [Parameter(Mandatory=$false)]
    [string]$WestUS2ResourceGroup = "EvalApiRg-UsWest2",
    
    [Parameter(Mandatory=$false)]
    [string]$TrafficManagerProfileName = "sxgevalapiprod",
    
    [Parameter(Mandatory=$false)]
    [string]$TrafficManagerResourceGroup = "EvalCommonRg-useast2",
    
    [Parameter(Mandatory=$false)]
    [string]$DomainResourceGroup = "EvalCommonRg-useast2",
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Custom Domain Setup" -ForegroundColor Cyan
Write-Host "SXG Evaluation Platform API" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Domain: $DomainName" -ForegroundColor Green
Write-Host ""

# Check Azure CLI login
Write-Host "[Step 1/8] Checking Azure CLI login..." -ForegroundColor Yellow
$loginCheck = az account show --output json 2>$null
if (-not $loginCheck) {
    Write-Host "✗ Not logged in to Azure CLI." -ForegroundColor Red
    Write-Host "Please run: az login" -ForegroundColor Yellow
    exit 1
}

if ($SubscriptionId) {
    az account set --subscription $SubscriptionId
}

$currentSub = (az account show --query name -o tsv)
Write-Host "✓ Using subscription: $currentSub" -ForegroundColor Green
Write-Host ""

# Step 2: Check domain availability
Write-Host "[Step 2/8] Checking domain availability..." -ForegroundColor Yellow
Write-Host ""
Write-Host "IMPORTANT: Azure App Service Domains are registered through Azure Portal." -ForegroundColor Cyan
Write-Host "The domain registration cannot be fully automated via CLI." -ForegroundColor Cyan
Write-Host ""
Write-Host "To register '$DomainName':" -ForegroundColor Yellow
Write-Host "1. Go to Azure Portal: https://portal.azure.com" -ForegroundColor White
Write-Host "2. Search for 'App Service Domains'" -ForegroundColor White
Write-Host "3. Click '+ Create'" -ForegroundColor White
Write-Host "4. Enter domain: $DomainName" -ForegroundColor White
Write-Host "5. Select subscription: $currentSub" -ForegroundColor White
Write-Host "6. Resource group: $DomainResourceGroup" -ForegroundColor White
Write-Host "7. Complete purchase (~$12-15/year)" -ForegroundColor White
Write-Host ""
Write-Host "Cost Breakdown:" -ForegroundColor Cyan
Write-Host "  • Domain registration: ~$12/year" -ForegroundColor Gray
Write-Host "  • Privacy protection: FREE (included)" -ForegroundColor Gray
Write-Host "  • Auto-renewal: Enabled by default" -ForegroundColor Gray
Write-Host "  • SSL Certificate: FREE (App Service Managed)" -ForegroundColor Gray
Write-Host ""

$domainRegistered = Read-Host "Have you already registered '$DomainName' in Azure? (y/n)"

if ($domainRegistered -ne "y") {
    Write-Host ""
    Write-Host "⚠ Please register the domain first, then run this script again." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Quick Link: https://portal.azure.com/#create/Microsoft.Domain" -ForegroundColor Cyan
    Write-Host ""
    exit 0
}

Write-Host "✓ Domain registered" -ForegroundColor Green
Write-Host ""

# Step 3: Get Traffic Manager FQDN
Write-Host "[Step 3/8] Getting Traffic Manager FQDN..." -ForegroundColor Yellow

$tmFqdn = az network traffic-manager profile show `
    --name $TrafficManagerProfileName `
    --resource-group $TrafficManagerResourceGroup `
    --query "dnsConfig.fqdn" -o tsv

if (-not $tmFqdn) {
    Write-Host "✗ Failed to get Traffic Manager FQDN" -ForegroundColor Red
    Write-Host "Please run Setup-TrafficManager.ps1 first" -ForegroundColor Yellow
    exit 1
}

Write-Host "✓ Traffic Manager FQDN: $tmFqdn" -ForegroundColor Green
Write-Host ""

# Step 4: Configure DNS - CNAME to Traffic Manager
Write-Host "[Step 4/8] Configuring DNS..." -ForegroundColor Yellow
Write-Host ""
Write-Host "You need to create a CNAME record:" -ForegroundColor Cyan
Write-Host "  Host: @ (or blank for root domain)" -ForegroundColor White
Write-Host "  Type: CNAME" -ForegroundColor White
Write-Host "  Value: $tmFqdn" -ForegroundColor White
Write-Host "  TTL: 3600" -ForegroundColor White
Write-Host ""
Write-Host "To configure DNS:" -ForegroundColor Yellow
Write-Host "1. Go to Azure Portal" -ForegroundColor White
Write-Host "2. Navigate to: Resource Groups → $DomainResourceGroup → $DomainName" -ForegroundColor White
Write-Host "3. Click 'Manage DNS records'" -ForegroundColor White
Write-Host "4. Add CNAME record as shown above" -ForegroundColor White
Write-Host ""

$dnsConfigured = Read-Host "Have you configured the CNAME record? (y/n)"

if ($dnsConfigured -ne "y") {
    Write-Host ""
    Write-Host "⚠ Please configure DNS, then run this script again." -ForegroundColor Yellow
    Write-Host ""
    exit 0
}

Write-Host "✓ DNS configured" -ForegroundColor Green
Write-Host ""

# Step 5: Add custom domain to East US 2 App Service
Write-Host "[Step 5/8] Adding custom domain to East US 2 App Service..." -ForegroundColor Yellow

Write-Host "Adding domain to $EastUS2AppService..." -ForegroundColor Cyan

az webapp config hostname add `
    --webapp-name $EastUS2AppService `
    --resource-group $EastUS2ResourceGroup `
    --hostname $DomainName `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Failed to add custom domain to East US 2" -ForegroundColor Red
    Write-Host ""
    Write-Host "Common issues:" -ForegroundColor Yellow
    Write-Host "  • DNS not yet propagated (wait 5-10 minutes)" -ForegroundColor Gray
    Write-Host "  • Domain ownership not verified" -ForegroundColor Gray
    Write-Host "  • CNAME record incorrect" -ForegroundColor Gray
    Write-Host ""
    Write-Host "You can add it manually in Azure Portal:" -ForegroundColor Cyan
    Write-Host "  App Service → Custom domains → Add custom domain" -ForegroundColor White
    Write-Host ""
    exit 1
}

Write-Host "✓ Custom domain added to East US 2" -ForegroundColor Green
Write-Host ""

# Step 6: Add custom domain to West US 2 App Service
Write-Host "[Step 6/8] Adding custom domain to West US 2 App Service..." -ForegroundColor Yellow

Write-Host "Adding domain to $WestUS2AppService..." -ForegroundColor Cyan

az webapp config hostname add `
    --webapp-name $WestUS2AppService `
    --resource-group $WestUS2ResourceGroup `
    --hostname $DomainName `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Host "⚠ Warning: Failed to add custom domain to West US 2" -ForegroundColor Yellow
    Write-Host "You can add it manually later" -ForegroundColor Gray
}
else {
    Write-Host "✓ Custom domain added to West US 2" -ForegroundColor Green
}
Write-Host ""

# Step 7: Create SSL certificate for East US 2
Write-Host "[Step 7/8] Creating free SSL certificate for East US 2..." -ForegroundColor Yellow

Write-Host "Creating App Service Managed Certificate..." -ForegroundColor Cyan

az webapp config ssl create `
    --resource-group $EastUS2ResourceGroup `
    --name $EastUS2AppService `
    --hostname $DomainName `
    --output none 2>$null

if ($LASTEXITCODE -ne 0) {
    Write-Host "⚠ Warning: SSL certificate creation failed" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To create manually:" -ForegroundColor Cyan
    Write-Host "1. Go to App Service → TLS/SSL settings" -ForegroundColor White
    Write-Host "2. Click 'Private Key Certificates (.pfx)'" -ForegroundColor White
    Write-Host "3. Click '+ Create App Service Managed Certificate'" -ForegroundColor White
    Write-Host "4. Select: $DomainName" -ForegroundColor White
    Write-Host "5. Click 'Create'" -ForegroundColor White
    Write-Host ""
}
else {
    Write-Host "✓ SSL certificate created for East US 2" -ForegroundColor Green
    
    # Bind certificate
    Write-Host "Binding SSL certificate..." -ForegroundColor Cyan
    
    az webapp config ssl bind `
        --resource-group $EastUS2ResourceGroup `
        --name $EastUS2AppService `
        --certificate-thumbprint auto `
        --ssl-type SNI `
        --output none 2>$null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ SSL certificate bound" -ForegroundColor Green
    }
}
Write-Host ""

# Step 8: Create SSL certificate for West US 2
Write-Host "[Step 8/8] Creating free SSL certificate for West US 2..." -ForegroundColor Yellow

az webapp config ssl create `
    --resource-group $WestUS2ResourceGroup `
    --name $WestUS2AppService `
    --hostname $DomainName `
    --output none 2>$null

if ($LASTEXITCODE -ne 0) {
    Write-Host "⚠ Warning: SSL certificate creation failed" -ForegroundColor Yellow
    Write-Host "You can create it manually later using the same steps as East US 2" -ForegroundColor Gray
}
else {
    Write-Host "✓ SSL certificate created for West US 2" -ForegroundColor Green
    
    # Bind certificate
    az webapp config ssl bind `
        --resource-group $WestUS2ResourceGroup `
        --name $WestUS2AppService `
        --certificate-thumbprint auto `
        --ssl-type SNI `
        --output none 2>$null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ SSL certificate bound" -ForegroundColor Green
    }
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✓ Custom Domain Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "🌐 Your API URL: https://$DomainName" -ForegroundColor Cyan
Write-Host "📊 Health Check: https://$DomainName/api/v1/health" -ForegroundColor Cyan
Write-Host "📚 Swagger: https://$DomainName/swagger" -ForegroundColor Cyan
Write-Host ""
Write-Host "Regional Endpoints:" -ForegroundColor Yellow
Write-Host "  East US 2: https://$EastUS2AppService.azurewebsites.net" -ForegroundColor Gray
Write-Host "  West US 2: https://$WestUS2AppService.azurewebsites.net" -ForegroundColor Gray
Write-Host ""
Write-Host "⏱ DNS Propagation:" -ForegroundColor Yellow
Write-Host "  • Global propagation: 24-48 hours" -ForegroundColor White
Write-Host "  • Most regions: 1-2 hours" -ForegroundColor White
Write-Host "  • Test readiness: nslookup $DomainName" -ForegroundColor White
Write-Host ""
Write-Host "✅ SSL Certificate:" -ForegroundColor Yellow
Write-Host "  • Auto-renewal: Enabled" -ForegroundColor White
Write-Host "  • Expires: 6 months (auto-renewed)" -ForegroundColor White
Write-Host "  • Type: App Service Managed (FREE)" -ForegroundColor White
Write-Host ""
Write-Host "Testing Commands:" -ForegroundColor Yellow
Write-Host "  # Check DNS resolution" -ForegroundColor Gray
Write-Host "  nslookup $DomainName" -ForegroundColor White
Write-Host "" -ForegroundColor Gray
Write-Host "  # Test HTTPS" -ForegroundColor Gray
Write-Host "  curl https://$DomainName/api/v1/health" -ForegroundColor White
Write-Host "" -ForegroundColor Gray
Write-Host "  # Or PowerShell" -ForegroundColor Gray
Write-Host "  Invoke-RestMethod https://$DomainName/api/v1/health" -ForegroundColor White
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Completed at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan

exit 0
