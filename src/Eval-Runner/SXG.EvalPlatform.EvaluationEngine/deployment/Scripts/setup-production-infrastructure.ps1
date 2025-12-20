#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Production Infrastructure Setup for SXG Evaluation Platform - Multi-Region Deployment
.DESCRIPTION
    Creates Azure resources for production deployment across East US 2 and West US 2
    
    PREREQUISITES (These resources must already exist):
    - Storage Account: sxgagentevalprod (with queues)
    - Application Insights: eval-runner-prod-app-insights
    - Resource Group: EvalCommonRg-UsEast2
    
    This script creates:
    - Container Registry: evalplatformregistryprod (geo-redundant with East US 2 and West US 2)
    - Container Apps and Environments for both regions
    - Azure OpenAI for both regions
    - All required RBAC permissions using Managed Identity (including AcrPull)
    
.NOTES
    Run this script with an account that has Owner or User Access Administrator role
#>

param(
    [switch]$WhatIf,
    [switch]$SkipEastUS2,
    [switch]$SkipWestUS2,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$VerbosePreference = if ($Verbose) { "Continue" } else { "SilentlyContinue" }

# ============================================================================
# CONFIGURATION
# ============================================================================

# Shared Resources (Common to both regions)
$SharedResourceGroup = "EvalCommonRg-UsEast2"
$SharedLocation = "eastus2"
$ContainerRegistry = "evalplatformregistryprod"  # Geo-redundant ACR shared by both regions
$SharedStorageAccount = "sxgagentevalprod"  # Must match existing storage account name
$SharedApplicationInsights = "eval-runner-prod-app-insights"  # Must match existing App Insights name

# East US 2 Region Configuration
$EastUS2 = @{
    Location = "eastus2"
    ResourceGroup = "EvalPlatformRg-ProdEastUS2"
    ContainerAppName = "eval-platform-containerapp-prod-eastus2"
    ContainerAppEnvironment = "eval-platform-managedenv-prod-eastus2"
    AzureOpenAI = "evalplatformopenai-prod-eastus2"
    AzureAIFoundry = "evalplatformprojectproduseast2"  # Existing AI Foundry in East US 2
    AzureAIFoundryRegion = "eastus2"
}

# West US 2 Region Configuration
$WestUS2 = @{
    Location = "westus2"
    ResourceGroup = "EvalPlatformRg-ProdWestUS2"
    ContainerAppName = "eval-platform-containerapp-prod-westus2"
    ContainerAppEnvironment = "eval-platform-managedenv-prod-westus2"
    AzureOpenAI = "evalplatformopenai-prod-westus2"
    AzureAIFoundry = "evalplatformprojectprodnorthcentral"  # Existing AI Foundry in North Central
    AzureAIFoundryRegion = "northcentralus"
}

# Common Configuration
$QueueNames = @(
    "eval-processing-requests",
    "eval-processing-requests-completed",
    "eval-processing-requests-failed"
)

$Tags = @{
    Environment = "Production"
    Project = "SXG-EvalPlatform"
    ManagedBy = "Infrastructure-Script"
}

$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$LogFile = "production-setup-${Timestamp}.log"

# ============================================================================
# HELPER FUNCTIONS
# ============================================================================

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $LogMessage = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] [$Level] $Message"
    Write-Host $LogMessage
    $LogMessage | Out-File -FilePath $LogFile -Append
}

function ConvertTo-TagString {
    param([hashtable]$Tags)
    $tagPairs = $Tags.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }
    return $tagPairs -join " "
}

