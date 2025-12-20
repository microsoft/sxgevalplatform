#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Setup Production Resources for East US 2 Region
.DESCRIPTION
    Creates all Azure resources needed for East US 2 production deployment:
    - Container Registry (geo-redundant, shared) - if first run
    - Container App Environment
    - Container App with System-Assigned Managed Identity
    - Azure OpenAI
    - All required RBAC permissions
    
    PREREQUISITES (must already exist):
    - Resource Group: EvalCommonRg-UsEast2
    - Storage Account: sxgagentevalprod
    - Application Insights: eval-runner-prod-app-insights
    - AI Foundry Project: evalplatformprojectproduseast2
#>

param(
    [switch]$WhatIf,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$VerbosePreference = if ($Verbose) { "Continue" } else { "SilentlyContinue" }

# ============================================================================
# CONFIGURATION - East US 2
# ============================================================================

$Region = "East US 2"
$Location = "eastus2"

# Shared Resources
$SharedResourceGroup = "EvalCommonRg-UsEast2"
$ContainerRegistry = "evalplatformregistryprod"
$SharedStorageAccount = "sxgagentevalprod"
$SharedApplicationInsights = "eval-runner-prod-app-insights"

# Regional Resources
$ResourceGroup = "EvalRunnerRG-EastUS2"
$ContainerAppName = "eval-runner-prod-eus2"  # Max 32 chars, lowercase, alphanumeric or '-'
$ContainerAppEnvironment = "eval-runner-env-prod-eus2"
$AzureOpenAI = "evalplatformopenai-prod-eastus2"
$AIFoundryProject = "evalplatformprojectproduseast2"

$QueueName = "eval-processing-requests"

# Tags for resource group (required by policy)
$ResourceGroupTags = @{
    Environment = "Production"
    Project = "SXG-EvalPlatform"
    Region = "EastUS2"
    ComponentId = "SXG-EvalPlatform-EvalRunner"
    Env = "Production"
}

$Tags = @{
    Environment = "Production"
    Project = "SXG-EvalPlatform"
    Region = "EastUS2"
}

$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$LogFile = "eastus2-setup-${Timestamp}.log"

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
    param([string]$Type, [string]$Name, [string]$ResourceGroup = $null)
    try {
        switch ($Type) {
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
            "ApplicationInsights" {
                $result = az monitor app-insights component show --app $Name --resource-group $ResourceGroup --query "name" -o tsv 2>$null
            }
        }
        return $null -ne $result -and $result -ne ""
    } catch {
        return $false
    }
}

# ============================================================================
# MAIN SETUP
# ============================================================================

