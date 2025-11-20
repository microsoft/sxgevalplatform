#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Setup Managed Identity Access for PPE Environment

.DESCRIPTION
    This script configures managed identity access for the PPE Container App to:
    - Azure Storage Account (Queue and Blob operations)
    - Azure OpenAI Service 
    - Application Insights
    - Container Registry (pull images)

.PARAMETER SubscriptionId
    Azure subscription ID (default: d2ef7484-d847-4ca9-88be-d2d9f2a8a50f)

.PARAMETER ResourceGroupName
    Resource group name (default: rg-sxg-agent-evaluation-platform)

.EXAMPLE
    ./setup-ppe-managed-identity.ps1
    
.EXAMPLE
    ./setup-ppe-managed-identity.ps1 -Verbose
#>

param(
    [string]$SubscriptionId = "d2ef7484-d847-4ca9-88be-d2d9f2a8a50f",
    [string]$ResourceGroupName = "rg-sxg-agent-evaluation-platform"
)

# PPE Environment Configuration (matching appsettings.PPE.json)
$PPEConfig = @{
    ManagedIdentityName = "eval-framework-identity-ppe"
    ContainerAppName = "eval-framework-app-ppe"
    
    # Azure Resources (now using shared resources)
    StorageAccountName = "sxgagentevalppe"
    OpenAIAccountName = "evalplatform"  # Shared OpenAI resource
    AppInsightsName = "eval-platform-insights-ppe"
    ContainerRegistryName = "sxgevalregistry"
    
    # Queue Names
    QueueName = "eval-processing-requests"
    SuccessQueueName = "eval-processing-requests-completed" 
    FailureQueueName = "eval-processing-requests-failed"
}

Write-Host "[IDENTITY] Setting up Managed Identity Access for PPE Environment" -ForegroundColor Green
Write-Host "[INFO] Configuration:" -ForegroundColor Yellow
$PPEConfig.GetEnumerator() | ForEach-Object { Write-Host "   $($_.Key): $($_.Value)" -ForegroundColor Gray }

# Ensure we're logged in to Azure
try {
    $currentContext = az account show --query name -o tsv 2>$null
    if (-not $currentContext) {
        throw "Not logged in"
    }
    Write-Host "[SUCCESS] Connected to Azure: $currentContext" -ForegroundColor Green
} catch {
    Write-Host "[ERROR] Please login to Azure first: az login" -ForegroundColor Red
    exit 1
}

# Set subscription
Write-Host "[CONFIG] Setting subscription to: $SubscriptionId" -ForegroundColor Blue
az account set --subscription $SubscriptionId

# Get Managed Identity details
Write-Host "[IDENTITY] Getting managed identity details..." -ForegroundColor Blue
try {
    $identity = az identity show --name $PPEConfig.ManagedIdentityName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
    $identityPrincipalId = $identity.principalId
    $identityClientId = $identity.clientId
    
    Write-Host "[SUCCESS] Managed Identity found:" -ForegroundColor Green
    Write-Host "   Name: $($PPEConfig.ManagedIdentityName)" -ForegroundColor Gray
    Write-Host "   Principal ID: $identityPrincipalId" -ForegroundColor Gray
    Write-Host "   Client ID: $identityClientId" -ForegroundColor Gray
} catch {
    Write-Host "[ERROR] Managed identity '$($PPEConfig.ManagedIdentityName)' not found. Run deploy-ppe-containerapp.ps1 first." -ForegroundColor Red
    exit 1
}

# Function to assign role with retry logic
function Grant-RoleAssignment {
    param(
        [string]$PrincipalId,
        [string]$Role,
        [string]$Scope,
        [string]$Description,
        [int]$MaxRetries = 3
    )
    
    Write-Host "[ROLE] Assigning '$Role' to $Description..." -ForegroundColor Blue
    
    for ($i = 1; $i -le $MaxRetries; $i++) {
        try {
            $result = az role assignment create `
                --assignee $PrincipalId `
                --role $Role `
                --scope $Scope `
                2>$null
                
            if ($result) {
                Write-Host "[SUCCESS] Role assigned: $Role for $Description" -ForegroundColor Green
                return $true
            }
        } catch {
            Write-Host "[WARNING] Attempt $i failed for $Description" -ForegroundColor Yellow
        }
        
        if ($i -lt $MaxRetries) {
            Write-Host "[WAIT] Waiting 10 seconds before retry..." -ForegroundColor Yellow
            Start-Sleep -Seconds 10
        }
    }
    
    Write-Host "[ERROR] Failed to assign role after $MaxRetries attempts: $Description" -ForegroundColor Red
    return $false
}

