# Azure Deployment Script for SXG Evaluation Platform API - Development Environment
# Enhanced version with automatic appsettings.json parsing and health verification
# This script automates the entire deployment lifecycle

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-sxg-agent-evaluation-platform",
    
    [Parameter(Mandatory=$false)]
    [string]$AppName = "sxgevalapidev",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "eastus",
    
    [Parameter(Mandatory=$false)]
    [string]$StorageAccountName = "sxgagentevaldev",

    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId,

    [Parameter(Mandatory=$false)]
    [int]$WarmupWaitSeconds = 120,

    [Parameter(Mandatory=$false)]
    [int]$HealthCheckWaitSeconds = 120
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
     # Recursively process nested objects
            $settings += ConvertTo-AppSettings -JsonObject $value -Prefix $key
        }
        elseif ($value -is [Array]) {
   # Handle arrays (convert to JSON string or skip based on need)
       for ($i = 0; $i < $value.Count; $i++) {
           if ($value[$i] -is [PSCustomObject]) {
  $settings += ConvertTo-AppSettings -JsonObject $value[$i] -Prefix "${key}__${i}"
         }
                else {
   $settings += "${key}__${i}=$($value[$i])"
                }
    }
        }
        else {
            # Simple value
            $settings += "${key}=${value}"
 }
    }
    
    return $settings
}

# Function to merge appsettings.json with environment-specific overrides
function Get-MergedAppSettings {
    param (
        [Parameter(Mandatory=$true)]
        [string]$Environment,
    
        [Parameter(Mandatory=$true)]
     [string]$StorageAccountName
    )
    
    $baseSettingsPath = "../appsettings.json"
    $envSettingsPath = "../appsettings.$Environment.json"
    
    Write-Host "Reading base appsettings from: $baseSettingsPath" -ForegroundColor Cyan
    
    if (-not (Test-Path $baseSettingsPath)) {
        throw "Base appsettings.json not found at: $baseSettingsPath"
    }
    
    # Read and parse base settings
    $baseSettings = Get-Content $baseSettingsPath -Raw | ConvertFrom-Json
    
    # Read and merge environment-specific settings if they exist
    if (Test-Path $envSettingsPath) {
        Write-Host "Reading environment-specific appsettings from: $envSettingsPath" -ForegroundColor Cyan
      $envSettings = Get-Content $envSettingsPath -Raw | ConvertFrom-Json
    
        # Deep merge (environment settings override base settings)
        foreach ($property in $envSettings.PSObject.Properties) {
         $baseSettings | Add-Member -MemberType NoteProperty -Name $property.Name -Value $property.Value -Force
        }
    }
    
    # Override specific values for the environment
  $baseSettings.ApiSettings.Environment = $Environment
    $baseSettings.AzureStorage.AccountName = $StorageAccountName
    
    # Environment-specific Redis configuration
    switch ($Environment) {
 "Development" {
       $baseSettings.Cache.Redis.Endpoint = "sxgagenteval.redis.cache.windows.net:6380"
    $baseSettings.Cache.Redis.InstanceName = "evalplatformcachedev"
        }
        "PPE" {
            $baseSettings.Cache.Redis.Endpoint = "sxgagenteval.redis.cache.windows.net:6380"
         $baseSettings.Cache.Redis.InstanceName = "evalplatformcacheppe"
        }
        "Production" {
          $baseSettings.Cache.Redis.Endpoint = "evalplatformcacheprod.redis.cache.windows.net:6380"
            $baseSettings.Cache.Redis.InstanceName = "evalplatformcacheprod"
        }
    }
    
  # Environment-specific DataVerse API configuration
    switch ($Environment) {
        "Development" {
        $baseSettings.DataVerseAPI.DatasetEnrichmentRequestAPIEndPoint = "https://sxg-eval-dev.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun"
     $baseSettings.DataVerseAPI.Scope = "https://sxg-eval-dev.crm.dynamics.com/.default"
        }
        "PPE" {
       $baseSettings.DataVerseAPI.DatasetEnrichmentRequestAPIEndPoint = "https://sxg-eval-ppe.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun"
          $baseSettings.DataVerseAPI.Scope = "https://sxg-eval-ppe.crm.dynamics.com/.default"
        }
        "Production" {
      $baseSettings.DataVerseAPI.DatasetEnrichmentRequestAPIEndPoint = "https://sxg-eval-prod.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun"
  $baseSettings.DataVerseAPI.Scope = "https://sxg-eval-prod.crm.dynamics.com/.default"
        }
    }
    
    # Convert to flat App Settings format
    $appSettings = ConvertTo-AppSettings -JsonObject $baseSettings
    
    # Add ASPNETCORE_ENVIRONMENT as the first setting
    $appSettings = @("ASPNETCORE_ENVIRONMENT=$Environment") + $appSettings
    
    Write-Host "? Merged and converted appsettings. Total settings: $($appSettings.Count)" -ForegroundColor Green
    
    return $appSettings
}

# Function to check app service health
function Test-AppServiceHealth {
    param (
     [Parameter(Mandatory=$true)]
        [string]$AppServiceUrl,
    
        [Parameter(Mandatory=$false)]
        [int]$MaxRetries = 3
    )
    
    Write-Host "Checking app service health at: $AppServiceUrl/api/v1/health/detailed" -ForegroundColor Yellow
    
    for ($i = 1; $i -le $MaxRetries; $i++) {
    try {
   Write-Host "Health check attempt $i/$MaxRetries..." -ForegroundColor Cyan
       
     $response = Invoke-RestMethod -Uri "$AppServiceUrl/api/v1/health/detailed" -Method Get -TimeoutSec 30
     
            Write-Host "? Health check response received" -ForegroundColor Green
            Write-Host "Overall Status: $($response.Status)" -ForegroundColor $(if ($response.Status -eq "Healthy") { "Green" } else { "Yellow" })
            
            # Check dependencies
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
          
            # Return health status
            if ($response.Status -eq "Healthy") {
     Write-Host "`n? All health checks passed!" -ForegroundColor Green
           return @{ Success = $true; Status = $response.Status; UnhealthyDependencies = @() }
}
            else {
 Write-Host "`n?? Application is in degraded state" -ForegroundColor Yellow
      return @{ Success = $false; Status = $response.Status; UnhealthyDependencies = $unhealthyDeps }
            }
        }
  catch {
   Write-Host "? Health check attempt $i failed: $($_.Exception.Message)" -ForegroundColor Red
          
if ($i -lt $MaxRetries) {
        Write-Host "Waiting 10 seconds before retry..." -ForegroundColor Yellow
           Start-Sleep -Seconds 10
  }
     }
    }
    
    Write-Host "`n? All health check attempts failed" -ForegroundColor Red
    return @{ Success = $false; Status = "Unreachable"; UnhealthyDependencies = @() }
}

# Main deployment script
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SXG Evaluation Platform API" -ForegroundColor Cyan
Write-Host "AUTOMATED DEPLOYMENT - Development" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Target App Service: $AppName" -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Green
Write-Host "Storage Account: $StorageAccountName" -ForegroundColor Green
Write-Host "Warmup Wait: $WarmupWaitSeconds seconds" -ForegroundColor Green
Write-Host "Health Check Wait: $HealthCheckWaitSeconds seconds" -ForegroundColor Green
Write-Host ""

# Login check
Write-Host "[1/10] Checking Azure CLI login status..." -ForegroundColor Yellow
$loginCheck = az account show --output json 2>$null
if (-not $loginCheck) {
    Write-Host "? Not logged in to Azure CLI." -ForegroundColor Red
    Write-Host "Please run: az login" -ForegroundColor Yellow
    exit 1
}

# Set subscription if provided
if ($SubscriptionId) {
    Write-Host "Setting subscription to: $SubscriptionId" -ForegroundColor Yellow
    az account set --subscription $SubscriptionId
}

$currentSub = (az account show --query name -o tsv)
Write-Host "? Using subscription: $currentSub" -ForegroundColor Green
Write-Host ""

# Step 1: Verify Resource Group exists
Write-Host "[2/10] Verifying resource group: $ResourceGroupName" -ForegroundColor Yellow
$rgExists = az group show --name $ResourceGroupName --query "name" -o tsv 2>$null
if (-not $rgExists) {
    Write-Host "? Resource group $ResourceGroupName does not exist." -ForegroundColor Red
    exit 1
}
Write-Host "? Resource group verified" -ForegroundColor Green
Write-Host ""

# Step 2: Verify App Service exists
Write-Host "[3/10] Verifying App Service: $AppName" -ForegroundColor Yellow
$appExists = az webapp show --name $AppName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null

