# Alternative PPE Deployment Script with Enhanced Error Handling
# Uses different deployment strategies to resolve Status 400 issues

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
Write-Host "Alternative PPE Deployment Script" -ForegroundColor Cyan
Write-Host "Status 400 Error Resolution" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Step 1: Login Check
Write-Host "[1/9] Checking Azure CLI login..." -ForegroundColor Yellow
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
    az account set --subscription $SubscriptionId
}

$currentSub = (az account show --query name -o tsv)
Write-Host "?? Using subscription: $currentSub" -ForegroundColor Green
Write-Host ""

# Step 2: Stop App Service first (prevents deployment conflicts)
Write-Host "[2/9] Stopping App Service to prevent conflicts..." -ForegroundColor Yellow
az webapp stop --name $AppName --resource-group $ResourceGroupName --output none
if ($LASTEXITCODE -eq 0) {
    Write-Host "? App Service stopped" -ForegroundColor Green
    Start-Sleep -Seconds 10
} else {
    Write-Host "?? Could not stop App Service (might already be stopped)" -ForegroundColor Yellow
}
Write-Host ""

# Step 3: Build Application
Write-Host "[3/9] Building application..." -ForegroundColor Yellow
$projectPath = "../SXG.EvalPlatform.API.csproj"

dotnet clean $projectPath --configuration Release --verbosity quiet
dotnet build $projectPath --configuration Release --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Build failed" -ForegroundColor Red
    az webapp start --name $AppName --resource-group $ResourceGroupName --output none
    exit 1
}
Write-Host "? Build successful" -ForegroundColor Green
Write-Host ""

# Step 4: Publish with optimizations
Write-Host "[4/9] Publishing with size optimizations..." -ForegroundColor Yellow
$publishPath = "./publish-ppe-alt"
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

# Use single-file publish to reduce package size
dotnet publish $projectPath `
    --configuration Release `
    --output $publishPath `
    --no-build `
    --verbosity quiet `
    --self-contained false `
    --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Publish failed" -ForegroundColor Red
    az webapp start --name $AppName --resource-group $ResourceGroupName --output none
    exit 1
}

# Remove unnecessary files to reduce package size
Write-Host "?? Optimizing package size..." -ForegroundColor Cyan
Remove-Item "$publishPath\*.pdb" -Force -ErrorAction SilentlyContinue
Remove-Item "$publishPath\*.xml" -Force -ErrorAction SilentlyContinue
Remove-Item "$publishPath\runtimes\*" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "? Publish successful and optimized" -ForegroundColor Green
Write-Host ""

# Step 5: Create smaller deployment package
Write-Host "[5/9] Creating optimized deployment package..." -ForegroundColor Yellow
$zipPath = "./deploy-ppe-alt.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

# Use maximum compression
Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -CompressionLevel Optimal -Force
$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Host "? Optimized package: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Green
Write-Host ""

# Step 6: Try ZIP deployment method 1 - Standard Deploy
Write-Host "[6/9] Attempting standard zip deployment..." -ForegroundColor Yellow

$deployResult1 = az webapp deploy `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --src-path $zipPath `
    --type zip `
    --timeout 900 `
    --async false 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Standard deployment successful!" -ForegroundColor Green
    $deploymentSuccess = $true
} else {
    Write-Host "? Standard deployment failed: $deployResult1" -ForegroundColor Red
    $deploymentSuccess = $false
}

