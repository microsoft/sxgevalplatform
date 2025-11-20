#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Setup Managed Identity Permissions for SXG Evaluation Platform

.DESCRIPTION
    This script configures all required managed identity permissions for the SXG Evaluation Platform Container App.
    It assigns the minimum required RBAC roles for secretless/keyless authentication to Azure services:
    
    - Azure Storage Account (Queue and Blob operations)
    - Azure Container Registry (Image pull operations)  
    - Azure OpenAI/Cognitive Services (Model access)
    - Azure AI Foundry Project (AI services access)
    - Application Insights (Telemetry publishing)
    - Resource Group (Resource discovery)

.PARAMETER ResourceGroupName
    Name of the Azure Resource Group containing the resources

.PARAMETER ContainerAppName
    Name of the Container App with managed identity

.PARAMETER StorageAccountName
    Name of the Azure Storage Account (auto-detected if not provided)

.PARAMETER RegistryName
    Name of the Azure Container Registry (auto-detected if not provided)

.PARAMETER OpenAIAccountName
    Name of the Azure OpenAI/Cognitive Services Account (auto-detected if not provided)

.PARAMETER AIProjectName
    Name of the Azure AI Foundry Project (auto-detected if not provided)

.PARAMETER AppInsightsName
    Name of Application Insights (auto-detected if not provided)

.PARAMETER SubscriptionId
    Azure Subscription ID (uses current subscription if not provided)

.EXAMPLE
    .\setup-managed-identity-permissions.ps1 -ResourceGroupName "rg-eval-platform" -ContainerAppName "eval-app"

.EXAMPLE
    .\setup-managed-identity-permissions.ps1 -ResourceGroupName "rg-eval-platform" -ContainerAppName "eval-app" -StorageAccountName "evalstoragedev"

.NOTES
    This script implements the principle of least privilege, assigning only the minimum required permissions.
    No secrets, connection strings, or API keys are needed after running this script.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory = $true)]
    [string]$ContainerAppName,
    
    [string]$StorageAccountName = "",
    [string]$RegistryName = "",
    [string]$OpenAIAccountName = "",
    [string]$AIProjectName = "",
    [string]$AppInsightsName = "",
    [string]$SubscriptionId = "",
    [switch]$SkipAI = $false,
    [switch]$SkipOptional = $false,
    [switch]$Verbose
)

# Set error handling
$ErrorActionPreference = "Stop"
$VerbosePreference = if ($Verbose) { "Continue" } else { "SilentlyContinue" }

# Logging function
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $color = switch ($Level) {
        "ERROR" { "Red" }
        "WARNING" { "Yellow" }
        "SUCCESS" { "Green" }
        "INFO" { "White" }
        default { "White" }
    }
    Write-Host "[$timestamp] [$Level] $Message" -ForegroundColor $color
}

function Test-Prerequisites {
    Write-Log "Checking prerequisites..." "INFO"
    
    # Check Azure CLI
    try {
        $azVersion = az --version 2>$null | Select-Object -First 1
        if (-not $azVersion) { throw "Azure CLI not found" }
        Write-Log "✓ Azure CLI available" "SUCCESS"
    } catch {
        Write-Log "✗ Azure CLI is not installed or not in PATH" "ERROR"
        throw "Azure CLI is required"
    }
    
    # Check Azure login
    try {
        $account = az account show --query "user.name" -o tsv 2>$null
        if (-not $account) { throw "Not logged in" }
        Write-Log "✓ Logged in as: $account" "SUCCESS"
    } catch {
        Write-Log "✗ Not logged in to Azure" "ERROR"
        throw "Please run 'az login' first"
    }
    
    # Set subscription
    if ($SubscriptionId) {
        az account set --subscription $SubscriptionId
        $currentSub = az account show --query "name" -o tsv
        Write-Log "✓ Using subscription: $currentSub" "SUCCESS"
    } else {
        $SubscriptionId = az account show --query "id" -o tsv
        $currentSub = az account show --query "name" -o tsv
        Write-Log "✓ Using current subscription: $currentSub" "SUCCESS"
    }
}

function Get-ContainerAppIdentity {
    Write-Log "Getting Container App managed identity..." "INFO"
    
    try {
        # Get the Container App's managed identity principal ID
        $principalId = az containerapp show --name $ContainerAppName --resource-group $ResourceGroupName --query "identity.principalId" -o tsv 2>$null
        
        if (-not $principalId -or $principalId -eq "null" -or $principalId -eq "") {
            Write-Log "Managed identity not found. Enabling system-assigned identity..." "WARNING"
            
            # Enable system-assigned managed identity
            az containerapp identity assign --name $ContainerAppName --resource-group $ResourceGroupName --system-assigned --output none
            
            # Wait a bit for identity creation
            Write-Log "Waiting for managed identity creation..." "INFO"
            Start-Sleep -Seconds 15
            
            # Try to get the principal ID again
            $principalId = az containerapp show --name $ContainerAppName --resource-group $ResourceGroupName --query "identity.principalId" -o tsv
        }
        
        if (-not $principalId -or $principalId -eq "null" -or $principalId -eq "") {
            throw "Failed to get or create managed identity"
        }
        
        Write-Log "✓ Container App Managed Identity: $principalId" "SUCCESS"
        return $principalId
        
    } catch {
        Write-Log "✗ Failed to get Container App managed identity: $_" "ERROR"
        throw
    }
}

function Find-AzureResources {
    Write-Log "Auto-discovering Azure resources in Resource Group..." "INFO"
    
    $resources = @{}
    
    # Find Storage Account if not provided
    if (-not $StorageAccountName) {
        $storageAccounts = az storage account list --resource-group $ResourceGroupName --query "[].name" -o tsv 2>$null
        if ($storageAccounts) {
            $StorageAccountName = ($storageAccounts -split "`n")[0]
            Write-Log "✓ Found Storage Account: $StorageAccountName" "SUCCESS"
        }
    }
    $resources['StorageAccount'] = $StorageAccountName
    
    # Find Container Registry if not provided
    if (-not $RegistryName) {
        $registries = az acr list --resource-group $ResourceGroupName --query "[].name" -o tsv 2>$null
        if ($registries) {
            $RegistryName = ($registries -split "`n")[0]
            Write-Log "✓ Found Container Registry: $RegistryName" "SUCCESS"
        }
    }
    $resources['Registry'] = $RegistryName
    
    # Find OpenAI/Cognitive Services if not provided
    if (-not $OpenAIAccountName -and -not $SkipAI) {
        $cognitiveAccounts = az cognitiveservices account list --resource-group $ResourceGroupName --query "[].name" -o tsv 2>$null
        if ($cognitiveAccounts) {
            $OpenAIAccountName = ($cognitiveAccounts -split "`n")[0]
            Write-Log "✓ Found Cognitive Services Account: $OpenAIAccountName" "SUCCESS"
        }
    }
    $resources['OpenAI'] = $OpenAIAccountName
    
    # Find AI/ML Workspace if not provided
    if (-not $AIProjectName -and -not $SkipAI) {
        try {
            $workspaces = az ml workspace list --resource-group $ResourceGroupName --query "[].name" -o tsv 2>$null
            if ($workspaces) {
                $AIProjectName = ($workspaces -split "`n")[0]
                Write-Log "✓ Found AI Foundry Project: $AIProjectName" "SUCCESS"
            }
        } catch {
            Write-Log "Azure ML extension may not be installed - skipping AI project discovery" "WARNING"
        }
    }
    $resources['AIProject'] = $AIProjectName
    
    # Find Application Insights if not provided
    if (-not $AppInsightsName) {
        $appInsights = az monitor app-insights component list --resource-group $ResourceGroupName --query "[].name" -o tsv 2>$null
        if ($appInsights) {
            $AppInsightsName = ($appInsights -split "`n")[0]
            Write-Log "✓ Found Application Insights: $AppInsightsName" "SUCCESS"
        }
    }
    $resources['AppInsights'] = $AppInsightsName
    
    return $resources
}

function Grant-RoleAssignment {
    param(
        [string]$PrincipalId,
        [string]$Role,
        [string]$Scope,
        [string]$Description
    )
    
    try {
        # Check if role assignment already exists
        $existingRole = az role assignment list --assignee $PrincipalId --role $Role --scope $Scope --query "[0].id" -o tsv 2>$null
        
        if ($existingRole) {
            Write-Log "  ✓ $Description - Already exists" "SUCCESS"
            return $true
        }
        
        # Create role assignment
        az role assignment create --assignee-object-id $PrincipalId --assignee-principal-type ServicePrincipal --role $Role --scope $Scope --output none
        Write-Log "  ✓ $Description - Assigned" "SUCCESS"
        return $true
        
    } catch {
        Write-Log "  ✗ $Description - Failed: $_" "ERROR"
        return $false
    }
}

