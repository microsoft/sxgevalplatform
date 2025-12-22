# Diagnostic Script for Azure App Service Deployment Issues
# This script checks App Service configuration, deployment slots, and file structure

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("EastUS2", "WestUS2")]
    [string]$Region
)

# Set error action preference
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

$appName = $regionConfig[$Region].AppName
$resourceGroup = $regionConfig[$Region].ResourceGroup

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "APP SERVICE DIAGNOSTICS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Region: $Region" -ForegroundColor Green
Write-Host "App Service: $appName" -ForegroundColor Green
Write-Host "Resource Group: $resourceGroup" -ForegroundColor Green
Write-Host ""

# Check 1: Deployment Slots
Write-Host "[1/8] Checking Deployment Slots..." -ForegroundColor Yellow
$slots = az webapp deployment slot list --name $appName --resource-group $resourceGroup --query "[].name" -o json 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | ConvertFrom-Json

if ($slots -and $slots.Count -gt 0) {
    Write-Host "  ? Found $($slots.Count) deployment slot(s):" -ForegroundColor Yellow
    foreach ($slot in $slots) {
        Write-Host "    - $slot" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "  ?? WARNING: Deployment slots detected! This might explain the issue." -ForegroundColor Yellow
    Write-Host "  Your deployment might have gone to a slot instead of production." -ForegroundColor Yellow
} else {
    Write-Host "  ? No deployment slots found (deploying to production slot)" -ForegroundColor Green
}
Write-Host ""

# Check 2: App Service Configuration
Write-Host "[2/8] Checking App Service Configuration..." -ForegroundColor Yellow
$appConfig = az webapp show --name $appName --resource-group $resourceGroup --query "{state:state, defaultHostName:defaultHostName, httpsOnly:httpsOnly, siteConfig:{netFrameworkVersion:siteConfig.netFrameworkVersion, linuxFxVersion:siteConfig.linuxFxVersion, alwaysOn:siteConfig.alwaysOn}}" -o json 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | ConvertFrom-Json

Write-Host "  State: $($appConfig.state)" -ForegroundColor $(if ($appConfig.state -eq "Running") { "Green" } else { "Red" })
Write-Host "  URL: https://$($appConfig.defaultHostName)" -ForegroundColor Cyan
Write-Host "  HTTPS Only: $($appConfig.httpsOnly)" -ForegroundColor White
Write-Host "  Always On: $($appConfig.siteConfig.alwaysOn)" -ForegroundColor White

if ($appConfig.siteConfig.netFrameworkVersion) {
    Write-Host "  .NET Framework: $($appConfig.siteConfig.netFrameworkVersion)" -ForegroundColor White
}
if ($appConfig.siteConfig.linuxFxVersion) {
    Write-Host "  Linux FX Version: $($appConfig.siteConfig.linuxFxVersion)" -ForegroundColor White
}
Write-Host ""

# Check 3: App Settings
Write-Host "[3/8] Checking Critical App Settings..." -ForegroundColor Yellow
$appSettings = az webapp config appsettings list --name $appName --resource-group $resourceGroup --query "[?name=='ASPNETCORE_ENVIRONMENT' || name=='WEBSITE_RUN_FROM_PACKAGE' || name=='SCM_DO_BUILD_DURING_DEPLOYMENT']" -o json 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | ConvertFrom-Json

foreach ($setting in $appSettings) {
    $color = if ($setting.name -eq "ASPNETCORE_ENVIRONMENT") { "Green" } elseif ($setting.name -eq "WEBSITE_RUN_FROM_PACKAGE") { "Yellow" } else { "White" }
    Write-Host "  $($setting.name) = $($setting.value)" -ForegroundColor $color
}

# Check for WEBSITE_RUN_FROM_PACKAGE
$runFromPackage = $appSettings | Where-Object { $_.name -eq "WEBSITE_RUN_FROM_PACKAGE" }
if ($runFromPackage) {
    Write-Host ""
    Write-Host "  ?? WARNING: WEBSITE_RUN_FROM_PACKAGE is set!" -ForegroundColor Yellow
    Write-Host "  This means the app is running from a ZIP package, not extracted files." -ForegroundColor Yellow
    Write-Host "  Value: $($runFromPackage.value)" -ForegroundColor White
}
Write-Host ""

# Check 4: Recent Deployments
Write-Host "[4/8] Checking Recent Deployments..." -ForegroundColor Yellow
$deployments = az webapp deployment list --name $appName --resource-group $resourceGroup --query "[0:3].{id:id, status:status, author:author, receivedTime:receivedTime}" -o json 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | ConvertFrom-Json

if ($deployments -and $deployments.Count -gt 0) {
    Write-Host "  Last 3 deployments:" -ForegroundColor Cyan
    foreach ($dep in $deployments) {
        $statusColor = if ($dep.status -eq 4) { "Green" } else { "Red" }
        $statusText = if ($dep.status -eq 4) { "Success" } else { "Failed (Status: $($dep.status))" }
        Write-Host "    $($dep.receivedTime) - $statusText by $($dep.author)" -ForegroundColor $statusColor
    }
} else {
    Write-Host "  ?? No deployment history found" -ForegroundColor Yellow
}
Write-Host ""

# Check 5: wwwroot Files via Kudu
Write-Host "[5/8] Checking wwwroot Files..." -ForegroundColor Yellow
try {
    # Get publishing credentials
    $publishProfile = az webapp deployment list-publishing-profiles --name $appName --resource-group $resourceGroup --query "[?publishMethod=='MSDeploy']" -o json 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | ConvertFrom-Json
    
    if ($publishProfile -and $publishProfile.Count -gt 0) {
        $username = $publishProfile[0].userName
        $password = $publishProfile[0].userPWD
        $kuduUrl = "https://$appName.scm.azurewebsites.net/api/vfs/site/wwwroot/"
        
        $base64AuthInfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("${username}:${password}"))
        $headers = @{
            Authorization = "Basic $base64AuthInfo"
        }
        
        $files = Invoke-RestMethod -Uri $kuduUrl -Method Get -Headers $headers -TimeoutSec 30
        
        Write-Host "  Total files/folders in wwwroot: $($files.Count)" -ForegroundColor Cyan
        
        # Check for key files
        $hasWebConfig = $files | Where-Object { $_.name -eq "web.config" }
        $hasHostingStartup = $files | Where-Object { $_.name -match "hostfxr\.dll" }
        $hasDll = $files | Where-Object { $_.name -match "\.dll$" }
        $hasApiDll = $files | Where-Object { $_.name -match "SXG\.EvalPlatform\.API\.dll" }
        
        Write-Host ""
        Write-Host "  Key files:" -ForegroundColor Cyan
        Write-Host "    web.config: $(if ($hasWebConfig) { "? Found" } else { "? Missing" })" -ForegroundColor $(if ($hasWebConfig) { "Green" } else { "Red" })
        Write-Host "    SXG.EvalPlatform.API.dll: $(if ($hasApiDll) { "? Found" } else { "? Missing" })" -ForegroundColor $(if ($hasApiDll) { "Green" } else { "Red" })
        Write-Host "    Total DLL files: $($hasDll.Count)" -ForegroundColor White
        
        if (-not $hasApiDll) {
            Write-Host ""
            Write-Host "  ?? CRITICAL: Main API DLL not found!" -ForegroundColor Red
            Write-Host "  This explains why you're seeing the default app page." -ForegroundColor Red
            Write-Host "  The deployment did not extract your application files." -ForegroundColor Red
        }
        
        # Show top-level files and folders
        Write-Host ""
        Write-Host "  Top-level contents:" -ForegroundColor Cyan
        foreach ($item in $files | Select-Object -First 15) {
            $icon = if ($item.mime -eq "inode/directory") { "??" } else { "??" }
            Write-Host "    $icon $($item.name)" -ForegroundColor Gray
        }
        if ($files.Count -gt 15) {
            Write-Host "    ... and $($files.Count - 15) more items" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "  ?? Could not access Kudu files: $($_.Exception.Message)" -ForegroundColor Yellow
}
Write-Host ""

# Check 6: Application Logs
Write-Host "[6/8] Checking Recent Application Logs..." -ForegroundColor Yellow
Write-Host "  Enabling application logging temporarily..." -ForegroundColor Cyan
az webapp log config --name $appName --resource-group $resourceGroup --application-logging filesystem --level information --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null

Write-Host "  Fetching recent logs (last 30 seconds)..." -ForegroundColor Cyan
$logJob = Start-Job -ScriptBlock {
    param($appName, $resourceGroup)
    $env:PYTHONWARNINGS = "ignore"
    az webapp log tail --name $appName --resource-group $resourceGroup 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Select-Object -First 20
} -ArgumentList $appName, $resourceGroup

Start-Sleep -Seconds 5
Stop-Job -Job $logJob
$logs = Receive-Job -Job $logJob
Remove-Job -Job $logJob

if ($logs) {
    Write-Host "  Recent log entries:" -ForegroundColor Cyan
    foreach ($log in $logs | Select-Object -First 10) {
        Write-Host "    $log" -ForegroundColor Gray
    }
} else {
    Write-Host "  ?? No recent logs found" -ForegroundColor Yellow
}
Write-Host ""

# Check 7: Test API Endpoint
Write-Host "[7/8] Testing API Endpoints..." -ForegroundColor Yellow
$appUrl = "https://$appName.azurewebsites.net"

Write-Host "  Testing root endpoint: $appUrl" -ForegroundColor Cyan
try {
    $rootResponse = Invoke-WebRequest -Uri $appUrl -Method Get -TimeoutSec 10 -MaximumRedirection 0 -ErrorAction SilentlyContinue
    Write-Host "    Status: $($rootResponse.StatusCode)" -ForegroundColor Green
    Write-Host "    Content-Type: $($rootResponse.Headers['Content-Type'])" -ForegroundColor White
    
    # Check if it's HTML (default app) or JSON/API response
    if ($rootResponse.Content -match "<!DOCTYPE html>") {
        Write-Host "    ?? WARNING: Receiving HTML response (likely default app page)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "    Status: $($_.Exception.Response.StatusCode.Value__) - $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "  Testing Swagger endpoint: $appUrl/swagger" -ForegroundColor Cyan
try {
    $swaggerResponse = Invoke-WebRequest -Uri "$appUrl/swagger" -Method Get -TimeoutSec 10 -ErrorAction SilentlyContinue
    Write-Host "    Status: $($swaggerResponse.StatusCode) ?" -ForegroundColor Green
} catch {
    $statusCode = $_.Exception.Response.StatusCode.Value__
    $statusColor = if ($statusCode -eq 404) { "Red" } else { "Yellow" }
    Write-Host "    Status: $statusCode ?" -ForegroundColor $statusColor
    if ($statusCode -eq 404) {
        Write-Host "    ?? Swagger not found - API not deployed correctly" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "  Testing Health endpoint: $appUrl/api/v1/health" -ForegroundColor Cyan
try {
    $healthResponse = Invoke-RestMethod -Uri "$appUrl/api/v1/health" -Method Get -TimeoutSec 10
    Write-Host "    Status: Success ?" -ForegroundColor Green
    Write-Host "    Response: $($healthResponse | ConvertTo-Json -Compress)" -ForegroundColor White
} catch {
    $statusCode = $_.Exception.Response.StatusCode.Value__
    Write-Host "    Status: $statusCode ?" -ForegroundColor Red
    Write-Host "    ?? Health endpoint not responding - API not running" -ForegroundColor Red
}
Write-Host ""

# Check 8: Recommendations
Write-Host "[8/8] Diagnosis Summary & Recommendations" -ForegroundColor Yellow
Write-Host ""

$issues = @()

if ($slots -and $slots.Count -gt 0) {
    $issues += "??  Deployment slots detected - ensure deploying to production"
}

if ($runFromPackage) {
    $issues += "??  WEBSITE_RUN_FROM_PACKAGE is set - app running from package not extracted files"
}

if (-not $hasApiDll) {
    $issues += "?  Main API DLL (SXG.EvalPlatform.API.dll) not found in wwwroot"
}

if ($appConfig.state -ne "Running") {
    $issues += "?  App Service not in Running state: $($appConfig.state)"
}

if ($issues.Count -gt 0) {
    Write-Host "Issues Found:" -ForegroundColor Red
    foreach ($issue in $issues) {
        Write-Host "  $issue" -ForegroundColor Yellow
    }
    Write-Host ""
    
    Write-Host "Recommended Actions:" -ForegroundColor Green
    Write-Host ""
    
    if ($runFromPackage) {
        Write-Host "1. Remove WEBSITE_RUN_FROM_PACKAGE setting:" -ForegroundColor Cyan
        Write-Host "   az webapp config appsettings delete --name $appName --resource-group $resourceGroup --setting-names WEBSITE_RUN_FROM_PACKAGE" -ForegroundColor White
        Write-Host ""
    }
    
    if (-not $hasApiDll) {
        Write-Host "2. Redeploy using the updated deployment script:" -ForegroundColor Cyan
        Write-Host "   .\Deploy-To-Azure-PROD-AUTOMATED.ps1 -Region $Region" -ForegroundColor White
        Write-Host ""
    }
    
    if ($slots -and $slots.Count -gt 0) {
        Write-Host "3. If you want to deploy to a specific slot, use:" -ForegroundColor Cyan
        Write-Host "   az webapp deployment slot list --name $appName --resource-group $resourceGroup" -ForegroundColor White
        Write-Host "   Then swap slots or deploy directly to production" -ForegroundColor White
        Write-Host ""
    }
    
    Write-Host "4. After fixing, restart the app:" -ForegroundColor Cyan
    Write-Host "   az webapp restart --name $appName --resource-group $resourceGroup" -ForegroundColor White
} else {
    Write-Host "? No critical issues detected!" -ForegroundColor Green
    Write-Host ""
    Write-Host "If you're still seeing the default page, try:" -ForegroundColor Cyan
    Write-Host "  1. Clear browser cache or use incognito mode" -ForegroundColor White
    Write-Host "  2. Wait 2-3 minutes for App Service to fully start" -ForegroundColor White
    Write-Host "  3. Check Application Insights for errors" -ForegroundColor White
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Diagnostics completed at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan
