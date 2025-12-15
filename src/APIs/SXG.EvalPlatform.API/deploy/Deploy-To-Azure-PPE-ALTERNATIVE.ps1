# Alternative PPE Deployment Script with Enhanced Error Handling
# Uses different deployment strategies to resolve Status 400 issues
# UPDATED: Now includes automatic appsettings.json parsing

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-sxg-agent-evaluation-platform",
    
    [Parameter(Mandatory=$false)]
    [string]$AppName = "sxgevalapippe",
    
    [Parameter(Mandatory=$false)]
    [string]$StorageAccountName = "sxgagentevalppe",
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory=$false)]
    [int]$WarmupWaitSeconds = 120,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipHealthCheck,
    
    [Parameter(Mandatory=$false)]
    [string]$DeploymentUsername,
    
    [Parameter(Mandatory=$false)]
    [string]$DeploymentPassword
)

$ErrorActionPreference = "Continue"
$env:PYTHONWARNINGS = "ignore"

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
        [string]$StorageAccountName
    )
    
    # Get script directory and construct paths relative to it
    $scriptDir = Split-Path -Parent $PSCommandPath
    $baseSettingsPath = Join-Path $scriptDir "..\appsettings.json"
    $envSettingsPath = Join-Path $scriptDir "..\appsettings.$Environment.json"
    
    Write-Host "?? Reading base appsettings from: $baseSettingsPath" -ForegroundColor Cyan
    
    if (-not (Test-Path $baseSettingsPath)) {
        throw "Base appsettings.json not found at: $baseSettingsPath"
    }
    
    # Read and parse base settings
    $baseSettings = Get-Content $baseSettingsPath -Raw | ConvertFrom-Json
    
    # Read and merge environment-specific settings if they exist
    if (Test-Path $envSettingsPath) {
        Write-Host "?? Reading environment-specific appsettings from: $envSettingsPath" -ForegroundColor Cyan
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

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Alternative PPE Deployment Script" -ForegroundColor Cyan
Write-Host "Status 400 Error Resolution - v2.0" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Target App Service: $AppName" -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Green
Write-Host "Storage Account: $StorageAccountName" -ForegroundColor Green
Write-Host ""

# Step 1: Login Check
Write-Host "[1/10] Checking Azure CLI login..." -ForegroundColor Yellow
try {
    $loginCheck = az account show --output json 2>$null
    if (-not $loginCheck) {
        Write-Host "? Not logged in. Please run: az login" -ForegroundColor Red
        exit 1
    }
    Write-Host "? Azure CLI authenticated" -ForegroundColor Green
} catch {
    Write-Host "? Login check failed: $($_.Exception.Message)" -ForegroundColor Red
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

# Step 2: Verify Resource Group exists
Write-Host "[2/10] Verifying resource group: $ResourceGroupName" -ForegroundColor Yellow
$rgExists = az group show --name $ResourceGroupName --query "name" -o tsv 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" }
if (-not $rgExists) {
    Write-Host "? Resource group $ResourceGroupName does not exist." -ForegroundColor Red
    exit 1
}
Write-Host "? Resource group verified" -ForegroundColor Green
Write-Host ""

# Step 3: Verify App Service exists and check configuration
Write-Host "[3/10] Verifying App Service: $AppName" -ForegroundColor Yellow
$appInfo = az webapp show --name $AppName --resource-group $ResourceGroupName --query "{name:name, state:state, sku:appServicePlanId, kind:kind, enabledHostNames:enabledHostNames}" -o json 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | ConvertFrom-Json

if (-not $appInfo.name) {
    Write-Host "? App Service $AppName does not exist" -ForegroundColor Red
    exit 1
}

Write-Host "? App Service verified: $($appInfo.name)" -ForegroundColor Green
Write-Host "   State: $($appInfo.state)" -ForegroundColor Cyan
Write-Host "   Kind: $($appInfo.kind)" -ForegroundColor Cyan
Write-Host "   Hostname: $($appInfo.enabledHostNames[0])" -ForegroundColor Cyan

# Check App Service Plan
if ($appInfo.sku) {
    $planName = Split-Path $appInfo.sku -Leaf
    $planInfo = az appservice plan show --name $planName --resource-group $ResourceGroupName --query "{sku:sku.name, tier:sku.tier, capacity:sku.capacity}" -o json 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | ConvertFrom-Json
    
    if ($planInfo) {
        Write-Host "   Service Plan: $planName ($($planInfo.sku) - $($planInfo.tier))" -ForegroundColor Cyan
        
        if ($planInfo.tier -eq "Free" -or $planInfo.tier -eq "Shared") {
            Write-Host "   ?? Warning: Free/Shared tier may have deployment limitations" -ForegroundColor Yellow
        }
    }
}
Write-Host ""

# Step 4: Get Deployment Credentials EARLY (before stopping app)
Write-Host "[4/10] Retrieving deployment credentials..." -ForegroundColor Yellow

if ($DeploymentUsername -and $DeploymentPassword) {
    Write-Host "? Using provided deployment credentials" -ForegroundColor Green
    $deploymentCreds = @{
        publishingUserName = $DeploymentUsername
        publishingPassword = $DeploymentPassword
        username = $DeploymentUsername
        password = $DeploymentPassword
    }
} else {
    try {
        Write-Host "   Fetching credentials from Azure..." -ForegroundColor Cyan
        $credsJson = az webapp deployment list-publishing-credentials --name $AppName --resource-group $ResourceGroupName -o json 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" }
        
        if ($credsJson -and $credsJson -notmatch "ERROR" -and $credsJson -notmatch "Unauthorized") {
            $deploymentCreds = $credsJson | ConvertFrom-Json
            Write-Host "? Credentials retrieved successfully" -ForegroundColor Green
            Write-Host "   Username: $($deploymentCreds.publishingUserName)" -ForegroundColor Cyan
        } else {
            Write-Host "? Failed to retrieve credentials via primary method" -ForegroundColor Yellow
            Write-Host "   Trying publish profile method..." -ForegroundColor Cyan
            
            # Try publish profile as fallback
            $publishProfile = az webapp deployment list-publishing-profiles --name $AppName --resource-group $ResourceGroupName --query "[?publishMethod=='MSDeploy']" -o json 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" }
            
            if ($publishProfile) {
                $profile = $publishProfile | ConvertFrom-Json
                if ($profile -and $profile.Count -gt 0) {
                    $deploymentCreds = @{
                        publishingUserName = $profile[0].userName
                        publishingPassword = $profile[0].userPWD
                        username = $profile[0].userName
                        password = $profile[0].userPWD
                    }
                    Write-Host "? Credentials retrieved via publish profile" -ForegroundColor Green
                }
            }
        }
    } catch {
        Write-Host "? Error retrieving credentials: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    if (-not $deploymentCreds -or (-not $deploymentCreds.username -and -not $deploymentCreds.publishingUserName)) {
        Write-Host "" -ForegroundColor Red
        Write-Host "============================================" -ForegroundColor Red
        Write-Host "? CREDENTIAL RETRIEVAL FAILED" -ForegroundColor Red
        Write-Host "============================================" -ForegroundColor Red
        Write-Host "" -ForegroundColor Yellow
        Write-Host "Possible causes:" -ForegroundColor Yellow
        Write-Host "  1. Azure CLI token expired - try: az logout && az login" -ForegroundColor White
        Write-Host "  2. Wrong subscription selected" -ForegroundColor White
        Write-Host "  3. Permissions not yet propagated (wait 5-10 minutes)" -ForegroundColor White
        Write-Host "  4. Resource lock preventing access" -ForegroundColor White
        Write-Host "" -ForegroundColor Yellow
        Write-Host "Diagnostic commands:" -ForegroundColor Yellow
        Write-Host "  Check your access:" -ForegroundColor White
        Write-Host "    az role assignment list --resource-group $ResourceGroupName --assignee `$(az account show --query user.name -o tsv)" -ForegroundColor Cyan
        Write-Host "  Check for locks:" -ForegroundColor White
        Write-Host "    az lock list --resource-group $ResourceGroupName -o table" -ForegroundColor Cyan
        Write-Host "  Verify subscription:" -ForegroundColor White
        Write-Host "    az account show" -ForegroundColor Cyan
        Write-Host "" -ForegroundColor Yellow
        Write-Host "============================================" -ForegroundColor Red
        exit 1
    }
}
Write-Host ""

# Step 5: STOP the App Service
Write-Host "[5/10] Stopping App Service: $AppName" -ForegroundColor Yellow
Write-Host "This ensures clean deployment without active connections..." -ForegroundColor Cyan

az webapp stop --name $AppName --resource-group $ResourceGroupName --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Failed to stop App Service" -ForegroundColor Red
    exit 1
}

Write-Host "? App Service stopped successfully" -ForegroundColor Green
Start-Sleep -Seconds 5
Write-Host ""

# Step 6: Read and Deploy App Settings from appsettings.json
Write-Host "[6/10] Reading and deploying App Settings from appsettings.json..." -ForegroundColor Yellow

try {
    $appSettings = Get-MergedAppSettings -Environment "PPE" -StorageAccountName $StorageAccountName
    
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
        --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null

    if ($LASTEXITCODE -ne 0) {
        Write-Host "? Failed to configure App Settings" -ForegroundColor Red
        
        # Restart the app even if settings failed
        Write-Host "?? Restarting App Service despite configuration failure..." -ForegroundColor Yellow
        az webapp start --name $AppName --resource-group $ResourceGroupName --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
        exit 1
    }
    
    Write-Host "? App Settings configured successfully" -ForegroundColor Green
    Write-Host "   Total settings deployed: $($appSettings.Count)" -ForegroundColor Cyan
}
catch {
    Write-Host "? Error reading or deploying appsettings: $($_.Exception.Message)" -ForegroundColor Red
    
    # Restart the app even if settings failed
    Write-Host "?? Restarting App Service despite configuration failure..." -ForegroundColor Yellow
    az webapp start --name $AppName --resource-group $ResourceGroupName --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
    exit 1
}
Write-Host ""

# Step 7: Build Application
Write-Host "[7/10] Building application..." -ForegroundColor Yellow
$scriptDir = Split-Path -Parent $PSCommandPath
$projectPath = Join-Path $scriptDir "..\SXG.EvalPlatform.API.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Host "? Project file not found: $projectPath" -ForegroundColor Red
    
    # Restart the app
    az webapp start --name $AppName --resource-group $ResourceGroupName --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
    exit 1
}

