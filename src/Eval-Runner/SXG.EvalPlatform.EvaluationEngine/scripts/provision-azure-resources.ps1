#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Provision Azure Resources for SXG Evaluation Platform

.DESCRIPTION
    This script provisions all required Azure resources for the SXG Evaluation Platform:
    - Resource Group
    - Container Apps Environment  
    - Container Registry
    - Storage Account (with queue)
    - Azure OpenAI/Cognitive Services Account
    - Azure AI Foundry Project
    - Application Insights
    - Container App (with system-assigned managed identity)

.PARAMETER ResourceGroupName
    Name of the Azure Resource Group to create/use

.PARAMETER Location
    Azure region for resource deployment (default: eastus)

.PARAMETER ContainerAppName
    Name for the Container App

.PARAMETER ContainerAppEnvName
    Name for the Container Apps Environment

.PARAMETER RegistryName
    Name for the Azure Container Registry

.PARAMETER StorageAccountName
    Name for the Azure Storage Account

.PARAMETER OpenAIAccountName
    Name for the Azure OpenAI/Cognitive Services Account

.PARAMETER AIProjectName
    Name for the Azure AI Foundry Project

.PARAMETER AppInsightsName
    Name for Application Insights

.PARAMETER SubscriptionId
    Azure Subscription ID

.PARAMETER Environment
    Environment name (Development, PPE, Production)

.EXAMPLE
    .\provision-azure-resources.ps1 -ResourceGroupName "rg-eval-platform" -ContainerAppName "eval-app"

.EXAMPLE
    .\provision-azure-resources.ps1 -ResourceGroupName "rg-eval-platform" -Environment "PPE" -Location "westus2"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory = $true)]
    [string]$ContainerAppName,
    
    [string]$Location = "eastus",
    [string]$ContainerAppEnvName = "$ContainerAppName-env",
    [string]$RegistryName = "",  # Will auto-generate if empty
    [string]$StorageAccountName = "", # Will auto-generate if empty
    [string]$OpenAIAccountName = "",  # Will auto-generate if empty
    [string]$AIProjectName = "",      # Will auto-generate if empty
    [string]$AppInsightsName = "",    # Will auto-generate if empty
    [string]$SubscriptionId = "",
    [string]$Environment = "Development",
    [switch]$SkipAI = $false,
    [switch]$Verbose
)

# Set error handling
$ErrorActionPreference = "Stop"
$VerbosePreference = if ($Verbose) { "Continue" } else { "SilentlyContinue" }

# Generate unique names if not provided
$timestamp = Get-Date -Format "yyyyMMdd"
$envLower = $Environment.ToLower()

if (-not $RegistryName) {
    $RegistryName = "evalreg$envLower$timestamp"
}

if (-not $StorageAccountName) {
    $StorageAccountName = "evalstore$envLower$timestamp"
}

if (-not $OpenAIAccountName) {
    $OpenAIAccountName = "evalopenai$envLower$timestamp"
}

if (-not $AIProjectName) {
    $AIProjectName = "evalaiproject$envLower"
}

if (-not $AppInsightsName) {
    $AppInsightsName = "evalinsights$envLower"
}

# Logging function
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logMessage = "[$timestamp] [$Level] $Message"
    Write-Host $logMessage
}

function Test-Prerequisites {
    Write-Log "Checking prerequisites..." "INFO"
    
    # Check Azure CLI
    try {
        $azVersion = az --version 2>$null | Select-Object -First 1
        if (-not $azVersion) { throw "Azure CLI not found" }
        Write-Log "✓ Azure CLI available" "INFO"
    } catch {
        Write-Log "✗ Azure CLI is not installed or not in PATH" "ERROR"
        throw "Azure CLI is required. Install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    }
    
    # Check Azure login
    try {
        $account = az account show --query "user.name" -o tsv 2>$null
        if (-not $account) { throw "Not logged in" }
        Write-Log "✓ Logged in as: $account" "INFO"
    } catch {
        Write-Log "✗ Not logged in to Azure" "ERROR"
        throw "Please run 'az login' first"
    }
    
    # Set subscription if provided
    if ($SubscriptionId) {
        Write-Log "Setting subscription: $SubscriptionId" "INFO"
        az account set --subscription $SubscriptionId
        $currentSub = az account show --query "name" -o tsv
        Write-Log "✓ Using subscription: $currentSub" "INFO"
    } else {
        $SubscriptionId = az account show --query "id" -o tsv
        Write-Log "✓ Using current subscription: $SubscriptionId" "INFO"
    }
}

function New-ResourceGroup {
    Write-Log "Creating Resource Group: $ResourceGroupName" "INFO"
    
    $exists = az group exists --name $ResourceGroupName --output tsv
    if ($exists -eq "true") {
        Write-Log "✓ Resource Group already exists: $ResourceGroupName" "INFO"
        return
    }
    
    try {
        az group create --name $ResourceGroupName --location $Location --output none
        Write-Log "✓ Resource Group created: $ResourceGroupName" "INFO"
    } catch {
        Write-Log "✗ Failed to create Resource Group: $_" "ERROR"
        throw
    }
}

function New-ContainerRegistry {
    Write-Log "Creating Container Registry: $RegistryName" "INFO"
    
    try {
        # Check if registry exists
        $existingRegistry = az acr show --name $RegistryName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null
        if ($existingRegistry) {
            Write-Log "✓ Container Registry already exists: $RegistryName" "INFO"
            return
        }
        
        # Create registry
        az acr create `
            --name $RegistryName `
            --resource-group $ResourceGroupName `
            --location $Location `
            --sku Basic `
            --admin-enabled false `
            --output none
        
        Write-Log "✓ Container Registry created: $RegistryName" "INFO"
        Write-Log "  Registry URL: $RegistryName.azurecr.io" "INFO"
    } catch {
        Write-Log "✗ Failed to create Container Registry: $_" "ERROR"
        throw
    }
}

function New-StorageAccount {
    Write-Log "Creating Storage Account: $StorageAccountName" "INFO"
    
    try {
        # Check if storage account exists
        $existingStorage = az storage account show --name $StorageAccountName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null
        if ($existingStorage) {
            Write-Log "✓ Storage Account already exists: $StorageAccountName" "INFO"
        } else {
            # Create storage account
            az storage account create `
                --name $StorageAccountName `
                --resource-group $ResourceGroupName `
                --location $Location `
                --sku Standard_LRS `
                --kind StorageV2 `
                --access-tier Hot `
                --allow-blob-public-access false `
                --output none
            
            Write-Log "✓ Storage Account created: $StorageAccountName" "INFO"
        }
        
        # Create evaluation queue
        Write-Log "Creating evaluation queue..." "INFO"
        $queueName = "eval-processing-requests"
        
        # Get storage account key for queue creation
        $storageKey = az storage account keys list --account-name $StorageAccountName --resource-group $ResourceGroupName --query "[0].value" -o tsv
        
        # Create queue (will skip if exists)
        az storage queue create `
            --name $queueName `
            --account-name $StorageAccountName `
            --account-key $storageKey `
            --output none 2>$null
        
        Write-Log "✓ Storage queue created: $queueName" "INFO"
        
    } catch {
        Write-Log "✗ Failed to create Storage Account: $_" "ERROR"
        throw
    }
}

function New-ApplicationInsights {
    Write-Log "Creating Application Insights: $AppInsightsName" "INFO"
    
    try {
        # Check if App Insights exists
        $existingInsights = az monitor app-insights component show --app $AppInsightsName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null
        if ($existingInsights) {
            Write-Log "✓ Application Insights already exists: $AppInsightsName" "INFO"
            return
        }
        
        # Create Application Insights
        az monitor app-insights component create `
            --app $AppInsightsName `
            --resource-group $ResourceGroupName `
            --location $Location `
            --kind web `
            --application-type web `
            --output none
        
        Write-Log "✓ Application Insights created: $AppInsightsName" "INFO"
        
        # Get connection string for reference
        $connectionString = az monitor app-insights component show --app $AppInsightsName --resource-group $ResourceGroupName --query "connectionString" -o tsv
        Write-Log "  Connection String available for configuration" "INFO"
        
    } catch {
        Write-Log "✗ Failed to create Application Insights: $_" "ERROR"
        throw
    }
}

function New-CognitiveServices {
    if ($SkipAI) {
        Write-Log "Skipping Azure OpenAI/Cognitive Services creation" "INFO"
        return
    }
    
    Write-Log "Creating Azure OpenAI/Cognitive Services: $OpenAIAccountName" "INFO"
    
    try {
        # Check if OpenAI account exists
        $existingOpenAI = az cognitiveservices account show --name $OpenAIAccountName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null
        if ($existingOpenAI) {
            Write-Log "✓ Azure OpenAI account already exists: $OpenAIAccountName" "INFO"
            return
        }
        
        # Create Azure OpenAI account
        az cognitiveservices account create `
            --name $OpenAIAccountName `
            --resource-group $ResourceGroupName `
            --location $Location `
            --kind OpenAI `
            --sku S0 `
            --custom-domain $OpenAIAccountName `
            --output none
        
        Write-Log "✓ Azure OpenAI account created: $OpenAIAccountName" "INFO"
        Write-Log "  Endpoint: https://$OpenAIAccountName.openai.azure.com/" "INFO"
        Write-Log "  Note: You'll need to deploy models manually in Azure Portal" "INFO"
        
    } catch {
        Write-Log "✗ Failed to create Azure OpenAI account: $_" "ERROR"
        Write-Log "  This might be due to regional availability or quota limits" "WARNING"
        Write-Log "  You can create this manually later in the Azure Portal" "WARNING"
    }
}

function New-AIFoundryProject {
    if ($SkipAI) {
        Write-Log "Skipping Azure AI Foundry Project creation" "INFO"
        return
    }
    
    Write-Log "Creating Azure AI Foundry Project: $AIProjectName" "INFO"
    
    try {
        # Check if AI project exists (requires ML extension)
        $extensionInstalled = az extension show --name ml --query "name" -o tsv 2>$null
        if (-not $extensionInstalled) {
            Write-Log "Installing Azure ML CLI extension..." "INFO"
            az extension add --name ml --output none
        }
        
        # Check if workspace/project exists
        $existingProject = az ml workspace show --name $AIProjectName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null
        if ($existingProject) {
            Write-Log "✓ Azure AI Project already exists: $AIProjectName" "INFO"
            return
        }
        
        # Create Azure AI Foundry Project (ML Workspace)
        az ml workspace create `
            --name $AIProjectName `
            --resource-group $ResourceGroupName `
            --location $Location `
            --output none
        
        Write-Log "✓ Azure AI Foundry Project created: $AIProjectName" "INFO"
        
    } catch {
        Write-Log "✗ Failed to create Azure AI Foundry Project: $_" "ERROR"
        Write-Log "  You can create this manually later in Azure Portal" "WARNING"
    }
}

function New-ContainerAppsEnvironment {
    Write-Log "Creating Container Apps Environment: $ContainerAppEnvName" "INFO"
    
    try {
        # Check if environment exists
        $existingEnv = az containerapp env show --name $ContainerAppEnvName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null
        if ($existingEnv) {
            Write-Log "✓ Container Apps Environment already exists: $ContainerAppEnvName" "INFO"
            return
        }
        
        # Get Application Insights instrumentation key for environment
        $appInsightsKey = az monitor app-insights component show --app $AppInsightsName --resource-group $ResourceGroupName --query "instrumentationKey" -o tsv 2>$null
        
        $createArgs = @(
            "containerapp", "env", "create",
            "--name", $ContainerAppEnvName,
            "--resource-group", $ResourceGroupName,
            "--location", $Location,
            "--output", "none"
        )
        
        if ($appInsightsKey) {
            $createArgs += "--instrumentation-key"
            $createArgs += $appInsightsKey
            Write-Log "  Configuring with Application Insights monitoring" "INFO"
        }
        
        az @createArgs
        Write-Log "✓ Container Apps Environment created: $ContainerAppEnvName" "INFO"
        
    } catch {
        Write-Log "✗ Failed to create Container Apps Environment: $_" "ERROR"
        throw
    }
}

function New-ContainerApp {
    Write-Log "Creating Container App: $ContainerAppName" "INFO"
    
    try {
        # Check if container app exists
        $existingApp = az containerapp show --name $ContainerAppName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null
        if ($existingApp) {
            Write-Log "✓ Container App already exists: $ContainerAppName" "INFO"
            
            # Ensure managed identity is enabled
            Write-Log "Ensuring system-assigned managed identity is enabled..." "INFO"
            az containerapp identity assign --name $ContainerAppName --resource-group $ResourceGroupName --system-assigned --output none
            Write-Log "✓ Managed identity configured" "INFO"
            return
        }
        
        # Create initial container app with placeholder image
        az containerapp create `
            --name $ContainerAppName `
            --resource-group $ResourceGroupName `
            --environment $ContainerAppEnvName `
            --image mcr.microsoft.com/k8se/quickstart:latest `
            --target-port 80 `
            --ingress external `
            --min-replicas 1 `
            --max-replicas 3 `
            --cpu 1.0 `
            --memory 2Gi `
            --system-assigned `
            --output none
        
        Write-Log "✓ Container App created: $ContainerAppName" "INFO"
        Write-Log "  Note: Created with placeholder image - update with your application image" "INFO"
        
        # Get the managed identity principal ID
        Start-Sleep -Seconds 10  # Wait for identity creation
        $principalId = az containerapp show --name $ContainerAppName --resource-group $ResourceGroupName --query "identity.principalId" -o tsv
        if ($principalId) {
            Write-Log "✓ Managed Identity Principal ID: $principalId" "INFO"
        } else {
            Write-Log "⚠ Managed Identity not yet available - may need a few moments" "WARNING"
        }
        
    } catch {
        Write-Log "✗ Failed to create Container App: $_" "ERROR"
        throw
    }
}

function Show-DeploymentSummary {
    Write-Host "`n" -NoNewline
    Write-Log "🎉 AZURE RESOURCES PROVISIONING COMPLETED!" "INFO"
    Write-Host "================================================================" -ForegroundColor Yellow
    
    Write-Host "`n📋 RESOURCE SUMMARY:" -ForegroundColor Cyan
    Write-Host "✅ Resource Group: $ResourceGroupName" -ForegroundColor Green
    Write-Host "✅ Container App: $ContainerAppName" -ForegroundColor Green
    Write-Host "✅ Container Environment: $ContainerAppEnvName" -ForegroundColor Green
    Write-Host "✅ Container Registry: $RegistryName.azurecr.io" -ForegroundColor Green
    Write-Host "✅ Storage Account: $StorageAccountName" -ForegroundColor Green
    Write-Host "✅ Application Insights: $AppInsightsName" -ForegroundColor Green
    
    if (-not $SkipAI) {
        Write-Host "✅ Azure OpenAI: $OpenAIAccountName" -ForegroundColor Green
        Write-Host "✅ AI Foundry Project: $AIProjectName" -ForegroundColor Green
    } else {
        Write-Host "⏭️  Azure AI services skipped (use --SkipAI flag)" -ForegroundColor Yellow
    }
    
    Write-Host "`n🔧 NEXT STEPS:" -ForegroundColor Cyan
    Write-Host "1. Run permission setup script:" -ForegroundColor White
    Write-Host "   .\setup-managed-identity-permissions.ps1 -ResourceGroupName '$ResourceGroupName' -ContainerAppName '$ContainerAppName'" -ForegroundColor Gray
    
    Write-Host "`n2. Build and deploy your application:" -ForegroundColor White
    Write-Host "   .\deploy-bulletproof.ps1 -Environment '$Environment'" -ForegroundColor Gray
    
    Write-Host "`n3. Configure application settings in appsettings.json with:" -ForegroundColor White
    Write-Host "   - Storage Account: $StorageAccountName" -ForegroundColor Gray
    Write-Host "   - Container Registry: $RegistryName" -ForegroundColor Gray
    if (-not $SkipAI) {
        Write-Host "   - Azure OpenAI: $OpenAIAccountName" -ForegroundColor Gray
        Write-Host "   - AI Project: $AIProjectName" -ForegroundColor Gray
    }
    
    # Get container app URL if available
    $appUrl = az containerapp show --name $ContainerAppName --resource-group $ResourceGroupName --query "properties.configuration.ingress.fqdn" -o tsv 2>$null
    if ($appUrl) {
        Write-Host "`n🌐 Container App URL: https://$appUrl" -ForegroundColor Cyan
    }
    
    Write-Host "`n🔍 VERIFICATION COMMANDS:" -ForegroundColor Cyan
    Write-Host "Check container app status:" -ForegroundColor White
    Write-Host "az containerapp show --name $ContainerAppName --resource-group $ResourceGroupName --query properties.runningStatus" -ForegroundColor Gray
    
    Write-Host "`nGet managed identity principal ID:" -ForegroundColor White
    Write-Host "az containerapp show --name $ContainerAppName --resource-group $ResourceGroupName --query identity.principalId" -ForegroundColor Gray
    
    Write-Host "`n✅ All resources provisioned successfully!" -ForegroundColor Green
}

# Main execution
try {
    Write-Log "🚀 Starting Azure Resource Provisioning for SXG Evaluation Platform" "INFO"
    Write-Log "Environment: $Environment | Location: $Location" "INFO"
    Write-Log "Resource Group: $ResourceGroupName" "INFO"
    
    # Step 1: Prerequisites
    Test-Prerequisites
    
    # Step 2: Create Resource Group
    New-ResourceGroup
    
    # Step 3: Create Application Insights (needed for Container Environment)
    New-ApplicationInsights
    
    # Step 4: Create Container Apps Environment
    New-ContainerAppsEnvironment
    
    # Step 5: Create Container Registry
    New-ContainerRegistry
    
    # Step 6: Create Storage Account
    New-StorageAccount
    
    # Step 7: Create Azure AI services (if not skipped)
    New-CognitiveServices
    New-AIFoundryProject
    
    # Step 8: Create Container App
    New-ContainerApp
    
    # Step 9: Show summary
    Show-DeploymentSummary
    
} catch {
    Write-Log "💥 Provisioning failed: $_" "ERROR"
    Write-Host "`nTroubleshooting:" -ForegroundColor Yellow
    Write-Host "- Check Azure CLI login: az login" -ForegroundColor Gray
    Write-Host "- Verify subscription permissions" -ForegroundColor Gray
    Write-Host "- Check resource name availability" -ForegroundColor Gray
    Write-Host "- Verify Azure quotas and regional availability" -ForegroundColor Gray
    exit 1
}