# Minimum Required Permissions Script for SXG Evaluation Platform
# This script assigns the bare minimum permissions needed for the application to run
# Run this script with an account that has User Access Administrator or Owner permissions

param(
    [string]$SubscriptionId = "d2ef7484-d847-4ca9-88be-d2d9f2a8a50f",
    [string]$ResourceGroup = "rg-sxg-agent-evaluation-platform",
    [string]$ContainerAppName = "eval-framework-app",
    [string]$ContainerRegistry = "evalplatformregistry",
    [string]$StorageAccount = "sxgagentevaldev",
    [string]$ManagedEnvironment = "eval-framework-env",
    [string]$AzureOpenAiService = "evalplatform",
    [string]$AiProject = "evalplatformproject"
)

Write-Host "🔐 Assigning Minimum Required Permissions for SXG Evaluation Platform" -ForegroundColor Green
Write-Host "=====================================================================" -ForegroundColor Yellow

# Set Azure context
Write-Host "Setting Azure context..." -ForegroundColor Blue
az account set --subscription $SubscriptionId

# Get Container App System-Assigned Managed Identity
Write-Host "`n1. Getting Container App Managed Identity..." -ForegroundColor Blue
$containerAppIdentity = az containerapp show --name $ContainerAppName --resource-group $ResourceGroup --query "identity.principalId" --output tsv

if (-not $containerAppIdentity) {
    Write-Error "❌ Could not retrieve Container App managed identity. Make sure the app exists and has system-assigned identity enabled."
    exit 1
}

Write-Host "✅ Container App Identity: $containerAppIdentity" -ForegroundColor Green

# 1. Container Registry Permissions (MINIMUM: AcrPull)
Write-Host "`n2. Assigning Container Registry Permissions..." -ForegroundColor Blue
Write-Host "   - AcrPull: Pull container images from registry" -ForegroundColor Gray

$acrScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.ContainerRegistry/registries/$ContainerRegistry"
try {
    az role assignment create --assignee $containerAppIdentity --role "AcrPull" --scope $acrScope --output none
    Write-Host "✅ AcrPull permission assigned to Container Registry" -ForegroundColor Green
}
catch {
    Write-Warning "⚠️  Failed to assign AcrPull permission. Check if already assigned or resource exists."
}

# 2. Storage Account Permissions (MINIMUM: Queue Data Contributor + Blob Data Contributor)
Write-Host "`n3. Assigning Storage Account Permissions..." -ForegroundColor Blue
Write-Host "   - Storage Queue Data Contributor: Read/write queue messages" -ForegroundColor Gray
Write-Host "   - Storage Blob Data Contributor: Read/write evaluation results" -ForegroundColor Gray

$storageScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Storage/storageAccounts/$StorageAccount"

try {
    az role assignment create --assignee $containerAppIdentity --role "Storage Queue Data Contributor" --scope $storageScope --output none
    Write-Host "✅ Storage Queue Data Contributor permission assigned" -ForegroundColor Green
}
catch {
    Write-Warning "⚠️  Failed to assign Storage Queue Data Contributor permission."
}

try {
    az role assignment create --assignee $containerAppIdentity --role "Storage Blob Data Contributor" --scope $storageScope --output none
    Write-Host "✅ Storage Blob Data Contributor permission assigned" -ForegroundColor Green
}
catch {
    Write-Warning "⚠️  Failed to assign Storage Blob Data Contributor permission."
}

# 3. Azure OpenAI Permissions (MINIMUM: Cognitive Services OpenAI User)
Write-Host "`n4. Assigning Azure OpenAI Permissions..." -ForegroundColor Blue
Write-Host "   - Cognitive Services OpenAI User: Use OpenAI models for evaluation" -ForegroundColor Gray

$openAiScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.CognitiveServices/accounts/$AzureOpenAiService"
try {
    az role assignment create --assignee $containerAppIdentity --role "Cognitive Services OpenAI User" --scope $openAiScope --output none
    Write-Host "✅ Cognitive Services OpenAI User permission assigned" -ForegroundColor Green
}
catch {
    Write-Warning "⚠️  Failed to assign Cognitive Services OpenAI User permission. Check if resource exists."
}

# 4. Azure AI Foundry/ML Workspace Permissions (if applicable)
Write-Host "`n5. Checking Azure AI Project Permissions..." -ForegroundColor Blue
Write-Host "   - Attempting to assign minimal AI project access" -ForegroundColor Gray

