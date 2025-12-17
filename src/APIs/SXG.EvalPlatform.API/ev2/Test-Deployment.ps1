# Test Deployment Script
# Use this to test the deployment process in a safe manner

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("EastUS2", "WestUS2")]
    [string]$Region,
    
    [Parameter(Mandatory=$false)]
    [switch]$DryRun = $false
)

$ErrorActionPreference = "Stop"

# Configuration
$config = @{
    "EastUS2" = @{
        ResourceGroup = "EvalApiRg-UsEast2"
        AppService = "sxgevalapiproduseast2"
        EndpointName = "eastus2-endpoint"
    }
    "WestUS2" = @{
        ResourceGroup = "EvalApiRg-UsWest2"
        AppService = "sxgevalapiproduswest2"
        EndpointName = "westus2-endpoint"
    }
}

$regionConfig = $config[$Region]
$storageAccount = "stevalplatformprod"
$trafficManager = "sxgevalapiprod"
$trafficManagerRG = "EvalCommonRg-useast2"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Deployment - $Region" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
    Write-Host ""
}

# Test 1: Check Azure CLI
Write-Host "[Test 1/8] Azure CLI Login" -ForegroundColor Yellow
try {
    $account = az account show --query "{name:name, id:id}" -o json | ConvertFrom-Json
    Write-Host "✓ Logged in as: $($account.name)" -ForegroundColor Green
    Write-Host "  Subscription: $($account.id)" -ForegroundColor Gray
}
catch {
    Write-Host "✗ Not logged in to Azure CLI" -ForegroundColor Red
    Write-Host "Run: az login" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# Test 2: Check Resource Group
Write-Host "[Test 2/8] Resource Group Exists" -ForegroundColor Yellow
$rgExists = az group show --name $regionConfig.ResourceGroup --query "name" -o tsv 2>$null
if ($rgExists) {
    Write-Host "✓ Resource Group exists: $($regionConfig.ResourceGroup)" -ForegroundColor Green
}
else {
    Write-Host "✗ Resource Group not found: $($regionConfig.ResourceGroup)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Test 3: Check App Service
Write-Host "[Test 3/8] App Service Exists" -ForegroundColor Yellow
$appExists = az webapp show --name $regionConfig.AppService --resource-group $regionConfig.ResourceGroup --query "{name:name, state:state}" -o json 2>$null
if ($appExists) {
    $app = $appExists | ConvertFrom-Json
    Write-Host "✓ App Service exists: $($app.name)" -ForegroundColor Green
    Write-Host "  State: $($app.state)" -ForegroundColor Gray
}
else {
    Write-Host "✗ App Service not found: $($regionConfig.AppService)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Test 4: Check Storage Account
Write-Host "[Test 4/8] Storage Account Exists" -ForegroundColor Yellow
$storageExists = az storage account show --name $storageAccount --resource-group "EvalCommonRg-useast2" --query "name" -o tsv 2>$null
if ($storageExists) {
    Write-Host "✓ Storage Account exists: $storageAccount" -ForegroundColor Green
}
else {
    Write-Host "✗ Storage Account not found: $storageAccount" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Test 5: Check Traffic Manager
Write-Host "[Test 5/8] Traffic Manager Profile" -ForegroundColor Yellow
$tmExists = az network traffic-manager profile show --name $trafficManager --resource-group $trafficManagerRG --query "{name:name, fqdn:dnsConfig.fqdn}" -o json 2>$null
if ($tmExists) {
    $tm = $tmExists | ConvertFrom-Json
    Write-Host "✓ Traffic Manager exists: $($tm.name)" -ForegroundColor Green
    Write-Host "  FQDN: $($tm.fqdn)" -ForegroundColor Gray
}
else {
    Write-Host "✗ Traffic Manager not found: $trafficManager" -ForegroundColor Red
    Write-Host "Run: .\Setup-TrafficManager.ps1" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# Test 6: Check Traffic Manager Endpoint
Write-Host "[Test 6/8] Traffic Manager Endpoint" -ForegroundColor Yellow
$endpointExists = az network traffic-manager endpoint show `
    --name $regionConfig.EndpointName `
    --profile-name $trafficManager `
    --resource-group $trafficManagerRG `
    --type azureEndpoints `
    --query "{name:name, status:endpointStatus, monitor:endpointMonitorStatus}" `
    -o json 2>$null

if ($endpointExists) {
    $endpoint = $endpointExists | ConvertFrom-Json
    Write-Host "✓ Endpoint exists: $($endpoint.name)" -ForegroundColor Green
    Write-Host "  Status: $($endpoint.status)" -ForegroundColor Gray
    Write-Host "  Monitor: $($endpoint.monitor)" -ForegroundColor Gray
}
else {
    Write-Host "✗ Endpoint not found: $($regionConfig.EndpointName)" -ForegroundColor Red
    Write-Host "Run: .\Setup-TrafficManager.ps1" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# Test 7: Check App Service Health
Write-Host "[Test 7/8] App Service Health Check" -ForegroundColor Yellow
$appUrl = "https://$($regionConfig.AppService).azurewebsites.net"
$healthUrl = "$appUrl/api/v1/health"

try {
    $response = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 10
    Write-Host "✓ Health endpoint responsive" -ForegroundColor Green
    Write-Host "  URL: $healthUrl" -ForegroundColor Gray
    Write-Host "  Status: $($response.Status)" -ForegroundColor Gray
}
catch {
    Write-Host "⚠ Health check failed (may be expected if first deployment)" -ForegroundColor Yellow
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
}
Write-Host ""

# Test 8: Check Deployment Script
Write-Host "[Test 8/8] Deployment Script Exists" -ForegroundColor Yellow
$scriptPath = Join-Path $PSScriptRoot "Deploy-Regional-With-TrafficManager.ps1"
if (Test-Path $scriptPath) {
    Write-Host "✓ Deployment script found" -ForegroundColor Green
    Write-Host "  Path: $scriptPath" -ForegroundColor Gray
}
else {
    Write-Host "✗ Deployment script not found" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✓ All Pre-Deployment Checks Passed" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN - Would execute:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host ".\Deploy-Regional-With-TrafficManager.ps1 \" -ForegroundColor Gray
    Write-Host "  -Region `"$Region`" \" -ForegroundColor Gray
    Write-Host "  -ResourceGroupName `"$($regionConfig.ResourceGroup)`" \" -ForegroundColor Gray
    Write-Host "  -AppServiceName `"$($regionConfig.AppService)`" \" -ForegroundColor Gray
    Write-Host "  -StorageAccountName `"$storageAccount`" \" -ForegroundColor Gray
    Write-Host "  -TrafficManagerProfileName `"$trafficManager`" \" -ForegroundColor Gray
    Write-Host "  -TrafficManagerResourceGroup `"$trafficManagerRG`" \" -ForegroundColor Gray
    Write-Host "  -TrafficManagerEndpointName `"$($regionConfig.EndpointName)`" \" -ForegroundColor Gray
    Write-Host "  -HealthCheckWaitSeconds 300" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Remove -DryRun to execute deployment" -ForegroundColor Yellow
}
else {
    Write-Host "Ready to deploy to $Region" -ForegroundColor Green
    Write-Host ""
    $confirm = Read-Host "Proceed with deployment? (yes/no)"
    
    if ($confirm -eq "yes") {
        Write-Host ""
        Write-Host "Starting deployment..." -ForegroundColor Cyan
        Write-Host ""
        
        & $scriptPath `
            -Region $Region `
            -ResourceGroupName $regionConfig.ResourceGroup `
            -AppServiceName $regionConfig.AppService `
            -StorageAccountName $storageAccount `
            -TrafficManagerProfileName $trafficManager `
            -TrafficManagerResourceGroup $trafficManagerRG `
            -TrafficManagerEndpointName $regionConfig.EndpointName `
            -HealthCheckWaitSeconds 300
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "========================================" -ForegroundColor Cyan
            Write-Host "✓ Deployment Completed Successfully" -ForegroundColor Green
            Write-Host "========================================" -ForegroundColor Cyan
        }
        else {
            Write-Host ""
            Write-Host "========================================" -ForegroundColor Red
            Write-Host "✗ Deployment Failed" -ForegroundColor Red
            Write-Host "========================================" -ForegroundColor Red
            exit 1
        }
    }
    else {
        Write-Host "Deployment cancelled" -ForegroundColor Yellow
    }
}
Write-Host ""
