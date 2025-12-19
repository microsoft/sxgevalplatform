#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Production Deployment Script for West US 2 Region
.DESCRIPTION
    Deploys the evaluation platform to the West US 2 production region
#>

param(
    [switch]$Force,
    [switch]$SkipBuild,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$VerbosePreference = if ($Verbose) { "Continue" } else { "SilentlyContinue" }

# Configuration
$Environment = "Production-WestUS2"
$ResourceGroup = "EvalRunnerRG-WestUS2"
$ContainerAppName = "eval-runner-container-prod-wus2"
$RegistryName = "evalplatformregistryprod"
$ImageName = "eval-runner-app"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$ImageTag = "${RegistryName}.azurecr.io/${ImageName}:prod-westus2-${Timestamp}"

# Logging setup
$LogFile = "deployment-prod-westus2-${Timestamp}.log"

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $LogMessage = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] [$Level] $Message"
    Write-Host $LogMessage
    $LogMessage | Out-File -FilePath $LogFile -Append
}

function Test-Prerequisites {
    Write-Log "Checking prerequisites..." "INFO"
    
    # Check Azure CLI
    try {
        $null = az --version
        Write-Log "Azure CLI is available" "INFO"
    } catch {
        Write-Log "Azure CLI is not installed or not in PATH" "ERROR"
        throw "Azure CLI is required"
    }
    
    # Check Azure login
    try {
        $account = az account show --query "user.name" -o tsv
        Write-Log "Logged in as: $account" "INFO"
    } catch {
        Write-Log "Not logged in to Azure" "ERROR"
        throw "Please run 'az login' first"
    }
    
    # Check ACR access
    try {
        $null = az acr show --name $RegistryName --query "name" -o tsv
        Write-Log "Azure Container Registry '$RegistryName' is accessible" "INFO"
    } catch {
        Write-Log "Cannot access Azure Container Registry '$RegistryName'" "ERROR"
        throw "ACR access required for image builds"
    }
}

function Build-DockerImage {
    Write-Log "Building Docker image..." "INFO"
    
    try {
        # Build and push using ACR build (builds in Azure, no local Docker required)
        Write-Log "Building image in Azure Container Registry..." "INFO"
        az acr build `
            --registry $RegistryName `
            --image "${ImageName}:prod-westus2-${Timestamp}" `
            --image "${ImageName}:prod-westus2-latest" `
            --file Dockerfile `
            --platform linux `
            .
        
        Write-Log "Docker image built successfully: $ImageTag" "INFO"
        return $ImageTag
    } catch {
        Write-Log "Failed to build Docker image: $_" "ERROR"
        throw
    }
}

function Get-EnvironmentVariables {
    param([string]$Environment)
    
    Write-Log "Extracting environment variables for: $Environment" "INFO"
    
    $envVars = @{}
    
    # Read environment-specific settings
    $envFile = "appsettings.$Environment.json"
    if (Test-Path $envFile) {
        $envConfig = Get-Content $envFile | ConvertFrom-Json
        $flattenedEnv = ConvertTo-FlatDictionary $envConfig
        foreach ($key in $flattenedEnv.Keys) {
            $envVars[$key] = $flattenedEnv[$key]
        }
    } else {
        throw "Configuration file not found: $envFile"
    }
    
    # Add runtime environment
    $envVars["RUNTIME_ENVIRONMENT"] = $Environment
    
    Write-Log "Extracted $($envVars.Count) environment variables" "INFO"
    return $envVars
}

function ConvertTo-FlatDictionary {
    param([PSObject]$Object, [string]$Prefix = "")
    
    $result = @{}
    
    foreach ($property in $Object.PSObject.Properties) {
        $key = if ($Prefix) { "${Prefix}__$($property.Name)" } else { $property.Name }
        
        if ($property.Value -is [PSCustomObject]) {
            $nested = ConvertTo-FlatDictionary $property.Value $key
            foreach ($nestedKey in $nested.Keys) {
                $result[$nestedKey] = $nested[$nestedKey]
            }
        } else {
            $result[$key] = $property.Value
        }
    }
    
    return $result
}

