# Azure Deployment Script for SXG Evaluation Platform API - PPE Environment
# This script deploys the API to existing App Service "sxgevalapippe"
# FIXED VERSION: Handles Azure CLI cryptography warnings properly

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-sxg-agent-evaluation-platform",
    
    [Parameter(Mandatory=$false)]
    [string]$AppName = "sxgevalapippe",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "eastus",
    
    [Parameter(Mandatory=$false)]
    [string]$StorageAccountName = "sxgagentevalppe",
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId
)

# Set error action preference - Continue instead of Stop to handle warnings
$ErrorActionPreference = "Continue"

# Suppress Python warnings from Azure CLI
$env:PYTHONWARNINGS = "ignore"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SXG Evaluation Platform API" -ForegroundColor Cyan
Write-Host "PPE Environment Deployment (FIXED)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Target App Service: $AppName" -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Green
Write-Host "Storage Account: $StorageAccountName" -ForegroundColor Green
Write-Host ""

# Helper function to run Azure CLI commands and capture only stdout
function Invoke-AzCommand {
    param(
        [string]$Command
    )
    
    # Redirect stderr to null, capture only stdout
    $output = Invoke-Expression "$Command 2>&1" | Where-Object { $_ -is [string] -and $_ -notmatch "cryptography|UserWarning|64-bit Python" }
    return ($output -join "`n").Trim()
}

# Login check
Write-Host "[1/8] Checking Azure CLI login status..." -ForegroundColor Yellow
try {
    $loginCheck = Invoke-AzCommand "az account show --output json"
    if ([string]::IsNullOrWhiteSpace($loginCheck)) {
        Write-Host "? Not logged in to Azure CLI." -ForegroundColor Red
        Write-Host "Please run: az login" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "? Not logged in to Azure CLI." -ForegroundColor Red
    Write-Host "Please run: az login" -ForegroundColor Yellow
    exit 1
}

# Set subscription if provided
if ($SubscriptionId) {
    Write-Host "Setting subscription to: $SubscriptionId" -ForegroundColor Yellow
    Invoke-AzCommand "az account set --subscription $SubscriptionId" | Out-Null
}

$currentSub = Invoke-AzCommand "az account show --query name -o tsv"
Write-Host "? Using subscription: $currentSub" -ForegroundColor Green
Write-Host ""

# Step 1: Verify Resource Group exists
Write-Host "[2/8] Verifying resource group: $ResourceGroupName" -ForegroundColor Yellow
$rgExists = Invoke-AzCommand "az group show --name $ResourceGroupName --query name -o tsv"

if ([string]::IsNullOrWhiteSpace($rgExists)) {
    Write-Host "? Resource group $ResourceGroupName does not exist." -ForegroundColor Red
    exit 1
}
Write-Host "? Resource group verified" -ForegroundColor Green
Write-Host ""

# Step 2: Verify App Service exists
Write-Host "[3/8] Verifying App Service: $AppName" -ForegroundColor Yellow
$appExists = Invoke-AzCommand "az webapp show --name $AppName --resource-group $ResourceGroupName --query name -o tsv"

if ([string]::IsNullOrWhiteSpace($appExists)) {
    Write-Host "? App Service $AppName does not exist in resource group $ResourceGroupName" -ForegroundColor Red
    Write-Host "Please create the App Service first or use the full deployment script." -ForegroundColor Yellow
    exit 1
}
Write-Host "? App Service verified: $appExists" -ForegroundColor Green
Write-Host ""

# Step 3: Build Application
Write-Host "[4/8] Building application..." -ForegroundColor Yellow
$projectPath = "../SXG.EvalPlatform.API.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Host "? Project file not found: $projectPath" -ForegroundColor Red
    exit 1
}

dotnet clean $projectPath --configuration Release | Out-Null
dotnet build $projectPath --configuration Release --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "? Build successful" -ForegroundColor Green
Write-Host ""

# Step 4: Publish Application
Write-Host "[5/8] Publishing application..." -ForegroundColor Yellow
$publishPath = "./publish-ppe"
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

dotnet publish $projectPath --configuration Release --output $publishPath --no-build --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Publish failed" -ForegroundColor Red
    exit 1
}
Write-Host "? Publish successful" -ForegroundColor Green
Write-Host ""

# Step 5: Create Deployment Package
Write-Host "[6/8] Creating deployment package..." -ForegroundColor Yellow
$zipPath = ".\deploy-ppe.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force
$zipSize = (Get-Item $zipPath).Length / 1MB
Write-Host "? Deployment package created: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Green
Write-Host ""

# Step 6: Deploy to Azure
Write-Host "[7/8] Deploying to Azure App Service..." -ForegroundColor Yellow
Write-Host "This may take a few minutes..." -ForegroundColor Cyan

