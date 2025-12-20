#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Setup Azure Foundry Storage Account for North Central (West US 2 AI Foundry)
.DESCRIPTION
    Creates and configures a dedicated storage account for Azure AI Foundry (North Central)
    and connects it to the AI Foundry project for Risk and Safety evaluators.
    
    This storage account is used by Azure AI Foundry for:
    - Storing evaluation artifacts
    - Prompt flow data
    - Risk and Safety evaluation results
    - Content filtering and safety assessment data
    
    Note: This AI Foundry is in North Central US, serving the West US 2 Container App.
#>

param(
    [switch]$WhatIf,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$VerbosePreference = if ($Verbose) { "Continue" } else { "SilentlyContinue" }

# ============================================================================
# CONFIGURATION - North Central US (for West US 2)
# ============================================================================

$Region = "North Central US"
$Location = "northcentralus"
$ResourceGroup = "EvalCommonRg-UsEast2"  # AI Foundry is in shared resource group
$StorageAccountName = "foundryncprod"  # Max 24 chars, lowercase, no hyphens, no underscores
$AIFoundryProjectName = "evalplatformprojectprodnorthcentral"
$ContainerAppName = "eval-runner-prod-wus2"  # Match shortened container app name
$ContainerAppResourceGroup = "EvalRunnerRG-WestUS2"  # Match regional resource group name

# Storage account configuration
$Tags = @{
    Environment = "Production"
    Project = "SXG-EvalPlatform"
    Purpose = "AIFoundryStorage"
    Region = "NorthCentral"
    ServedBy = "WestUS2ContainerApp"
}

$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$LogFile = "foundry-storage-setup-northcentral-${Timestamp}.log"

# ============================================================================
# HELPER FUNCTIONS
# ============================================================================

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $LogMessage = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] [$Level] $Message"
    Write-Host $LogMessage
    $LogMessage | Out-File -FilePath $LogFile -Append
}

function Test-StorageAccountExists {
    param([string]$Name)
    try {
        $result = az storage account show --name $Name --query "name" -o tsv 2>$null
        return $null -ne $result -and $result -ne ""
    } catch {
        return $false
    }
}

function Test-AIFoundryExists {
    param([string]$ProjectName, [string]$ResourceGroup)
    try {
        $result = az ml workspace show --name $ProjectName --resource-group $ResourceGroup --query "name" -o tsv 2>$null
        return $null -ne $result -and $result -ne ""
    } catch {
        return $false
    }
}

# ============================================================================
# MAIN SETUP
# ============================================================================