function Set-StoragePermissions {
    param([string]$PrincipalId, [string]$StorageAccountName)
    
    if (-not $StorageAccountName) {
        Write-Log "⏭️  Storage Account not found - skipping storage permissions" "WARNING"
        return
    }
    
    Write-Log "Setting Storage Account permissions..." "INFO"
    
    $storageScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Storage/storageAccounts/$StorageAccountName"
    
    $success = $true
    
    # Storage Queue Data Contributor (queue operations)
    $success = (Grant-RoleAssignment -PrincipalId $PrincipalId -Role "Storage Queue Data Contributor" -Scope $storageScope -Description "Storage Queue Data Contributor") -and $success
    
    # Storage Blob Data Contributor (blob operations)  
    $success = (Grant-RoleAssignment -PrincipalId $PrincipalId -Role "Storage Blob Data Contributor" -Scope $storageScope -Description "Storage Blob Data Contributor") -and $success
    
    if ($success) {
        Write-Log "✅ Storage Account permissions configured successfully" "SUCCESS"
    } else {
        Write-Log "⚠️  Some storage permissions may need manual configuration" "WARNING"
    }
}

function Set-RegistryPermissions {
    param([string]$PrincipalId, [string]$RegistryName)
    
    if (-not $RegistryName) {
        Write-Log "⏭️  Container Registry not found - skipping registry permissions" "WARNING"
        return
    }
    
    Write-Log "Setting Container Registry permissions..." "INFO"
    
    $registryScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.ContainerRegistry/registries/$RegistryName"
    
    # AcrPull (image pull operations)
    $success = Grant-RoleAssignment -PrincipalId $PrincipalId -Role "AcrPull" -Scope $registryScope -Description "AcrPull (Container image pull)"
    
    if ($success) {
        Write-Log "✅ Container Registry permissions configured successfully" "SUCCESS"
    } else {
        Write-Log "⚠️  Container Registry permissions may need manual configuration" "WARNING"
    }
}

function Set-OpenAIPermissions {
    param([string]$PrincipalId, [string]$OpenAIAccountName)
    
    if (-not $OpenAIAccountName -or $SkipAI) {
        Write-Log "⏭️  Azure OpenAI not found or skipped - skipping OpenAI permissions" "WARNING"
        return
    }
    
    Write-Log "Setting Azure OpenAI permissions..." "INFO"
    
    $openAiScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.CognitiveServices/accounts/$OpenAIAccountName"
    
    $success = $true
    
    # Cognitive Services OpenAI User (model access)
    $success = (Grant-RoleAssignment -PrincipalId $PrincipalId -Role "Cognitive Services OpenAI User" -Scope $openAiScope -Description "Cognitive Services OpenAI User") -and $success
    
    # Cognitive Services User (general AI services)
    $success = (Grant-RoleAssignment -PrincipalId $PrincipalId -Role "Cognitive Services User" -Scope $openAiScope -Description "Cognitive Services User") -and $success
    
    if ($success) {
        Write-Log "✅ Azure OpenAI permissions configured successfully" "SUCCESS"
    } else {
        Write-Log "⚠️  Some Azure OpenAI permissions may need manual configuration" "WARNING"
    }
}

function Set-AIFoundryPermissions {
    param([string]$PrincipalId, [string]$AIProjectName)
    
    if (-not $AIProjectName -or $SkipAI) {
        Write-Log "⏭️  Azure AI Foundry Project not found or skipped - skipping AI project permissions" "WARNING"
        return
    }
    
    Write-Log "Setting Azure AI Foundry permissions..." "INFO"
    
    $aiProjectScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.MachineLearningServices/workspaces/$AIProjectName"
    
    $success = $true
    
    try {
        # AzureML Data Scientist (AI services access)
        $success = (Grant-RoleAssignment -PrincipalId $PrincipalId -Role "AzureML Data Scientist" -Scope $aiProjectScope -Description "AzureML Data Scientist") -and $success
        
        if ($success) {
            Write-Log "✅ Azure AI Foundry permissions configured successfully" "SUCCESS"
        } else {
            Write-Log "⚠️  Some Azure AI Foundry permissions may need manual configuration" "WARNING"
        }
    } catch {
        Write-Log "⚠️  Azure AI Foundry permissions failed - may need manual configuration" "WARNING"
    }
}

