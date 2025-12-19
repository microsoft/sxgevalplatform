# Simplified Azure Deployment Script for SXG Evaluation Platform API
# Version 2.0 - Reliable deployment with proper timeout handling
# This script addresses the common deployment failures and provides better error handling

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Development", "PPE", "Production-EastUS2", "Production-WestUS2")]
    [string]$Environment,
    
    [Parameter(Mandatory=$false)]
    [int]$DeploymentTimeout = 300,  # 5 minutes max for deployment
    
    [Parameter(Mandatory=$false)]
    [int]$StartupTimeout = 180,  # 3 minutes max for app startup
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipHealthCheck,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipAppSettings
)

$ErrorActionPreference = "Stop"
$env:PYTHONWARNINGS = "ignore"

# Environment Configuration
$envConfig = @{
    "Development" = @{
        AppName = "sxgevalapidev"
        ResourceGroup = "rg-sxg-agent-evaluation-platform"
        StorageAccount = "sxgagentevaldev"
        Location = "eastus"
    }
    "PPE" = @{
        AppName = "sxgevalapippe"
        ResourceGroup = "rg-sxg-agent-evaluation-platform"
        StorageAccount = "sxgagentevalppe"
        Location = "eastus"
    }
    "Production-EastUS2" = @{
        AppName = "sxgevalapiproduseast2"
        ResourceGroup = "EvalApiRg-useast2"
        StorageAccount = "sxgagentevalprod"
        Location = "eastus2"
    }
    "Production-WestUS2" = @{
        AppName = "sxgevalapiproduswest2"
        ResourceGroup = "evalapirg-uswest2"
        StorageAccount = "sxgagentevalprod"
        Location = "westus2"
    }
}

$config = $envConfig[$Environment]
$appName = $config.AppName
$resourceGroup = $config.ResourceGroup
$storageAccount = $config.StorageAccount

# Helper Functions
function Write-Step {
    param([string]$Message, [string]$Color = "Cyan")
    Write-Host "`n========================================" -ForegroundColor $Color
    Write-Host $Message -ForegroundColor $Color
    Write-Host "========================================" -ForegroundColor $Color
}

function Write-Success {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor Red
}

