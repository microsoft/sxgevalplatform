# Quick Fix - Assign Missing AcrPull Permission
# Run this with admin permissions to fix the container app deployment issue

param(
    [string]$SubscriptionId = "d2ef7484-d847-4ca9-88be-d2d9f2a8a50f",
    [string]$ResourceGroup = "rg-sxg-agent-evaluation-platform", 
    [string]$ContainerAppName = "eval-framework-app",
    [string]$ContainerRegistry = "evalplatformregistry"
)

Write-Host "🔧 QUICK FIX - Assigning Missing AcrPull Permission" -ForegroundColor Yellow
Write-Host "=================================================" -ForegroundColor Gray

# Set context
az account set --subscription $SubscriptionId

# Get managed identity
Write-Host "Getting container app managed identity..." -ForegroundColor Blue
$identity = az containerapp show --name $ContainerAppName --resource-group $ResourceGroup --query "identity.principalId" --output tsv

if (-not $identity) {
    Write-Error "❌ No managed identity found"
    exit 1
}

Write-Host "✅ Identity: $identity" -ForegroundColor Green

# Check if AcrPull already exists
Write-Host "Checking existing permissions..." -ForegroundColor Blue
$acrScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.ContainerRegistry/registries/$ContainerRegistry"
$existing = az role assignment list --assignee $identity --role "AcrPull" --scope $acrScope --query "[0].id" --output tsv

if ($existing) {
    Write-Host "✅ AcrPull permission already exists" -ForegroundColor Green
}
else {
    Write-Host "❌ AcrPull permission missing - assigning now..." -ForegroundColor Red
    
    try {
        az role assignment create --assignee-object-id $identity --assignee-principal-type ServicePrincipal --role "AcrPull" --scope $acrScope --output none
        Write-Host "✅ AcrPull permission assigned successfully!" -ForegroundColor Green
    }
    catch {
        Write-Error "❌ Failed to assign permission: $_"
        exit 1
    }
}

Write-Host "`n⏱️  Waiting 30 seconds for permissions to propagate..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

Write-Host "`n🎯 Now try your deployment command:" -ForegroundColor Cyan
Write-Host "az containerapp update --name $ContainerAppName --resource-group $ResourceGroup --image evalplatformregistry.azurecr.io/eval-framework-app:latest" -ForegroundColor Gray

Write-Host "`n✅ Quick fix completed!" -ForegroundColor Green