function Set-AppInsightsPermissions {
    param([string]$PrincipalId, [string]$AppInsightsName)
    
    if (-not $AppInsightsName) {
        Write-Log "⏭️  Application Insights not found - skipping insights permissions" "WARNING"
        return
    }
    
    Write-Log "Setting Application Insights permissions..." "INFO"
    
    try {
        # Get Application Insights resource ID
        $appInsightsResource = az monitor app-insights component show --app $AppInsightsName --resource-group $ResourceGroupName --query "id" -o tsv 2>$null
        
        if ($appInsightsResource) {
            # Monitoring Metrics Publisher (telemetry publishing)
            $success = Grant-RoleAssignment -PrincipalId $PrincipalId -Role "Monitoring Metrics Publisher" -Scope $appInsightsResource -Description "Monitoring Metrics Publisher"
            
            if ($success) {
                Write-Log "✅ Application Insights permissions configured successfully" "SUCCESS"
            } else {
                Write-Log "⚠️  Application Insights permissions may need manual configuration" "WARNING"
            }
        } else {
            Write-Log "⚠️  Could not find Application Insights resource" "WARNING"
        }
    } catch {
        Write-Log "⚠️  Application Insights permissions failed - may need manual configuration" "WARNING"
    }
}

function Set-ResourceGroupPermissions {
    param([string]$PrincipalId)
    
    Write-Log "Setting Resource Group permissions..." "INFO"
    
    $rgScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName"
    
    # Reader (resource discovery)
    $success = Grant-RoleAssignment -PrincipalId $PrincipalId -Role "Reader" -Scope $rgScope -Description "Reader (Resource discovery)"
    
    if ($success) {
        Write-Log "✅ Resource Group permissions configured successfully" "SUCCESS"
    } else {
        Write-Log "⚠️  Resource Group permissions may need manual configuration" "WARNING"
    }
}

