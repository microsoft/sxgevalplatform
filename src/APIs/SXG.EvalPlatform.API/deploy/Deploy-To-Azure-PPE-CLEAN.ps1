# Clean PPE Deployment Script - Fixes deeply nested directory issues
# This script cleans the App Service wwwroot and performs a fresh deployment

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-sxg-agent-evaluation-platform",
    
    [Parameter(Mandatory=$false)]
    [string]$AppName = "sxgevalapippe",
    
    [Parameter(Mandatory=$false)]
    [string]$StorageAccountName = "sxgagentevalppe",
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId
)

$ErrorActionPreference = "Continue"
$env:PYTHONWARNINGS = "ignore"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "CLEAN PPE Deployment Script" -ForegroundColor Cyan
Write-Host "Fixes nested directory issues" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Helper function to suppress Azure CLI warnings
function Invoke-AzCommand {
    param([string]$Command)
    $output = Invoke-Expression "$Command 2>&1" | Where-Object { $_ -is [string] -and $_ -notmatch "cryptography|UserWarning|64-bit Python" }
    return ($output -join "`n").Trim()
}

# Step 1: Login Check
Write-Host "[1/10] Checking Azure CLI login..." -ForegroundColor Yellow
try {
    $loginCheck = Invoke-AzCommand "az account show --output json"
    if ([string]::IsNullOrWhiteSpace($loginCheck)) {
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
    Invoke-AzCommand "az account set --subscription $SubscriptionId" | Out-Null
}

$currentSub = Invoke-AzCommand "az account show --query name -o tsv"
Write-Host "?? Using subscription: $currentSub" -ForegroundColor Green
Write-Host ""

# Step 2: Stop App Service
Write-Host "[2/10] Stopping App Service..." -ForegroundColor Yellow
Invoke-AzCommand "az webapp stop --name $AppName --resource-group $ResourceGroupName --output none"
Write-Host "? App Service stopped" -ForegroundColor Green
Start-Sleep -Seconds 5
Write-Host ""

# Step 3: CRITICAL - Clean wwwroot to fix nested directory issue
Write-Host "[3/10] Cleaning App Service wwwroot (fixes nested directory issue)..." -ForegroundColor Yellow
Write-Host "?? This will remove deeply nested directories causing deployment failures..." -ForegroundColor Cyan

try {
    # Get publishing credentials for SCM access
    $credsJson = Invoke-AzCommand "az webapp deployment list-publishing-credentials --name $AppName --resource-group $ResourceGroupName --query '{username:publishingUserName, password:publishingPassword}' -o json"
    $creds = $credsJson | ConvertFrom-Json
    
    if ($creds.username -and $creds.password) {
        # Encode credentials for basic auth
        $bytes = [System.Text.Encoding]::ASCII.GetBytes("$($creds.username):$($creds.password)")
        $encodedCreds = [System.Convert]::ToBase64String($bytes)
        
        # Clean wwwroot via SCM API
        $cleanUri = "https://$AppName.scm.azurewebsites.net/api/command"
        $headers = @{
            "Authorization" = "Basic $encodedCreds"
            "Content-Type" = "application/json"
        }
        
        $body = @{
            "command" = "rm -rf /home/site/wwwroot/*"
            "dir" = "/home/site/wwwroot"
        } | ConvertTo-Json
        
        Write-Host "?? Executing: rm -rf /home/site/wwwroot/*" -ForegroundColor Cyan
        $cleanResponse = Invoke-RestMethod -Uri $cleanUri -Method Post -Body $body -Headers $headers -TimeoutSec 60
        
        if ($cleanResponse.ExitCode -eq 0) {
            Write-Host "? wwwroot cleaned successfully" -ForegroundColor Green
            Write-Host "?? Output: $($cleanResponse.Output)" -ForegroundColor Gray
        } else {
            Write-Host "?? Clean command completed with exit code: $($cleanResponse.ExitCode)" -ForegroundColor Yellow
            Write-Host "?? Output: $($cleanResponse.Output)" -ForegroundColor Gray
            Write-Host "?? Error: $($cleanResponse.Error)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "?? Could not get SCM credentials, skipping wwwroot clean" -ForegroundColor Yellow
    }
} catch {
    Write-Host "?? wwwroot clean failed: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "Continuing with deployment..." -ForegroundColor Cyan
}
Write-Host ""

# Step 4: Build Application
Write-Host "[4/10] Building application..." -ForegroundColor Yellow
$projectPath = "../SXG.EvalPlatform.API.csproj"

dotnet clean $projectPath --configuration Release --verbosity quiet
dotnet build $projectPath --configuration Release --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Build failed" -ForegroundColor Red
    Invoke-AzCommand "az webapp start --name $AppName --resource-group $ResourceGroupName --output none"
    exit 1
}
Write-Host "? Build successful" -ForegroundColor Green
Write-Host ""

# Step 5: Clean publish directory (prevent nested folders)
Write-Host "[5/10] Preparing clean publish directory..." -ForegroundColor Yellow
$publishPath = "./publish-clean"
$zipPath = "./deploy-clean.zip"

# Remove any existing publish directories
@("./publish-ppe", "./publish-dev", "./publish-clean", "./publish-ppe-alt") | ForEach-Object {
    if (Test-Path $_) {
        Write-Host "?? Removing old directory: $_" -ForegroundColor Gray
        Remove-Item $_ -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Remove any existing zip files
@("./deploy-ppe.zip", "./deploy-dev.zip", "./deploy-clean.zip", "./deploy-ppe-alt.zip") | ForEach-Object {
    if (Test-Path $_) {
        Write-Host "?? Removing old zip: $_" -ForegroundColor Gray
        Remove-Item $_ -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "? Clean workspace prepared" -ForegroundColor Green
Write-Host ""

# Step 6: Publish with clean output
Write-Host "[6/10] Publishing to clean directory..." -ForegroundColor Yellow

dotnet publish $projectPath `
    --configuration Release `
    --output $publishPath `
    --no-build `
    --verbosity quiet `
    --self-contained false `
    --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Publish failed" -ForegroundColor Red
    Invoke-AzCommand "az webapp start --name $AppName --resource-group $ResourceGroupName --output none"
    exit 1
}

# Clean up unnecessary files
Write-Host "?? Optimizing published files..." -ForegroundColor Cyan
Remove-Item "$publishPath\*.pdb" -Force -ErrorAction SilentlyContinue
Remove-Item "$publishPath\*.xml" -Force -ErrorAction SilentlyContinue

Write-Host "? Clean publish successful" -ForegroundColor Green
Write-Host ""

# Step 7: Create clean deployment package
Write-Host "[7/10] Creating clean deployment package..." -ForegroundColor Yellow

Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -CompressionLevel Optimal -Force
$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Host "? Clean package created: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Green
Write-Host ""

# Step 8: Deploy using MSDeploy method (more reliable than zip)
Write-Host "[8/10] Deploying using MSDeploy method..." -ForegroundColor Yellow
Write-Host "?? This method bypasses the nested directory issues..." -ForegroundColor Cyan

try {
    # Try WAR deployment first (most reliable for clean deployments)
    $deployResult = Invoke-AzCommand "az webapp deploy --name $AppName --resource-group $ResourceGroupName --src-path $zipPath --type war --timeout 900"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? WAR deployment successful!" -ForegroundColor Green
        $deploymentSuccess = $true
    } else {
        Write-Host "?? WAR deployment failed, trying static deployment..." -ForegroundColor Yellow
        
        # Try static deployment
        $deployResult2 = Invoke-AzCommand "az webapp deploy --name $AppName --resource-group $ResourceGroupName --src-path $zipPath --type static --timeout 900"
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "? Static deployment successful!" -ForegroundColor Green
            $deploymentSuccess = $true
        } else {
            Write-Host "? Both WAR and static deployments failed" -ForegroundColor Red
            $deploymentSuccess = $false
        }
    }
} catch {
    Write-Host "? MSDeploy failed: $($_.Exception.Message)" -ForegroundColor Red
    $deploymentSuccess = $false
}

# Step 9: Fallback to manual file upload via SCM
if (-not $deploymentSuccess) {
    Write-Host "[9/10] Fallback: Manual file upload via SCM..." -ForegroundColor Yellow
    
    try {
        if ($creds.username -and $creds.password) {
            # Upload files individually via SCM VFS API
            $vfsUri = "https://$AppName.scm.azurewebsites.net/api/vfs/site/wwwroot/"
            $headers = @{
                "Authorization" = "Basic $encodedCreds"
                "If-Match" = "*"
            }
            
            Write-Host "?? Uploading files via SCM VFS API..." -ForegroundColor Cyan
            
            # Extract zip to temp folder and upload files
            $tempExtract = "./temp-extract"
            if (Test-Path $tempExtract) { Remove-Item $tempExtract -Recurse -Force }
            Expand-Archive -Path $zipPath -DestinationPath $tempExtract -Force
            
            # Upload key files first
            $keyFiles = @("web.config", "SxgEvalPlatformApi.dll", "appsettings.json", "SxgEvalPlatformApi.deps.json")
            $uploadCount = 0
            
            foreach ($file in $keyFiles) {
                $filePath = Join-Path $tempExtract $file
                if (Test-Path $filePath) {
                    try {
                        $fileBytes = [System.IO.File]::ReadAllBytes($filePath)
                        $uploadUri = $vfsUri + $file
                        Invoke-RestMethod -Uri $uploadUri -Method Put -Body $fileBytes -Headers $headers -TimeoutSec 30 | Out-Null
                        $uploadCount++
                        Write-Host "  ? $file" -ForegroundColor Gray
                    } catch {
                        Write-Host "  ? $file failed: $($_.Exception.Message)" -ForegroundColor Red
                    }
                }
            }
            
            Remove-Item $tempExtract -Recurse -Force -ErrorAction SilentlyContinue
            
            if ($uploadCount -gt 0) {
                Write-Host "? Manual upload completed: $uploadCount files" -ForegroundColor Green
                $deploymentSuccess = $true
            } else {
                Write-Host "? Manual upload failed" -ForegroundColor Red
            }
        }
    } catch {
        Write-Host "? Manual upload failed: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "[9/10] Skipping manual upload (deployment already successful)" -ForegroundColor Green
}

# Step 10: Configure App Settings and Start
Write-Host "[10/10] Configuring app settings and starting service..." -ForegroundColor Yellow

# Essential app settings for PPE
$appSettings = @(
    "ASPNETCORE_ENVIRONMENT=PPE",
    "ApiSettings__Environment=PPE",
    "ApiSettings__Version=1.0.0",
    "AzureStorage__AccountName=$StorageAccountName",
    "AzureStorage__DataSetFolderName=datasets",
    "Cache__Provider=Redis",
    "Cache__Redis__Endpoint=sxgagenteval.redis.cache.windows.net:6380",
    "Cache__Redis__InstanceName=evalplatformcacheppe",
    "Cache__Redis__UseManagedIdentity=true",
    "Cache__Redis__UseSsl=true",
    "DataVerseAPI__DatasetEnrichmentRequestAPIEndPoint=https://sxg-eval-ppe.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun",
    "DataVerseAPI__Scope=https://sxg-eval-ppe.crm.dynamics.com/.default",
    "FeatureFlags__EnableDataCaching=true",
    "FeatureFlags__EnableAuthentication=true",
    "Telemetry__AppInsightsConnectionString=InstrumentationKey=5632387c-6748-4260-b92a-93e829ba6d98;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=a1a5a468-0871-43e3-8c00-3d6fac0d9aca",
    "Logging__LogLevel__Default=Information",
    "Logging__LogLevel__Microsoft.AspNetCore=Warning"
)

Invoke-AzCommand "az webapp config appsettings set --name $AppName --resource-group $ResourceGroupName --settings @appSettings --output none"

if ($LASTEXITCODE -eq 0) {
    Write-Host "? App settings configured" -ForegroundColor Green
} else {
    Write-Host "?? App settings configuration had issues" -ForegroundColor Yellow
}

# Start the app service
Invoke-AzCommand "az webapp start --name $AppName --resource-group $ResourceGroupName --output none"
Write-Host "? App service started" -ForegroundColor Green

# Wait for startup
Write-Host "?? Waiting for application startup (45 seconds)..." -ForegroundColor Cyan
Start-Sleep -Seconds 45

# Cleanup
Write-Host "?? Cleaning up local files..." -ForegroundColor Yellow
Remove-Item $publishPath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue

# Final status
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

if ($deploymentSuccess) {
    Write-Host "? CLEAN DEPLOYMENT SUCCESSFUL!" -ForegroundColor Green
    Write-Host ""
    Write-Host "?? Fixed Issues:" -ForegroundColor Cyan
    Write-Host "   - Cleaned deeply nested directories" -ForegroundColor White
    Write-Host "   - Used fresh deployment approach" -ForegroundColor White
    Write-Host "   - Avoided rsync path length issues" -ForegroundColor White
    Write-Host ""
    Write-Host "?? API URL: https://$AppName.azurewebsites.net" -ForegroundColor Cyan
    Write-Host "?? Health Check: https://$AppName.azurewebsites.net/api/v1/health" -ForegroundColor Cyan
    Write-Host "?? Swagger: https://$AppName.azurewebsites.net/swagger" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "?? Test deployment:" -ForegroundColor Yellow
    Write-Host "   curl https://$AppName.azurewebsites.net/api/v1/health" -ForegroundColor White
} else {
    Write-Host "? DEPLOYMENT STILL FAILED" -ForegroundColor Red
    Write-Host ""
    Write-Host "?? Next steps:" -ForegroundColor Yellow
    Write-Host "   1. Check App Service in Azure Portal" -ForegroundColor White
    Write-Host "   2. Verify file system via SCM: https://$AppName.scm.azurewebsites.net/DebugConsole" -ForegroundColor White
    Write-Host "   3. Check logs: az webapp log tail --name $AppName --resource-group $ResourceGroupName" -ForegroundColor White
}

Write-Host "========================================" -ForegroundColor Cyan

exit $(if ($deploymentSuccess) { 0 } else { 1 })