# Use Invoke-Expression with error handling for deployment
$deployCommand = "az webapp deploy --name $AppName --resource-group $ResourceGroupName --src-path `"$zipPath`" --type zip --async false --timeout 600 2>&1"
$deployOutput = Invoke-Expression $deployCommand | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" }

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Deployment failed" -ForegroundColor Red
    Write-Host "Output: $deployOutput" -ForegroundColor Yellow
    exit 1
}
Write-Host "? Application deployed successfully" -ForegroundColor Green
Write-Host ""

# Step 7: Configure App Settings for PPE
Write-Host "[8/8] Configuring App Settings for PPE environment..." -ForegroundColor Yellow

$appSettings = @(
    "ASPNETCORE_ENVIRONMENT=PPE",
    
    # API Settings
    "ApiSettings__Version=1.0.0",
    "ApiSettings__Environment=PPE",
    
    # Azure Storage Settings
    "AzureStorage__AccountName=$StorageAccountName",
    "AzureStorage__DataSetFolderName=datasets",
    "AzureStorage__DatasetsFolderName=datasets",
    "AzureStorage__EvalResultsFolderName=evaluation-results",
    "AzureStorage__MetricsConfigurationsFolderName=metrics-configurations",
    "AzureStorage__PlatformConfigurationsContainer=platform-configurations",
    "AzureStorage__DefaultMetricsConfiguration=default-metric-configuration.json",
    "AzureStorage__MetricsConfigurationsTable=MetricsConfigurationsTable",
    "AzureStorage__DataSetsTable=DataSetsTable",
    "AzureStorage__EvalRunsTable=EvalRunsTable",
    "AzureStorage__DatasetEnrichmentRequestsQueueName=dataset-enrichment-requests",
    "AzureStorage__EvalProcessingRequestsQueueName=eval-processing-requests",
    
    # Cache Settings - Memory Cache for PPE
    "Cache__Provider=Memory",
    "Cache__DefaultExpirationMinutes=60",
    "Cache__Memory__SizeLimitMB=100",
    "Cache__Memory__CompactionPercentage=0.25",
    "Cache__Memory__ExpirationScanFrequencySeconds=60",
    
    # Cache Settings - Redis Configuration for PPE (Shared Redis Cache with Managed Identity)
    "Cache__Redis__Endpoint=sxgagenteval.redis.cache.windows.net:6380",
    "Cache__Redis__InstanceName=evalplatformcacheppe",
    "Cache__Redis__UseManagedIdentity=true",
    "Cache__Redis__ConnectTimeoutSeconds=5",
    "Cache__Redis__CommandTimeoutSeconds=3",
    "Cache__Redis__UseSsl=true",
    "Cache__Redis__Retry__Enabled=true",
    "Cache__Redis__Retry__MaxRetryAttempts=2",
    "Cache__Redis__Retry__BaseDelayMs=500",
    "Cache__Redis__Retry__MaxDelayMs=2000",
    
    # DataVerse API Settings - PPE Environment
    "DataVerseAPI__DatasetEnrichmentRequestAPIEndPoint=https://sxg-eval-ppe.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun",
    "DataVerseAPI__Scope=https://sxg-eval-ppe.crm.dynamics.com/.default",
    
    # Telemetry Settings
    "Telemetry__AppInsightsConnectionString=InstrumentationKey=5632387c-6748-4260-b92a-93e829ba6d98;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=a1a5a468-0871-43e3-8c00-3d6fac0d9aca",
    
    # OpenTelemetry Settings
    "OpenTelemetry__ServiceName=SXG-EvalPlatform-API",
    "OpenTelemetry__ServiceVersion=1.0.0",
    "OpenTelemetry__CloudRoleName=SXG-EvalPlatform-API",
    "OpenTelemetry__EnableConsoleExporter=false",
    "OpenTelemetry__EnableApplicationInsights=true",
    "OpenTelemetry__SamplingRatio=1.0",
    "OpenTelemetry__MaxExportBatchSize=100",
    "OpenTelemetry__ExportTimeoutMilliseconds=30000",
    
    # Feature Flags
    "FeatureFlags__EnableAuthentication=true",
    
    # Logging Settings
    "Logging__LogLevel__Default=Information",
    "Logging__LogLevel__Microsoft.AspNetCore=Warning",
    "Logging__LogLevel__Microsoft.ApplicationInsights=Warning",
    "Logging__ApplicationInsights__LogLevel__Default=Information",
    "Logging__ApplicationInsights__LogLevel__Microsoft=Warning"
)

# Configure app settings with warning suppression
$settingsCommand = "az webapp config appsettings set --name $AppName --resource-group $ResourceGroupName --settings @appSettings --output none 2>&1"
$settingsOutput = Invoke-Expression $settingsCommand | Where-Object { $_ -notmatch "cryptography|UserWarning|64-bit Python" }

if ($LASTEXITCODE -ne 0) {
    Write-Host "? Failed to configure App Settings" -ForegroundColor Red
    Write-Host "Output: $settingsOutput" -ForegroundColor Yellow
    exit 1
}
Write-Host "? App Settings configured successfully" -ForegroundColor Green
Write-Host "   Total settings configured: $($appSettings.Count)" -ForegroundColor Cyan
Write-Host ""

# Clean up local files
Write-Host "Cleaning up local deployment files..." -ForegroundColor Yellow
Remove-Item $publishPath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Write-Host "? Cleanup complete" -ForegroundColor Green
Write-Host ""

# Deployment Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "? DEPLOYMENT SUCCESSFUL!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Environment: PPE" -ForegroundColor Cyan
Write-Host "?? Version: 1.0.0" -ForegroundColor Cyan
Write-Host "?? API URL: https://$AppName.azurewebsites.net" -ForegroundColor Cyan
Write-Host "?? Swagger UI: https://$AppName.azurewebsites.net/swagger" -ForegroundColor Cyan
Write-Host "?? Storage: $StorageAccountName" -ForegroundColor Cyan
Write-Host ""
Write-Host "?? Verify Deployment:" -ForegroundColor Yellow
Write-Host "   Health Check: https://$AppName.azurewebsites.net/api/v1/health" -ForegroundColor White
Write-Host "   Default Config: https://$AppName.azurewebsites.net/api/v1/eval/configurations/defaultconfiguration" -ForegroundColor White
Write-Host ""
Write-Host "?? Monitor Deployment:" -ForegroundColor Yellow
Write-Host "   Portal: https://portal.azure.com" -ForegroundColor White
Write-Host "   Logs: az webapp log tail --name $AppName --resource-group $ResourceGroupName" -ForegroundColor White
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment completed at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan
