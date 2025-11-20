# RBAC Role Assignment Script for Container App Managed Identity
param(
    [Parameter(Mandatory = $true)]
    [string]$ContainerAppName = "eval-framework-app",
    
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup = "rg-sxg-agent-evaluation-platform",
    
    [Parameter(Mandatory = $true)]
    [string]$StorageAccountName,
    
    [Parameter(Mandatory = $true)]
    [string]$OpenAiAccountName = "evalplatform",
    
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId
)

Write-Host "[RBAC] Configuring RBAC permissions for Container App managed identity..." -ForegroundColor Blue

# Get the Container App's managed identity principal ID
Write-Host "Getting Container App managed identity..." -ForegroundColor Yellow
$principalId = az containerapp show --name $ContainerAppName --resource-group $ResourceGroup --query identity.principalId -o tsv

if (-not $principalId -or $principalId -eq "null") {
    Write-Host "[ERROR] Failed to get managed identity principal ID. Ensure the Container App has system-assigned managed identity enabled." -ForegroundColor Red
    exit 1
}

Write-Host "[SUCCESS] Found managed identity principal ID: $principalId" -ForegroundColor Green

# Storage Account Permissions
Write-Host "`n[STORAGE] Assigning Storage Account permissions..." -ForegroundColor Yellow

$storageScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Storage/storageAccounts/$StorageAccountName"

# Storage Queue Data Contributor
Write-Host "Assigning Storage Queue Data Contributor role..." -ForegroundColor Cyan
az role assignment create `
    --assignee $principalId `
    --role "Storage Queue Data Contributor" `
    --scope $storageScope

if ($LASTEXITCODE -eq 0) {
    Write-Host "[SUCCESS] Storage Queue Data Contributor role assigned" -ForegroundColor Green
}
else {
    Write-Host "[ERROR] Failed to assign Storage Queue Data Contributor role" -ForegroundColor Red
}

# Storage Blob Data Contributor
Write-Host "Assigning Storage Blob Data Contributor role..." -ForegroundColor Cyan
az role assignment create `
    --assignee $principalId `
    --role "Storage Blob Data Contributor" `
    --scope $storageScope

if ($LASTEXITCODE -eq 0) {
    Write-Host "[SUCCESS] Storage Blob Data Contributor role assigned" -ForegroundColor Green
}
else {
    Write-Host "[ERROR] Failed to assign Storage Blob Data Contributor role" -ForegroundColor Red
}

# Azure OpenAI Permissions
Write-Host "`n[OPENAI] Assigning Azure OpenAI permissions..." -ForegroundColor Yellow

$openAiScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.CognitiveServices/accounts/$OpenAiAccountName"

# Cognitive Services OpenAI User
Write-Host "Assigning Cognitive Services OpenAI User role..." -ForegroundColor Cyan
az role assignment create `
    --assignee $principalId `
    --role "Cognitive Services OpenAI User" `
    --scope $openAiScope

if ($LASTEXITCODE -eq 0) {
    Write-Host "[SUCCESS] Cognitive Services OpenAI User role assigned" -ForegroundColor Green
}
else {
    Write-Host "[ERROR] Failed to assign Cognitive Services OpenAI User role" -ForegroundColor Red
}

# Verify role assignments
Write-Host "`n[VERIFY] Verifying role assignments..." -ForegroundColor Yellow
$roleAssignments = az role assignment list --assignee $principalId --output table

Write-Host "`nCurrent role assignments for managed identity:" -ForegroundColor Cyan
Write-Host $roleAssignments

Write-Host "`n[SUCCESS] RBAC configuration completed!" -ForegroundColor Green
Write-Host "The Container App can now access:" -ForegroundColor Blue
Write-Host "  • Azure Storage queues and blobs" -ForegroundColor White
Write-Host "  • Azure OpenAI service" -ForegroundColor White
Write-Host "`nNote: Role propagation may take a few minutes to take effect." -ForegroundColor Yellow