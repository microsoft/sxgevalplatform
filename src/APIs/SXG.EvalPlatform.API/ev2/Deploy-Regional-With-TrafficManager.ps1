# Regional Deployment Script for SXG Evaluation Platform API - Production
# With Traffic Manager Integration for Zero-Downtime Deployment
# This script is designed to be called by EV2 orchestration

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("EastUS2", "WestUS2")]
    [string]$Region,
    
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$true)]
    [string]$AppServiceName,
    
    [Parameter(Mandatory=$true)]
    [string]$StorageAccountName,
    
    [Parameter(Mandatory=$true)]
    [string]$TrafficManagerProfileName,
    
    [Parameter(Mandatory=$true)]
    [string]$TrafficManagerResourceGroup,
    
    [Parameter(Mandatory=$true)]
    [string]$TrafficManagerEndpointName,
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory=$false)]
    [int]$HealthCheckWaitSeconds = 300,
    
    [Parameter(Mandatory=$false)]
    [string]$BuildArtifactPath
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Function to convert nested JSON to flat App Settings format
function ConvertTo-AppSettings {
    param (
        [Parameter(Mandatory=$true)]
        [PSCustomObject]$JsonObject,
        [Parameter(Mandatory=$false)]
        [string]$Prefix = ""
    )
    
    $settings = @()
    
    foreach ($property in $JsonObject.PSObject.Properties) {
        $key = if ($Prefix) { "${Prefix}__$($property.Name)" } else { $property.Name }
        $value = $property.Value
 
        if ($value -is [PSCustomObject]) {
            $settings += ConvertTo-AppSettings -JsonObject $value -Prefix $key
        }
        elseif ($value -is [Array]) {
            for ($i = 0; $i -lt $value.Count; $i++) {
                if ($value[$i] -is [PSCustomObject]) {
                    $settings += ConvertTo-AppSettings -JsonObject $value[$i] -Prefix "${key}__${i}"
                }
                else {
                    $settings += "${key}__${i}=$($value[$i])"
                }
            }
        }
        else {
            $settings += "${key}=${value}"
        }
    }
    
    return $settings
}

# Function to merge appsettings
function Get-MergedAppSettings {
    param (
        [Parameter(Mandatory=$true)]
        [string]$Environment,
        [Parameter(Mandatory=$true)]
        [string]$StorageAccountName,
        [Parameter(Mandatory=$true)]
        [string]$Region
    )
    
    $scriptDir = Split-Path -Parent $PSCommandPath
    $baseSettingsPath = Join-Path $scriptDir "..\appsettings.json"
    $envSettingsPath = Join-Path $scriptDir "..\appsettings.$Environment.json"
    
    Write-Host "Reading base appsettings from: $baseSettingsPath" -ForegroundColor Cyan
    
    if (-not (Test-Path $baseSettingsPath)) {
        throw "Base appsettings.json not found at: $baseSettingsPath"
    }
    
    $baseSettings = Get-Content $baseSettingsPath -Raw | ConvertFrom-Json
    
    if (Test-Path $envSettingsPath) {
        Write-Host "Reading environment-specific appsettings from: $envSettingsPath" -ForegroundColor Cyan
        $envSettings = Get-Content $envSettingsPath -Raw | ConvertFrom-Json
        
        foreach ($property in $envSettings.PSObject.Properties) {
            $baseSettings | Add-Member -MemberType NoteProperty -Name $property.Name -Value $property.Value -Force
        }
    }
    
    # Override values
    $baseSettings.ApiSettings.Environment = $Environment
    $baseSettings.ApiSettings | Add-Member -MemberType NoteProperty -Name "Region" -Value $Region -Force
    $baseSettings.AzureStorage.AccountName = $StorageAccountName
    
    # Production Redis configuration
    $baseSettings.Cache.Redis.Endpoint = "evalplatformcacheprod.redis.cache.windows.net:6380"
    $baseSettings.Cache.Redis.InstanceName = "evalplatformcacheprod"
    
    # Production DataVerse API configuration
    $baseSettings.DataVerseAPI.DatasetEnrichmentRequestAPIEndPoint = "https://sxg-eval-prod.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun"
    $baseSettings.DataVerseAPI.Scope = "https://sxg-eval-prod.crm.dynamics.com/.default"
    
    $appSettings = ConvertTo-AppSettings -JsonObject $baseSettings
    $appSettings = @("ASPNETCORE_ENVIRONMENT=$Environment") + $appSettings
    
    Write-Host "✓ Merged and converted appsettings. Total settings: $($appSettings.Count)" -ForegroundColor Green
    
    return $appSettings
}