try {
    Write-Log "======================================================" "INFO"
    Write-Log "Production Setup - East US 2 Region" "INFO"
    Write-Log "======================================================" "INFO"
    Write-Log "Started at: $(Get-Date)" "INFO"
    Write-Log "" "INFO"
    
    # Verify Azure CLI login
    $account = az account show --query "user.name" -o tsv
    Write-Log "Logged in as: $account" "INFO"
    Write-Log "" "INFO"
    
    # Step 1: Verify Common Resources
    Write-Log "Verifying common resources..." "INFO"
    
    if (-not (Test-ResourceExists -Type "ResourceGroup" -Name $SharedResourceGroup)) {
        Write-Log "❌ Resource Group '$SharedResourceGroup' not found!" "ERROR"
        exit 1
    }
    
    if (-not (Test-ResourceExists -Type "StorageAccount" -Name $SharedStorageAccount -ResourceGroup $SharedResourceGroup)) {
        Write-Log "❌ Storage Account '$SharedStorageAccount' not found!" "ERROR"
        exit 1
    }
    
    if (-not (Test-ResourceExists -Type "ApplicationInsights" -Name $SharedApplicationInsights -ResourceGroup $SharedResourceGroup)) {
        Write-Log "❌ Application Insights '$SharedApplicationInsights' not found!" "ERROR"
        exit 1
    }
    
    Write-Log "✅ All common resources verified" "SUCCESS"
    Write-Log "" "INFO"
    
    # Step 2: Create/Verify Container Registry
    Write-Log "Setting up Container Registry..." "INFO"
    if (Test-ResourceExists -Type "ContainerRegistry" -Name $ContainerRegistry) {
        Write-Log "✅ Container Registry exists: $ContainerRegistry" "INFO"
        
        # Check geo-replication
        $replications = az acr replication list --registry $ContainerRegistry --query "[].location" -o tsv 2>$null
        if ($replications) {
            Write-Log "Current replications: $($replications -join ', ')" "INFO"
            if ($replications -notcontains "westus2") {
                Write-Log "Adding West US 2 replication..." "INFO"
                if (-not $WhatIf) {
                    az acr replication create --registry $ContainerRegistry --location westus2
                    Write-Log "✅ Added West US 2 replication" "SUCCESS"
                }
            }
        }
    } else {
        Write-Log "Creating Container Registry: $ContainerRegistry" "INFO"
        if (-not $WhatIf) {
            az acr create `
                --resource-group $SharedResourceGroup `
                --name $ContainerRegistry `
                --sku Premium `
                --location $Location `
                --admin-enabled false `
                --tags Environment=Production Project=SXG-EvalPlatform
            
            Write-Log "✅ Created Container Registry" "SUCCESS"
            
            Start-Sleep -Seconds 5
            az acr replication create --registry $ContainerRegistry --location westus2
            Write-Log "✅ Added West US 2 replication" "SUCCESS"
        }
    }
    Write-Log "" "INFO"
    
    # Step 3: Create Regional Resource Group
    Write-Log "Creating regional resource group..." "INFO"
    if (-not (Test-ResourceExists -Type "ResourceGroup" -Name $ResourceGroup)) {
        if (-not $WhatIf) {
            # Build tags array for proper Azure CLI parsing
            $tagArgs = @()
            foreach ($tag in $ResourceGroupTags.GetEnumerator()) {
                $tagArgs += "$($tag.Key)=$($tag.Value)"
            }
            
            $cmd = "az group create --name $ResourceGroup --location $Location --tags $($tagArgs -join ' ')"
            Write-Log "Executing: $cmd" "INFO"
            $result = Invoke-Expression $cmd 2>&1
            
            if ($LASTEXITCODE -ne 0) {
                Write-Log "❌ Failed to create Resource Group: $result" "ERROR"
                exit 1
            }
            Write-Log "✅ Created Resource Group: $ResourceGroup" "SUCCESS"
        }
    } else {
        Write-Log "✅ Resource Group exists: $ResourceGroup" "INFO"
    }
    Write-Log "" "INFO"
    
    # Step 4: Create Azure OpenAI
    Write-Log "Creating Azure OpenAI..." "INFO"
    if (-not (Test-ResourceExists -Type "CognitiveServices" -Name $AzureOpenAI -ResourceGroup $ResourceGroup)) {
        if (-not $WhatIf) {
            az cognitiveservices account create `
                --name $AzureOpenAI `
                --resource-group $ResourceGroup `
                --location $Location `
                --kind OpenAI `
                --sku S0 `
                --custom-domain $AzureOpenAI `
                --tags Environment=Production Project=SXG-EvalPlatform Region=EastUS2
            
            Write-Log "✅ Created Azure OpenAI: $AzureOpenAI" "SUCCESS"
            
            Write-Log "Deploying GPT-4 model..." "INFO"
            az cognitiveservices account deployment create `
                --resource-group $ResourceGroup `
                --name $AzureOpenAI `
                --deployment-name "gpt-4" `
                --model-name "gpt-4" `
                --model-version "turbo-2024-04-09" `
                --model-format OpenAI `
                --sku-capacity 10 `
                --sku-name "Standard"
            
            Write-Log "✅ Deployed GPT-4 model" "SUCCESS"
        }
    } else {
        Write-Log "✅ Azure OpenAI exists: $AzureOpenAI" "INFO"
    }
    Write-Log "" "INFO"
    
    # Step 5: Create Container App Environment
    Write-Log "Creating Container App Environment..." "INFO"
    if (-not (Test-ResourceExists -Type "ContainerAppEnvironment" -Name $ContainerAppEnvironment -ResourceGroup $ResourceGroup)) {
        if (-not $WhatIf) {
            # Get Log Analytics workspace details from App Insights
            $appInsights = az monitor app-insights component show `
                --app $SharedApplicationInsights `
                --resource-group $SharedResourceGroup -o json | ConvertFrom-Json
            
            $workspaceResourceId = $appInsights.workspaceResourceId
            
            # Extract workspace name and resource group from the resource ID
            # Format: /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.OperationalInsights/workspaces/{name}
            $workspaceRg = $workspaceResourceId -replace '.*resourceGroups/([^/]+)/.*', '$1'
            $workspaceName = $workspaceResourceId -replace '.*workspaces/', ''
            
            # Get workspace ID and key
            $workspaceId = az monitor log-analytics workspace show `
                --resource-group $workspaceRg `
                --workspace-name $workspaceName `
                --query "customerId" -o tsv
            
            $workspaceKey = az monitor log-analytics workspace get-shared-keys `
                --resource-group $workspaceRg `
                --workspace-name $workspaceName `
                --query "primarySharedKey" -o tsv
            
            az containerapp env create `
                --name $ContainerAppEnvironment `
                --resource-group $ResourceGroup `
                --location $Location `
                --logs-destination log-analytics `
                --logs-workspace-id $workspaceId `
                --logs-workspace-key $workspaceKey `
                --tags Environment=Production Project=SXG-EvalPlatform Region=EastUS2
            
            if ($LASTEXITCODE -ne 0) {
                Write-Log "❌ Failed to create Container App Environment" "ERROR"
                exit 1
            }
            Write-Log "✅ Created Container App Environment" "SUCCESS"
        }
    } else {
        Write-Log "✅ Container App Environment exists" "INFO"
    }
    Write-Log "" "INFO"
    
    # Step 6: Create Container App
    Write-Log "Creating Container App..." "INFO"
    if (-not (Test-ResourceExists -Type "ContainerApp" -Name $ContainerAppName -ResourceGroup $ResourceGroup)) {
        if (-not $WhatIf) {
            az containerapp create `
                --name $ContainerAppName `
                --resource-group $ResourceGroup `
                --environment $ContainerAppEnvironment `
                --image mcr.microsoft.com/azuredocs/containerapps-helloworld:latest `
                --target-port 80 `
                --ingress external `
                --min-replicas 1 `
                --max-replicas 100 `
                --cpu 2 `
                --memory 4Gi `
                --system-assigned `
                --tags Environment=Production Project=SXG-EvalPlatform Region=EastUS2
            
            if ($LASTEXITCODE -ne 0) {
                Write-Log "❌ Failed to create Container App" "ERROR"
                exit 1
            }
            Write-Log "✅ Created Container App: $ContainerAppName" "SUCCESS"
        }
    } else {
        Write-Log "✅ Container App exists: $ContainerAppName" "INFO"
    }
    Write-Log "" "INFO"
    
    # Step 7: Setup RBAC Permissions
    Write-Log "Configuring RBAC permissions..." "INFO"
    
    $principalId = az containerapp show `
        --name $ContainerAppName `
        --resource-group $ResourceGroup `
        --query "identity.principalId" -o tsv
    
    Write-Log "Managed Identity Principal ID: $principalId" "INFO"
    Write-Log "Waiting 30 seconds for identity propagation..." "INFO"
    Start-Sleep -Seconds 30
    
    # Get resource IDs
    $storageId = az storage account show --name $SharedStorageAccount --resource-group $SharedResourceGroup --query "id" -o tsv
    $openaiId = az cognitiveservices account show --name $AzureOpenAI --resource-group $ResourceGroup --query "id" -o tsv
    $acrId = az acr show --name $ContainerRegistry --query "id" -o tsv
    $subscriptionId = az account show --query "id" -o tsv
    $aiFoundryId = "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.MachineLearningServices/workspaces/$AIFoundryProject"
    
    if (-not $WhatIf) {
        # AcrPull
        Write-Log "Assigning AcrPull role..." "INFO"
        az role assignment create --assignee $principalId --role "AcrPull" --scope $acrId 2>$null
        Write-Log "✅ Assigned AcrPull" "SUCCESS"
        
        # Storage Queue Data Contributor
        Write-Log "Assigning Storage Queue Data Contributor role..." "INFO"
        az role assignment create --assignee $principalId --role "Storage Queue Data Contributor" --scope $storageId 2>$null
        Write-Log "✅ Assigned Storage Queue Data Contributor" "SUCCESS"
        
        # Cognitive Services OpenAI User
        Write-Log "Assigning Cognitive Services OpenAI User role..." "INFO"
        az role assignment create --assignee $principalId --role "Cognitive Services OpenAI User" --scope $openaiId 2>$null
        Write-Log "✅ Assigned Cognitive Services OpenAI User" "SUCCESS"
        
        # Azure AI Developer
        Write-Log "Assigning Azure AI Developer role..." "INFO"
        az role assignment create --assignee $principalId --role "Azure AI Developer" --scope $aiFoundryId 2>$null
        Write-Log "✅ Assigned Azure AI Developer" "SUCCESS"
    }
    Write-Log "" "INFO"
    
    # Step 8: Configure Queue-based Auto-scaling
    Write-Log "Configuring queue-based auto-scaling..." "INFO"
    if (-not $WhatIf) {
        az containerapp update `
            --name $ContainerAppName `
            --resource-group $ResourceGroup `
            --min-replicas 1 `
            --max-replicas 100 `
            --scale-rule-name "queue-scaling" `
            --scale-rule-type "azure-queue" `
            --scale-rule-metadata "queueName=$QueueName" "queueLength=2" `
            --scale-rule-auth "connection=managed-identity" "storageAccountId=$storageId"
        
        Write-Log "✅ Configured auto-scaling (1-100 replicas)" "SUCCESS"
    }
    Write-Log "" "INFO"
    
    Write-Log "======================================================" "INFO"
    Write-Log "🎉 East US 2 Setup Complete!" "INFO"
    Write-Log "======================================================" "INFO"
    Write-Log "" "INFO"
    Write-Log "Resources Created:" "INFO"
    Write-Log "✅ Container Registry: $ContainerRegistry (geo-redundant)" "INFO"
    Write-Log "✅ Resource Group: $ResourceGroup" "INFO"
    Write-Log "✅ Azure OpenAI: $AzureOpenAI" "INFO"
    Write-Log "✅ Container App Environment: $ContainerAppEnvironment" "INFO"
    Write-Log "✅ Container App: $ContainerAppName" "INFO"
    Write-Log "✅ RBAC Permissions: AcrPull, Storage Queue Data Contributor, Cognitive Services OpenAI User, Azure AI Developer" "INFO"
    Write-Log "" "INFO"
    Write-Log "Next Steps:" "INFO"
    Write-Log "1. Run setup-foundry-storage-eastus2.ps1 to create AI Foundry storage" "INFO"
    Write-Log "2. Update appsettings.Production-EastUS2.json with connection strings" "INFO"
    Write-Log "3. Deploy application using deploy-production-eastus2.ps1" "INFO"
    Write-Log "" "INFO"
    Write-Log "Log file: $LogFile" "INFO"
    
} catch {
    Write-Log "❌ Setup failed: $_" "ERROR"
    Write-Log "Stack trace: $($_.ScriptStackTrace)" "ERROR"
    exit 1
}
