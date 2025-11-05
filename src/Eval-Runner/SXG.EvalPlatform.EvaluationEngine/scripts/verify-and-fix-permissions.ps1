# Verification and Fix Script for Container App Managed Identity Permissions
# This script verifies current permissions and fixes any missing ones
# Run this with an account that has User Access Administrator or Owner permissions

param(
    [string]$SubscriptionId = "d2ef7484-d847-4ca9-88be-d2d9f2a8a50f",
    [string]$ResourceGroup = "rg-sxg-agent-evaluation-platform",
    [string]$ContainerAppName = "eval-framework-app",
    [string]$ContainerRegistry = "evalplatformregistry"
)

Write-Host "🔍 VERIFYING AND FIXING CONTAINER APP PERMISSIONS" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Yellow

# Set Azure context
Write-Host "`nSetting Azure context..." -ForegroundColor Blue
az account set --subscription $SubscriptionId

# Get Container App System-Assigned Managed Identity
Write-Host "`n1. Getting Container App Managed Identity..." -ForegroundColor Blue
$containerAppIdentity = az containerapp show --name $ContainerAppName --resource-group $ResourceGroup --query "identity.principalId" --output tsv

if (-not $containerAppIdentity) {
    Write-Error "❌ Could not retrieve Container App managed identity. Make sure the app exists and has system-assigned identity enabled."
    Write-Host "To enable system-assigned identity:" -ForegroundColor Yellow
    Write-Host "az containerapp identity assign --name $ContainerAppName --resource-group $ResourceGroup --system-assigned" -ForegroundColor Gray
    exit 1
}

Write-Host "✅ Container App Identity: $containerAppIdentity" -ForegroundColor Green

# Check current identity status
$identityType = az containerapp show --name $ContainerAppName --resource-group $ResourceGroup --query "identity.type" --output tsv
Write-Host "Identity Type: $identityType" -ForegroundColor Gray

# 2. Check current ACR permissions
Write-Host "`n2. Checking Current ACR Permissions..." -ForegroundColor Blue
$acrScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.ContainerRegistry/registries/$ContainerRegistry"

Write-Host "Checking existing role assignments on ACR..." -ForegroundColor Gray
$existingAssignments = az role assignment list --assignee $containerAppIdentity --scope $acrScope --output json | ConvertFrom-Json

$hasAcrPull = $false
if ($existingAssignments) {
    foreach ($assignment in $existingAssignments) {
        if ($assignment.roleDefinitionName -eq "AcrPull") {
            $hasAcrPull = $true
            Write-Host "✅ AcrPull role already assigned" -ForegroundColor Green
            break
        }
    }
}

if (-not $hasAcrPull) {
    Write-Host "❌ AcrPull role NOT found. Attempting to assign..." -ForegroundColor Red
    try {
        az role assignment create --assignee-object-id $containerAppIdentity --assignee-principal-type ServicePrincipal --role "AcrPull" --scope $acrScope --output none
        Write-Host "✅ AcrPull role assigned successfully" -ForegroundColor Green
    }
    catch {
        Write-Error "❌ Failed to assign AcrPull role: $_"
        Write-Host "Manual command to run with elevated permissions:" -ForegroundColor Yellow
        Write-Host "az role assignment create --assignee-object-id $containerAppIdentity --assignee-principal-type ServicePrincipal --role `"AcrPull`" --scope `"$acrScope`"" -ForegroundColor Gray
    }
}

# 3. Verify ACR admin user status
Write-Host "`n3. Checking ACR Admin User Status..." -ForegroundColor Blue
$adminUserEnabled = az acr show --name $ContainerRegistry --resource-group $ResourceGroup --query "adminUserEnabled" --output tsv
Write-Host "ACR Admin User Enabled: $adminUserEnabled" -ForegroundColor Gray

if ($adminUserEnabled -eq "false") {
    Write-Host "ℹ️  ACR admin user is disabled (recommended for production)" -ForegroundColor Yellow
    Write-Host "If managed identity permissions fail, you can temporarily enable admin user with:" -ForegroundColor Gray
    Write-Host "az acr update --name $ContainerRegistry --admin-enabled true" -ForegroundColor Gray
}

# 4. Test ACR access
Write-Host "`n4. Testing ACR Access..." -ForegroundColor Blue
try {
    $loginServer = az acr show --name $ContainerRegistry --resource-group $ResourceGroup --query "loginServer" --output tsv
    Write-Host "ACR Login Server: $loginServer" -ForegroundColor Gray
    
    # Try to list repositories (this requires AcrPull permission)
    Write-Host "Testing repository access..." -ForegroundColor Gray
    az acr repository list --name $ContainerRegistry --output table
    Write-Host "✅ ACR access test successful" -ForegroundColor Green
}
catch {
    Write-Warning "⚠️  ACR access test failed: $_"
}

