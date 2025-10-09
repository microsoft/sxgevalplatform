# Azure Deployment Script for SXG Evaluation Platform API - Development Environment
# This script creates a new App Service "sxgevalapidev" and deploys the API

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-sxg-agent-evaluation-platform",
    
    [Parameter(Mandatory=$false)]
    [string]$AppName = "sxgevalapidev",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "eastus",
    
    [Parameter(Mandatory=$false)]
    [string]$StorageAccountName = "sxgagenteval",
    
    [Parameter(Mandatory=$false)]
    [string]$ContainerName = "eval-metrics-configuration",
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId
)

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting Azure deployment for SXG Evaluation Platform API - Dev Environment..." -ForegroundColor Green
Write-Host "Target App Service: $AppName" -ForegroundColor Cyan

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

# Step 1: Verify Resource Group exists
Write-Host "📦 Verifying resource group: $ResourceGroupName" -ForegroundColor Yellow
$rgExists = az group show --name $ResourceGroupName --query "name" -o tsv 2>$null
if (-not $rgExists) {
    Write-Host "❌ Resource group '$ResourceGroupName' not found" -ForegroundColor Red
    Write-Host "Please ensure the resource group exists or provide the correct name." -ForegroundColor Yellow
    exit 1
}
Write-Host "✅ Resource group verified: $ResourceGroupName" -ForegroundColor Green