dotnet clean $projectPath --configuration Release | Out-Null
dotnet build $projectPath --configuration Release --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Build failed" -ForegroundColor Red
    
    # Restart the app
    az webapp start --name $AppName --resource-group $ResourceGroupName --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
    exit 1
}
Write-Host "? Build successful" -ForegroundColor Green
Write-Host ""

# Step 8: Publish Application
Write-Host "[8/10] Publishing application..." -ForegroundColor Yellow
$scriptDir = Split-Path -Parent $PSCommandPath
$publishPath = Join-Path $scriptDir "publish-ppe"
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

dotnet publish $projectPath --configuration Release --output $publishPath --no-build --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Publish failed" -ForegroundColor Red
    
    # Restart the app
    az webapp start --name $AppName --resource-group $ResourceGroupName --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
    exit 1
}
Write-Host "? Publish successful" -ForegroundColor Green
Write-Host ""

# Step 9: Create Deployment Package
Write-Host "[9/10] Creating deployment package..." -ForegroundColor Yellow
$zipPath = Join-Path $scriptDir "deploy-ppe.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force
$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Host "? Deployment package created: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Green
Write-Host ""

# Step 10: Deploy to Azure
Write-Host "[10/10] Deploying to Azure App Service (while stopped)..." -ForegroundColor Yellow
Write-Host "This may take a few minutes..." -ForegroundColor Cyan