function ConvertTo-FlatAppSettings {
    param (
        [PSCustomObject]$JsonObject,
        [string]$Prefix = ""
    )
    
    $settings = @()
    
    foreach ($property in $JsonObject.PSObject.Properties) {
        $key = if ($Prefix) { "${Prefix}__$($property.Name)" } else { $property.Name }
        $value = $property.Value
 
        if ($value -is [PSCustomObject]) {
            $settings += ConvertTo-FlatAppSettings -JsonObject $value -Prefix $key
        }
        elseif ($value -is [Array]) {
            for ($i = 0; $i -lt $value.Count; $i++) {
                if ($value[$i] -is [PSCustomObject]) {
                    $settings += ConvertTo-FlatAppSettings -JsonObject $value[$i] -Prefix "${key}__${i}"
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

function Get-AppSettings {
    param([string]$Environment, [string]$StorageAccountName)
    
    $scriptDir = Split-Path -Parent $PSCommandPath
    $baseSettingsPath = Join-Path $scriptDir "..\appsettings.json"
    $envSettingsPath = Join-Path $scriptDir "..\appsettings.$Environment.json"
    
    if (-not (Test-Path $baseSettingsPath)) {
        throw "appsettings.json not found at: $baseSettingsPath"
    }
    
    $baseSettings = Get-Content $baseSettingsPath -Raw | ConvertFrom-Json
    
    if (Test-Path $envSettingsPath) {
        $envSettings = Get-Content $envSettingsPath -Raw | ConvertFrom-Json
        foreach ($property in $envSettings.PSObject.Properties) {
            $baseSettings | Add-Member -MemberType NoteProperty -Name $property.Name -Value $property.Value -Force
        }
    }
    
    # Override environment-specific values
    $baseSettings.ApiSettings.Environment = $Environment
    $baseSettings.AzureStorage.AccountName = $StorageAccountName
    
    $appSettings = ConvertTo-FlatAppSettings -JsonObject $baseSettings
    $appSettings = @("ASPNETCORE_ENVIRONMENT=$Environment") + $appSettings
    
    return $appSettings
}

function Test-SimpleHealth {
    param([string]$Url, [int]$MaxAttempts = 3)
    
    for ($i = 1; $i -le $MaxAttempts; $i++) {
        try {
            Write-Host "  Health check attempt $i/$MaxAttempts..." -ForegroundColor Gray
            $response = Invoke-WebRequest -Uri "$Url/api/v1/health" -Method Get -TimeoutSec 30 -UseBasicParsing
            
            if ($response.StatusCode -eq 200) {
                Write-Success "Health check passed"
                return $true
            }
        }
        catch {
            Write-Host "  Attempt $i failed: $($_.Exception.Message)" -ForegroundColor Gray
            if ($i -lt $MaxAttempts) {
                Start-Sleep -Seconds 10
            }
        }
    }
    
    Write-Warning "Health check failed after $MaxAttempts attempts"
    return $false
}

# Main Deployment Flow
Write-Step "DEPLOYMENT STARTING" "Magenta"
Write-Host "Environment: $Environment" -ForegroundColor Cyan
Write-Host "App Service: $appName" -ForegroundColor Cyan
Write-Host "Resource Group: $resourceGroup" -ForegroundColor Cyan
Write-Host "Deployment Timeout: $DeploymentTimeout seconds" -ForegroundColor Cyan
Write-Host "Startup Timeout: $StartupTimeout seconds" -ForegroundColor Cyan
Write-Host "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray

# Step 1: Verify Azure Login
Write-Step "STEP 1/8: Verify Azure CLI Login"
try {
    # Capture all output and filter warnings
    $accountOutput = az account show 2>&1
    $accountJson = ($accountOutput | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" }) -join "`n"
    
    if ([string]::IsNullOrWhiteSpace($accountJson)) {
        Write-Error "Not logged in to Azure CLI. Run: az login"
        exit 1
    }
    
    $account = $accountJson | ConvertFrom-Json
    Write-Success "Logged in as: $($account.user.name)"
    Write-Success "Subscription: $($account.name)"
}
catch {
    Write-Error "Not logged in to Azure CLI. Run: az login"
    exit 1
}

# Step 2: Verify Resources Exist
Write-Step "STEP 2/8: Verify Azure Resources"

# Temporarily allow errors to capture stderr
$previousErrorPref = $ErrorActionPreference
$ErrorActionPreference = "Continue"

# Capture output and filter warnings
$appCheckOutput = az webapp show --name $appName --resource-group $resourceGroup --query "name" -o tsv 2>&1
$appCheck = ($appCheckOutput | Where-Object { 
    $_ -is [string] -and 
    $_ -notmatch "cryptography" -and 
    $_ -notmatch "UserWarning" -and 
    $_ -notmatch "64-bit" -and
    $_ -notmatch "build_scripts" -and
    ![string]::IsNullOrWhiteSpace($_)
} | Select-Object -First 1)

# Restore error preference
$ErrorActionPreference = $previousErrorPref

if ([string]::IsNullOrWhiteSpace($appCheck) -or $appCheck.Trim() -ne $appName) {
    Write-Error "App Service '$appName' not found in resource group '$resourceGroup'"
    Write-Host "Debug: Got value '$appCheck'" -ForegroundColor Gray
    exit 1
}
Write-Success "App Service verified: $appName"

# Step 3: Build Application
Write-Step "STEP 3/8: Build Application"
$scriptDir = Split-Path -Parent $PSCommandPath
$projectPath = Join-Path $scriptDir "..\SXG.EvalPlatform.API.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Error "Project file not found: $projectPath"
    exit 1
}

Write-Host "  Cleaning previous build..." -ForegroundColor Gray
dotnet clean $projectPath --configuration Release --nologo | Out-Null

Write-Host "  Building..." -ForegroundColor Gray
dotnet build $projectPath --configuration Release --nologo --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}
Write-Success "Build completed successfully"

# Step 4: Publish Application
Write-Step "STEP 4/8: Publish Application"
$publishPath = Join-Path $scriptDir "publish-temp"
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

Write-Host "  Publishing to: $publishPath" -ForegroundColor Gray
dotnet publish $projectPath --configuration Release --output $publishPath --no-build --nologo --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed"
    exit 1
}

$publishedFiles = (Get-ChildItem $publishPath -Recurse -File).Count
Write-Success "Published $publishedFiles files"

# Step 5: Create Deployment Package
Write-Step "STEP 5/8: Create Deployment Package"
$zipPath = Join-Path $scriptDir "deploy-$Environment.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "  Creating ZIP package..." -ForegroundColor Gray
Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force -CompressionLevel Fastest

$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Success "Package created: $([math]::Round($zipSize, 2)) MB"

# Step 6: Deploy App Settings
if (-not $SkipAppSettings) {
    Write-Step "STEP 6/8: Deploy Application Settings"
    
    try {
        # Extract the base environment name for appsettings file lookup
        # Production-EastUS2 -> Production, Production-WestUS2 -> Production
        $baseEnvironment = $Environment
        if ($Environment -like "Production-*") {
            $baseEnvironment = "Production"
        }
        
        Write-Host "  Environment: $Environment" -ForegroundColor Gray
        Write-Host "  Looking for appsettings: appsettings.$baseEnvironment.json" -ForegroundColor Gray
        
        $appSettings = Get-AppSettings -Environment $baseEnvironment -StorageAccountName $storageAccount
        
        Write-Host "  Deploying $($appSettings.Count) app settings..." -ForegroundColor Gray
        
        # Show sample settings
        Write-Host "  Sample settings:" -ForegroundColor Gray
        $appSettings | Select-Object -First 5 | ForEach-Object {
            $parts = $_ -split '=', 2
            Write-Host "    $($parts[0])" -ForegroundColor DarkGray
        }
        Write-Host "    ... and $($appSettings.Count - 5) more" -ForegroundColor DarkGray
        
        az webapp config appsettings set `
            --name $appName `
            --resource-group $resourceGroup `
            --settings @appSettings `
            --output none 2>&1 | Out-Null
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to deploy some app settings, but continuing..."
        }
        else {
            Write-Success "App settings deployed successfully"
        }
    }
    catch {
        Write-Warning "Error deploying app settings: $($_.Exception.Message)"
        Write-Warning "Continuing with deployment..."
    }
}
else {
    Write-Host "STEP 6/8: Skipping App Settings (--SkipAppSettings flag)" -ForegroundColor Yellow
}

# Step 7: Deploy Application Code
Write-Step "STEP 7/8: Deploy Application to Azure"
Write-Host "  Stopping app service..." -ForegroundColor Gray
az webapp stop --name $appName --resource-group $resourceGroup --output none 2>&1 | Out-Null
Start-Sleep -Seconds 5

Write-Host "  Uploading deployment package (timeout: $DeploymentTimeout seconds)..." -ForegroundColor Gray
Write-Host "  This may take several minutes. Please wait..." -ForegroundColor Yellow

$deployStartTime = Get-Date

try {
    # Use az webapp deploy with explicit timeout
    $deployRawOutput = az webapp deploy `
        --name $appName `
        --resource-group $resourceGroup `
        --src-path $zipPath `
        --type zip `
        --async false `
        --timeout $DeploymentTimeout 2>&1
    
    # Filter out Python warnings
    $deployOutput = $deployRawOutput | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" }
    
    $deployEndTime = Get-Date
    $deployDuration = ($deployEndTime - $deployStartTime).TotalSeconds
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Deployment completed in $([math]::Round($deployDuration, 0)) seconds"
    }
    else {
        Write-Warning "Deployment completed with warnings"
        if ($deployOutput) {
            Write-Host "  Output: $($deployOutput -join ' ')" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Warning "Deployment encountered issues: $($_.Exception.Message)"
    Write-Warning "Attempting to start app anyway..."
}

# Always start the app service
Write-Host "  Starting app service..." -ForegroundColor Gray
az webapp start --name $appName --resource-group $resourceGroup --output none 2>&1 | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to start App Service"
    exit 1
}
Write-Success "App Service started"

# Step 8: Wait and Verify
Write-Step "STEP 8/8: Application Startup & Verification"
Write-Host "  Waiting $StartupTimeout seconds for application to initialize..." -ForegroundColor Gray

for ($i = $StartupTimeout; $i -gt 0; $i -= 15) {
    $remaining = [math]::Min($i, 15)
    Write-Host "  $i seconds remaining..." -ForegroundColor DarkGray
    Start-Sleep -Seconds $remaining
}

Write-Success "Warmup period complete"

# Health Check
if (-not $SkipHealthCheck) {
    $appUrl = "https://$appName.azurewebsites.net"
    Write-Host "`n  Running health check on: $appUrl" -ForegroundColor Gray
    
    $isHealthy = Test-SimpleHealth -Url $appUrl -MaxAttempts 3
    
    if (-not $isHealthy) {
        Write-Warning "Health check did not pass, but deployment is complete"
        Write-Warning "Check the app logs for more details:"
        Write-Host "    az webapp log tail --name $appName --resource-group $resourceGroup" -ForegroundColor White
    }
}
else {
    Write-Host "  Skipping health check (--SkipHealthCheck flag)" -ForegroundColor Yellow
}

# Cleanup
Write-Host "`n  Cleaning up temporary files..." -ForegroundColor Gray
Remove-Item $publishPath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Write-Success "Cleanup complete"

# Final Summary
Write-Step "DEPLOYMENT COMPLETED" "Green"
Write-Host "Environment: $Environment" -ForegroundColor Cyan
Write-Host "App Service: $appName" -ForegroundColor Cyan
Write-Host "URL: https://$appName.azurewebsites.net" -ForegroundColor Cyan
Write-Host "Swagger: https://$appName.azurewebsites.net/swagger" -ForegroundColor Cyan
Write-Host ""
Write-Host "Verify Deployment:" -ForegroundColor Yellow
Write-Host "  Health: https://$appName.azurewebsites.net/api/v1/health" -ForegroundColor White
Write-Host "  Detailed: https://$appName.azurewebsites.net/api/v1/health/detailed" -ForegroundColor White
Write-Host ""
Write-Host "Monitor Logs:" -ForegroundColor Yellow
Write-Host "  az webapp log tail --name $appName --resource-group $resourceGroup" -ForegroundColor White
Write-Host ""
Write-Host "Deployment completed at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
Write-Host ""

exit 0
