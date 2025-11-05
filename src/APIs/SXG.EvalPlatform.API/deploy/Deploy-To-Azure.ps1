# Azure Deployment Script for SXG Evaluation Platform API
# This script automates the deployment of the API to Azure App Service

param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName = "sxg-eval-rg",
    
    [Parameter(Mandatory=$true)]
    [string]$AppName = "sxg-eval-platform-api",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "eastus",
    
    [Parameter(Mandatory=$true)]
    [string]$StorageAccountName = "sxgagenteval",
    
    [Parameter(Mandatory=$false)]
    [string]$ContainerName = "eval-configurations",
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId
)

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting Azure deployment for SXG Evaluation Platform API..." -ForegroundColor Green

# Login check
Write-Host "📋 Checking Azure CLI login status..." -ForegroundColor Yellow
$loginCheck = az account show --output json 2>$null
if (-not $loginCheck) {
    Write-Host "❌ Not logged in to Azure CLI. Please login first." -ForegroundColor Red
    Write-Host "Run: az login" -ForegroundColor Yellow
    exit 1
}

# Set subscription if provided
if ($SubscriptionId) {
    Write-Host "🔧 Setting subscription to: $SubscriptionId" -ForegroundColor Yellow
    az account set --subscription $SubscriptionId
}

$currentSub = (az account show --query name -o tsv)
Write-Host "✅ Using subscription: $currentSub" -ForegroundColor Green

# Step 1: Create Resource Group
Write-Host "📦 Creating/verifying resource group: $ResourceGroupName" -ForegroundColor Yellow
az group create --name $ResourceGroupName --location $Location --output table

# Step 2: Create App Service Plan
Write-Host "🏗️ Creating App Service Plan..." -ForegroundColor Yellow
$planName = "$AppName-plan"
az appservice plan create `
    --name $planName `
    --resource-group $ResourceGroupName `
    --location $Location `
    --sku B1 `
    --is-linux `
    --output table

# Step 3: Create Web App
Write-Host "🌐 Creating Web App: $AppName" -ForegroundColor Yellow
az webapp create `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --plan $planName `
    --runtime "DOTNET|8.0" `
    --output table

# Step 4: Enable Managed Identity
Write-Host "🔐 Enabling Managed Identity..." -ForegroundColor Yellow
az webapp identity assign `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --output table

# Get Principal ID
$principalId = az webapp identity show `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --query principalId -o tsv

Write-Host "✅ Principal ID: $principalId" -ForegroundColor Green

# Step 5: Grant Storage Permissions
Write-Host "🔑 Granting storage permissions..." -ForegroundColor Yellow

# Check if storage account exists
$storageExists = az storage account show --name $StorageAccountName --resource-group $ResourceGroupName 2>$null
if (-not $storageExists) {
    Write-Host "❌ Storage account '$StorageAccountName' not found in resource group '$ResourceGroupName'" -ForegroundColor Red
    Write-Host "Please create the storage account first or provide the correct name." -ForegroundColor Yellow
    exit 1
}

$storageId = az storage account show `
    --name $StorageAccountName `
    --resource-group $ResourceGroupName `
    --query id -o tsv

# Assign Storage Blob Data Reader role
az role assignment create `
    --assignee $principalId `
    --role "Storage Blob Data Reader" `
    --scope $storageId `
    --output table

Write-Host "✅ Storage permissions granted" -ForegroundColor Green

# Step 6: Configure Application Settings
Write-Host "⚙️ Configuring application settings..." -ForegroundColor Yellow
az webapp config appsettings set `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --settings `
        "ASPNETCORE_ENVIRONMENT=Production" `
        "AzureStorage__AccountName=$StorageAccountName" `
        "AzureStorage__ConfigurationContainer=$ContainerName" `
        "AzureStorage__DefaultConfigurationBlob=default-metric-configuration.json" `
        "Logging__LogLevel__Default=Information" `
        "ApiSettings__Version=1.0.0" `
        "ApiSettings__Environment=Production" `
    --output table

# Step 7: Build and Publish Application
Write-Host "🔨 Building and publishing application..." -ForegroundColor Yellow
$projectPath = "D:\Projects\sxg-eval-platform\src\Sxg-Eval-Platform-Api"
$publishPath = "$projectPath\publish"

# Clean previous publish
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

# Build and publish
Push-Location $projectPath
try {
    dotnet publish -c Release -o $publishPath --no-restore
    
    # Create ZIP file for deployment
    $zipPath = "$projectPath\publish.zip"
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    
    Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force
    Write-Host "✅ Application packaged successfully" -ForegroundColor Green
    
    # Deploy to Azure
    Write-Host "🚀 Deploying to Azure..." -ForegroundColor Yellow
    az webapp deploy `
        --name $AppName `
        --resource-group $ResourceGroupName `
        --src-path $zipPath `
        --type zip `
        --output table
        
} finally {
    Pop-Location
}

# Step 8: Upload Default Configuration (if exists)
Write-Host "📄 Uploading default configuration..." -ForegroundColor Yellow
$configFilePath = "$projectPath\sample-data\default-metric-configuration.json"
if (Test-Path $configFilePath) {
    az storage blob upload `
        --account-name $StorageAccountName `
        --container-name $ContainerName `
        --name "default-metric-configuration.json" `
        --file $configFilePath `
        --auth-mode login `
        --overwrite `
        --output table
    Write-Host "✅ Default configuration uploaded" -ForegroundColor Green
} else {
    Write-Host "⚠️ Default configuration file not found: $configFilePath" -ForegroundColor Yellow
}

# Step 9: Get Application URL and Test
Write-Host "🌐 Getting application details..." -ForegroundColor Yellow
$appUrl = az webapp show `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --query defaultHostName -o tsv

Write-Host "=================================" -ForegroundColor Green
Write-Host "🎉 DEPLOYMENT COMPLETED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green
Write-Host ""
Write-Host "📱 Application URL: https://$appUrl" -ForegroundColor Cyan
Write-Host "📖 Swagger UI: https://$appUrl" -ForegroundColor Cyan
Write-Host "❤️ Health Check: https://$appUrl/api/v1/health" -ForegroundColor Cyan
Write-Host "⚙️ Default Config: https://$appUrl/api/v1/eval/configurations" -ForegroundColor Cyan
Write-Host ""

# Test health endpoint
Write-Host "🧪 Testing health endpoint..." -ForegroundColor Yellow
try {
    $healthResponse = Invoke-RestMethod -Uri "https://$appUrl/api/v1/health" -Method Get -TimeoutSec 30
    Write-Host "✅ Health check successful!" -ForegroundColor Green
    Write-Host "Response: $($healthResponse | ConvertTo-Json -Compress)" -ForegroundColor Gray
} catch {
    Write-Host "⚠️ Health check failed (app may still be starting up): $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host "1. Test all API endpoints using Swagger UI" -ForegroundColor White
Write-Host "2. Configure custom domain (if needed)" -ForegroundColor White
Write-Host "3. Set up Application Insights monitoring" -ForegroundColor White
Write-Host "4. Configure auto-scaling rules" -ForegroundColor White
Write-Host "5. Set up CI/CD pipeline for automated deployments" -ForegroundColor White

Write-Host ""
Write-Host "🎯 Deployment completed successfully! 🎉" -ForegroundColor Green