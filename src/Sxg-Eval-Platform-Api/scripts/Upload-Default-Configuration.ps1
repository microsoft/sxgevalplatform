# Upload Default Configuration to Azure Blob Storage
# This script uploads the default metric configuration to the Azure Storage account

param(
    [Parameter(Mandatory=$false)]
    [string]$StorageAccountName = "sxgagenteval",
    
    [Parameter(Mandatory=$false)]
    [string]$ContainerName = "eval-metrics-configuration",
    
    [Parameter(Mandatory=$false)]
    [string]$BlobName = "default-metric-configuration.json",
    
    [Parameter(Mandatory=$false)]
    [string]$ConfigFilePath = "..\metrics-configuration\default-metric-configuration.json"
)

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting upload of default configuration to Azure Blob Storage..." -ForegroundColor Green

# Check if configuration file exists
$fullConfigPath = Resolve-Path $ConfigFilePath -ErrorAction SilentlyContinue
if (-not $fullConfigPath -or -not (Test-Path $fullConfigPath)) {
    Write-Host "❌ Configuration file not found: $ConfigFilePath" -ForegroundColor Red
    Write-Host "Please ensure the default configuration file exists." -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Configuration file found: $fullConfigPath" -ForegroundColor Green

# Login check
Write-Host "📋 Checking Azure CLI login status..." -ForegroundColor Yellow
$loginCheck = az account show --output json 2>$null
if (-not $loginCheck) {
    Write-Host "❌ Not logged in to Azure CLI. Please login first." -ForegroundColor Red
    Write-Host "Run: az login" -ForegroundColor Yellow
    exit 1
}

$currentSub = (az account show --query name -o tsv)
Write-Host "✅ Using subscription: $currentSub" -ForegroundColor Green

# Check if storage account exists
Write-Host "🔍 Checking if storage account exists..." -ForegroundColor Yellow
$storageAccount = az storage account show --name $StorageAccountName --query "name" -o tsv 2>$null
if (-not $storageAccount) {
    Write-Host "❌ Storage account '$StorageAccountName' not found or not accessible" -ForegroundColor Red
    Write-Host "Please ensure the storage account exists and you have access to it." -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ Storage account found: $StorageAccountName" -ForegroundColor Green

# Create container if it doesn't exist
Write-Host "📦 Creating/verifying container: $ContainerName" -ForegroundColor Yellow
az storage container create `
    --account-name $StorageAccountName `
    --name $ContainerName `
    --auth-mode login `
    --public-access off `
    --output table

# Upload the configuration file
Write-Host "📤 Uploading configuration file..." -ForegroundColor Yellow
az storage blob upload `
    --account-name $StorageAccountName `
    --container-name $ContainerName `
    --name $BlobName `
    --file $fullConfigPath `
    --auth-mode login `
    --overwrite `
    --output table

# Verify the upload
Write-Host "🔍 Verifying upload..." -ForegroundColor Yellow
$blobInfo = az storage blob show `
    --account-name $StorageAccountName `
    --container-name $ContainerName `
    --name $BlobName `
    --auth-mode login `
    --query "{name:name,size:properties.contentLength,lastModified:properties.lastModified}" `
    --output table

Write-Host ""
Write-Host "=================================" -ForegroundColor Green
Write-Host "🎉 UPLOAD COMPLETED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green
Write-Host ""
Write-Host "📊 Blob Details:" -ForegroundColor Cyan
Write-Host $blobInfo
Write-Host ""
Write-Host "🌐 Blob URL: https://$StorageAccountName.blob.core.windows.net/$ContainerName/$BlobName" -ForegroundColor Cyan
Write-Host ""
Write-Host "✅ The default configuration is now available for the API to retrieve!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host "1. Test the API endpoint: GET /api/v1/eval/configurations/default" -ForegroundColor White
Write-Host "2. Create agent-specific configurations using: POST /api/v1/eval/configurations" -ForegroundColor White
Write-Host "3. Retrieve agent configurations using: GET /api/v1/eval/configurations/{agentId}" -ForegroundColor White

Write-Host ""
Write-Host "🎯 Upload completed successfully! 🎉" -ForegroundColor Green