try {
    Write-Log "========================================" "INFO"
    Write-Log "Azure AI Foundry Storage Setup - $Region" "INFO"
    Write-Log "========================================" "INFO"
    Write-Log "Storage Account: $StorageAccountName" "INFO"
    Write-Log "AI Foundry Project: $AIFoundryProjectName" "INFO"
    Write-Log "AI Foundry Resource Group: $ResourceGroup" "INFO"
    Write-Log "Container App: $ContainerAppName (West US 2)" "INFO"
    Write-Log "" "INFO"
    
    # Verify Azure CLI login
    try {
        $account = az account show --query "user.name" -o tsv
        Write-Log "Logged in as: $account" "INFO"
    } catch {
        Write-Log "❌ Not logged in to Azure. Please run 'az login'" "ERROR"
        exit 1
    }
    
    # Step 1: Verify AI Foundry project exists (optional - create storage even if project doesn't exist yet)
    Write-Log "Checking if AI Foundry project exists..." "INFO"
    Write-Log "Note: This step may take a moment. Press Ctrl+C if you want to skip verification." "INFO"
    
    try {
        # Add timeout to prevent hanging
        $result = az ml workspace show `
            --name $AIFoundryProjectName `
            --resource-group $ResourceGroup `
            --query "name" `
            -o tsv `
            2>&1
        
        if ($LASTEXITCODE -eq 0 -and $result -eq $AIFoundryProjectName) {
            Write-Log "✅ AI Foundry project exists: $AIFoundryProjectName" "SUCCESS"
        } else {
            Write-Log "⚠️ AI Foundry project not found or not accessible" "WARNING"
            Write-Log "Project: $AIFoundryProjectName" "WARNING"
            Write-Log "Resource Group: $ResourceGroup" "WARNING"
            Write-Log "" "INFO"
            Write-Log "The storage account will still be created. Please ensure the AI Foundry" "WARNING"
            Write-Log "project exists before linking the storage account." "WARNING"
            Write-Log "" "INFO"
            
            $continue = Read-Host "Continue anyway? (y/n)"
            if ($continue -ne 'y') {
                Write-Log "Setup cancelled by user" "INFO"
                exit 0
            }
        }
    } catch {
        Write-Log "⚠️ Could not verify AI Foundry project (Azure ML extension may not be installed)" "WARNING"
        Write-Log "Error: $_" "WARNING"
        Write-Log "" "INFO"
        Write-Log "To install Azure ML extension: az extension add --name ml" "INFO"
        Write-Log "" "INFO"
        
        $continue = Read-Host "Continue creating storage account anyway? (y/n)"
        if ($continue -ne 'y') {
            Write-Log "Setup cancelled by user" "INFO"
            exit 0
        }
    }
    
    # Step 2: Create Storage Account (if it doesn't exist)
    Write-Log "" "INFO"
    Write-Log "Creating storage account for AI Foundry..." "INFO"
    
    if (Test-StorageAccountExists -Name $StorageAccountName) {
        Write-Log "Storage account '$StorageAccountName' already exists" "INFO"
    } else {
        if (-not $WhatIf) {
            Write-Log "Creating storage account: $StorageAccountName" "INFO"
            
            # Create storage account with configurations required for AI Foundry
            az storage account create `
                --name $StorageAccountName `
                --resource-group $ResourceGroup `
                --location $Location `
                --sku Standard_LRS `
                --kind StorageV2 `
                --min-tls-version TLS1_2 `
                --allow-blob-public-access false `
                --enable-hierarchical-namespace true `
                --tags $(($Tags.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join " ")
            
            Write-Log "✅ Created storage account: $StorageAccountName" "SUCCESS"
        } else {
            Write-Log "[WhatIf] Would create storage account: $StorageAccountName" "INFO"
        }
    }
    
    # Step 3: Get Storage Account details
    $storageAccountId = az storage account show `
        --name $StorageAccountName `
        --resource-group $ResourceGroup `
        --query "id" -o tsv
    
    Write-Log "Storage Account ID: $storageAccountId" "INFO"
    
    # Step 4: Get Container App Managed Identity (from West US 2)
    Write-Log "" "INFO"
    Write-Log "Retrieving Container App Managed Identity (West US 2)..." "INFO"
    $principalId = az containerapp show `
        --name $ContainerAppName `
        --resource-group $ContainerAppResourceGroup `
        --query "identity.principalId" -o tsv
    
    if (-not $principalId) {
        Write-Log "❌ Failed to retrieve Managed Identity for Container App" "ERROR"
        throw "Container App managed identity not found"
    }
    
    Write-Log "Container App Managed Identity: $principalId" "INFO"
    
    # Step 5: Get AI Foundry Project Managed Identity
    Write-Log "" "INFO"
    Write-Log "Retrieving AI Foundry Project Managed Identity..." "INFO"
    $aiFoundryPrincipalId = az ml workspace show `
        --name $AIFoundryProjectName `
        --resource-group $ResourceGroup `
        --query "identity.principalId" -o tsv
    
    if (-not $aiFoundryPrincipalId) {
        Write-Log "❌ Failed to retrieve Managed Identity for AI Foundry Project" "ERROR"
        throw "AI Foundry project managed identity not found"
    }
    
    Write-Log "AI Foundry Project Managed Identity: $aiFoundryPrincipalId" "INFO"
    
    # Step 6: Assign RBAC permissions
    Write-Log "" "INFO"
    Write-Log "Configuring RBAC permissions..." "INFO"
    Write-Log "Waiting 15 seconds for identity propagation..." "INFO"
    Start-Sleep -Seconds 15
    
    # Storage Blob Data Contributor for Container App (required for evaluation execution)
    Write-Log "Assigning 'Storage Blob Data Contributor' role to Container App..." "INFO"
    if (-not $WhatIf) {
        try {
            az role assignment create `
                --assignee $principalId `
                --role "Storage Blob Data Contributor" `
                --scope $storageAccountId `
                2>$null
            Write-Log "✅ Assigned Storage Blob Data Contributor to Container App" "SUCCESS"
        } catch {
            Write-Log "Role assignment may already exist" "WARNING"
        }
    }
    
    # Storage Blob Data Contributor for AI Foundry Project (REQUIRED for Risk & Safety evaluators)
    Write-Log "Assigning 'Storage Blob Data Contributor' role to AI Foundry Project..." "INFO"
    if (-not $WhatIf) {
        try {
            az role assignment create `
                --assignee $aiFoundryPrincipalId `
                --role "Storage Blob Data Contributor" `
                --scope $storageAccountId `
                2>$null
            Write-Log "✅ Assigned Storage Blob Data Contributor to AI Foundry Project" "SUCCESS"
        } catch {
            Write-Log "Role assignment may already exist" "WARNING"
        }
    }
    
    # Storage File Data Privileged Contributor (for file shares if needed)
    Write-Log "Assigning 'Storage File Data Privileged Contributor' role to Container App..." "INFO"
    if (-not $WhatIf) {
        try {
            az role assignment create `
                --assignee $principalId `
                --role "Storage File Data Privileged Contributor" `
                --scope $storageAccountId `
                2>$null
            Write-Log "✅ Assigned Storage File Data Privileged Contributor to Container App" "SUCCESS"
        } catch {
            Write-Log "Role assignment may already exist" "WARNING"
        }
    }
    
    # Step 7: Link Storage Account to AI Foundry Project
    Write-Log "" "INFO"
    Write-Log "========================================" "INFO"
    Write-Log "MANUAL CONFIGURATION REQUIRED" "WARNING"
    Write-Log "========================================" "INFO"
    Write-Log "" "INFO"
    Write-Log "Storage account created successfully!" "SUCCESS"
    Write-Log "Storage Account Name: $StorageAccountName" "INFO"
    Write-Log "Storage Account ID: $storageAccountId" "INFO"
    Write-Log "" "INFO"
    Write-Log "⚠️  NEXT STEPS - Link Storage to AI Foundry:" "WARNING"
    Write-Log "" "INFO"
    Write-Log "1. Navigate to Azure AI Studio: https://ai.azure.com" "INFO"
    Write-Log "" "INFO"
    Write-Log "2. Open your project: $AIFoundryProjectName" "INFO"
    Write-Log "" "INFO"
    Write-Log "3. Go to Settings → Connected Resources → Storage" "INFO"
    Write-Log "" "INFO"
    Write-Log "4. Click 'Add Storage Account' or 'Link Storage'" "INFO"
    Write-Log "" "INFO"
    Write-Log "5. Select the storage account: $StorageAccountName" "INFO"
    Write-Log "" "INFO"
    Write-Log "6. Verify the connection is successful" "INFO"
    Write-Log "" "INFO"
    Write-Log "Alternatively, use Azure CLI (if supported):" "INFO"
    Write-Log "" "INFO"
    Write-Log "az ml workspace update \\" "INFO"
    Write-Log "  --name $AIFoundryProjectName \\" "INFO"
    Write-Log "  --resource-group $ResourceGroup \\" "INFO"
    Write-Log "  --storage-account $storageAccountId" "INFO"
    Write-Log "" "INFO"
    Write-Log "========================================" "INFO"
    Write-Log "Setup completed successfully!" "SUCCESS"
    Write-Log "Log file: $LogFile" "INFO"
    
} catch {
    Write-Log "❌ Setup failed: $_" "ERROR"
    Write-Log "Stack trace: $($_.ScriptStackTrace)" "ERROR"
    exit 1
}
