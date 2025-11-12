# Update App Settings for PPE Environment
# This script ONLY updates app settings without redeploying the application
# Use this when you need to update configuration without downtime

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

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Update App Settings - PPE Environment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Target App Service: $AppName" -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Green
Write-Host ""
Write-Host "⚠️  WARNING: This will update app settings and may restart the app" -ForegroundColor Yellow
Write-Host ""

# Login check
Write-Host "Checking Azure CLI login status..." -ForegroundColor Yellow
$loginCheck = az account show --output json 2>$null
if (-not $loginCheck) {
    Write-Host "❌ Not logged in to Azure CLI." -ForegroundColor Red
    Write-Host "Please run: az login" -ForegroundColor Yellow
    exit 1
}

# Set subscription if provided
if ($SubscriptionId) {
az account set --subscription $SubscriptionId
}

$currentSub = (az account show --query name -o tsv)
Write-Host "✅ Using subscription: $currentSub" -ForegroundColor Green
Write-Host ""

# Verify App Service exists
Write-Host "Verifying App Service: $AppName" -ForegroundColor Yellow
$appExists = az webapp show --name $AppName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null

if (-not $appExists) {
    Write-Host "❌ App Service $AppName does not exist" -ForegroundColor Red
    exit 1
}
Write-Host "✅ App Service verified" -ForegroundColor Green
Write-Host ""

# Show current settings count
Write-Host "Fetching current settings..." -ForegroundColor Yellow
$currentSettings = az webapp config appsettings list --name $AppName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
Write-Host "Current settings count: $($currentSettings.Count)" -ForegroundColor Cyan
Write-Host ""

# Configure all app settings
Write-Host "Configuring App Settings for PPE environment..." -ForegroundColor Yellow
Write-Host "This includes ALL settings from appsettings.json and appsettings.PPE.json" -ForegroundColor Cyan
Write-Host ""

$appSettings = @(
    "ASPNETCORE_ENVIRONMENT=PPE",
  
    # API Settings
    "ApiSettings__Version=1.0.0",
 "ApiSettings__Environment=Production",
    
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
    
    # Cache Settings - Memory
    "Cache__Provider=Memory",
    "Cache__DefaultExpirationMinutes=60",
    "Cache__Memory__SizeLimitMB=100",
    "Cache__Memory__CompactionPercentage=0.25",
    "Cache__Memory__ExpirationScanFrequencySeconds=60",
    
    # Cache Settings - Redis (for future use)
    "Cache__Redis__Endpoint=evalplatformcacheppe.redis.cache.windows.net:6380",
    "Cache__Redis__InstanceName=evalplatformcacheppe",
    "Cache__Redis__UseManagedIdentity=true",
    "Cache__Redis__ConnectTimeoutSeconds=5",
    "Cache__Redis__CommandTimeoutSeconds=3",
    "Cache__Redis__UseSsl=true",
    "Cache__Redis__Retry__Enabled=true",
    "Cache__Redis__Retry__MaxRetryAttempts=2",
    "Cache__Redis__Retry__BaseDelayMs=500",
    "Cache__Redis__Retry__MaxDelayMs=2000",
    
    # DataVerse API Settings
    "DataVerseAPI__DatasetEnrichmentRequestAPIEndPoint=https://sxg-eval-dev.crm.dynamics.com/api/data/v9.2/cr890_PostEvalRun",
    "DataVerseAPI__Scope=https://sxg-eval-dev.crm.dynamics.com/.default",
    
    # Telemetry Settings
    "Telemetry__AppInsightsConnectionString=InstrumentationKey=5632387c-6748-4260-b92a-93e829ba6d98;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/;ApplicationId=a1a5a468-0871-43e3-8c00-3d6fac0d9aca",
    
    # OpenTelemetry Settings
    "OpenTelemetry__ServiceName=SXG-EvalPlatform-API",
    "OpenTelemetry__ServiceVersion=1.0.0",
    "OpenTelemetry__EnableConsoleExporter=false",
    "OpenTelemetry__EnableApplicationInsights=true",
 "OpenTelemetry__SamplingRatio=1.0",
    "OpenTelemetry__MaxExportBatchSize=100",
    "OpenTelemetry__ExportTimeoutMilliseconds=30000",
    
    # Logging Settings
    "Logging__LogLevel__Default=Information",
    "Logging__LogLevel__Microsoft.AspNetCore=Warning",
    "Logging__LogLevel__Microsoft.ApplicationInsights=Warning",
    "Logging__ApplicationInsights__LogLevel__Default=Information",
    "Logging__ApplicationInsights__LogLevel__Microsoft=Warning"
)

Write-Host "Applying $($appSettings.Count) settings..." -ForegroundColor Yellow

az webapp config appsettings set `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --settings @appSettings `
 --output none

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to configure App Settings" -ForegroundColor Red
    exit 1
}

Write-Host "✅ App Settings updated successfully" -ForegroundColor Green
Write-Host ""

# Show updated settings count
Write-Host "Fetching updated settings..." -ForegroundColor Yellow
$updatedSettings = az webapp config appsettings list --name $AppName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
Write-Host "Updated settings count: $($updatedSettings.Count)" -ForegroundColor Cyan
Write-Host ""

# Display key settings
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Key Settings Configured:" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

$keySettings = @(
 "ASPNETCORE_ENVIRONMENT",
    "ApiSettings__Environment",
    "AzureStorage__AccountName",
    "Cache__Provider",
    "DataVerseAPI__DatasetEnrichmentRequestAPIEndPoint",
    "OpenTelemetry__EnableApplicationInsights"
)

foreach ($setting in $keySettings) {
    $value = ($updatedSettings | Where-Object { $_.name -eq $setting }).value
    if ($value) {
        # Truncate long values
        if ($value.Length -gt 60) {
      $value = $value.Substring(0, 57) + "..."
        }
    Write-Host "  ✓ $setting = $value" -ForegroundColor White
    } else {
   Write-Host "  ✗ $setting = NOT SET" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✅ SETTINGS UPDATE COMPLETE!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "📍 Environment: PPE" -ForegroundColor Cyan
Write-Host "🌐 API URL: https://$AppName.azurewebsites.net" -ForegroundColor Cyan
Write-Host "📚 Swagger: https://$AppName.azurewebsites.net/swagger" -ForegroundColor Cyan
Write-Host ""
Write-Host "💡 Note: App may restart to apply settings" -ForegroundColor Yellow
Write-Host "   Monitor: az webapp log tail --name $AppName --resource-group $ResourceGroupName" -ForegroundColor White
Write-Host ""
Write-Host "🔍 Verify settings in Azure Portal:" -ForegroundColor Yellow
Write-Host "   https://portal.azure.com → $AppName → Configuration → Application Settings" -ForegroundColor White
Write-Host ""
Write-Host "Completed at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
