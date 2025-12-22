# Azure Deployment Script for SXG Evaluation Platform API - Production Environment
# Multi-Region Deployment Support with automatic appsettings.json parsing and health verification
# This script automates the entire deployment lifecycle for production regions

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("EastUS2", "WestUS2", "Both")]
    [string]$Region,
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId,

    [Parameter(Mandatory=$false)]
    [int]$WarmupWaitSeconds = 180,

    [Parameter(Mandatory=$false)]
    [int]$HealthCheckWaitSeconds = 180
)

# Set error action preference - use Continue to allow warning filtering
$ErrorActionPreference = "Continue"

# Suppress Python/cryptography warnings from Azure CLI
$env:PYTHONWARNINGS = "ignore"

# Region-specific configuration
$regionConfig = @{
    "EastUS2" = @{
        AppName = "sxgevalapiproduseast2"
        ResourceGroup = "EvalApiRg-useast2"
        Location = "eastus2"
    }
    "WestUS2" = @{
        AppName = "sxgevalapiproduswest2"
        ResourceGroup = "evalapirg-uswest2"
        Location = "westus2"
    }
}

# Common production resources (shared across regions)
$commonResources = @{
    StorageAccountName = "sxgagentevalprod"
    ServiceBusNamespace = "sxgevalframework-produseast2.servicebus.windows.net"
    RedisCacheEndpoint = "evalplatformcacheprod.redis.cache.windows.net:6380"
    RedisCacheInstanceName = "evalplatformcacheprod"
    CommonResourceGroup = "EvalCommonRg-UsEast2"
    AppInsightsConnectionString = "InstrumentationKey=c39eecfd-ae90-494d-b35f-3677169fe4b7;IngestionEndpoint=https://eastus2-3.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus2.livediagnostics.monitor.azure.com/;ApplicationId=23443e12-0895-4302-b5b6-2e2d17f12e10"
}

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
            # Handle arrays
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
        [hashtable]$CommonResources
    )
    
    # Get script directory and construct paths relative to it
    $scriptDir = Split-Path -Parent $PSCommandPath
    $baseSettingsPath = Join-Path $scriptDir "..\appsettings.json"
    $envSettingsPath = Join-Path $scriptDir "..\appsettings.$Environment.json"
    
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
    
    # Override specific values for production environment
    $baseSettings.ApiSettings.Environment = $Environment
    $baseSettings.AzureStorage.AccountName = $CommonResources.StorageAccountName
    $baseSettings.ServiceBus.EventBusConnection = $CommonResources.ServiceBusNamespace
    $baseSettings.Cache.Redis.Endpoint = $CommonResources.RedisCacheEndpoint
    $baseSettings.Cache.Redis.InstanceName = $CommonResources.RedisCacheInstanceName
    $baseSettings.Telemetry.AppInsightsConnectionString = $CommonResources.AppInsightsConnectionString
    
    # Production-specific configurations from appsettings.Production.json
    $baseSettings.Cache.Provider = "None"  # As per your production config
    $baseSettings.OpenTelemetry.EnableConsoleExporter = $false
    $baseSettings.OpenTelemetry.SamplingRatio = 0.1
    $baseSettings.FeatureFlags.EnableAuthentication = $true
    $baseSettings.FeatureFlags.EnablePublishingEvalResultsToDataPlatform = $true
    
    # Production DataVerse API configuration
    $baseSettings.DataVerseAPI.DatasetEnrichmentRequestAPIEndPoint = "https://sxg-eval-prod.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun"
    $baseSettings.DataVerseAPI.Scope = "https://sxg-eval-prod.crm.dynamics.com/.default"
    
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

# Function to deploy to a specific region
function Deploy-ToRegion {
    param (
        [Parameter(Mandatory=$true)]
        [string]$RegionName,
        
        [Parameter(Mandatory=$true)]
        [hashtable]$RegionConfig,
        
        [Parameter(Mandatory=$true)]
        [hashtable]$CommonResources,
        
        [Parameter(Mandatory=$true)]
        [string]$ScriptDir
    )
    
    $appName = $RegionConfig.AppName
    $resourceGroup = $RegionConfig.ResourceGroup
    $location = $RegionConfig.Location
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "DEPLOYING TO REGION: $RegionName" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Target App Service: $appName" -ForegroundColor Green
    Write-Host "Resource Group: $resourceGroup" -ForegroundColor Green
    Write-Host "Location: $location" -ForegroundColor Green
    Write-Host "Storage Account: $($CommonResources.StorageAccountName)" -ForegroundColor Green
    Write-Host "Service Bus: $($CommonResources.ServiceBusNamespace)" -ForegroundColor Green
    Write-Host "Redis Cache: $($CommonResources.RedisCacheEndpoint)" -ForegroundColor Green
    Write-Host ""
    
    # Step 1: Verify Resource Group exists
    Write-Host "[1/12] Verifying resource group: $resourceGroup" -ForegroundColor Yellow
    $rgExists = az group show --name $resourceGroup --query "name" -o tsv 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" }
    if (-not $rgExists) {
        Write-Host "? Resource group $resourceGroup does not exist." -ForegroundColor Red
        return $false
    }
    Write-Host "? Resource group verified" -ForegroundColor Green
    Write-Host ""
    
    # Step 2: Verify App Service exists
    Write-Host "[2/12] Verifying App Service: $appName" -ForegroundColor Yellow
    $appExists = az webapp show --name $appName --resource-group $resourceGroup --query "name" -o tsv 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" }
    
    if (-not $appExists) {
        Write-Host "? App Service $appName does not exist" -ForegroundColor Red
        return $false
    }
    Write-Host "? App Service verified: $appName" -ForegroundColor Green
    Write-Host ""
    
    # Step 3: STOP the App Service
    Write-Host "[3/12] Stopping App Service: $appName" -ForegroundColor Yellow
    Write-Host "This ensures clean deployment without active connections..." -ForegroundColor Cyan
    
    az webapp stop --name $appName --resource-group $resourceGroup --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "? Failed to stop App Service" -ForegroundColor Red
        return $false
    }
    
    Write-Host "? App Service stopped successfully" -ForegroundColor Green
    Start-Sleep -Seconds 5
    Write-Host ""
    
    # Step 4: Read and Deploy App Settings from appsettings.json
    Write-Host "[4/12] Reading and deploying App Settings from appsettings.json..." -ForegroundColor Yellow
    
    try {
        $appSettings = Get-MergedAppSettings -Environment "Production" -CommonResources $CommonResources
        
        Write-Host "Deploying $($appSettings.Count) settings to Azure App Service..." -ForegroundColor Cyan
        
        # Display first few settings for verification
        Write-Host "`nSample settings being deployed:" -ForegroundColor Gray
        $appSettings | Select-Object -First 10 | ForEach-Object {
            $parts = $_ -split '=', 2
            Write-Host "  $($parts[0]) = $($parts[1])" -ForegroundColor Gray
        }
        Write-Host "  ... and $($appSettings.Count - 10) more settings`n" -ForegroundColor Gray
        
        az webapp config appsettings set `
            --name $appName `
            --resource-group $resourceGroup `
            --settings @appSettings `
            --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "? Failed to configure App Settings" -ForegroundColor Red
            
            # Restart the app even if settings failed
            Write-Host "?? Restarting App Service despite configuration failure..." -ForegroundColor Yellow
            az webapp start --name $appName --resource-group $resourceGroup --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
            return $false
        }
        
        Write-Host "? App Settings configured successfully" -ForegroundColor Green
        Write-Host "   Total settings deployed: $($appSettings.Count)" -ForegroundColor Cyan
    }
    catch {
        Write-Host "? Error reading or deploying appsettings: $($_.Exception.Message)" -ForegroundColor Red
        
        # Restart the app even if settings failed
        Write-Host "?? Restarting App Service despite configuration failure..." -ForegroundColor Yellow
        az webapp start --name $appName --resource-group $resourceGroup --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
        return $false
    }
    Write-Host ""
    
    # Step 5: Build Application
    Write-Host "[5/12] Building application..." -ForegroundColor Yellow
    $projectPath = Join-Path $ScriptDir "..\SXG.EvalPlatform.API.csproj"
    
    if (-not (Test-Path $projectPath)) {
        Write-Host "? Project file not found: $projectPath" -ForegroundColor Red
        
        # Restart the app
        az webapp start --name $appName --resource-group $resourceGroup --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
        return $false
    }
    
    dotnet clean $projectPath --configuration Release | Out-Null
    dotnet build $projectPath --configuration Release --nologo
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "? Build failed" -ForegroundColor Red
        
        # Restart the app
        az webapp start --name $appName --resource-group $resourceGroup --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
        return $false
    }
    Write-Host "? Build successful" -ForegroundColor Green
    Write-Host ""
    
    # Step 6: Publish Application
    Write-Host "[6/12] Publishing application..." -ForegroundColor Yellow
    $publishPath = Join-Path $ScriptDir "publish-prod-$RegionName"
    if (Test-Path $publishPath) {
        Remove-Item $publishPath -Recurse -Force
    }
    
    dotnet publish $projectPath --configuration Release --output $publishPath --no-build --nologo
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "? Publish failed" -ForegroundColor Red
        
        # Restart the app
        az webapp start --name $appName --resource-group $resourceGroup --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
        return $false
    }
    Write-Host "? Publish successful" -ForegroundColor Green
    Write-Host ""
    
    # Step 7: Create Deployment Package
    Write-Host "[7/12] Creating deployment package..." -ForegroundColor Yellow
    $zipPath = Join-Path $ScriptDir "deploy-prod-$RegionName.zip"
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    
    Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force
    $zipSize = (Get-Item $zipPath).Length / 1MB
    Write-Host "? Deployment package created: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Green
    Write-Host ""
    
    # Step 8: Deploy to Azure using Kudu ZipDeploy API with SCM_DO_BUILD_DURING_DEPLOYMENT
    Write-Host "[8/12] Deploying to Azure App Service (while stopped)..." -ForegroundColor Yellow
    Write-Host "This may take a few minutes..." -ForegroundColor Cyan
    
    $deploymentFailed = $false
    try {
        # Get publishing credentials
        Write-Host "  Getting deployment credentials..." -ForegroundColor Cyan
        $publishProfile = az webapp deployment list-publishing-profiles --name $appName --resource-group $resourceGroup --query "[?publishMethod=='MSDeploy']" -o json 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | ConvertFrom-Json
        
        if (-not $publishProfile -or $publishProfile.Count -eq 0) {
            Write-Host "  ? Failed to get publishing credentials" -ForegroundColor Red
            $deploymentFailed = $true
        }
        else {
            $username = $publishProfile[0].userName
            $password = $publishProfile[0].userPWD
            $kuduUrl = "https://$appName.scm.azurewebsites.net/api/zipdeploy"
            
            Write-Host "  ? Credentials retrieved" -ForegroundColor Green
            Write-Host "  Uploading deployment package to Kudu..." -ForegroundColor Cyan
            
            # Create credentials
            $base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${username}:${password}"))
            $headers = @{
                Authorization = "Basic $base64AuthInfo"
            }
            
            # Upload ZIP file using Kudu API with synchronous deployment
            Write-Host "  Using synchronous deployment to wwwroot..." -ForegroundColor Cyan
            $response = Invoke-RestMethod -Uri $kuduUrl -Method Post -InFile $zipPath -Headers $headers -ContentType "application/zip" -TimeoutSec 600 -ErrorAction Stop
            
            Write-Host "  ? Package uploaded successfully" -ForegroundColor Green
            Write-Host "  Waiting for deployment to complete..." -ForegroundColor Cyan
            Start-Sleep -Seconds 15
        }
    }
    catch {
        Write-Host "  ? Deployment exception: $($_.Exception.Message)" -ForegroundColor Red
        $deploymentFailed = $true
    }
    
    if ($deploymentFailed) {
        Write-Host ""
        Write-Host "? Deployment failed!" -ForegroundColor Red
        
        # Restart the app anyway
        Write-Host "?? Restarting App Service despite deployment failure..." -ForegroundColor Yellow
        az webapp start --name $appName --resource-group $resourceGroup --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
        return $false
    }
    
    Write-Host "? Application deployed successfully" -ForegroundColor Green
    Write-Host ""
    
    # Verify deployment by checking file system
    Write-Host "Verifying deployment files..." -ForegroundColor Cyan
    Start-Sleep -Seconds 10
    
    try {
        # Use Kudu API to verify files were deployed with proper authentication
        $kuduVfsUrl = "https://$appName.scm.azurewebsites.net/api/vfs/site/wwwroot/"
        $base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${username}:${password}"))
        $vfsHeaders = @{
            Authorization = "Basic $base64AuthInfo"
        }
        
        $response = Invoke-RestMethod -Uri $kuduVfsUrl -Method Get -Headers $vfsHeaders -TimeoutSec 10 -ErrorAction Stop
        
        if ($response -and $response.Count -gt 1) {
            Write-Host "  ? Verified: $($response.Count) files/folders deployed" -ForegroundColor Green
            
            # Look for key files
            $hasWebConfig = $response | Where-Object { $_.name -eq "web.config" }
            $hasDll = $response | Where-Object { $_.name -match "\.dll$" }
            $hasApiDll = $response | Where-Object { $_.name -match "SXG\.EvalPlatform\.API\.dll" }
            
            if ($hasWebConfig) {
                Write-Host "  ? web.config found" -ForegroundColor Green
            }
            if ($hasDll) {
                Write-Host "  ? Application DLLs found ($($hasDll.Count) DLL files)" -ForegroundColor Green
            }
            if ($hasApiDll) {
                Write-Host "  ? Main API DLL found (SXG.EvalPlatform.API.dll)" -ForegroundColor Green
            }
            else {
                Write-Host "  ?? Warning: Main API DLL not found!" -ForegroundColor Yellow
            }
        }
        else {
            Write-Host "  ?? Warning: Few or no files detected in wwwroot" -ForegroundColor Yellow
            Write-Host "  This might indicate deployment didn't complete properly" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "  ?? Could not verify file deployment (non-fatal): $($_.Exception.Message)" -ForegroundColor Yellow
    }
    Write-Host ""
    
    # Step 9: Wait before starting (reduced since deployment is synchronous)
    Write-Host "[9/12] Waiting 30 seconds before starting App Service..." -ForegroundColor Yellow
    Write-Host "This ensures deployment artifacts are fully written..." -ForegroundColor Cyan
    Start-Sleep -Seconds 30
    Write-Host "? Wait complete" -ForegroundColor Green
    Write-Host ""
    
    # Step 10: START the App Service
    Write-Host "[10/12] Starting App Service: $appName" -ForegroundColor Yellow
    
    az webapp start --name $appName --resource-group $resourceGroup --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "? Failed to start App Service" -ForegroundColor Red
        return $false
    }
    
    Write-Host "? App Service started successfully" -ForegroundColor Green
    Write-Host ""
    
    # Step 11: Wait for Application Warmup
    Write-Host "[11/12] Waiting $HealthCheckWaitSeconds seconds for application warmup..." -ForegroundColor Yellow
    Write-Host "This allows the application to initialize..." -ForegroundColor Cyan
    
    $appUrl = "https://$appName.azurewebsites.net"
    
    for ($i = $HealthCheckWaitSeconds; $i -gt 0; $i -= 10) {
        Write-Host "  $i seconds remaining..." -ForegroundColor Gray
        Start-Sleep -Seconds $(if ($i -gt 10) { 10 } else { $i })
    }
    
    Write-Host "? Warmup period complete" -ForegroundColor Green
    Write-Host ""
    
    # Step 12: Health Check Verification
    Write-Host "[12/12] Running comprehensive health check..." -ForegroundColor Yellow
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
    
    # Deployment Summary for this region
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "DEPLOYMENT SUMMARY - $RegionName" -ForegroundColor Cyan
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
    
    Write-Host ""
    Write-Host "?? Region: $RegionName ($location)" -ForegroundColor Cyan
    Write-Host "?? App Service: $appName" -ForegroundColor Cyan
    Write-Host "?? Resource Group: $resourceGroup" -ForegroundColor Cyan
    Write-Host "?? API URL: $appUrl" -ForegroundColor Cyan
    Write-Host "?? Swagger UI: $appUrl/swagger" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "?? Verify Deployment:" -ForegroundColor Yellow
    Write-Host "   Health Check: $appUrl/api/v1/health" -ForegroundColor White
    Write-Host "   Detailed Health: $appUrl/api/v1/health/detailed" -ForegroundColor White
    Write-Host "   Default Config: $appUrl/api/v1/eval/configurations/defaultconfiguration" -ForegroundColor White
    Write-Host ""
    
    return $healthResult.Success
}