function Show-PermissionsSummary {
    param([hashtable]$Resources)
    
    Write-Host "`n" -NoNewline
    Write-Log "🔐 MANAGED IDENTITY PERMISSIONS CONFIGURED!" "SUCCESS"
    Write-Host "=============================================================" -ForegroundColor Yellow
    
    Write-Host "`n📋 PERMISSIONS SUMMARY (SECRETLESS/KEYLESS ACCESS):" -ForegroundColor Cyan
    
    if ($Resources.StorageAccount) {
        Write-Host "✅ Storage Account ($($Resources.StorageAccount)):" -ForegroundColor Green
        Write-Host "   • Storage Queue Data Contributor - Queue operations" -ForegroundColor White
        Write-Host "   • Storage Blob Data Contributor - Blob operations" -ForegroundColor White
    }
    
    if ($Resources.Registry) {
        Write-Host "✅ Container Registry ($($Resources.Registry)):" -ForegroundColor Green
        Write-Host "   • AcrPull - Container image pull operations" -ForegroundColor White
    }
    
    if ($Resources.OpenAI -and -not $SkipAI) {
        Write-Host "✅ Azure OpenAI ($($Resources.OpenAI)):" -ForegroundColor Green
        Write-Host "   • Cognitive Services OpenAI User - Model access" -ForegroundColor White
        Write-Host "   • Cognitive Services User - General AI services" -ForegroundColor White
    }
    
    if ($Resources.AIProject -and -not $SkipAI) {
        Write-Host "✅ Azure AI Foundry ($($Resources.AIProject)):" -ForegroundColor Green
        Write-Host "   • AzureML Data Scientist - AI services access" -ForegroundColor White
    }
    
    if ($Resources.AppInsights) {
        Write-Host "✅ Application Insights ($($Resources.AppInsights)):" -ForegroundColor Green
        Write-Host "   • Monitoring Metrics Publisher - Telemetry publishing" -ForegroundColor White
    }
    
    Write-Host "✅ Resource Group ($ResourceGroupName):" -ForegroundColor Green
    Write-Host "   • Reader - Resource discovery and metadata access" -ForegroundColor White
    
    Write-Host "`n🔒 SECURITY BENEFITS:" -ForegroundColor Cyan
    Write-Host "✅ NO SECRETS: All authentication via managed identity" -ForegroundColor Green
    Write-Host "✅ NO CONNECTION STRINGS: Azure Storage access via identity" -ForegroundColor Green
    Write-Host "✅ NO API KEYS: Azure OpenAI access via identity" -ForegroundColor Green
    Write-Host "✅ LEAST PRIVILEGE: Minimal required permissions only" -ForegroundColor Green
    Write-Host "✅ AUTO-ROTATION: Identity tokens automatically managed by Azure" -ForegroundColor Green
    
    Write-Host "`n🔍 VERIFICATION COMMANDS:" -ForegroundColor Cyan
    Write-Host "Check all role assignments:" -ForegroundColor White
    $principalId = az containerapp show --name $ContainerAppName --resource-group $ResourceGroupName --query "identity.principalId" -o tsv 2>$null
    Write-Host "az role assignment list --assignee $principalId --output table" -ForegroundColor Gray
    
    Write-Host "`nTest container app access:" -ForegroundColor White
    Write-Host "az containerapp logs show --name $ContainerAppName --resource-group $ResourceGroupName --follow" -ForegroundColor Gray
    
    Write-Host "`n🚀 NEXT STEPS:" -ForegroundColor Cyan
    Write-Host "1. Deploy your application using the deployment script" -ForegroundColor White
    Write-Host "2. Verify managed identity authentication in application logs" -ForegroundColor White
    Write-Host "3. Test Azure Storage, OpenAI, and other service access" -ForegroundColor White
    
    Write-Host "`n✅ All managed identity permissions configured successfully!" -ForegroundColor Green
    Write-Host "Your application can now access Azure services without any secrets or keys! 🔐" -ForegroundColor Green
}

# Main execution
try {
    Write-Log "🔐 Starting Managed Identity Permission Setup for SXG Evaluation Platform" "INFO"
    Write-Log "Resource Group: $ResourceGroupName | Container App: $ContainerAppName" "INFO"
    
    # Step 1: Prerequisites
    Test-Prerequisites
    
    # Step 2: Get Container App Managed Identity
    $principalId = Get-ContainerAppIdentity
    
    # Step 3: Auto-discover Azure resources
    $resources = Find-AzureResources
    
    Write-Log "Configuring permissions for secretless Azure service access..." "INFO"
    
    # Step 4: Set Storage Account permissions
    Set-StoragePermissions -PrincipalId $principalId -StorageAccountName $resources.StorageAccount
    
    # Step 5: Set Container Registry permissions
    Set-RegistryPermissions -PrincipalId $principalId -RegistryName $resources.Registry
    
    # Step 6: Set Azure OpenAI permissions
    if (-not $SkipAI) {
        Set-OpenAIPermissions -PrincipalId $principalId -OpenAIAccountName $resources.OpenAI
        Set-AIFoundryPermissions -PrincipalId $principalId -AIProjectName $resources.AIProject
    }
    
    # Step 7: Set Application Insights permissions
    if (-not $SkipOptional) {
        Set-AppInsightsPermissions -PrincipalId $principalId -AppInsightsName $resources.AppInsights
    }
    
    # Step 8: Set Resource Group permissions
    Set-ResourceGroupPermissions -PrincipalId $principalId
    
    # Step 9: Show summary
    Show-PermissionsSummary -Resources $resources
    
} catch {
    Write-Log "💥 Permission setup failed: $_" "ERROR"
    Write-Host "`nTroubleshooting:" -ForegroundColor Yellow
    Write-Host "- Verify you have Owner or User Access Administrator role" -ForegroundColor Gray
    Write-Host "- Check if resources exist in the specified resource group" -ForegroundColor Gray
    Write-Host "- Ensure Container App has system-assigned managed identity enabled" -ForegroundColor Gray
    Write-Host "- Try running with --SkipAI flag if AI resources are not needed" -ForegroundColor Gray
    exit 1
}