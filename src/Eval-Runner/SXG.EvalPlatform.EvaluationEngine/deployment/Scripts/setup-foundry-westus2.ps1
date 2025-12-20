#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Complete Azure AI Foundry Setup for West US 2 Production
.DESCRIPTION
    Creates Azure AI Foundry Hub, Project, Azure OpenAI service, model deployment,
    and storage account with all necessary permissions and connections.
    
    This script creates:
    - Azure AI Hub (shared resources)
    - Azure AI Project (within the Hub)
    - Azure OpenAI service with GPT-4 deployment
    - Storage account for Foundry
    - Managed identity permissions
    - Connects all resources together
#>

param(
    [switch]$WhatIf,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$VerbosePreference = if ($Verbose) { "Continue" } else { "SilentlyContinue" }

# ============================================================================
# CONFIGURATION - West US 2
# ============================================================================

$Region = "West US 2"
$Location = "westus2"
$ResourceGroup = "EvalRunnerRG-WestUS2"
$SharedResourceGroup = "EvalCommonRg-UsEast2"

# Azure AI Foundry Resources
$AIHubName = "evalplatformhubprodwus2"
$AIProjectName = "evalplatformprojectprod"
$StorageAccountName = "foundrywus2prod"
$KeyVaultName = "foundry-kv-wus2-prod"
$AppInsightsName = "eval-runner-prod-app-insights"

# Azure OpenAI Resources
$OpenAIName = "evalplatformopenai-prod-wus2"
$OpenAIDeploymentName = "gpt-4"
$OpenAIModelName = "gpt-4"
$OpenAIModelVersion = "turbo-2024-04-09"

# Container App
$ContainerAppName = "eval-runner-prod-wus2"

# Tags
$ResourceGroupTags = @{
    ComponentId = "SXG-EvalPlatform-EvalRunner"
    Env = "Production"
    Environment = "Production"
    Project = "SXG-EvalPlatform"
    Region = "WestUS2"
}

$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$LogFile = "foundry-setup-westus2-${Timestamp}.log"

# ============================================================================
# HELPER FUNCTIONS
# ============================================================================

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $LogMessage = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] [$Level] $Message"
    Write-Host $LogMessage
    $LogMessage | Out-File -FilePath $LogFile -Append
}

function Test-ResourceExists {
    param([string]$Type, [string]$Name, [string]$ResourceGroup = "")
    
    try {
        switch ($Type) {
            "ResourceGroup" {
                $result = az group show --name $Name --query "name" -o tsv 2>$null
                return $null -ne $result -and $result -ne ""
            }
            "StorageAccount" {
                $result = az storage account show --name $Name --query "name" -o tsv 2>$null
                return $null -ne $result -and $result -ne ""
            }
            "KeyVault" {
                $result = az keyvault show --name $Name --query "name" -o tsv 2>$null
                return $null -ne $result -and $result -ne ""
            }
            "OpenAI" {
                $result = az cognitiveservices account show --name $Name --resource-group $ResourceGroup --query "name" -o tsv 2>$null
                return $null -ne $result -and $result -ne ""
            }
            "AIHub" {
                $result = az ml workspace show --name $Name --resource-group $ResourceGroup --query "name" -o tsv 2>$null
                return $null -ne $result -and $result -ne ""
            }
            "ContainerApp" {
                $result = az containerapp show --name $Name --resource-group $ResourceGroup --query "name" -o tsv 2>$null
                return $null -ne $result -and $result -ne ""
            }
        }
    } catch {
        return $false
    }
    return $false
}

# ============================================================================
# MAIN SETUP
# ============================================================================