# Function to check app service health
function Test-AppServiceHealth {
    param (
        [Parameter(Mandatory=$true)]
        [string]$AppServiceUrl,
        [Parameter(Mandatory=$false)]
        [int]$MaxRetries = 5
    )
    
    Write-Host "Checking app service health at: $AppServiceUrl/api/v1/health/detailed" -ForegroundColor Yellow
    
    for ($i = 1; $i -le $MaxRetries; $i++) {
        try {
            Write-Host "Health check attempt $i/$MaxRetries..." -ForegroundColor Cyan
            
            $response = Invoke-RestMethod -Uri "$AppServiceUrl/api/v1/health/detailed" -Method Get -TimeoutSec 30
            
            Write-Host "✓ Health check response received" -ForegroundColor Green
            Write-Host "Overall Status: $($response.Status)" -ForegroundColor $(if ($response.Status -eq "Healthy") { "Green" } else { "Yellow" })
            
            Write-Host "`nDependency Status:" -ForegroundColor Cyan
            $unhealthyDeps = @()
            
            foreach ($dep in $response.Dependencies) {
                $statusColor = if ($dep.Status -eq "Healthy") { "Green" } else { "Red" }
                Write-Host "[$($dep.Status)] $($dep.Name) - Response Time: $($dep.ResponseTime)" -ForegroundColor $statusColor
                
                if ($dep.Status -ne "Healthy") {
                    $unhealthyDeps += $dep
                    if ($dep.ErrorMessage) {
                        Write-Host "    Error: $($dep.ErrorMessage)" -ForegroundColor Red
                    }
                    if ($dep.AdditionalInfo) {
                        Write-Host "    Info: $($dep.AdditionalInfo)" -ForegroundColor Yellow
                    }
                }
            }
            
            if ($response.Status -eq "Healthy") {
                Write-Host "`n✓ All health checks passed!" -ForegroundColor Green
                return @{ Success = $true; Status = $response.Status; UnhealthyDependencies = @() }
            }
            else {
                Write-Host "`n⚠ Application is in degraded state" -ForegroundColor Yellow
                return @{ Success = $false; Status = $response.Status; UnhealthyDependencies = $unhealthyDeps }
            }
        }
        catch {
            Write-Host "✗ Health check attempt $i failed: $($_.Exception.Message)" -ForegroundColor Red
            
            if ($i -lt $MaxRetries) {
                Write-Host "Waiting 30 seconds before retry..." -ForegroundColor Yellow
                Start-Sleep -Seconds 30
            }
        }
    }
    
    Write-Host "`n✗ All health check attempts failed" -ForegroundColor Red
    return @{ Success = $false; Status = "Unreachable"; UnhealthyDependencies = @() }
}

# Function to disable Traffic Manager endpoint
function Disable-TrafficManagerEndpoint {
    param (
        [Parameter(Mandatory=$true)]
        [string]$ProfileName,
        [Parameter(Mandatory=$true)]
        [string]$ResourceGroup,
        [Parameter(Mandatory=$true)]
        [string]$EndpointName
    )
    
    Write-Host "Disabling Traffic Manager endpoint: $EndpointName" -ForegroundColor Yellow
    
    az network traffic-manager endpoint update `
        --name $EndpointName `
        --profile-name $ProfileName `
        --resource-group $ResourceGroup `
        --type azureEndpoints `
        --endpoint-status Disabled `
        --output none
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to disable Traffic Manager endpoint"
    }
    
    Write-Host "✓ Traffic Manager endpoint disabled successfully" -ForegroundColor Green
    Write-Host "Waiting 60 seconds for traffic to drain..." -ForegroundColor Cyan
    Start-Sleep -Seconds 60
}

# Function to enable Traffic Manager endpoint
function Enable-TrafficManagerEndpoint {
    param (
        [Parameter(Mandatory=$true)]
        [string]$ProfileName,
        [Parameter(Mandatory=$true)]
        [string]$ResourceGroup,
        [Parameter(Mandatory=$true)]
        [string]$EndpointName
    )
    
    Write-Host "Enabling Traffic Manager endpoint: $EndpointName" -ForegroundColor Yellow
    
    az network traffic-manager endpoint update `
        --name $EndpointName `
        --profile-name $ProfileName `
        --resource-group $ResourceGroup `
        --type azureEndpoints `
        --endpoint-status Enabled `
        --output none
    
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to enable Traffic Manager endpoint"
    }
    
    Write-Host "✓ Traffic Manager endpoint enabled successfully" -ForegroundColor Green
}