# Step 2: Check if App Service already exists
Write-Host "🔍 Checking if App Service already exists..." -ForegroundColor Yellow
$existingApp = az webapp show --name $AppName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null
if ($existingApp) {
    Write-Host "⚠️ App Service '$AppName' already exists. Will update existing app." -ForegroundColor Yellow
} else {
    Write-Host "✨ Creating new App Service: $AppName" -ForegroundColor Green
    
    # Create App Service Plan
    Write-Host "🏗️ Creating App Service Plan..." -ForegroundColor Yellow
    $planName = "$AppName-plan"
    az appservice plan create `
        --name $planName `
        --resource-group $ResourceGroupName `
        --location $Location `
        --sku B1 `
        --is-linux `
        --output table

    # Create Web App
    Write-Host "🌐 Creating Web App: $AppName" -ForegroundColor Yellow
    az webapp create `
        --name $AppName `
        --resource-group $ResourceGroupName `
        --plan $planName `
        --runtime "DOTNET|8.0" `
        --output table
}

# Step 3: Enable Managed Identity
Write-Host "🔐 Enabling/Verifying Managed Identity..." -ForegroundColor Yellow
az webapp identity assign `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --output table

# Get Principal ID
$principalId = az webapp identity show `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --query principalId -o tsv

Write-Host "✅ Managed Identity Principal ID: $principalId" -ForegroundColor Green

# Step 4: Verify Storage Account
Write-Host "🔍 Verifying storage account..." -ForegroundColor Yellow
$storageExists = az storage account show --name $StorageAccountName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null
if (-not $storageExists) {
    Write-Host "❌ Storage account '$StorageAccountName' not found in resource group '$ResourceGroupName'" -ForegroundColor Red
    Write-Host "Please ensure the storage account exists and is accessible." -ForegroundColor Yellow
    exit 1
}
Write-Host "✅ Storage account verified: $StorageAccountName" -ForegroundColor Green

# Get storage account resource ID
$storageId = az storage account show `
    --name $StorageAccountName `
    --resource-group $ResourceGroupName `
    --query id -o tsv

# Step 5: Grant Storage Permissions (try with error handling)
Write-Host "🔑 Configuring storage permissions..." -ForegroundColor Yellow

# Storage Blob Data Contributor (read/write blobs)
Write-Host "  📝 Assigning Storage Blob Data Contributor role..." -ForegroundColor Gray
try {
    az role assignment create `
        --assignee $principalId `
        --role "Storage Blob Data Contributor" `
        --scope $storageId `
        --output table 2>$null
    Write-Host "  ✅ Storage Blob Data Contributor role assigned" -ForegroundColor Green
} catch {
    Write-Host "  ⚠️ Storage Blob Data Contributor role may already be assigned" -ForegroundColor Yellow
}

# Storage Table Data Contributor (read/write tables)
Write-Host "  📊 Assigning Storage Table Data Contributor role..." -ForegroundColor Gray
try {
    az role assignment create `
        --assignee $principalId `
        --role "Storage Table Data Contributor" `
        --scope $storageId `
        --output table 2>$null
    Write-Host "  ✅ Storage Table Data Contributor role assigned" -ForegroundColor Green
} catch {
    Write-Host "  ⚠️ Storage Table Data Contributor role may already be assigned" -ForegroundColor Yellow
}

# Storage Queue Data Contributor (read/write queues)
Write-Host "  📬 Assigning Storage Queue Data Contributor role..." -ForegroundColor Gray
try {
    az role assignment create `
        --assignee $principalId `
        --role "Storage Queue Data Contributor" `
        --scope $storageId `
        --output table 2>$null
    Write-Host "  ✅ Storage Queue Data Contributor role assigned" -ForegroundColor Green
} catch {
    Write-Host "  ⚠️ Storage Queue Data Contributor role may already be assigned" -ForegroundColor Yellow
}

# Step 6: Configure Application Settings
Write-Host "⚙️ Configuring application settings..." -ForegroundColor Yellow
az webapp config appsettings set `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --settings `
        "ASPNETCORE_ENVIRONMENT=Development" `
        "AzureStorage__AccountName=$StorageAccountName" `
        "AzureStorage__ConfigurationContainer=$ContainerName" `
        "AzureStorage__DefaultConfigurationBlob=default-metric-configuration.json" `
        "Logging__LogLevel__Default=Information" `
        "Logging__LogLevel__Microsoft.AspNetCore=Warning" `
        "ApiSettings__Version=1.0.0" `
        "ApiSettings__Environment=Development" `
    --output table

# Step 7: Build and Publish Application
Write-Host "🔨 Building and publishing application..." -ForegroundColor Yellow
$projectPath = (Get-Location).Path
$publishPath = "$projectPath\publish"

# Clean previous publish
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

# Build and publish
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
        
} catch {
    Write-Host "❌ Build or deployment failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 8: Create and Upload Default Configuration (if not exists)
Write-Host "📄 Checking/uploading default configuration..." -ForegroundColor Yellow
$configFilePath = "$projectPath\metrics-configuration\default-metric-configuration.json"
if (Test-Path $configFilePath) {
    try {
        # Create container if it doesn't exist
        az storage container create `
            --account-name $StorageAccountName `
            --name $ContainerName `
            --auth-mode login `
            --public-access off `
            --output table 2>$null
        
        # Check if blob already exists
        $blobExists = az storage blob exists `
            --account-name $StorageAccountName `
            --container-name $ContainerName `
            --name "default-metric-configuration.json" `
            --auth-mode login `
            --query "exists" -o tsv
        
        if ($blobExists -eq "true") {
            Write-Host "  ℹ️ Default configuration already exists in storage" -ForegroundColor Gray
        } else {
            az storage blob upload `
                --account-name $StorageAccountName `
                --container-name $ContainerName `
                --name "default-metric-configuration.json" `
                --file $configFilePath `
                --auth-mode login `
                --overwrite `
                --output table
            Write-Host "  ✅ Default configuration uploaded" -ForegroundColor Green
        }
    } catch {
        Write-Host "  ⚠️ Could not upload default configuration: $($_.Exception.Message)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ⚠️ Default configuration file not found: $configFilePath" -ForegroundColor Yellow
}

# Step 9: Get Application URL and Test
Write-Host "🌐 Getting application details..." -ForegroundColor Yellow
$appUrl = az webapp show `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --query defaultHostName -o tsv

Write-Host ""
Write-Host "=================================" -ForegroundColor Green
Write-Host "🎉 DEPLOYMENT COMPLETED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green
Write-Host ""
Write-Host "📱 Application URL: https://$appUrl" -ForegroundColor Cyan
Write-Host "📖 Swagger UI: https://$appUrl/swagger" -ForegroundColor Cyan
Write-Host "❤️ Health Check: https://$appUrl/api/v1/health" -ForegroundColor Cyan
Write-Host "⚙️ Default Config: https://$appUrl/api/v1/eval/configurations/default" -ForegroundColor Cyan
Write-Host "💾 Configuration API: https://$appUrl/api/v1/eval/configurations" -ForegroundColor Cyan
Write-Host ""

# Test health endpoint
Write-Host "🧪 Testing health endpoint..." -ForegroundColor Yellow
Start-Sleep -Seconds 10  # Give app time to warm up
try {
    $healthResponse = Invoke-RestMethod -Uri "https://$appUrl/api/v1/health" -Method Get -TimeoutSec 30
    Write-Host "✅ Health check successful!" -ForegroundColor Green
    Write-Host "Response: $($healthResponse | ConvertTo-Json -Compress)" -ForegroundColor Gray
} catch {
    Write-Host "⚠️ Health check failed (app may still be starting up): $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "Please wait a few minutes and try manually: https://$appUrl/api/v1/health" -ForegroundColor Gray
}

Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host "1. Test all API endpoints using Swagger UI: https://$appUrl/swagger" -ForegroundColor White
Write-Host "2. Test default configuration endpoint: https://$appUrl/api/v1/eval/configurations/default" -ForegroundColor White
Write-Host "3. Create agent-specific configurations using POST endpoint" -ForegroundColor White
Write-Host "4. Configure custom domain (if needed)" -ForegroundColor White
Write-Host "5. Set up Application Insights monitoring" -ForegroundColor White

Write-Host ""
Write-Host "🎯 Deployment completed successfully! 🎉" -ForegroundColor Green