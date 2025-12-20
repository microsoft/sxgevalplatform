# Role Assignment Script for Production East US 2
# This script assigns all necessary RBAC roles and permissions for the eval-runner-container-prod-eus2 managed identity

param(
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId = "d2ef7484-d847-4ca9-88be-d2d9f2a8a50f",
    
    [Parameter(Mandatory=$false)]
    [string]$ManagedIdentityPrincipalId = "fff8a56f-0c21-42e8-856f-588b0d8e1aa8",
    
    [Parameter(Mandatory=$false)]
    [string]$ContainerAppName = "eval-runner-container-prod-eus2",
    
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "EvalRunnerRG-EastUS2",
    
    [Parameter(Mandatory=$false)]
    [string]$CommonResourceGroup = "EvalCommonRg-UsEast2",
    
    [Parameter(Mandatory=$false)]
    [string]$ContainerRegistryName = "evalplatformregistryprod",
    
    [Parameter(Mandatory=$false)]
    [string]$StorageAccountName = "sxgagentevalprod",
    
    [Parameter(Mandatory=$false)]
    [string]$ApiAppId = "dafa5810-9bba-4c5b-b443-abe1a40aa240",
    
    [Parameter(Mandatory=$false)]
    [string]$ApiAppRoleId = "ea602f63-82ed-429b-b2da-6e8b7e494d90"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Role Assignment Script - East US 2" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Set subscription context
Write-Host "Setting subscription context..." -ForegroundColor Yellow
az account set --subscription $SubscriptionId
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to set subscription context" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Subscription context set" -ForegroundColor Green
Write-Host ""

# Build resource scopes
$acrScope = "/subscriptions/$SubscriptionId/resourceGroups/$CommonResourceGroup/providers/Microsoft.ContainerRegistry/registries/$ContainerRegistryName"
$storageScope = "/subscriptions/$SubscriptionId/resourceGroups/$CommonResourceGroup/providers/Microsoft.Storage/storageAccounts/$StorageAccountName"

Write-Host "Resource Scopes:" -ForegroundColor Cyan
Write-Host "  Container Registry: $acrScope" -ForegroundColor Gray
Write-Host "  Storage Account: $storageScope" -ForegroundColor Gray
Write-Host "  Managed Identity: $ManagedIdentityPrincipalId" -ForegroundColor Gray
Write-Host ""

# 1. Assign AcrPull role on Container Registry
Write-Host "1. Assigning AcrPull role on Container Registry..." -ForegroundColor Yellow
$existingAcrRole = az role assignment list --assignee $ManagedIdentityPrincipalId --scope $acrScope --query "[?roleDefinitionName=='AcrPull'].id" -o tsv 2>$null

if ($existingAcrRole) {
    Write-Host "   ✓ AcrPull role already assigned" -ForegroundColor Green
} else {
    az role assignment create --assignee $ManagedIdentityPrincipalId --role "AcrPull" --scope $acrScope
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✓ AcrPull role assigned successfully" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Failed to assign AcrPull role" -ForegroundColor Red
    }
}
Write-Host ""

# 2. Assign Storage Queue Data Contributor role on Storage Account
Write-Host "2. Assigning Storage Queue Data Contributor role on Storage Account..." -ForegroundColor Yellow
$existingStorageRole = az role assignment list --assignee $ManagedIdentityPrincipalId --scope $storageScope --query "[?roleDefinitionName=='Storage Queue Data Contributor'].id" -o tsv 2>$null

if ($existingStorageRole) {
    Write-Host "   ✓ Storage Queue Data Contributor role already assigned" -ForegroundColor Green
} else {
    az role assignment create --assignee $ManagedIdentityPrincipalId --role "Storage Queue Data Contributor" --scope $storageScope
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✓ Storage Queue Data Contributor role assigned successfully" -ForegroundColor Green
    } else {
        Write-Host "   ✗ Failed to assign Storage Queue Data Contributor role" -ForegroundColor Red
    }
}
Write-Host ""

# 3. Assign API App Role (EvalPlatformAPI.FullAccess)
Write-Host "3. Assigning EvalPlatformAPI.FullAccess app role..." -ForegroundColor Yellow
$existingAppRole = az rest --method GET --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ManagedIdentityPrincipalId/appRoleAssignments" --query "value[?appRoleId=='$ApiAppRoleId'].id" -o tsv 2>$null

if ($existingAppRole) {
    Write-Host "   ✓ EvalPlatformAPI.FullAccess app role already assigned" -ForegroundColor Green
} else {
    # Get the resource service principal ID
    $resourceSpId = az ad sp show --id $ApiAppId --query "id" -o tsv 2>$null
    
    if ($resourceSpId) {
        $body = @{
            principalId = $ManagedIdentityPrincipalId
            resourceId = $resourceSpId
            appRoleId = $ApiAppRoleId
        } | ConvertTo-Json
        
        az rest --method POST --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ManagedIdentityPrincipalId/appRoleAssignments" --headers "Content-Type=application/json" --body $body 2>$null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "   ✓ EvalPlatformAPI.FullAccess app role assigned successfully" -ForegroundColor Green
        } else {
            Write-Host "   ✗ Failed to assign EvalPlatformAPI.FullAccess app role" -ForegroundColor Red
        }
    } else {
        Write-Host "   ✗ Failed to get API service principal ID" -ForegroundColor Red
    }
}
Write-Host ""

# 4. Configure Container App to use managed identity for ACR
Write-Host "4. Configuring Container App ACR authentication..." -ForegroundColor Yellow
az containerapp registry set --name $ContainerAppName --resource-group $ResourceGroupName --server "$ContainerRegistryName.azurecr.io" --identity system 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✓ Container App ACR authentication configured successfully" -ForegroundColor Green
} else {
    Write-Host "   ✗ Failed to configure Container App ACR authentication" -ForegroundColor Red
}
Write-Host ""

# Wait for identity propagation
Write-Host "Waiting 30 seconds for identity propagation..." -ForegroundColor Yellow
Start-Sleep -Seconds 30
Write-Host "✓ Identity propagation complete" -ForegroundColor Green
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Role Assignment Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Verifying all role assignments..." -ForegroundColor Yellow
Write-Host ""

# Verify RBAC roles
Write-Host "RBAC Role Assignments:" -ForegroundColor Cyan
az role assignment list --assignee $ManagedIdentityPrincipalId --all --query "[].{Role:roleDefinitionName, Scope:scope}" -o table
Write-Host ""

# Verify App Role
Write-Host "App Role Assignments:" -ForegroundColor Cyan
az rest --method GET --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ManagedIdentityPrincipalId/appRoleAssignments" --query "value[].{AppRole:appRoleId, Resource:resourceDisplayName}" -o table 2>$null
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✓ Role assignment script completed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