# Function to verify Traffic Manager health
function Test-TrafficManagerEndpoint {
    param (
        [Parameter(Mandatory=$true)]
        [string]$ProfileName,
        [Parameter(Mandatory=$true)]
        [string]$ResourceGroup,
        [Parameter(Mandatory=$true)]
        [string]$EndpointName
    )
    
    Write-Host "Checking Traffic Manager endpoint status..." -ForegroundColor Yellow
    
    $endpointStatus = az network traffic-manager endpoint show `
        --name $EndpointName `
        --profile-name $ProfileName `
        --resource-group $ResourceGroup `
        --type azureEndpoints `
        --query "{status:endpointStatus, monitorStatus:endpointMonitorStatus}" `
        -o json | ConvertFrom-Json
    
    Write-Host "Endpoint Status: $($endpointStatus.status)" -ForegroundColor Cyan
    Write-Host "Monitor Status: $($endpointStatus.monitorStatus)" -ForegroundColor Cyan
    
    if ($endpointStatus.status -eq "Enabled" -and $endpointStatus.monitorStatus -eq "Online") {
        Write-Host "✓ Traffic Manager endpoint is healthy" -ForegroundColor Green
        return $true
    }
    else {
        Write-Host "⚠ Traffic Manager endpoint health check failed" -ForegroundColor Yellow
        return $false
    }
}

# Main deployment script
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SXG Evaluation Platform API" -ForegroundColor Cyan
Write-Host "PRODUCTION DEPLOYMENT - $Region" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Region: $Region" -ForegroundColor Green
Write-Host "App Service: $AppServiceName" -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Green
Write-Host "Storage Account: $StorageAccountName" -ForegroundColor Green
Write-Host "Traffic Manager: $TrafficManagerProfileName" -ForegroundColor Green
Write-Host "TM Endpoint: $TrafficManagerEndpointName" -ForegroundColor Green
Write-Host ""

# Check Azure CLI login
Write-Host "[Step 1/12] Checking Azure CLI login status..." -ForegroundColor Yellow
$loginCheck = az account show --output json 2>$null
if (-not $loginCheck) {
    throw "Not logged in to Azure CLI. Please run: az login"
}

if ($SubscriptionId) {
    Write-Host "Setting subscription to: $SubscriptionId" -ForegroundColor Yellow
    az account set --subscription $SubscriptionId
}

$currentSub = (az account show --query name -o tsv)
Write-Host "✓ Using subscription: $currentSub" -ForegroundColor Green
Write-Host ""

