# Setup Managed Identity Permissions for Local Development
# This script sets up the necessary permissions for the application to access Azure Blob Storage

param(
    [Parameter(Mandatory=$false)]
    [string]$StorageAccountName = "sxgagenteval",
    
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-sxg-agent-evaluation-platform"
)

Write-Host "🔧 Setting up local development access to Azure Storage..." -ForegroundColor Green

# Check if logged in
$loginCheck = az account show --output json 2>$null
if (-not $loginCheck) {
    Write-Host "❌ Please login to Azure CLI first: az login" -ForegroundColor Red
    exit 1
}

# Get current user
$currentUser = az account show --query user.name -o tsv
Write-Host "✅ Current user: $currentUser" -ForegroundColor Green

# Get storage account resource ID
$storageId = az storage account show --name $StorageAccountName --resource-group $ResourceGroupName --query id -o tsv 2>$null
if (-not $storageId) {
    Write-Host "❌ Storage account '$StorageAccountName' not found in resource group '$ResourceGroupName'" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Storage account found: $storageId" -ForegroundColor Green

# Assign Storage Blob Data Contributor role for local development
Write-Host "🔑 Assigning Storage Blob Data Contributor role..." -ForegroundColor Yellow
az role assignment create `
    --assignee $currentUser `
    --role "Storage Blob Data Contributor" `
    --scope $storageId `
    --output table

Write-Host "✅ Permissions assigned successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Note: For production, use the web app's managed identity instead." -ForegroundColor Yellow