try {
    Write-Log "========================================" "INFO"
    Write-Log "Azure AI Foundry Setup - $Region" "INFO"
    Write-Log "========================================" "INFO"
    Write-Log "Started at: $(Get-Date)" "INFO"
    Write-Log "" "INFO"
    
    # Verify Azure CLI login
    try {
        $account = az account show --query "user.name" -o tsv
        Write-Log "Logged in as: $account" "INFO"
    } catch {
        Write-Log "❌ Not logged in to Azure. Please run 'az login'" "ERROR"
        exit 1
    }
    
    # Check if ML extension is installed
    $mlExtension = az extension list --query "[?name=='ml'].version" -o tsv
    if (-not $mlExtension) {
        Write-Log "Installing Azure ML extension..." "INFO"
        az extension add --name ml --upgrade
        Write-Log "✅ Installed Azure ML extension" "SUCCESS"
    }
    
    # Step 1: Create or verify Resource Group
    Write-Log "" "INFO"
    Write-Log "Setting up Resource Group..." "INFO"
    if (-not (Test-ResourceExists -Type "ResourceGroup" -Name $ResourceGroup)) {
        if (-not $WhatIf) {
            $tagArgs = @()
            foreach ($tag in $ResourceGroupTags.GetEnumerator()) {
                $tagArgs += "$($tag.Key)=$($tag.Value)"
            }
            
            $cmd = "az group create --name $ResourceGroup --location $Location --tags $($tagArgs -join ' ')"
            Write-Log "Executing: $cmd" "INFO"
            Invoke-Expression $cmd
            
            if ($LASTEXITCODE -ne 0) {
                Write-Log "❌ Failed to create Resource Group" "ERROR"
                exit 1
            }
            Write-Log "✅ Created Resource Group: $ResourceGroup" "SUCCESS"
        }
    } else {
        Write-Log "✅ Resource Group exists: $ResourceGroup" "INFO"
    }
    
    # Step 2: Create Storage Account for AI Foundry
    Write-Log "" "INFO"
    Write-Log "Creating Storage Account for AI Foundry..." "INFO"
    if (-not (Test-ResourceExists -Type "StorageAccount" -Name $StorageAccountName)) {
        if (-not $WhatIf) {
            az storage account create `
                --name $StorageAccountName `
                --resource-group $ResourceGroup `
                --location $Location `
                --sku Standard_LRS `
                --kind StorageV2 `
                --min-tls-version TLS1_2 `
                --allow-blob-public-access false `
                --enable-hierarchical-namespace true
            
            Write-Log "✅ Created Storage Account: $StorageAccountName" "SUCCESS"
        }
    } else {
        Write-Log "✅ Storage Account exists: $StorageAccountName" "INFO"
    }
    
    # Step 3: Create Key Vault for AI Foundry
    Write-Log "" "INFO"
    Write-Log "Creating Key Vault for AI Foundry..." "INFO"
    if (-not (Test-ResourceExists -Type "KeyVault" -Name $KeyVaultName)) {
        if (-not $WhatIf) {
            az keyvault create `
                --name $KeyVaultName `
                --resource-group $ResourceGroup `
                --location $Location `
                --enable-rbac-authorization true
            
            Write-Log "✅ Created Key Vault: $KeyVaultName" "SUCCESS"
        }
    } else {
        Write-Log "✅ Key Vault exists: $KeyVaultName" "INFO"
    }
    
    # Step 4: Create Azure OpenAI Service
    Write-Log "" "INFO"
    Write-Log "Creating Azure OpenAI Service..." "INFO"
    if (-not (Test-ResourceExists -Type "OpenAI" -Name $OpenAIName -ResourceGroup $ResourceGroup)) {
        if (-not $WhatIf) {
            az cognitiveservices account create `
                --name $OpenAIName `
                --resource-group $ResourceGroup `
                --location $Location `
                --kind OpenAI `
                --sku S0 `
                --custom-domain $OpenAIName `
                --tags Environment=Production Project=SXG-EvalPlatform Region=WestUS2
            
            Write-Log "✅ Created Azure OpenAI: $OpenAIName" "SUCCESS"
            Write-Log "Waiting 10 seconds for OpenAI service to be ready..." "INFO"
            Start-Sleep -Seconds 10
        }
    } else {
        Write-Log "✅ Azure OpenAI exists: $OpenAIName" "INFO"
    }
    
    # Step 5: Deploy GPT-4 Model
    Write-Log "" "INFO"
    Write-Log "Deploying GPT-4 model..." "INFO"
    $existingDeployment = az cognitiveservices account deployment list `
        --name $OpenAIName `
        --resource-group $ResourceGroup `
        --query "[?name=='$OpenAIDeploymentName'].name" -o tsv 2>$null
    
    if (-not $existingDeployment) {
        if (-not $WhatIf) {
            az cognitiveservices account deployment create `
                --name $OpenAIName `
                --resource-group $ResourceGroup `
                --deployment-name $OpenAIDeploymentName `
                --model-name $OpenAIModelName `
                --model-version $OpenAIModelVersion `
                --model-format OpenAI `
                --sku-capacity 50 `
                --sku-name Standard
            
            Write-Log "✅ Deployed GPT-4 model: $OpenAIDeploymentName" "SUCCESS"
        }
    } else {
        Write-Log "✅ GPT-4 deployment exists: $OpenAIDeploymentName" "INFO"
    }
    
    # Step 6: Create AI Hub
    Write-Log "" "INFO"
    Write-Log "Creating Azure AI Hub..." "INFO"
    if (-not (Test-ResourceExists -Type "AIHub" -Name $AIHubName -ResourceGroup $ResourceGroup)) {
        if (-not $WhatIf) {
            # Get resource IDs
            $storageId = az storage account show --name $StorageAccountName --resource-group $ResourceGroup --query "id" -o tsv
            $keyVaultId = az keyvault show --name $KeyVaultName --resource-group $ResourceGroup --query "id" -o tsv
            $appInsightsId = az monitor app-insights component show --app $AppInsightsName --resource-group $SharedResourceGroup --query "id" -o tsv
            
            az ml workspace create `
                --kind hub `
                --name $AIHubName `
                --resource-group $ResourceGroup `
                --location $Location `
                --storage-account $storageId `
                --key-vault $keyVaultId `
                --application-insights $appInsightsId `
                --identity-type SystemAssigned `
                --public-network-access Enabled
            
            Write-Log "✅ Created AI Hub: $AIHubName" "SUCCESS"
            Write-Log "Waiting 15 seconds for Hub identity propagation..." "INFO"
            Start-Sleep -Seconds 15
        }
    } else {
        Write-Log "✅ AI Hub exists: $AIHubName" "INFO"
    }
    
    # Step 7: Create AI Project
    Write-Log "" "INFO"
    Write-Log "Creating Azure AI Project..." "INFO"
    if (-not (Test-ResourceExists -Type "AIHub" -Name $AIProjectName -ResourceGroup $ResourceGroup)) {
        if (-not $WhatIf) {
            az ml workspace create `
                --kind project `
                --name $AIProjectName `
                --resource-group $ResourceGroup `
                --location $Location `
                --hub-id "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$ResourceGroup/providers/Microsoft.MachineLearningServices/workspaces/$AIHubName" `
                --identity-type SystemAssigned `
                --public-network-access Enabled
            
            Write-Log "✅ Created AI Project: $AIProjectName" "SUCCESS"
            Write-Log "Waiting 15 seconds for Project identity propagation..." "INFO"
            Start-Sleep -Seconds 15
        }
    } else {
        Write-Log "✅ AI Project exists: $AIProjectName" "INFO"
    }
    
    # Step 8: Connect Azure OpenAI to AI Hub
    Write-Log "" "INFO"
    Write-Log "Connecting Azure OpenAI to AI Hub..." "INFO"
    if (-not $WhatIf) {
        $openAIId = az cognitiveservices account show --name $OpenAIName --resource-group $ResourceGroup --query "id" -o tsv
        
        # Create connection file
        $connectionFile = "aoai-connection-temp.json"
        $connectionJson = @"
{
  "name": "aoai-connection",
  "type": "azure_open_ai",
  "target": "$openAIId",
  "auth_type": "aad"
}
"@
        $connectionJson | Out-File -FilePath $connectionFile -Encoding utf8
        
        # Create connection (this may fail if connection exists, which is okay)
        try {
            az ml connection create `
                --workspace-name $AIHubName `
                --resource-group $ResourceGroup `
                --file $connectionFile
            Write-Log "✅ Connected Azure OpenAI to Hub" "SUCCESS"
        } catch {
            Write-Log "⚠️ Connection may already exist" "WARNING"
        } finally {
            Remove-Item $connectionFile -ErrorAction SilentlyContinue
        }
    }
    
    # Step 9: Configure RBAC Permissions
    Write-Log "" "INFO"
    Write-Log "Configuring RBAC permissions..." "INFO"
    
    # Get managed identities
    if (Test-ResourceExists -Type "ContainerApp" -Name $ContainerAppName -ResourceGroup $ResourceGroup) {
        $containerAppPrincipalId = az containerapp show `
            --name $ContainerAppName `
            --resource-group $ResourceGroup `
            --query "identity.principalId" -o tsv
        
        if ($containerAppPrincipalId) {
            Write-Log "Container App Managed Identity: $containerAppPrincipalId" "INFO"
            
            # Grant Container App access to OpenAI
            Write-Log "Assigning Cognitive Services OpenAI User to Container App..." "INFO"
            $openAIId = az cognitiveservices account show --name $OpenAIName --resource-group $ResourceGroup --query "id" -o tsv
            az role assignment create `
                --assignee $containerAppPrincipalId `
                --role "Cognitive Services OpenAI User" `
                --scope $openAIId 2>$null
            Write-Log "✅ Assigned OpenAI User role" "SUCCESS"
            
            # Grant Container App access to AI Project
            Write-Log "Assigning Azure AI Developer to Container App..." "INFO"
            $projectId = az ml workspace show --name $AIProjectName --resource-group $ResourceGroup --query "id" -o tsv
            az role assignment create `
                --assignee $containerAppPrincipalId `
                --role "Azure AI Developer" `
                --scope $projectId 2>$null
            Write-Log "✅ Assigned Azure AI Developer role" "SUCCESS"
            
            # Grant Container App access to Storage
            Write-Log "Assigning Storage Blob Data Contributor to Container App..." "INFO"
            $storageId = az storage account show --name $StorageAccountName --resource-group $ResourceGroup --query "id" -o tsv
            az role assignment create `
                --assignee $containerAppPrincipalId `
                --role "Storage Blob Data Contributor" `
                --scope $storageId 2>$null
            Write-Log "✅ Assigned Storage Blob Data Contributor role" "SUCCESS"
        }
    } else {
        Write-Log "⚠️ Container App not found - will need to assign permissions after Container App is created" "WARNING"
    }
    
    # Grant AI Project identity access to OpenAI
    $projectPrincipalId = az ml workspace show `
        --name $AIProjectName `
        --resource-group $ResourceGroup `
        --query "identity.principalId" -o tsv
    
    Write-Log "AI Project Managed Identity: $projectPrincipalId" "INFO"
    Write-Log "Assigning Cognitive Services OpenAI User to AI Project..." "INFO"
    $openAIId = az cognitiveservices account show --name $OpenAIName --resource-group $ResourceGroup --query "id" -o tsv
    az role assignment create `
        --assignee $projectPrincipalId `
        --role "Cognitive Services OpenAI User" `
        --scope $openAIId 2>$null
    Write-Log "✅ Assigned OpenAI User role to AI Project" "SUCCESS"
    
    # Grant AI Project identity access to Storage
    Write-Log "Assigning Storage Blob Data Contributor to AI Project..." "INFO"
    $storageId = az storage account show --name $StorageAccountName --resource-group $ResourceGroup --query "id" -o tsv
    az role assignment create `
        --assignee $projectPrincipalId `
        --role "Storage Blob Data Contributor" `
        --scope $storageId 2>$null
    Write-Log "✅ Assigned Storage Blob Data Contributor to AI Project" "SUCCESS"
    
    # Summary
    Write-Log "" "INFO"
    Write-Log "========================================" "INFO"
    Write-Log "🎉 Azure AI Foundry Setup Complete!" "INFO"
    Write-Log "========================================" "INFO"
    Write-Log "" "INFO"
    Write-Log "Resources Created:" "INFO"
    Write-Log "✅ Resource Group: $ResourceGroup" "INFO"
    Write-Log "✅ Storage Account: $StorageAccountName" "INFO"
    Write-Log "✅ Key Vault: $KeyVaultName" "INFO"
    Write-Log "✅ Azure OpenAI: $OpenAIName" "INFO"
    Write-Log "✅ GPT-4 Deployment: $OpenAIDeploymentName" "INFO"
    Write-Log "✅ AI Hub: $AIHubName" "INFO"
    Write-Log "✅ AI Project: $AIProjectName" "INFO"
    Write-Log "" "INFO"
    Write-Log "Next Steps:" "INFO"
    Write-Log "1. Verify the setup in Azure AI Studio: https://ai.azure.com" "INFO"
    Write-Log "2. Update appsettings.Production-WestUS2.json with:" "INFO"
    Write-Log "   - AzureOpenAI.Endpoint: https://$OpenAIName.openai.azure.com/" "INFO"
    Write-Log "   - AzureOpenAI.DeploymentName: $OpenAIDeploymentName" "INFO"
    Write-Log "   - AzureOpenAI.ResourceName: $OpenAIName" "INFO"
    Write-Log "   - AzureAI.ProjectName: $AIProjectName" "INFO"
    Write-Log "   - AzureAI.ResourceName: $AIProjectName" "INFO"
    Write-Log "" "INFO"
    Write-Log "Log file: $LogFile" "INFO"
    
} catch {
    Write-Log "❌ Setup failed: $_" "ERROR"
    Write-Log "Stack trace: $($_.ScriptStackTrace)" "ERROR"
    exit 1
}