# Step 2: Disable Traffic Manager Endpoint
Write-Host "[Step 2/12] Disabling Traffic Manager endpoint for $Region..." -ForegroundColor Yellow
try {
    Disable-TrafficManagerEndpoint `
        -ProfileName $TrafficManagerProfileName `
        -ResourceGroup $TrafficManagerResourceGroup `
        -EndpointName $TrafficManagerEndpointName
}
catch {
    Write-Host "✗ Failed to disable Traffic Manager endpoint: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 3: Stop App Service
Write-Host "[Step 3/12] Stopping App Service: $AppServiceName" -ForegroundColor Yellow
az webapp stop --name $AppServiceName --resource-group $ResourceGroupName --output none

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Failed to stop App Service" -ForegroundColor Red
    exit 1
}

Write-Host "✓ App Service stopped successfully" -ForegroundColor Green
Start-Sleep -Seconds 5
Write-Host ""

# Step 4: Deploy App Settings
Write-Host "[Step 4/12] Deploying App Settings..." -ForegroundColor Yellow

try {
    $appSettings = Get-MergedAppSettings -Environment "Production" -StorageAccountName $StorageAccountName -Region $Region
    
    Write-Host "Deploying $($appSettings.Count) settings to Azure App Service..." -ForegroundColor Cyan
    
    az webapp config appsettings set `
        --name $AppServiceName `
        --resource-group $ResourceGroupName `
        --settings @appSettings `
        --output none

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to configure App Settings"
    }
    
    Write-Host "✓ App Settings configured successfully" -ForegroundColor Green
}
catch {
    Write-Host "✗ Error deploying appsettings: $($_.Exception.Message)" -ForegroundColor Red
    az webapp start --name $AppServiceName --resource-group $ResourceGroupName --output none
    exit 1
}
Write-Host ""

# Step 5: Build Application
Write-Host "[Step 5/12] Building application..." -ForegroundColor Yellow
$scriptDir = Split-Path -Parent $PSCommandPath

if ($BuildArtifactPath) {
    $projectPath = Join-Path $BuildArtifactPath "SXG.EvalPlatform.API.csproj"
}
else {
    $projectPath = Join-Path $scriptDir "..\SXG.EvalPlatform.API.csproj"
}

if (-not (Test-Path $projectPath)) {
    Write-Host "✗ Project file not found: $projectPath" -ForegroundColor Red
    az webapp start --name $AppServiceName --resource-group $ResourceGroupName --output none
    exit 1
}

dotnet clean $projectPath --configuration Release | Out-Null
dotnet build $projectPath --configuration Release --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed" -ForegroundColor Red
    az webapp start --name $AppServiceName --resource-group $ResourceGroupName --output none
    exit 1
}
Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

# Step 6: Publish Application
Write-Host "[Step 6/12] Publishing application..." -ForegroundColor Yellow
$publishPath = Join-Path $scriptDir "publish-$Region"
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

dotnet publish $projectPath --configuration Release --output $publishPath --no-build --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Publish failed" -ForegroundColor Red
    az webapp start --name $AppServiceName --resource-group $ResourceGroupName --output none
    exit 1
}
Write-Host "✓ Publish successful" -ForegroundColor Green
Write-Host ""

# Step 7: Create Deployment Package
Write-Host "[Step 7/12] Creating deployment package..." -ForegroundColor Yellow
$zipPath = Join-Path $scriptDir "deploy-$Region.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force
$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Host "✓ Deployment package created: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Green
Write-Host ""

# Step 8: Deploy to Azure
Write-Host "[Step 8/12] Deploying to Azure App Service..." -ForegroundColor Yellow
Write-Host "This may take a few minutes..." -ForegroundColor Cyan

az webapp deploy `
    --name $AppServiceName `
    --resource-group $ResourceGroupName `
    --src-path $zipPath `
    --type zip `
    --async false `
    --timeout 600

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Deployment failed" -ForegroundColor Red
    az webapp start --name $AppServiceName --resource-group $ResourceGroupName --output none
    exit 1
}
Write-Host "✓ Application deployed successfully" -ForegroundColor Green
Write-Host ""

# Step 9: Start App Service
Write-Host "[Step 9/12] Starting App Service: $AppServiceName" -ForegroundColor Yellow

az webapp start --name $AppServiceName --resource-group $ResourceGroupName --output none

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Failed to start App Service" -ForegroundColor Red
    exit 1
}

Write-Host "✓ App Service started successfully" -ForegroundColor Green
Write-Host ""

# Step 10: Wait for Application Warmup
Write-Host "[Step 10/12] Waiting $HealthCheckWaitSeconds seconds for application warmup..." -ForegroundColor Yellow
$appUrl = "https://$AppServiceName.azurewebsites.net"

for ($i = $HealthCheckWaitSeconds; $i -gt 0; $i -= 30) {
    Write-Host "  $i seconds remaining..." -ForegroundColor Gray
    Start-Sleep -Seconds $(if ($i -gt 30) { 30 } else { $i })
}

Write-Host "✓ Warmup period complete" -ForegroundColor Green
Write-Host ""

# Step 11: Health Check Verification
Write-Host "[Step 11/12] Running health checks..." -ForegroundColor Yellow
$healthResult = Test-AppServiceHealth -AppServiceUrl $appUrl -MaxRetries 5

if (-not $healthResult.Success) {
    Write-Host "✗ Health checks failed. Not enabling Traffic Manager endpoint." -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 12: Enable Traffic Manager Endpoint
Write-Host "[Step 12/12] Enabling Traffic Manager endpoint for $Region..." -ForegroundColor Yellow
try {
    Enable-TrafficManagerEndpoint `
        -ProfileName $TrafficManagerProfileName `
        -ResourceGroup $TrafficManagerResourceGroup `
        -EndpointName $TrafficManagerEndpointName
    
    Write-Host "Waiting 60 seconds for Traffic Manager to update..." -ForegroundColor Cyan
    Start-Sleep -Seconds 60
    
    # Verify Traffic Manager health
    $tmHealthy = Test-TrafficManagerEndpoint `
        -ProfileName $TrafficManagerProfileName `
        -ResourceGroup $TrafficManagerResourceGroup `
        -EndpointName $TrafficManagerEndpointName
    
    if (-not $tmHealthy) {
        Write-Host "⚠ Warning: Traffic Manager endpoint may not be fully healthy" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "✗ Failed to enable Traffic Manager endpoint: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Cleanup
Write-Host "Cleaning up local deployment files..." -ForegroundColor Yellow
Remove-Item $publishPath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Write-Host "✓ Cleanup complete" -ForegroundColor Green
Write-Host ""

# Final Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✓ DEPLOYMENT SUCCESSFUL - $Region" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "📍 Region: $Region" -ForegroundColor Cyan
Write-Host "🌐 API URL: $appUrl" -ForegroundColor Cyan
Write-Host "📊 Health Check: $appUrl/api/v1/health" -ForegroundColor Cyan
Write-Host "🔍 Detailed Health: $appUrl/api/v1/health/detailed" -ForegroundColor Cyan
Write-Host "📚 Swagger UI: $appUrl/swagger" -ForegroundColor Cyan
Write-Host "🚦 Traffic Manager: Enabled" -ForegroundColor Cyan
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment completed at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan

exit 0
