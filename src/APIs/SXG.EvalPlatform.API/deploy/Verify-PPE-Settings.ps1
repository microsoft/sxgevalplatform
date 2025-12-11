# Verify PPE App Settings
# This script checks that all required app settings are configured correctly in Azure

param(
    [Parameter(Mandatory=$false)]
    [string]$AppName = "sxgevalapippe",
 
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-sxg-agent-evaluation-platform"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "PPE App Settings Verification" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "App Service: $AppName" -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Green
Write-Host ""

# Check Azure login
Write-Host "[1/4] Checking Azure CLI login..." -ForegroundColor Yellow
$loginCheck = az account show --output json 2>$null
if (-not $loginCheck) {
    Write-Host "? Not logged in. Please run: az login" -ForegroundColor Red
    exit 1
}
Write-Host "? Logged in" -ForegroundColor Green
Write-Host ""

# Get current app settings
Write-Host "[2/4] Retrieving current app settings..." -ForegroundColor Yellow
$currentSettings = az webapp config appsettings list `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --output json | ConvertFrom-Json

$settingsDict = @{}
foreach ($setting in $currentSettings) {
    $settingsDict[$setting.name] = $setting.value
}

Write-Host "? Retrieved $($currentSettings.Count) settings" -ForegroundColor Green
Write-Host ""

# Define required settings
Write-Host "[3/4] Verifying required settings..." -ForegroundColor Yellow

$requiredSettings = @{
    "ASPNETCORE_ENVIRONMENT" = "PPE"
    "ApiSettings__Environment" = "PPE"
    "ApiSettings__Version" = "1.0.0"
    "AzureStorage__AccountName" = "sxgagentevalppe"
    "Cache__Provider" = "Redis"
    "Cache__Redis__Endpoint" = "sxgagenteval.redis.cache.windows.net:6380"
    "DataVerseAPI__DatasetEnrichmentRequestAPIEndPoint" = "https://sxg-eval-ppe.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun"
    "DataVerseAPI__Scope" = "https://sxg-eval-ppe.crm.dynamics.com/.default"
    "OpenTelemetry__ServiceName" = "SXG-EvalPlatform-API"
    "OpenTelemetry__CloudRoleName" = "SXG-EvalPlatform-API"
    "OpenTelemetry__EnableApplicationInsights" = "true"
}

$missingSettings = @()
$incorrectSettings = @()
$correctSettings = 0

foreach ($key in $requiredSettings.Keys) {
    if (-not $settingsDict.ContainsKey($key)) {
        $missingSettings += $key
      Write-Host "  ? MISSING: $key" -ForegroundColor Red
    }
    elseif ($settingsDict[$key] -ne $requiredSettings[$key]) {
        $incorrectSettings += @{
       Key = $key
            Expected = $requiredSettings[$key]
            Actual = $settingsDict[$key]
        }
     Write-Host "  ? INCORRECT: $key" -ForegroundColor Yellow
      Write-Host "      Expected: $($requiredSettings[$key])" -ForegroundColor Gray
    Write-Host "      Actual:   $($settingsDict[$key])" -ForegroundColor Gray
    }
    else {
        $correctSettings++
        Write-Host "  ? OK: $key" -ForegroundColor Green
    }
}

Write-Host ""

# Verification summary
Write-Host "[4/4] Verification Summary" -ForegroundColor Yellow
Write-Host "  ? Correct: $correctSettings" -ForegroundColor Green
Write-Host "  ? Incorrect: $($incorrectSettings.Count)" -ForegroundColor $(if ($incorrectSettings.Count -eq 0) { "Green" } else { "Yellow" })
Write-Host "  ? Missing: $($missingSettings.Count)" -ForegroundColor $(if ($missingSettings.Count -eq 0) { "Green" } else { "Red" })
Write-Host ""

# Additional checks
Write-Host "Additional Configuration Checks:" -ForegroundColor Cyan
Write-Host ""

# Check DataVerse is PPE (not DEV)
$dataverseEndpoint = $settingsDict["DataVerseAPI__DatasetEnrichmentRequestAPIEndPoint"]
if ($dataverseEndpoint -like "*sxg-eval-ppe*") {
    Write-Host "  ? DataVerse API: PPE environment" -ForegroundColor Green
}
elseif ($dataverseEndpoint -like "*sxg-eval-dev*") {
    Write-Host "  ? DataVerse API: WARNING - Points to DEV!" -ForegroundColor Red
}
else {
    Write-Host "  ? DataVerse API: Unknown environment" -ForegroundColor Yellow
}

# Check storage account is PPE
$storageAccount = $settingsDict["AzureStorage__AccountName"]
if ($storageAccount -eq "sxgagentevalppe") {
    Write-Host "  ? Storage Account: PPE (sxgagentevalppe)" -ForegroundColor Green
}
else {
    Write-Host "  ? Storage Account: $storageAccount (Expected: sxgagentevalppe)" -ForegroundColor Yellow
}

# Check cache provider
$cacheProvider = $settingsDict["Cache__Provider"]
Write-Host "  ? Cache Provider: $cacheProvider" -ForegroundColor $(if ($cacheProvider -eq "Memory") { "Green" } else { "Cyan" })

# Check Redis endpoint
$redisEndpoint = $settingsDict["Cache__Redis__Endpoint"]
if ($redisEndpoint -like "*sxgagenteval*") {
    Write-Host "  ? Redis Cache: PPE (shared with Dev)" -ForegroundColor Green
}
else {
    Write-Host "  ?? Redis Cache: $redisEndpoint" -ForegroundColor Yellow
}

# Check OpenTelemetry
$otelEnabled = $settingsDict["OpenTelemetry__EnableApplicationInsights"]
if ($otelEnabled -eq "true") {
    Write-Host "  ? OpenTelemetry: Enabled" -ForegroundColor Green
}
else {
    Write-Host "  ? OpenTelemetry: Disabled" -ForegroundColor Yellow
}

$cloudRole = $settingsDict["OpenTelemetry__CloudRoleName"]
Write-Host "  ? Cloud Role Name: $cloudRole" -ForegroundColor Cyan

Write-Host ""

# Final status
if ($missingSettings.Count -eq 0 -and $incorrectSettings.Count -eq 0) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "? ALL SETTINGS VERIFIED!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "? App is ready for use" -ForegroundColor Green
    Write-Host "? Health Check: https://$AppName.azurewebsites.net/api/v1/health" -ForegroundColor Cyan
    Write-Host "? Swagger UI: https://$AppName.azurewebsites.net/swagger" -ForegroundColor Cyan
}
else {
    Write-Host "========================================" -ForegroundColor Cyan
 Write-Host "? ISSUES FOUND" -ForegroundColor Yellow
  Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    
    if ($missingSettings.Count -gt 0) {
        Write-Host "Missing Settings ($($missingSettings.Count)):" -ForegroundColor Red
        foreach ($setting in $missingSettings) {
            Write-Host "  - $setting" -ForegroundColor Red
      }
   Write-Host ""
    }
    
    if ($incorrectSettings.Count -gt 0) {
        Write-Host "Incorrect Settings ($($incorrectSettings.Count)):" -ForegroundColor Yellow
        foreach ($setting in $incorrectSettings) {
         Write-Host "  - $($setting.Key)" -ForegroundColor Yellow
     Write-Host "      Expected: $($setting.Expected)" -ForegroundColor Gray
            Write-Host "      Actual:   $($setting.Actual)" -ForegroundColor Gray
  }
Write-Host ""
    }
    
    Write-Host "? Action Required:" -ForegroundColor Yellow
    Write-Host "  Run deployment script to fix: .\Deploy-To-Azure-PPE.ps1" -ForegroundColor White
}

Write-Host ""
Write-Host "Verification completed at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