# Try Method 1: Standard az webapp deploy
Write-Host "?? Attempting Method 1: az webapp deploy..." -ForegroundColor Cyan
$deployOutput = az webapp deploy `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --src-path $zipPath `
    --type zip `
    --async false `
    --timeout 600 2>&1

$deploySuccess = $LASTEXITCODE -eq 0

if (-not $deploySuccess) {
Write-Host "? Method 1 failed with Status 400" -ForegroundColor Red
Write-Host "?? Retrieving detailed error information from Kudu..." -ForegroundColor Yellow
    
# Use the credentials we retrieved earlier
$creds = $deploymentCreds
    
# Get deployment details from Kudu
try {
    $username = if ($creds.publishingUserName) { $creds.publishingUserName } else { $creds.username }
    $password = if ($creds.publishingPassword) { $creds.publishingPassword } else { $creds.password }
        
    if ($username -and $password) {
        $bytes = [System.Text.Encoding]::ASCII.GetBytes("${username}:${password}")
        $encodedCreds = [System.Convert]::ToBase64String($bytes)
            
        $headers = @{
            "Authorization" = "Basic $encodedCreds"
        }
            
        # Get latest deployment info
        $deploymentInfo = Invoke-RestMethod -Uri "https://$AppName.scm.azurewebsites.net/api/deployments/latest" -Headers $headers -Method Get -ErrorAction SilentlyContinue
            
        if ($deploymentInfo) {
            Write-Host "`n?? Deployment Error Details:" -ForegroundColor Yellow
            Write-Host "   Status: $($deploymentInfo.status)" -ForegroundColor White
            Write-Host "   Message: $($deploymentInfo.message)" -ForegroundColor White
            Write-Host "   Author: $($deploymentInfo.author)" -ForegroundColor White
                
            if ($deploymentInfo.log_url) {
                Write-Host "`n?? Fetching deployment logs..." -ForegroundColor Yellow
                $logs = Invoke-RestMethod -Uri $deploymentInfo.log_url -Headers $headers -Method Get -ErrorAction SilentlyContinue
                if ($logs) {
                    Write-Host "`nDeployment Logs:" -ForegroundColor Cyan
                    $logs | ForEach-Object {
                        Write-Host "   [$($_.log_time)] $($_.message)" -ForegroundColor Gray
                    }
                }
            }
        }
    }
} catch {
    Write-Host "Could not retrieve detailed error information: $($_.Exception.Message)" -ForegroundColor Gray
}
    
Write-Host "`n?? Attempting Method 2: Direct ZipDeploy API..." -ForegroundColor Yellow
    
try {
    # Use publishingPassword field for Kudu API
    $username = if ($creds.publishingUserName) { $creds.publishingUserName } else { $creds.username }
    $password = if ($creds.publishingPassword) { $creds.publishingPassword } else { $creds.password }
        
        if ($username -and $password) {
            Write-Host "   Using credentials for: $username" -ForegroundColor Cyan
            
            $bytes = [System.Text.Encoding]::ASCII.GetBytes("${username}:${password}")
            $encodedCreds = [System.Convert]::ToBase64String($bytes)
            
            $headers = @{
                "Authorization" = "Basic $encodedCreds"
                "Content-Type" = "application/octet-stream"
            }
            
            Write-Host "   Reading deployment package..." -ForegroundColor Cyan
            $fileBytes = [System.IO.File]::ReadAllBytes($zipPath)
            Write-Host "   Package size: $([math]::Round($fileBytes.Length / 1MB, 2)) MB" -ForegroundColor Cyan
            
            Write-Host "   Uploading to ZipDeploy API..." -ForegroundColor Cyan
            $uri = "https://$AppName.scm.azurewebsites.net/api/zipdeploy"
            
            $response = Invoke-WebRequest -Uri $uri -Method Post -Body $fileBytes -Headers $headers -TimeoutSec 900 -UseBasicParsing
            
            if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 202) {
                Write-Host "? Method 2: ZipDeploy API successful!" -ForegroundColor Green
                $deploySuccess = $true
            } else {
                Write-Host "? Method 2 failed with status: $($response.StatusCode)" -ForegroundColor Red
                Write-Host "   Response: $($response.Content)" -ForegroundColor Gray
            }
        } else {
            Write-Host "? No valid credentials available for Method 2" -ForegroundColor Red
        }
    } catch {
        Write-Host "? Method 2 failed: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.Exception.Response) {
            Write-Host "   Status Code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Gray
        }
        
        # Try Method 3: OneDeploy API (no server-side build)
        Write-Host "`n?? Attempting Method 3: OneDeploy API (pre-built package)..." -ForegroundColor Yellow
        
        try {
            if ($username -and $password) {
                $bytes = [System.Text.Encoding]::ASCII.GetBytes("${username}:${password}")
                $encodedCreds = [System.Convert]::ToBase64String($bytes)
                
                $headers = @{
                    "Authorization" = "Basic $encodedCreds"
                    "Content-Type" = "application/zip"
                }
                
                Write-Host "   Reading deployment package..." -ForegroundColor Cyan
                $fileBytes = [System.IO.File]::ReadAllBytes($zipPath)
                Write-Host "   Package size: $([math]::Round($fileBytes.Length / 1MB, 2)) MB" -ForegroundColor Cyan
                
                Write-Host "   Uploading to OneDeploy API (skips server builds)..." -ForegroundColor Cyan
                # Use OneDeploy API with type=zip to deploy pre-built package without building
                $uri = "https://$AppName.scm.azurewebsites.net/api/publish?type=zip"
                
                $response = Invoke-WebRequest -Uri $uri -Method Post -Body $fileBytes -Headers $headers -TimeoutSec 900 -UseBasicParsing
                
                if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 202) {
                    Write-Host "? Method 3: OneDeploy API successful!" -ForegroundColor Green
                    $deploySuccess = $true
                } else {
                    Write-Host "? Method 3 failed with status: $($response.StatusCode)" -ForegroundColor Red
                    Write-Host "   Response: $($response.Content)" -ForegroundColor Gray
                }
            } else {
                Write-Host "? No valid credentials available for Method 3" -ForegroundColor Red
            }
        } catch {
            Write-Host "? Method 3 failed: $($_.Exception.Message)" -ForegroundColor Red
            if ($_.Exception.Response) {
                Write-Host "   Status Code: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Gray
                Write-Host "   Response: $($_.Exception.Response.StatusDescription)" -ForegroundColor Gray
            }
        }
    }
}