function Test-ResourceExists {
    param(
        [string]$ResourceType,
        [string]$Name,
        [string]$ResourceGroup = $null
    )
    
    try {
        switch ($ResourceType) {
            "ResourceGroup" {
                $result = az group show --name $Name --query "name" -o tsv 2>$null
            }
            "ContainerRegistry" {
                $result = az acr show --name $Name --query "name" -o tsv 2>$null
            }
            "StorageAccount" {
                $result = az storage account show --name $Name --resource-group $ResourceGroup --query "name" -o tsv 2>$null
            }
            "ContainerAppEnvironment" {
                $result = az containerapp env show --name $Name --resource-group $ResourceGroup --query "name" -o tsv 2>$null
            }
            "ContainerApp" {
                $result = az containerapp show --name $Name --resource-group $ResourceGroup --query "name" -o tsv 2>$null
            }
            "CognitiveServices" {
                $result = az cognitiveservices account show --name $Name --resource-group $ResourceGroup --query "name" -o tsv 2>$null
            }
            "LogAnalytics" {
                $result = az monitor log-analytics workspace show --resource-group $ResourceGroup --workspace-name $Name --query "name" -o tsv 2>$null
            }
            "ApplicationInsights" {
                $result = az monitor app-insights component show --app $Name --resource-group $ResourceGroup --query "name" -o tsv 2>$null
            }
        }
        return $null -ne $result -and $result -ne ""
    } catch {
        return $false
    }
}

function New-ResourceGroupIfNotExists {
    param(
        [string]$Name,
        [string]$Location
    )
    
    if (Test-ResourceExists -ResourceType "ResourceGroup" -Name $Name) {
        Write-Log "Resource Group '$Name' already exists" "INFO"
        return
    }
    
    Write-Log "Creating Resource Group: $Name in $Location" "INFO"
    if (-not $WhatIf) {
        az group create --name $Name --location $Location --tags $(ConvertTo-TagString $Tags) | Out-Null
        Write-Log "✅ Created Resource Group: $Name" "SUCCESS"
    }
}

# ============================================================================
# CONTAINER REGISTRY SETUP (SHARED)
# ============================================================================

function New-SharedContainerRegistry {
    Write-Log "" "INFO"
    Write-Log "========================================" "INFO"
    Write-Log "Setting up Shared Container Registry" "INFO"
    Write-Log "========================================" "INFO"
    
    # Create shared resource group if it doesn't exist
    New-ResourceGroupIfNotExists -Name $SharedResourceGroup -Location $SharedLocation
    
    # Check if ACR exists
    if (Test-ResourceExists -ResourceType "ContainerRegistry" -Name $ContainerRegistry) {
        Write-Log "Container Registry '$ContainerRegistry' already exists" "INFO"
        
        # Check geo-replication
        Write-Log "Checking geo-replication status..." "INFO"
        $replications = az acr replication list --registry $ContainerRegistry --query "[].location" -o tsv 2>$null
        
        if ($replications) {
            Write-Log "Current replications: $($replications -join ', ')" "INFO"
            
            if ($replications -notcontains "westus2") {
                Write-Log "Adding West US 2 replication..." "INFO"
                if (-not $WhatIf) {
                    az acr replication create --registry $ContainerRegistry --location westus2
                    Write-Log "✅ Added West US 2 replication" "SUCCESS"
                }
            } else {
                Write-Log "✅ West US 2 replication already exists" "SUCCESS"
            }
        }
    } else {
        Write-Log "Creating Container Registry: $ContainerRegistry" "INFO"
        if (-not $WhatIf) {
            # Create ACR with Premium SKU (required for geo-replication)
            az acr create `
                --resource-group $SharedResourceGroup `
                --name $ContainerRegistry `
                --sku Premium `
                --location $SharedLocation `
                --admin-enabled false `
                --tags $(ConvertTo-TagString $Tags)
            
            Write-Log "✅ Created Container Registry: $ContainerRegistry" "SUCCESS"
            
            # Add geo-replication to West US 2
            Write-Log "Adding geo-replication to West US 2..." "INFO"
            Start-Sleep -Seconds 5  # Wait for ACR to be ready
            az acr replication create --registry $ContainerRegistry --location westus2
            Write-Log "✅ Added West US 2 replication" "SUCCESS"
        }
    }
}

# ============================================================================
# REGION-SPECIFIC RESOURCE CREATION
# ============================================================================