if (-not $appExists) {
    Write-Host "? App Service $AppName does not exist" -ForegroundColor Red
    exit 1
}
Write-Host "? App Service verified: $AppName" -ForegroundColor Green
Write-Host ""

# Step 3: STOP the App Service
Write-Host "[4/10] Stopping App Service: $AppName" -ForegroundColor Yellow
Write-Host "This ensures clean deployment without active connections..." -ForegroundColor Cyan

az webapp stop --name $AppName --resource-group $ResourceGroupName --output none

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Failed to stop App Service" -ForegroundColor Red
    exit 1
}

Write-Host "? App Service stopped successfully" -ForegroundColor Green
Start-Sleep -Seconds 5
Write-Host ""

# Step 4: Read and Deploy App Settings from appsettings.json
Write-Host "[5/10] Reading and deploying App Settings from appsettings.json..." -ForegroundColor Yellow

try {
    $appSettings = Get-MergedAppSettings -Environment "Development" -StorageAccountName $StorageAccountName
    
    Write-Host "Deploying $($appSettings.Count) settings to Azure App Service..." -ForegroundColor Cyan
    
    # Display first few settings for verification
    Write-Host "`nSample settings being deployed:" -ForegroundColor Gray
    $appSettings | Select-Object -First 10 | ForEach-Object {
    $parts = $_ -split '=', 2
        Write-Host "  $($parts[0]) = $($parts[1])" -ForegroundColor Gray
    }
    Write-Host "  ... and $($appSettings.Count - 10) more settings`n" -ForegroundColor Gray
    
    az webapp config appsettings set `
        --name $AppName `
        --resource-group $ResourceGroupName `
        --settings @appSettings `
        --output none

    if ($LASTEXITCODE -ne 0) {
  Write-Host "? Failed to configure App Settings" -ForegroundColor Red
  
        # Restart the app even if settings failed
        Write-Host "?? Restarting App Service despite configuration failure..." -ForegroundColor Yellow
        az webapp start --name $AppName --resource-group $ResourceGroupName --output none
        exit 1
    }
    
    Write-Host "? App Settings configured successfully" -ForegroundColor Green
    Write-Host "   Total settings deployed: $($appSettings.Count)" -ForegroundColor Cyan
}
catch {
    Write-Host "? Error reading or deploying appsettings: $($_.Exception.Message)" -ForegroundColor Red
    
    # Restart the app even if settings failed
    Write-Host "?? Restarting App Service despite configuration failure..." -ForegroundColor Yellow
  az webapp start --name $AppName --resource-group $ResourceGroupName --output none
    exit 1
}
Write-Host ""

# Step 5: Build Application
Write-Host "[6/10] Building application..." -ForegroundColor Yellow
$projectPath = "../SXG.EvalPlatform.API.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Host "? Project file not found: $projectPath" -ForegroundColor Red
    
    # Restart the app
    az webapp start --name $AppName --resource-group $ResourceGroupName --output none
    exit 1
}

dotnet clean $projectPath --configuration Release | Out-Null
dotnet build $projectPath --configuration Release --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Build failed" -ForegroundColor Red
    
    # Restart the app
    az webapp start --name $AppName --resource-group $ResourceGroupName --output none
    exit 1
}
Write-Host "? Build successful" -ForegroundColor Green
Write-Host ""

# Step 6: Publish Application
Write-Host "[7/10] Publishing application..." -ForegroundColor Yellow
$publishPath = "./publish-dev"
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

dotnet publish $projectPath --configuration Release --output $publishPath --no-build --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Publish failed" -ForegroundColor Red
    
    # Restart the app
    az webapp start --name $AppName --resource-group $ResourceGroupName --output none
    exit 1
}
Write-Host "? Publish successful" -ForegroundColor Green
Write-Host ""

# Step 7: Create Deployment Package
Write-Host "[7/10] Creating deployment package..." -ForegroundColor Yellow
$zipPath = ".\deploy-dev.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force
$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Host "? Deployment package created: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Green
Write-Host ""

# Step 8: Deploy to Azure
Write-Host "[8/10] Deploying to Azure App Service (while stopped)..." -ForegroundColor Yellow
Write-Host "This may take a few minutes..." -ForegroundColor Cyan