if (-not $deploySuccess) {
    Write-Host "`n? All deployment methods failed" -ForegroundColor Red
    Write-Host "?? Please check:" -ForegroundColor Yellow
    Write-Host "   1. App Service plan has enough resources" -ForegroundColor White
    Write-Host "   2. No deployment locks on the resource group" -ForegroundColor White
    Write-Host "   3. Kudu service is responsive: https://$AppName.scm.azurewebsites.net" -ForegroundColor White
    Write-Host "   4. Check Azure Portal for any service issues" -ForegroundColor White
    
    # Restart the app anyway
    az webapp start --name $AppName --resource-group $ResourceGroupName --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null
    exit 1
} else {
    Write-Host "? Application deployed successfully" -ForegroundColor Green
}
Write-Host ""

# Step 11: START the App Service and Warmup
Write-Host "[11/11] Starting App Service: $AppName" -ForegroundColor Yellow

az webapp start --name $AppName --resource-group $ResourceGroupName --output none 2>&1 | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" } | Out-Null

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Failed to start App Service" -ForegroundColor Red
    exit 1
}

Write-Host "? App Service started successfully" -ForegroundColor Green

# Wait for Application Warmup
if (-not $SkipHealthCheck) {
    Write-Host "`nWaiting $WarmupWaitSeconds seconds for application warmup..." -ForegroundColor Yellow
    Write-Host "This allows the application to initialize..." -ForegroundColor Cyan

    $appUrl = "https://$AppName.azurewebsites.net"

    for ($i = $WarmupWaitSeconds; $i -gt 0; $i -= 10) {
        Write-Host "  $i seconds remaining..." -ForegroundColor Gray
        Start-Sleep -Seconds $(if ($i -gt 10) { 10 } else { $i })
    }

    Write-Host "? Warmup period complete" -ForegroundColor Green
    
    # Quick health check
    Write-Host "`nPerforming quick health check..." -ForegroundColor Yellow
    try {
        $healthResponse = Invoke-RestMethod -Uri "$appUrl/api/v1/health" -Method Get -TimeoutSec 30 -ErrorAction Stop
        Write-Host "? Health check passed: $($healthResponse.status)" -ForegroundColor Green
    } catch {
        Write-Host "?? Health check not available yet (this is normal): $($_.Exception.Message)" -ForegroundColor Yellow
    }
}
Write-Host ""