function Configure-ACRAuthentication {
    Write-Log "Configuring ACR authentication with managed identity" "INFO"
    
    try {
        az containerapp registry set `
            --name $ContainerAppName `
            --resource-group $ResourceGroup `
            --server "${RegistryName}.azurecr.io" `
            --identity system
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to configure ACR authentication"
        }
        
        Write-Log "ACR authentication configured successfully" "INFO"
    } catch {
        Write-Log "Failed to configure ACR authentication: $_" "ERROR"
        throw
    }
}

function Update-ContainerApp {
    param([string]$ImageTag, [hashtable]$EnvVars)
    
    Write-Log "Updating Container App: $ContainerAppName" "INFO"
    
    try {
        # First, update the image
        Write-Log "Updating container image to: $ImageTag" "INFO"
        az containerapp update `
            --name $ContainerAppName `
            --resource-group $ResourceGroup `
            --image $ImageTag
        
        if ($LASTEXITCODE -ne 0) {
            throw "Container app image update failed"
        }
        
        # Build environment variable arguments for replace-env-vars
        $envArgs = @()
        foreach ($key in $EnvVars.Keys) {
            $value = $EnvVars[$key]
            if ($null -ne $value -and $value -ne "") {
                $envArgs += "${key}=`"${value}`""
            }
        }
        
        Write-Log "Updating with $($envArgs.Count) environment variables" "INFO"
        
        # Update environment variables separately using replace-env-vars
        $envArgsString = $envArgs -join " "
        $command = "az containerapp update --name $ContainerAppName --resource-group $ResourceGroup --replace-env-vars $envArgsString"
        Invoke-Expression $command
        
        if ($LASTEXITCODE -ne 0) {
            throw "Container app environment variables update failed"
        }
        
        Write-Log "Container app updated successfully" "INFO"
        return $true
    } catch {
        Write-Log "Failed to update container app: $_" "ERROR"
        throw
    }
}

# Main deployment process
try {
    Write-Log "========================================" "INFO"
    Write-Log "Starting Production Deployment - West US 2" "INFO"
    Write-Log "========================================" "INFO"
    Write-Log "Container App: $ContainerAppName" "INFO"
    Write-Log "Image tag: $ImageTag" "INFO"
    
    # Step 1: Prerequisites check
    Test-Prerequisites
    
    # Step 2: Extract environment variables
    $envVars = Get-EnvironmentVariables $Environment
    
    # Step 3: Configure ACR Authentication
    Configure-ACRAuthentication
    
    # Step 4: Build Docker image (unless skipped)
    if (-not $SkipBuild) {
        $imageTag = Build-DockerImage
    } else {
        Write-Log "Skipping Docker build as requested" "INFO"
        $imageTag = "${RegistryName}.azurecr.io/${ImageName}:prod-westus2-latest"
    }
    
    # Step 5: Update Container App
    Update-ContainerApp $imageTag $envVars
    
    # Step 6: Wait for deployment to stabilize
    Write-Log "Waiting for deployment to stabilize..." "INFO"
    Start-Sleep -Seconds 30
    
    # Step 7: Check status
    $status = az containerapp show --name $ContainerAppName --resource-group $ResourceGroup --query 'properties.runningStatus' -o tsv
    Write-Log "Container app status: $status" "INFO"
    
    if ($status -eq "Running") {
        Write-Log "🎉 Deployment completed successfully!" "INFO"
        Write-Log "Region: West US 2" "INFO"
        Write-Log "Image deployed: $imageTag" "INFO"
    } else {
        Write-Log "⚠️  Deployment completed but app is not running" "WARNING"
        Write-Log "Please check the Azure portal for details" "WARNING"
    }
    
} catch {
    Write-Log "💥 Deployment failed: $_" "ERROR"
    Write-Log "Check the log file for details: $LogFile" "ERROR"
    exit 1
}

Write-Log "Deployment script completed. Log saved to: $LogFile" "INFO"