az webapp deploy `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --src-path $zipPath `
    --type zip `
    --async false `
    --timeout 600

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Deployment failed" -ForegroundColor Red
    
# Restart the app anyway
    az webapp start --name $AppName --resource-group $ResourceGroupName --output none
    exit 1
}
Write-Host "? Application deployed successfully" -ForegroundColor Green
Write-Host ""

# Step 9: Wait before starting (2 minutes as requested)
Write-Host "[9/10] Waiting $WarmupWaitSeconds seconds before starting App Service..." -ForegroundColor Yellow
Write-Host "This ensures deployment artifacts are fully written..." -ForegroundColor Cyan
Start-Sleep -Seconds $WarmupWaitSeconds
Write-Host "? Wait complete" -ForegroundColor Green
Write-Host ""

# Step 10: START the App Service
Write-Host "[10/10] Starting App Service: $AppName" -ForegroundColor Yellow

az webapp start --name $AppName --resource-group $ResourceGroupName --output none

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Failed to start App Service" -ForegroundColor Red
    exit 1
}

Write-Host "? App Service started successfully" -ForegroundColor Green
Write-Host ""

# Step 11: Wait for Application Warmup
Write-Host "[11/10] Waiting $HealthCheckWaitSeconds seconds for application warmup..." -ForegroundColor Yellow
Write-Host "This allows the application to initialize..." -ForegroundColor Cyan

$appUrl = "https://$AppName.azurewebsites.net"

for ($i = $HealthCheckWaitSeconds; $i -gt 0; $i -= 10) {
    Write-Host "  $i seconds remaining..." -ForegroundColor Gray
    Start-Sleep -Seconds $(if ($i -gt 10) { 10 } else { $i })
}

Write-Host "? Warmup period complete" -ForegroundColor Green
Write-Host ""

# Step 12: Health Check Verification
Write-Host "[12/10] Running comprehensive health check..." -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan

$healthResult = Test-AppServiceHealth -AppServiceUrl $appUrl -MaxRetries 3

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

# Clean up local files
Write-Host "`nCleaning up local deployment files..." -ForegroundColor Yellow
Remove-Item $publishPath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Write-Host "? Cleanup complete" -ForegroundColor Green
Write-Host ""

# Final Deployment Summary
Write-Host "========================================" -ForegroundColor Cyan

if ($healthResult.Success) {
    Write-Host "? DEPLOYMENT SUCCESSFUL!" -ForegroundColor Green
}
else {
    Write-Host "?? DEPLOYMENT COMPLETED WITH WARNINGS" -ForegroundColor Yellow
    Write-Host "`nApplication deployed but health checks indicate issues:" -ForegroundColor Yellow
    
    if ($healthResult.UnhealthyDependencies.Count -gt 0) {
        Write-Host "`nUnhealthy Dependencies:" -ForegroundColor Red
        foreach ($dep in $healthResult.UnhealthyDependencies) {
    Write-Host "  - $($dep.Name): $($dep.ErrorMessage)" -ForegroundColor Red
        }
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Environment: Development" -ForegroundColor Cyan
Write-Host "?? Version: 1.0.0" -ForegroundColor Cyan
Write-Host "?? API URL: $appUrl" -ForegroundColor Cyan
Write-Host "?? Swagger UI: $appUrl/swagger" -ForegroundColor Cyan
Write-Host "?? Storage: $StorageAccountName" -ForegroundColor Cyan
Write-Host "?? Settings Deployed: $($appSettings.Count)" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Verify Deployment:" -ForegroundColor Yellow
Write-Host "   Health Check: $appUrl/api/v1/health" -ForegroundColor White
Write-Host "   Detailed Health: $appUrl/api/v1/health/detailed" -ForegroundColor White
Write-Host "   Default Config: $appUrl/api/v1/eval/configurations/defaultconfiguration" -ForegroundColor White
Write-Host ""
Write-Host "?? Monitor Deployment:" -ForegroundColor Yellow
Write-Host "   Portal: https://portal.azure.com" -ForegroundColor White
Write-Host "   Logs: az webapp log tail --name $AppName --resource-group $ResourceGroupName" -ForegroundColor White
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment completed at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan

# Exit with appropriate code
if ($healthResult.Success) {
    exit 0
}
else {
    Write-Host "`n?? Review the health check results above and investigate any issues" -ForegroundColor Yellow
    exit 1
}