function New-RegionResources {
    param([hashtable]$Config, [string]$RegionName, [string]$WorkspaceId)
    
    Write-Log "" "INFO"
    Write-Log "========================================" "INFO"
    Write-Log "Setting up $RegionName Region" "INFO"
    Write-Log "========================================" "INFO"
    
    # 1. Create Resource Group
    New-ResourceGroupIfNotExists -Name $Config.ResourceGroup -Location $Config.Location
    
    # 2. Create Azure OpenAI
    Write-Log "Creating Azure OpenAI: $($Config.AzureOpenAI)" "INFO"
    if (-not (Test-ResourceExists -ResourceType "CognitiveServices" -Name $Config.AzureOpenAI -ResourceGroup $Config.ResourceGroup)) {
        if (-not $WhatIf) {
            az cognitiveservices account create `
                --name $Config.AzureOpenAI `
                --resource-group $Config.ResourceGroup `
                --location $Config.Location `
                --kind OpenAI `
                --sku S0 `
                --custom-domain $Config.AzureOpenAI `
                --tags $(ConvertTo-TagString $Tags)
            Write-Log "✅ Created Azure OpenAI" "SUCCESS"
            
            # Deploy GPT-4 model
            Write-Log "Deploying GPT-4 model..." "INFO"
            az cognitiveservices account deployment create `
                --resource-group $Config.ResourceGroup `
                --name $Config.AzureOpenAI `
                --deployment-name "gpt-4" `
                --model-name "gpt-4" `
                --model-version "turbo-2024-04-09" `
                --model-format OpenAI `
                --sku-capacity 10 `
                --sku-name "Standard"
            Write-Log "✅ Deployed GPT-4 model" "SUCCESS"
        }
    } else {
        Write-Log "Azure OpenAI already exists" "INFO"
    }
    
    # 3. Verify AI Foundry exists
    Write-Log "Verifying AI Foundry: $($Config.AzureAIFoundry) in $($Config.AzureAIFoundryRegion)" "INFO"
    $subscriptionId = az account show --query "id" -o tsv
    $aiFoundryRg = if ($Config.AzureAIFoundryRegion -eq "eastus2") { $Config.ResourceGroup } else { $SharedResourceGroup }
    
    try {
        $aiExists = az ml workspace show `
            --name $Config.AzureAIFoundry `
            --resource-group $aiFoundryRg `
            --query "name" -o tsv 2>$null
        
        if ($aiExists) {
            Write-Log "✅ AI Foundry exists: $($Config.AzureAIFoundry)" "INFO"
        } else {
            Write-Log "⚠️  AI Foundry not found: $($Config.AzureAIFoundry)" "WARNING"
            Write-Log "   Please create it manually in Azure AI Studio" "WARNING"
        }
    } catch {
        Write-Log "⚠️  Could not verify AI Foundry - assuming it exists" "WARNING"
    }
    
    # 4. Create Container App Environment
    Write-Log "Creating Container App Environment: $($Config.ContainerAppEnvironment)" "INFO"
    if (-not (Test-ResourceExists -ResourceType "ContainerAppEnvironment" -Name $Config.ContainerAppEnvironment -ResourceGroup $Config.ResourceGroup)) {
        if (-not $WhatIf) {
            az containerapp env create `
                --name $Config.ContainerAppEnvironment `
                --resource-group $Config.ResourceGroup `
                --location $Config.Location `
                --logs-workspace-id $WorkspaceId `
                --tags $(ConvertTo-TagString $Tags)
            Write-Log "✅ Created Container App Environment" "SUCCESS"
        }
    } else {
        Write-Log "Container App Environment already exists" "INFO"
    }
    
    # 5. Create Container App with System-Assigned Managed Identity
    Write-Log "Creating Container App: $($Config.ContainerAppName)" "INFO"
    if (-not (Test-ResourceExists -ResourceType "ContainerApp" -Name $Config.ContainerAppName -ResourceGroup $Config.ResourceGroup)) {
        if (-not $WhatIf) {
            # Create with a placeholder image - will be updated during deployment
            az containerapp create `
                --name $Config.ContainerAppName `
                --resource-group $Config.ResourceGroup `
                --environment $Config.ContainerAppEnvironment `
                --image mcr.microsoft.com/azuredocs/containerapps-helloworld:latest `
                --target-port 80 `
                --ingress internal `
                --min-replicas 1 `
                --max-replicas 100 `
                --cpu 2.0 `
                --memory 4Gi `
                --system-assigned `
                --registry-server "$ContainerRegistry.azurecr.io" `
                --tags $(ConvertTo-TagString $Tags)
            Write-Log "✅ Created Container App with System-Assigned Managed Identity" "SUCCESS"
        }
    } else {
        Write-Log "Container App already exists" "INFO"
        
        # Ensure managed identity is enabled
        Write-Log "Ensuring System-Assigned Managed Identity is enabled..." "INFO"
        if (-not $WhatIf) {
            az containerapp identity assign `
                --name $Config.ContainerAppName `
                --resource-group $Config.ResourceGroup `
                --system-assigned
            Write-Log "✅ System-Assigned Managed Identity enabled" "SUCCESS"
        }
    }
}

# ============================================================================
# RBAC PERMISSIONS SETUP
# ============================================================================

function Set-RegionRBACPermissions {
    param([hashtable]$Config, [string]$RegionName)
    
    Write-Log "" "INFO"
    Write-Log "========================================" "INFO"
    Write-Log "Setting up RBAC Permissions for $RegionName" "INFO"
    Write-Log "========================================" "INFO"
    
    # Get Container App's Managed Identity Principal ID
    Write-Log "Retrieving Managed Identity Principal ID..." "INFO"
    $principalId = az containerapp show `
        --name $Config.ContainerAppName `
        --resource-group $Config.ResourceGroup `
        --query "identity.principalId" -o tsv
    
    if (-not $principalId) {
        Write-Log "❌ Failed to retrieve Managed Identity Principal ID" "ERROR"
        throw "Cannot proceed without Managed Identity"
    }
    
    Write-Log "Managed Identity Principal ID: $principalId" "INFO"
    
    # Get resource IDs (shared resources)
    $storageAccountId = az storage account show `
        --name $SharedStorageAccount `
        --resource-group $SharedResourceGroup `
        --query "id" -o tsv
    
    $openaiId = az cognitiveservices account show `
        --name $Config.AzureOpenAI `
        --resource-group $Config.ResourceGroup `
        --query "id" -o tsv
    
    $acrId = az acr show `
        --name $ContainerRegistry `
        --query "id" -o tsv
    
    # Azure AI Foundry resource ID (Machine Learning workspace)
    $subscriptionId = az account show --query "id" -o tsv
    $aiFoundryRg = if ($Config.AzureAIFoundryRegion -eq "eastus2") { $Config.ResourceGroup } else { $SharedResourceGroup }
    $aiFoundryId = "/subscriptions/$subscriptionId/resourceGroups/$aiFoundryRg/providers/Microsoft.MachineLearningServices/workspaces/$($Config.AzureAIFoundry)"
    
    # Wait for identity propagation
    Write-Log "Waiting 30 seconds for identity propagation..." "INFO"
    Start-Sleep -Seconds 30
    
    # 1. Storage Queue Data Contributor (shared storage)
    Write-Log "Assigning 'Storage Queue Data Contributor' role on shared storage..." "INFO"
    if (-not $WhatIf) {
        try {
            az role assignment create `
                --assignee $principalId `
                --role "Storage Queue Data Contributor" `
                --scope $storageAccountId `
                2>$null
            Write-Log "✅ Assigned Storage Queue Data Contributor" "SUCCESS"
        } catch {
            Write-Log "Role assignment may already exist or is in progress" "WARNING"
        }
    }
    
    # 2. Cognitive Services OpenAI User (regional OpenAI)
    Write-Log "Assigning 'Cognitive Services OpenAI User' role..." "INFO"
    if (-not $WhatIf) {
        try {
            az role assignment create `
                --assignee $principalId `
                --role "Cognitive Services OpenAI User" `
                --scope $openaiId `
                2>$null
            Write-Log "✅ Assigned Cognitive Services OpenAI User" "SUCCESS"
        } catch {
            Write-Log "Role assignment may already exist or is in progress" "WARNING"
        }
    }
    
    # 3. AcrPull (shared registry)
    Write-Log "Assigning 'AcrPull' role on shared registry..." "INFO"
    if (-not $WhatIf) {
        try {
            az role assignment create `
                --assignee $principalId `
                --role "AcrPull" `
                --scope $acrId `
                2>$null
            Write-Log "✅ Assigned AcrPull" "SUCCESS"
        } catch {
            Write-Log "Role assignment may already exist or is in progress" "WARNING"
        }
    }
    
    # 4. Azure AI Developer (for AI Foundry)
    Write-Log "Assigning 'Azure AI Developer' role for AI Foundry in $($Config.AzureAIFoundryRegion)..." "INFO"
    if (-not $WhatIf) {
        try {
            az role assignment create `
                --assignee $principalId `
                --role "Azure AI Developer" `
                --scope $aiFoundryId `
                2>$null
            Write-Log "✅ Assigned Azure AI Developer" "SUCCESS"
        } catch {
            Write-Log "Role assignment may already exist or is in progress" "WARNING"
            Write-Log "   AI Foundry may be in different resource group - verify manually" "WARNING"
        }
    }
    
    Write-Log "" "INFO"
    Write-Log "✅ All RBAC permissions configured for $RegionName" "SUCCESS"
}

# ============================================================================
# SCALING RULES SETUP
# ============================================================================

function Set-ContainerAppScaling {
    param([hashtable]$Config, [string]$RegionName)
    
    Write-Log "" "INFO"
    Write-Log "Configuring auto-scaling for $RegionName..." "INFO"
    
    if (-not $WhatIf) {
        # Get shared storage account ID for scaling rule
        $storageAccountId = az storage account show `
            --name $SharedStorageAccount `
            --resource-group $SharedResourceGroup `
            --query "id" -o tsv
        
        # Configure queue-based scaling with managed identity
        az containerapp update `
            --name $Config.ContainerAppName `
            --resource-group $Config.ResourceGroup `
            --min-replicas 1 `
            --max-replicas 100 `
            --scale-rule-name "queue-scaling" `
            --scale-rule-type "azure-queue" `
            --scale-rule-metadata "queueName=eval-processing-requests" "queueLength=2" `
            --scale-rule-auth "connection=managed-identity" "storageAccountId=$storageAccountId"
        
        Write-Log "✅ Configured queue-based auto-scaling (1-100 replicas)" "SUCCESS"
    }
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

try {
    Write-Log "======================================================" "INFO"
    Write-Log "Production Infrastructure Setup - Multi-Region" "INFO"
    Write-Log "======================================================" "INFO"
    Write-Log "Started at: $(Get-Date)" "INFO"
    Write-Log "WhatIf Mode: $WhatIf" "INFO"
    Write-Log "" "INFO"
    
    # Verify Azure CLI login
    try {
        $account = az account show --query "user.name" -o tsv
        Write-Log "Logged in as: $account" "INFO"
    } catch {
        Write-Log "❌ Not logged in to Azure. Please run 'az login'" "ERROR"
        exit 1
    }
    
    # Set subscription (if needed)
    # az account set --subscription "your-subscription-id"
    
    # 1. Create Container Registry (geo-redundant)
    New-SharedContainerRegistry
    
    Write-Log "" "INFO"
    Write-Log "========================================" "INFO"
    Write-Log "Verifying Other Common Resources" "INFO"
    Write-Log "========================================" "INFO"
    
    # 2. Verify Storage Account exists (prerequisite)
    Write-Log "Checking Storage Account: $SharedStorageAccount" "INFO"
    if (-not (Test-ResourceExists -ResourceType "StorageAccount" -Name $SharedStorageAccount -ResourceGroup $SharedResourceGroup)) {
        Write-Log "❌ Storage Account '$SharedStorageAccount' not found!" "ERROR"
        Write-Log "Please create it first in resource group: $SharedResourceGroup" "ERROR"
        exit 1
    }
    Write-Log "✅ Storage Account exists" "SUCCESS"
    
    # 3. Verify Application Insights exists (prerequisite)
    Write-Log "Checking Application Insights: $SharedApplicationInsights" "INFO"
    if (-not (Test-ResourceExists -ResourceType "ApplicationInsights" -Name $SharedApplicationInsights -ResourceGroup $SharedResourceGroup)) {
        Write-Log "❌ Application Insights '$SharedApplicationInsights' not found!" "ERROR"
        Write-Log "Please create it first in resource group: $SharedResourceGroup" "ERROR"
        exit 1
    }
    Write-Log "✅ Application Insights exists" "SUCCESS"
    
    # Get workspace ID for Container App Environment creation
    $workspaceId = az monitor app-insights component show `
        --app $SharedApplicationInsights `
        --resource-group $SharedResourceGroup `
        --query "workspaceResourceId" -o tsv
    
    Write-Log "All common resources verified!" "SUCCESS"
    Write-Log "" "INFO"
    
    # 4. Create East US 2 Resources
    if (-not $SkipEastUS2) {
        New-RegionResources -Config $EastUS2 -RegionName "East US 2" -WorkspaceId $workspaceId
        Set-RegionRBACPermissions -Config $EastUS2 -RegionName "East US 2"
        Set-ContainerAppScaling -Config $EastUS2 -RegionName "East US 2"
    } else {
        Write-Log "Skipping East US 2 setup" "INFO"
    }
    
    # 5. Create West US 2 Resources
    if (-not $SkipWestUS2) {
        New-RegionResources -Config $WestUS2 -RegionName "West US 2" -WorkspaceId $workspaceId
        Set-RegionRBACPermissions -Config $WestUS2 -RegionName "West US 2"
        Set-ContainerAppScaling -Config $WestUS2 -RegionName "West US 2"
    } else {
        Write-Log "Skipping West US 2 setup" "INFO"
    }
    
    Write-Log "" "INFO"
    Write-Log "======================================================" "INFO"
    Write-Log "🎉 Production Infrastructure Setup Complete!" "INFO"
    Write-Log "======================================================" "INFO"
    Write-Log "" "INFO"
    Write-Log "Summary:" "INFO"
    Write-Log "✅ Created/Verified Container Registry: $ContainerRegistry (geo-redundant)" "INFO"
    Write-Log "✅ Verified Storage Account: $SharedStorageAccount" "INFO"
    Write-Log "✅ Verified Application Insights: $SharedApplicationInsights" "INFO"
    Write-Log "✅ Created East US 2 Container App: $($EastUS2.ContainerAppName)" "INFO"
    Write-Log "✅ Created West US 2 Container App: $($WestUS2.ContainerAppName)" "INFO"
    Write-Log "" "INFO"
    Write-Log "Next Steps:" "INFO"
    Write-Log "1. Run setup-foundry-storage-eastus2.ps1 to create AI Foundry storage" "INFO"
    Write-Log "2. Run setup-foundry-storage-northcentral.ps1 to create AI Foundry storage" "INFO"
    Write-Log "3. Link storage accounts to AI Foundry projects in Azure AI Studio" "INFO"
    Write-Log "4. Update appsettings files with Application Insights connection string" "INFO"
    Write-Log "5. Deploy applications using deploy-production-eastus2.ps1 and deploy-production-westus2.ps1" "INFO"
    Write-Log "" "INFO"
    Write-Log "Log file: $LogFile" "INFO"
    
} catch {
    Write-Log "❌ Setup failed: $_" "ERROR"
    Write-Log "Stack trace: $($_.ScriptStackTrace)" "ERROR"
    exit 1
}