# Main deployment script
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SXG Evaluation Platform API" -ForegroundColor Cyan
Write-Host "AUTOMATED PRODUCTION DEPLOYMENT" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Deployment Configuration:" -ForegroundColor Cyan
Write-Host "  Region: $Region" -ForegroundColor Green
Write-Host "  Environment: Production" -ForegroundColor Green
Write-Host "  Warmup Wait: $WarmupWaitSeconds seconds" -ForegroundColor Green
Write-Host "  Health Check Wait: $HealthCheckWaitSeconds seconds" -ForegroundColor Green
Write-Host ""

# Login check
Write-Host "Checking Azure CLI login status..." -ForegroundColor Yellow
$loginCheck = az account show --output json 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" }
if (-not $loginCheck) {
    Write-Host "? Not logged in to Azure CLI." -ForegroundColor Red
    Write-Host "Please run: az login" -ForegroundColor Yellow
    exit 1
}

# Set subscription if provided
if ($SubscriptionId) {
    Write-Host "Setting subscription to: $SubscriptionId" -ForegroundColor Yellow
    az account set --subscription $SubscriptionId 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
}

$currentSub = (az account show --query name -o tsv 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" })
Write-Host "? Using subscription: $currentSub" -ForegroundColor Green
Write-Host ""

# Get script directory
$scriptDir = Split-Path -Parent $PSCommandPath

# Track deployment results
$deploymentResults = @{}
$overallSuccess = $true

# Deploy to specified region(s)
if ($Region -eq "Both") {
    Write-Host "?? DEPLOYING TO BOTH REGIONS" -ForegroundColor Magenta
    Write-Host ""
    
    # Deploy to East US 2 first
    Write-Host "Starting deployment to East US 2..." -ForegroundColor Magenta
    $eastDeployment = Deploy-ToRegion -RegionName "EastUS2" -RegionConfig $regionConfig["EastUS2"] -CommonResources $commonResources -ScriptDir $scriptDir
    $deploymentResults["EastUS2"] = $eastDeployment
    $overallSuccess = $overallSuccess -and $eastDeployment
    
    Write-Host ""
    Write-Host "? Waiting 30 seconds before deploying to next region..." -ForegroundColor Yellow
    Start-Sleep -Seconds 30
    Write-Host ""
    
    # Deploy to West US 2
    Write-Host "Starting deployment to West US 2..." -ForegroundColor Magenta
    $westDeployment = Deploy-ToRegion -RegionName "WestUS2" -RegionConfig $regionConfig["WestUS2"] -CommonResources $commonResources -ScriptDir $scriptDir
    $deploymentResults["WestUS2"] = $westDeployment
    $overallSuccess = $overallSuccess -and $westDeployment
}
else {
    # Deploy to single region
    Write-Host "?? DEPLOYING TO SINGLE REGION: $Region" -ForegroundColor Magenta
    Write-Host ""
    
    $singleDeployment = Deploy-ToRegion -RegionName $Region -RegionConfig $regionConfig[$Region] -CommonResources $commonResources -ScriptDir $scriptDir
    $deploymentResults[$Region] = $singleDeployment
    $overallSuccess = $singleDeployment
}

# Final Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "OVERALL DEPLOYMENT SUMMARY" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Environment: Production" -ForegroundColor Cyan
Write-Host "Deployment Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host ""
Write-Host "Region Deployment Status:" -ForegroundColor Cyan
foreach ($region in $deploymentResults.Keys) {
    $status = if ($deploymentResults[$region]) { "? SUCCESS" } else { "? FAILED" }
    $color = if ($deploymentResults[$region]) { "Green" } else { "Red" }
    Write-Host "  $region : $status" -ForegroundColor $color
}
Write-Host ""

if ($overallSuccess) {
    Write-Host "? ALL DEPLOYMENTS COMPLETED SUCCESSFULLY!" -ForegroundColor Green
    Write-Host ""
    Write-Host "?? Common Production Resources:" -ForegroundColor Cyan
    Write-Host "   Storage Account: $($commonResources.StorageAccountName)" -ForegroundColor White
    Write-Host "   Service Bus: $($commonResources.ServiceBusNamespace)" -ForegroundColor White
    Write-Host "   Redis Cache: $($commonResources.RedisCacheEndpoint)" -ForegroundColor White
    Write-Host "   Common Resource Group: $($commonResources.CommonResourceGroup)" -ForegroundColor White
    Write-Host ""
    Write-Host "?? Monitor Deployments:" -ForegroundColor Yellow
    Write-Host "   Portal: https://portal.azure.com" -ForegroundColor White
    Write-Host ""
    foreach ($region in $deploymentResults.Keys) {
        $appName = $regionConfig[$region].AppName
        $resourceGroup = $regionConfig[$region].ResourceGroup
        Write-Host "   $region Logs: az webapp log tail --name $appName --resource-group $resourceGroup" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    exit 0
}
else {
    Write-Host "?? SOME DEPLOYMENTS FAILED OR COMPLETED WITH WARNINGS" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please review the deployment logs above for details." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    exit 1
}
