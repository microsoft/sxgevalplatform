# Traffic Manager Setup Script
# This script creates and configures Traffic Manager with both regional endpoints

param(
    [Parameter(Mandatory=$false)]
    [string]$TrafficManagerProfileName = "sxgevalapiprod",
    
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "EvalCommonRg-useast2",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "global",
    
    [Parameter(Mandatory=$false)]
    [string]$EastUS2AppService = "sxgevalapiproduseast2",
    
    [Parameter(Mandatory=$false)]
    [string]$EastUS2ResourceGroup = "EvalApiRg-UsEast2",
    
    [Parameter(Mandatory=$false)]
    [string]$WestUS2AppService = "sxgevalapiproduswest2",
    
    [Parameter(Mandatory=$false)]
    [string]$WestUS2ResourceGroup = "EvalApiRg-UsWest2",
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Traffic Manager Setup" -ForegroundColor Cyan
Write-Host "SXG Evaluation Platform API" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check Azure CLI login
Write-Host "[1/6] Checking Azure CLI login status..." -ForegroundColor Yellow
$loginCheck = az account show --output json 2>$null
if (-not $loginCheck) {
    Write-Host "✗ Not logged in to Azure CLI." -ForegroundColor Red
    Write-Host "Please run: az login" -ForegroundColor Yellow
    exit 1
}

if ($SubscriptionId) {
    Write-Host "Setting subscription to: $SubscriptionId" -ForegroundColor Yellow
    az account set --subscription $SubscriptionId
}

$currentSub = (az account show --query name -o tsv)
Write-Host "✓ Using subscription: $currentSub" -ForegroundColor Green
Write-Host ""

# Check if Traffic Manager profile exists
Write-Host "[2/6] Checking Traffic Manager profile..." -ForegroundColor Yellow
$profileExists = az network traffic-manager profile show `
    --name $TrafficManagerProfileName `
    --resource-group $ResourceGroupName `
    --query "name" -o tsv 2>$null

if ($profileExists) {
    Write-Host "Traffic Manager profile already exists: $TrafficManagerProfileName" -ForegroundColor Yellow
    $update = Read-Host "Do you want to update it? (y/n)"
    if ($update -ne "y") {
        Write-Host "Exiting without changes" -ForegroundColor Yellow
        exit 0
    }
}
else {
    Write-Host "Creating new Traffic Manager profile..." -ForegroundColor Cyan
    
    az network traffic-manager profile create `
        --name $TrafficManagerProfileName `
        --resource-group $ResourceGroupName `
        --routing-method Performance `
        --unique-dns-name $TrafficManagerProfileName `
        --ttl 30 `
        --protocol HTTPS `
        --port 443 `
        --path "/api/v1/health" `
        --interval 30 `
        --timeout 10 `
        --max-failures 3 `
        --output none
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Failed to create Traffic Manager profile" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✓ Traffic Manager profile created" -ForegroundColor Green
}
Write-Host ""

# Get App Service resource IDs
Write-Host "[3/6] Getting App Service resource IDs..." -ForegroundColor Yellow

$eastUS2AppServiceId = az webapp show `
    --name $EastUS2AppService `
    --resource-group $EastUS2ResourceGroup `
    --query "id" -o tsv

if (-not $eastUS2AppServiceId) {
    Write-Host "✗ Failed to get East US 2 App Service ID" -ForegroundColor Red
    exit 1
}
Write-Host "✓ East US 2 App Service ID: $eastUS2AppServiceId" -ForegroundColor Green

$westUS2AppServiceId = az webapp show `
    --name $WestUS2AppService `
    --resource-group $WestUS2ResourceGroup `
    --query "id" -o tsv

if (-not $westUS2AppServiceId) {
    Write-Host "✗ Failed to get West US 2 App Service ID" -ForegroundColor Red
    exit 1
}
Write-Host "✓ West US 2 App Service ID: $westUS2AppServiceId" -ForegroundColor Green
Write-Host ""

# Create or update East US 2 endpoint
Write-Host "[4/6] Configuring East US 2 endpoint..." -ForegroundColor Yellow

$eastEndpointExists = az network traffic-manager endpoint show `
    --name "eastus2-endpoint" `
    --profile-name $TrafficManagerProfileName `
    --resource-group $ResourceGroupName `
    --type azureEndpoints `
    --query "name" -o tsv 2>$null

if ($eastEndpointExists) {
    Write-Host "Updating existing East US 2 endpoint..." -ForegroundColor Cyan
    
    az network traffic-manager endpoint update `
        --name "eastus2-endpoint" `
        --profile-name $TrafficManagerProfileName `
        --resource-group $ResourceGroupName `
        --type azureEndpoints `
        --target-resource-id $eastUS2AppServiceId `
        --endpoint-status Enabled `
        --priority 1 `
        --weight 50 `
        --output none
}
else {
    Write-Host "Creating new East US 2 endpoint..." -ForegroundColor Cyan
    
    az network traffic-manager endpoint create `
        --name "eastus2-endpoint" `
        --profile-name $TrafficManagerProfileName `
        --resource-group $ResourceGroupName `
        --type azureEndpoints `
        --target-resource-id $eastUS2AppServiceId `
        --endpoint-status Enabled `
        --priority 1 `
        --weight 50 `
        --output none
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Failed to configure East US 2 endpoint" -ForegroundColor Red
    exit 1
}
Write-Host "✓ East US 2 endpoint configured" -ForegroundColor Green
Write-Host ""

# Create or update West US 2 endpoint
Write-Host "[5/6] Configuring West US 2 endpoint..." -ForegroundColor Yellow

$westEndpointExists = az network traffic-manager endpoint show `
    --name "westus2-endpoint" `
    --profile-name $TrafficManagerProfileName `
    --resource-group $ResourceGroupName `
    --type azureEndpoints `
    --query "name" -o tsv 2>$null

if ($westEndpointExists) {
    Write-Host "Updating existing West US 2 endpoint..." -ForegroundColor Cyan
    
    az network traffic-manager endpoint update `
        --name "westus2-endpoint" `
        --profile-name $TrafficManagerProfileName `
        --resource-group $ResourceGroupName `
        --type azureEndpoints `
        --target-resource-id $westUS2AppServiceId `
        --endpoint-status Enabled `
        --priority 2 `
        --weight 50 `
        --output none
}
else {
    Write-Host "Creating new West US 2 endpoint..." -ForegroundColor Cyan
    
    az network traffic-manager endpoint create `
        --name "westus2-endpoint" `
        --profile-name $TrafficManagerProfileName `
        --resource-group $ResourceGroupName `
        --type azureEndpoints `
        --target-resource-id $westUS2AppServiceId `
        --endpoint-status Enabled `
        --priority 2 `
        --weight 50 `
        --output none
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Failed to configure West US 2 endpoint" -ForegroundColor Red
    exit 1
}
Write-Host "✓ West US 2 endpoint configured" -ForegroundColor Green
Write-Host ""

# Verify configuration
Write-Host "[6/6] Verifying Traffic Manager configuration..." -ForegroundColor Yellow

$profile = az network traffic-manager profile show `
    --name $TrafficManagerProfileName `
    --resource-group $ResourceGroupName `
    --query "{fqdn:dnsConfig.fqdn, routingMethod:trafficRoutingMethod, monitorPath:monitorConfig.path, monitorProtocol:monitorConfig.protocol, monitorPort:monitorConfig.port}" `
    -o json | ConvertFrom-Json

Write-Host "`nTraffic Manager Profile:" -ForegroundColor Cyan
Write-Host "  FQDN: $($profile.fqdn)" -ForegroundColor White
Write-Host "  Routing Method: $($profile.routingMethod)" -ForegroundColor White
Write-Host "  Monitor Path: $($profile.monitorPath)" -ForegroundColor White
Write-Host "  Monitor Protocol: $($profile.monitorProtocol)" -ForegroundColor White
Write-Host "  Monitor Port: $($profile.monitorPort)" -ForegroundColor White

Write-Host "`nEndpoints:" -ForegroundColor Cyan

$endpoints = az network traffic-manager endpoint list `
    --profile-name $TrafficManagerProfileName `
    --resource-group $ResourceGroupName `
    --type azureEndpoints `
    --query "[].{name:name, status:endpointStatus, monitorStatus:endpointMonitorStatus, priority:priority, weight:weight}" `
    -o json | ConvertFrom-Json

foreach ($endpoint in $endpoints) {
    $statusColor = if ($endpoint.monitorStatus -eq "Online") { "Green" } else { "Yellow" }
    Write-Host "  [$($endpoint.status)] $($endpoint.name)" -ForegroundColor $statusColor
    Write-Host "    Monitor Status: $($endpoint.monitorStatus)" -ForegroundColor $statusColor
    Write-Host "    Priority: $($endpoint.priority), Weight: $($endpoint.weight)" -ForegroundColor White
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✓ Traffic Manager Setup Complete" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "🌐 Traffic Manager URL: https://$($profile.fqdn)" -ForegroundColor Cyan
Write-Host "📊 Health Check: https://$($profile.fqdn)/api/v1/health" -ForegroundColor Cyan
Write-Host "📚 Swagger: https://$($profile.fqdn)/swagger" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Wait 2-3 minutes for DNS propagation" -ForegroundColor White
Write-Host "2. Test health endpoint: https://$($profile.fqdn)/api/v1/health" -ForegroundColor White
Write-Host "3. Verify both endpoints are 'Online' in Azure Portal" -ForegroundColor White
Write-Host "4. Configure custom domain if needed" -ForegroundColor White
Write-Host ""
Write-Host "Monitor endpoints:" -ForegroundColor Yellow
Write-Host "  az network traffic-manager endpoint list --profile-name $TrafficManagerProfileName --resource-group $ResourceGroupName --type azureEndpoints --query '[].{name:name, status:endpointStatus, monitor:endpointMonitorStatus}' -o table" -ForegroundColor Gray
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

exit 0