# 5. Check all role assignments for the managed identity
Write-Host "`n5. All Current Role Assignments for Managed Identity..." -ForegroundColor Blue
Write-Host "Managed Identity: $containerAppIdentity" -ForegroundColor Gray
$allAssignments = az role assignment list --assignee $containerAppIdentity --output table
if ($allAssignments) {
    $allAssignments
}
else {
    Write-Host "❌ No role assignments found for this managed identity" -ForegroundColor Red
}

# 6. Container App deployment test
Write-Host "`n6. Testing Container App Deployment..." -ForegroundColor Blue
Write-Host "Attempting to update container app with current image..." -ForegroundColor Gray

$currentImage = az containerapp show --name $ContainerAppName --resource-group $ResourceGroup --query "properties.template.containers[0].image" --output tsv
Write-Host "Current Image: $currentImage" -ForegroundColor Gray

$latestImage = "$($loginServer)/eval-framework-app:latest"
Write-Host "Target Image: $latestImage" -ForegroundColor Gray

if ($currentImage -ne $latestImage) {
    Write-Host "Image update needed. Testing deployment..." -ForegroundColor Yellow
    
    # Test with minimal update first
    try {
        az containerapp update --name $ContainerAppName --resource-group $ResourceGroup --image $latestImage --output none
        Write-Host "✅ Container app image update successful" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ Container app image update failed" -ForegroundColor Red
        Write-Host "Error: $_" -ForegroundColor Red
        
        # Provide troubleshooting steps
        Write-Host "`n🔧 TROUBLESHOOTING STEPS:" -ForegroundColor Cyan
        Write-Host "1. Ensure managed identity has AcrPull role:" -ForegroundColor White
        Write-Host "   az role assignment create --assignee-object-id $containerAppIdentity --assignee-principal-type ServicePrincipal --role `"AcrPull`" --scope `"$acrScope`"" -ForegroundColor Gray
        
        Write-Host "2. Alternative - temporarily enable ACR admin user:" -ForegroundColor White
        Write-Host "   az acr update --name $ContainerRegistry --admin-enabled true" -ForegroundColor Gray
        
        Write-Host "3. Wait 5-10 minutes for role assignment propagation" -ForegroundColor White
        
        Write-Host "4. Retry deployment:" -ForegroundColor White
        Write-Host "   az containerapp update --name $ContainerAppName --resource-group $ResourceGroup --image $latestImage" -ForegroundColor Gray
    }
}
else {
    Write-Host "✅ Container app is already using the latest image" -ForegroundColor Green
}

Write-Host "`n📋 SUMMARY" -ForegroundColor Cyan
Write-Host "==========" -ForegroundColor Cyan
Write-Host "Container App: $ContainerAppName" -ForegroundColor White
Write-Host "Managed Identity: $containerAppIdentity" -ForegroundColor White
Write-Host "ACR: $ContainerRegistry" -ForegroundColor White
Write-Host "Required Role: AcrPull" -ForegroundColor White

Write-Host "`n🎯 NEXT STEPS" -ForegroundColor Cyan
Write-Host "=============" -ForegroundColor Cyan
if ($hasAcrPull) {
    Write-Host "✅ Permissions are correctly configured" -ForegroundColor Green
    Write-Host "You can now proceed with deployment" -ForegroundColor White
}
else {
    Write-Host "❌ Missing AcrPull permission - run the commands above with elevated access" -ForegroundColor Red
}

Write-Host "`n🚀 DEPLOYMENT COMMAND" -ForegroundColor Cyan
Write-Host "====================" -ForegroundColor Cyan
Write-Host "Once permissions are fixed, use this command to deploy:" -ForegroundColor White
Write-Host "az containerapp update --name $ContainerAppName --resource-group $ResourceGroup \" -ForegroundColor Gray
Write-Host "  --image $latestImage \" -ForegroundColor Gray
Write-Host "  --set-env-vars MAX_DATASET_CONCURRENCY=3 MAX_METRICS_CONCURRENCY=8 \" -ForegroundColor Gray
Write-Host "  EVALUATION_TIMEOUT=30 HTTP_POOL_CONNECTIONS=20 HTTP_POOL_MAXSIZE=10 \" -ForegroundColor Gray
Write-Host "  HTTP_SESSION_LIFETIME=3600 AZURE_STORAGE_TIMEOUT=30 \" -ForegroundColor Gray
Write-Host "  AZURE_STORAGE_CONNECT_TIMEOUT=60 ENABLE_PERFORMANCE_LOGGING=true" -ForegroundColor Gray