# Cleanup
Write-Host "`nCleaning up local deployment files..." -ForegroundColor Yellow
Remove-Item $publishPath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Write-Host "? Cleanup complete" -ForegroundColor Green
Write-Host ""

# Final Deployment Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? DEPLOYMENT SUCCESSFUL!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Environment: PPE" -ForegroundColor Cyan
Write-Host "?? Version: 1.0.0" -ForegroundColor Cyan
Write-Host "?? API URL: https://$AppName.azurewebsites.net" -ForegroundColor Cyan
Write-Host "?? Swagger UI: https://$AppName.azurewebsites.net/swagger" -ForegroundColor Cyan
Write-Host "?? Storage: $StorageAccountName" -ForegroundColor Cyan
Write-Host "?? Settings Deployed: $($appSettings.Count)" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Verify Deployment:" -ForegroundColor Yellow
Write-Host "   Health Check: https://$AppName.azurewebsites.net/api/v1/health" -ForegroundColor White
Write-Host "   Detailed Health: https://$AppName.azurewebsites.net/api/v1/health/detailed" -ForegroundColor White
Write-Host "   Default Config: https://$AppName.azurewebsites.net/api/v1/eval/configurations/defaultconfiguration" -ForegroundColor White
Write-Host ""
Write-Host "?? Monitor Deployment:" -ForegroundColor Yellow
Write-Host "   Portal: https://portal.azure.com" -ForegroundColor White
Write-Host "   Logs: az webapp log tail --name $AppName --resource-group $ResourceGroupName" -ForegroundColor White
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment completed at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan

exit 0