# 1. Azure Storage Account Access
Write-Host "`n[STORAGE] Configuring Azure Storage access..." -ForegroundColor Cyan

try {
    $storageAccount = az storage account show --name $PPEConfig.StorageAccountName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
    $storageScope = $storageAccount.id
    
    Write-Host "[SUCCESS] Storage account found: $($PPEConfig.StorageAccountName)" -ForegroundColor Green
    
    # Assign Storage roles
    $storageRoles = @(
        @{ Role = "Storage Queue Data Contributor"; Description = "Queue operations" },
        @{ Role = "Storage Blob Data Contributor"; Description = "Blob operations" },
        @{ Role = "Storage Account Contributor"; Description = "Account management" }
    )
    
    $storageSuccess = $true
    foreach ($roleAssignment in $storageRoles) {
        $success = Grant-RoleAssignment -PrincipalId $identityPrincipalId -Role $roleAssignment.Role -Scope $storageScope -Description "Storage Account - $($roleAssignment.Description)"
        if (-not $success) { $storageSuccess = $false }
    }
    
    if ($storageSuccess) {
        Write-Host "[SUCCESS] All storage permissions configured successfully" -ForegroundColor Green
    } else {
        Write-Host "[WARNING] Some storage permissions may need manual configuration" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "[ERROR] Storage account '$($PPEConfig.StorageAccountName)' not found or accessible" -ForegroundColor Red
}

# 2. Azure OpenAI Service Access
Write-Host "`n[OPENAI] Configuring Azure OpenAI access..." -ForegroundColor Cyan

try {
    $openaiAccount = az cognitiveservices account show --name $PPEConfig.OpenAIAccountName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
    $openaiScope = $openaiAccount.id
    
    Write-Host "[SUCCESS] OpenAI account found: $($PPEConfig.OpenAIAccountName)" -ForegroundColor Green
    
    # Assign OpenAI roles
    $openaiRoles = @(
        @{ Role = "Cognitive Services OpenAI User"; Description = "OpenAI API access" },
        @{ Role = "Cognitive Services User"; Description = "General AI services access" }
    )
    
    $openaiSuccess = $true
    foreach ($roleAssignment in $openaiRoles) {
        $success = Grant-RoleAssignment -PrincipalId $identityPrincipalId -Role $roleAssignment.Role -Scope $openaiScope -Description "OpenAI - $($roleAssignment.Description)"
        if (-not $success) { $openaiSuccess = $false }
    }
    
    if ($openaiSuccess) {
        Write-Host "[SUCCESS] All OpenAI permissions configured successfully" -ForegroundColor Green
    } else {
        Write-Host "[WARNING] Some OpenAI permissions may need manual configuration" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "[ERROR] OpenAI account '$($PPEConfig.OpenAIAccountName)' not found or accessible" -ForegroundColor Red
}

# 3. Application Insights Access
Write-Host "`n[INSIGHTS] Configuring Application Insights access..." -ForegroundColor Cyan

try {
    # Find Application Insights component
    $appInsights = az monitor app-insights component show --app $PPEConfig.AppInsightsName --resource-group $ResourceGroupName --output json 2>$null | ConvertFrom-Json
    
    if ($appInsights) {
        $appInsightsScope = $appInsights.id
        Write-Host "[SUCCESS] Application Insights found: $($PPEConfig.AppInsightsName)" -ForegroundColor Green
        
        # Assign Application Insights roles
        $appInsightsRoles = @(
            @{ Role = "Monitoring Contributor"; Description = "Telemetry publishing" },
            @{ Role = "Application Insights Component Contributor"; Description = "Component access" }
        )
        
        $appInsightsSuccess = $true
        foreach ($roleAssignment in $appInsightsRoles) {
            $success = Grant-RoleAssignment -PrincipalId $identityPrincipalId -Role $roleAssignment.Role -Scope $appInsightsScope -Description "App Insights - $($roleAssignment.Description)"
            if (-not $success) { $appInsightsSuccess = $false }
        }
        
        if ($appInsightsSuccess) {
            Write-Host "[SUCCESS] All Application Insights permissions configured successfully" -ForegroundColor Green
        } else {
            Write-Host "[WARNING] Some Application Insights permissions may need manual configuration" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[WARNING] Application Insights '$($PPEConfig.AppInsightsName)' not found. Skipping..." -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "[WARNING] Could not configure Application Insights access. Manual setup may be required." -ForegroundColor Yellow
}

# 4. Container Registry Access
Write-Host "`n[REGISTRY] Configuring Container Registry access..." -ForegroundColor Cyan

try {
    $containerRegistry = az acr show --name $PPEConfig.ContainerRegistryName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
    $registryScope = $containerRegistry.id
    
    Write-Host "[SUCCESS] Container registry found: $($PPEConfig.ContainerRegistryName)" -ForegroundColor Green
    
    # Assign Container Registry role
    $success = Grant-RoleAssignment -PrincipalId $identityPrincipalId -Role "AcrPull" -Scope $registryScope -Description "Container Registry - Image pull access"
    
    if ($success) {
        Write-Host "[SUCCESS] Container Registry permissions configured successfully" -ForegroundColor Green
    } else {
        Write-Host "[WARNING] Container Registry permissions may need manual configuration" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "[ERROR] Container registry '$($PPEConfig.ContainerRegistryName)' not found or accessible" -ForegroundColor Red
}

# 5. Create Storage Queues if they don't exist
Write-Host "`n[QUEUES] Creating storage queues if needed..." -ForegroundColor Cyan

$queuesToCreate = @($PPEConfig.QueueName, $PPEConfig.SuccessQueueName, $PPEConfig.FailureQueueName)

foreach ($queueName in $queuesToCreate) {
    try {
        Write-Host "[CHECK] Checking queue: $queueName" -ForegroundColor Blue
        $queueExists = az storage queue exists --name $queueName --account-name $PPEConfig.StorageAccountName --auth-mode login --output tsv 2>$null
        
        if ($queueExists -eq "True") {
            Write-Host "[SUCCESS] Queue already exists: $queueName" -ForegroundColor Green
        } else {
            Write-Host "[CREATE] Creating queue: $queueName" -ForegroundColor Blue
            $result = az storage queue create --name $queueName --account-name $PPEConfig.StorageAccountName --auth-mode login 2>$null
            
            if ($result) {
                Write-Host "[SUCCESS] Queue created: $queueName" -ForegroundColor Green
            } else {
                Write-Host "[WARNING] Could not create queue: $queueName (may need manual creation)" -ForegroundColor Yellow
            }
        }
    } catch {
        Write-Host "[WARNING] Could not verify/create queue: $queueName" -ForegroundColor Yellow
    }
}

# 6. Restart Container App to pick up new permissions
Write-Host "`n[RESTART] Restarting container app to apply permissions..." -ForegroundColor Cyan

try {
    $restartResult = az containerapp revision restart --name $PPEConfig.ContainerAppName --resource-group $ResourceGroupName --output table
    if ($restartResult) {
        Write-Host "[SUCCESS] Container app restarted successfully" -ForegroundColor Green
    }
} catch {
    Write-Host "[WARNING] Could not restart container app automatically. Consider restarting manually." -ForegroundColor Yellow
}

# Summary
Write-Host "`n[SUMMARY] PPE Managed Identity Setup Summary:" -ForegroundColor Green
Write-Host "   Resource Group: $ResourceGroupName" -ForegroundColor Gray
Write-Host "   Managed Identity: $($PPEConfig.ManagedIdentityName)" -ForegroundColor Gray
Write-Host "   Principal ID: $identityPrincipalId" -ForegroundColor Gray
Write-Host "   Client ID: $identityClientId" -ForegroundColor Gray
Write-Host "   Storage Account: $($PPEConfig.StorageAccountName)" -ForegroundColor Gray
Write-Host "   OpenAI Account: $($PPEConfig.OpenAIAccountName)" -ForegroundColor Gray
Write-Host "   Container Registry: $($PPEConfig.ContainerRegistryName)" -ForegroundColor Gray

Write-Host "`n[NEXT] NEXT STEPS:" -ForegroundColor Yellow
Write-Host "   1. Wait 5-10 minutes for Azure AD permissions to propagate" -ForegroundColor White
Write-Host "   2. Test the container app health endpoint" -ForegroundColor White
Write-Host "   3. Submit a test evaluation request to validate queue processing" -ForegroundColor White
Write-Host "   4. Monitor Application Insights for telemetry data" -ForegroundColor White

Write-Host "`n[SUCCESS] PPE Managed Identity setup completed!" -ForegroundColor Green

Write-Host "`n[MANUAL] MANUAL VERIFICATION COMMANDS:" -ForegroundColor Cyan
Write-Host "   # Test storage access:" -ForegroundColor Gray
Write-Host "   az storage queue list --account-name $($PPEConfig.StorageAccountName) --auth-mode login" -ForegroundColor White
Write-Host "   # Test container app logs:" -ForegroundColor Gray  
Write-Host "   az containerapp logs show --name $($PPEConfig.ContainerAppName) --resource-group $ResourceGroupName --follow" -ForegroundColor White