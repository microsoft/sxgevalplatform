# Azure Deployment Script for SXG Evaluation Platform API - PPE Environment
# This script creates a new App Service "sxgevalapippe" and deploys the API

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
    [string]$ContainerName = "eval-metrics-configuration",
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId
)

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "Starting Azure deployment for SXG Evaluation Platform API - PPE Environment..." -ForegroundColor Green
Write-Host "Target App Service: $AppName" -ForegroundColor Cyan

# Login check
Write-Host "Checking Azure CLI login status..." -ForegroundColor Yellow
$loginCheck = az account show --output json 2>$null
if (-not $loginCheck) {
    Write-Host "Not logged in to Azure CLI. Please login first." -ForegroundColor Red
    Write-Host "Run: az login" -ForegroundColor Yellow
    exit 1
}

# Set subscription if provided
if ($SubscriptionId) {
    Write-Host "Setting subscription to: $SubscriptionId" -ForegroundColor Yellow
    az account set --subscription $SubscriptionId
}

$currentSub = (az account show --query name -o tsv)
Write-Host "Using subscription: $currentSub" -ForegroundColor Green

# Step 1: Verify Resource Group exists
Write-Host "Verifying resource group: $ResourceGroupName" -ForegroundColor Yellow
$rgExists = az group show --name $ResourceGroupName --query "name" -o tsv 2>$null
if (-not $rgExists) {
    Write-Host "Resource group $ResourceGroupName does not exist. Please create it first." -ForegroundColor Red
    exit 1
}
Write-Host "Resource group verified: $ResourceGroupName" -ForegroundColor Green

# Step 2: Check if App Service Plan exists
Write-Host "Checking App Service Plan..." -ForegroundColor Yellow
$appServicePlan = "asp-sxg-eval-platform"
$planExists = az appservice plan show --name $appServicePlan --resource-group $ResourceGroupName --query "name" -o tsv 2>$null

if (-not $planExists) {
    Write-Host "Creating App Service Plan: $appServicePlan" -ForegroundColor Yellow
    az appservice plan create `
        --name $appServicePlan `
        --resource-group $ResourceGroupName `
        --sku B1 `
        --is-linux `
        --output table
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to create App Service Plan" -ForegroundColor Red
        exit 1
    }
    Write-Host "App Service Plan created: $appServicePlan" -ForegroundColor Green
} else {
    Write-Host "App Service Plan already exists: $appServicePlan" -ForegroundColor Green
}

# Step 3: Create App Service (PPE)
Write-Host "Creating App Service: $AppName" -ForegroundColor Yellow
$appExists = az webapp show --name $AppName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null

if (-not $appExists) {
    az webapp create `
        --name $AppName `
        --resource-group $ResourceGroupName `
        --plan $appServicePlan `
        --runtime "DOTNETCORE:8.0" `
        --output table
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to create App Service" -ForegroundColor Red
        exit 1
    }
    Write-Host "App Service created: $AppName" -ForegroundColor Green
} else {
    Write-Host "App Service already exists: $AppName" -ForegroundColor Green
}

# Step 4: Configure App Settings for PPE Environment
Write-Host "Configuring App Settings for PPE..." -ForegroundColor Yellow
az webapp config appsettings set `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --settings `
        "ASPNETCORE_ENVIRONMENT=PPE" `
        "AzureStorage__AccountName=$StorageAccountName" `
        "ApiSettings__Environment=PPE" `
    --output table

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to configure App Settings" -ForegroundColor Red
    exit 1
}
Write-Host "App Settings configured for PPE environment" -ForegroundColor Green

# Step 5: Build and Deploy
Write-Host "Building application..." -ForegroundColor Yellow
$projectPath = "../../SXG.EvalPlatform.API.csproj"

if (-not (Test-Path $projectPath)) {
    Write-Host "Project file not found: $projectPath" -ForegroundColor Red
    exit 1
}

dotnet build $projectPath --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "Build successful" -ForegroundColor Green

# Step 6: Create deployment package
Write-Host "Creating deployment package..." -ForegroundColor Yellow
$publishPath = "./publish-ppe"
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

dotnet publish $projectPath --configuration Release --output $publishPath --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed" -ForegroundColor Red
    exit 1
}
Write-Host "Publish successful" -ForegroundColor Green

# Step 7: Deploy to Azure
Write-Host "Deploying to Azure App Service..." -ForegroundColor Yellow
az webapp deploy `
    --name $AppName `
    --resource-group $ResourceGroupName `
    --src-path "$publishPath" `
    --type zip `
    --async false

if ($LASTEXITCODE -ne 0) {
    Write-Host "Deployment failed" -ForegroundColor Red
    exit 1
}

# Clean up
Remove-Item $publishPath -Recurse -Force

Write-Host "Deployment completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "PPE API URL: https://$AppName.azurewebsites.net" -ForegroundColor Cyan
Write-Host "Swagger UI: https://$AppName.azurewebsites.net/swagger" -ForegroundColor Cyan
Write-Host "Storage Account: $StorageAccountName" -ForegroundColor Cyan
Write-Host ""
Write-Host "You can monitor the deployment at:" -ForegroundColor Yellow
Write-Host "   https://portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$ResourceGroupName/providers/Microsoft.Web/sites/$AppName" -ForegroundColor Yellow
Write-Host ""
Write-Host "PPE Environment deployment complete!" -ForegroundColor Green