# Step 7: Alternative method - Upload via SCM API
if (-not $deploymentSuccess) {
    Write-Host "[7/9] Trying alternative deployment via SCM API..." -ForegroundColor Yellow
    
    try {
        # Get publishing credentials
        $creds = az webapp deployment list-publishing-credentials --name $AppName --resource-group $ResourceGroupName --query '{username:publishingUserName, password:publishingPassword}' -o json | ConvertFrom-Json
        
        if ($creds.username -and $creds.password) {
            # Encode credentials
            $bytes = [System.Text.Encoding]::ASCII.GetBytes("$($creds.username):$($creds.password)")
            $encodedCreds = [System.Convert]::ToBase64String($bytes)
            
            # Upload via REST API
            $uri = "https://$AppName.scm.azurewebsites.net/api/zipdeploy"
            $headers = @{
                "Authorization" = "Basic $encodedCreds"
                "Content-Type" = "application/octet-stream"
            }
            
            Write-Host "?? Uploading via SCM API..." -ForegroundColor Cyan
            $fileBytes = [System.IO.File]::ReadAllBytes($zipPath)
            $response = Invoke-RestMethod -Uri $uri -Method Post -Body $fileBytes -Headers $headers -TimeoutSec 900
            
            Write-Host "? SCM API deployment successful!" -ForegroundColor Green
            $deploymentSuccess = $true
        } else {
            Write-Host "? Could not retrieve publishing credentials" -ForegroundColor Red
        }
    } catch {
        Write-Host "? SCM API deployment failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Step 8: Final fallback - FTP deployment
if (-not $deploymentSuccess) {
    Write-Host "[8/9] Final attempt - checking app service configuration..." -ForegroundColor Yellow
    
    # Check if app service is healthy
    $appStatus = az webapp show --name $AppName --resource-group $ResourceGroupName --query "state" -o tsv 2>$null
    Write-Host "?? App Service state: $appStatus" -ForegroundColor Cyan
    
    # Try restart and simple deployment
    Write-Host "?? Restarting app service..." -ForegroundColor Cyan
    az webapp restart --name $AppName --resource-group $ResourceGroupName --output none
    Start-Sleep -Seconds 30
    
    # Final deployment attempt
    Write-Host "?? Final deployment attempt..." -ForegroundColor Cyan
    $finalResult = az webapp deploy `
        --name $AppName `
        --resource-group $ResourceGroupName `
        --src-path $zipPath `
        --type zip `
        --timeout 1200 `
        --restart true 2>&1
        
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? Final deployment successful!" -ForegroundColor Green
        $deploymentSuccess = $true
    } else {
        Write-Host "? All deployment methods failed" -ForegroundColor Red
        Write-Host "Error details: $finalResult" -ForegroundColor Red
    }
}

# Step 9: Configure App Settings and Start
if ($deploymentSuccess) {
    Write-Host "[9/9] Configuring app settings and starting service..." -ForegroundColor Yellow
    
    # Configure essential app settings
    $appSettings = @(
        "ASPNETCORE_ENVIRONMENT=PPE",
        "ApiSettings__Environment=PPE",
        "AzureStorage__AccountName=$StorageAccountName",
        "Cache__Provider=Redis",
        "Cache__Redis__Endpoint=sxgagenteval.redis.cache.windows.net:6380",
        "Cache__Redis__InstanceName=evalplatformcacheppe",
        "Cache__Redis__UseManagedIdentity=true",
        "DataVerseAPI__DatasetEnrichmentRequestAPIEndPoint=https://sxg-eval-ppe.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun",
        "DataVerseAPI__Scope=https://sxg-eval-ppe.crm.dynamics.com/.default",
        "FeatureFlags__EnableDataCaching=true"
    )
    
    az webapp config appsettings set `
        --name $AppName `
        --resource-group $ResourceGroupName `
        --settings @appSettings `
        --output none
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? App settings configured" -ForegroundColor Green
    } else {
        Write-Host "?? App settings configuration had issues" -ForegroundColor Yellow
    }
    
    # Start the app service
    az webapp start --name $AppName --resource-group $ResourceGroupName --output none
    Write-Host "? App service started" -ForegroundColor Green
    
    # Wait for warmup
    Write-Host "?? Waiting for application warmup (60 seconds)..." -ForegroundColor Cyan
    Start-Sleep -Seconds 60
    
} else {
    Write-Host "? Deployment failed - starting app service anyway..." -ForegroundColor Red
    az webapp start --name $AppName --resource-group $ResourceGroupName --output none
}

# Cleanup
Write-Host "?? Cleaning up..." -ForegroundColor Yellow
Remove-Item $publishPath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue

# Final status
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
if ($deploymentSuccess) {
    Write-Host "? DEPLOYMENT SUCCESSFUL!" -ForegroundColor Green
    Write-Host ""
    Write-Host "?? API URL: https://$AppName.azurewebsites.net" -ForegroundColor Cyan
    Write-Host "?? Health Check: https://$AppName.azurewebsites.net/api/v1/health" -ForegroundColor Cyan
    Write-Host "?? Swagger: https://$AppName.azurewebsites.net/swagger" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "?? Verify deployment with:" -ForegroundColor Yellow
    Write-Host "   curl https://$AppName.azurewebsites.net/api/v1/health" -ForegroundColor White
} else {
    Write-Host "? DEPLOYMENT FAILED" -ForegroundColor Red
    Write-Host ""
    Write-Host "?? Check logs with:" -ForegroundColor Yellow
    Write-Host "   az webapp log tail --name $AppName --resource-group $ResourceGroupName" -ForegroundColor White
    Write-Host "   Visit: https://$AppName.scm.azurewebsites.net/api/deployments/latest" -ForegroundColor White
}
Write-Host "========================================" -ForegroundColor Cyan

exit $(if ($deploymentSuccess) { 0 } else { 1 })