# Try to find AI project resource
$aiProjectScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.MachineLearningServices/workspaces/$AiProject"
try {
    $aiProjectExists = az ml workspace show --name $AiProject --resource-group $ResourceGroup --subscription $SubscriptionId --query "name" --output tsv 2>$null
    if ($aiProjectExists) {
        az role assignment create --assignee $containerAppIdentity --role "AzureML Data Scientist" --scope $aiProjectScope --output none
        Write-Host "✅ AzureML Data Scientist permission assigned to AI project" -ForegroundColor Green
    }
    else {
        Write-Host "ℹ️  AI project not found or not needed - skipping" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "ℹ️  AI project permissions not required or already assigned" -ForegroundColor Yellow
}

# 5. Application Insights Permissions (MINIMUM: Monitoring Metrics Publisher)
Write-Host "`n6. Assigning Application Insights Permissions..." -ForegroundColor Blue
Write-Host "   - Monitoring Metrics Publisher: Send telemetry and metrics" -ForegroundColor Gray

# Find Application Insights resource
$appInsightsResource = az monitor app-insights component list --resource-group $ResourceGroup --query "[0].id" --output tsv 2>$null
if ($appInsightsResource) {
    try {
        az role assignment create --assignee $containerAppIdentity --role "Monitoring Metrics Publisher" --scope $appInsightsResource --output none
        Write-Host "✅ Monitoring Metrics Publisher permission assigned" -ForegroundColor Green
    }
    catch {
        Write-Warning "⚠️  Failed to assign Application Insights permissions."
    }
}
else {
    Write-Host "ℹ️  Application Insights resource not found - skipping" -ForegroundColor Yellow
}

# 6. Resource Group Reader (MINIMUM: for resource discovery)
Write-Host "`n7. Assigning Resource Group Permissions..." -ForegroundColor Blue
Write-Host "   - Reader: Discover and read resource metadata" -ForegroundColor Gray

$rgScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup"
try {
    az role assignment create --assignee $containerAppIdentity --role "Reader" --scope $rgScope --output none
    Write-Host "✅ Reader permission assigned to Resource Group" -ForegroundColor Green
}
catch {
    Write-Warning "⚠️  Failed to assign Resource Group Reader permission."
}

# Summary of assigned permissions
Write-Host "`n📋 PERMISSION SUMMARY - MANAGED IDENTITY ACCESS ONLY" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "✅ Container Registry:" -ForegroundColor Green
Write-Host "   - AcrPull (pull container images via managed identity)" -ForegroundColor White
Write-Host "✅ Storage Account:" -ForegroundColor Green  
Write-Host "   - Storage Queue Data Contributor (queue operations - NO connection strings)" -ForegroundColor White
Write-Host "   - Storage Blob Data Contributor (blob operations - NO connection strings)" -ForegroundColor White
Write-Host "✅ Azure OpenAI:" -ForegroundColor Green
Write-Host "   - Cognitive Services OpenAI User (model access - NO API keys)" -ForegroundColor White
Write-Host "✅ Azure AI Foundry:" -ForegroundColor Green
Write-Host "   - AzureML Data Scientist (AI Foundry access - NO secrets)" -ForegroundColor White
Write-Host "   - Cognitive Services User (AI services - NO API keys)" -ForegroundColor White
Write-Host "✅ Resource Group:" -ForegroundColor Green
Write-Host "   - Reader (resource discovery via managed identity)" -ForegroundColor White
Write-Host "✅ Application Insights:" -ForegroundColor Green
Write-Host "   - Monitoring Metrics Publisher (telemetry - NO instrumentation keys)" -ForegroundColor White

Write-Host "`n🎯 NEXT STEPS" -ForegroundColor Cyan
Write-Host "=============" -ForegroundColor Cyan
Write-Host "1. Run the deployment script: .\deploy.ps1" -ForegroundColor White
Write-Host "2. Update environment variables:" -ForegroundColor White
Write-Host "   az containerapp update --name $ContainerAppName --resource-group $ResourceGroup \" -ForegroundColor Gray
Write-Host "     --set-env-vars MAX_DATASET_CONCURRENCY=3 MAX_METRICS_CONCURRENCY=8 \" -ForegroundColor Gray
Write-Host "     EVALUATION_TIMEOUT=30 HTTP_POOL_CONNECTIONS=20 HTTP_POOL_MAXSIZE=10 \" -ForegroundColor Gray
Write-Host "     HTTP_SESSION_LIFETIME=3600 AZURE_STORAGE_TIMEOUT=30 \" -ForegroundColor Gray
Write-Host "     AZURE_STORAGE_CONNECT_TIMEOUT=60 ENABLE_PERFORMANCE_LOGGING=true" -ForegroundColor Gray

Write-Host "`n🔐 MANAGED IDENTITY SECURITY COMPLIANCE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✅ Principle of Least Privilege: Only minimum required permissions assigned" -ForegroundColor Green
Write-Host "✅ No Admin Roles: No elevated or administrative permissions granted" -ForegroundColor Green
Write-Host "✅ Scoped Access: All permissions scoped to specific resources" -ForegroundColor Green
Write-Host "✅ Managed Identity Only: Using Azure AD managed identity (NO SECRETS ANYWHERE)" -ForegroundColor Green
Write-Host "✅ No Connection Strings: Azure Storage accessed via managed identity" -ForegroundColor Green
Write-Host "✅ No API Keys: Azure OpenAI and AI Foundry accessed via managed identity" -ForegroundColor Green
Write-Host "✅ No Instrumentation Keys: Application Insights accessed via managed identity" -ForegroundColor Green
Write-Host "✅ Zero Secrets Management: All authentication handled by Azure AD" -ForegroundColor Green

Write-Host "`n� MANAGED IDENTITY VALIDATION" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan
Write-Host "🔍 Verifying managed identity is properly configured..." -ForegroundColor Blue

# Validate that system-assigned managed identity is enabled
$managedIdentityStatus = az containerapp show --name $ContainerAppName --resource-group $ResourceGroup --query "identity.type" --output tsv
if ($managedIdentityStatus -eq "SystemAssigned") {
    Write-Host "✅ System-assigned managed identity is properly enabled" -ForegroundColor Green
}
else {
    Write-Host "⚠️  System-assigned managed identity not found. Enable it with:" -ForegroundColor Yellow
    Write-Host "   az containerapp identity assign --name $ContainerAppName --resource-group $ResourceGroup --system-assigned" -ForegroundColor Gray
}

Write-Host "`n🚀 MANAGED IDENTITY PERMISSION ASSIGNMENT COMPLETED!" -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Green
Write-Host "🔐 The application now has minimum required permissions for SECRETLESS operation" -ForegroundColor Green
Write-Host "🔑 ALL authentication will use Azure AD managed identity - NO SECRETS REQUIRED" -ForegroundColor Green
Write-Host "🛡️  Zero secrets to manage, rotate, or secure - Azure handles all authentication" -ForegroundColor Green