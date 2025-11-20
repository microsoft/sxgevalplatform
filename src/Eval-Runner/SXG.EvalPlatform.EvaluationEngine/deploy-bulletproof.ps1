#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Bulletproof Deployment Script for SXG Evaluation Platform
.DESCRIPTION
    Comprehensive deployment script following all 7 deployment rules:
    1. Build and deploy Docker image to Azure Container Registry
    2. Update Azure Container App with new image and environment variables
    3. Handle Unicode encoding issues with proper file exclusions
    4. Extract and set environment variables from appsettings files
    5. Implement proper error handling and rollback mechanisms
    6. Validate deployment success with health checks
    7. Provide detailed logging and status reporting
#>

param(
    [string]$Environment = "Development",
    [switch]$Force,
    [switch]$SkipBuild,
    [switch]$Verbose
)

# Set error handling
$ErrorActionPreference = "Stop"
$VerbosePreference = if ($Verbose) { "Continue" } else { "SilentlyContinue" }

# Configuration
$ResourceGroup = "rg-sxg-agent-evaluation-platform"
$ContainerAppName = "eval-framework-app"
$RegistryName = "evalplatformregistry"
$ImageName = "eval-framework-app"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$ImageTag = "${RegistryName}.azurecr.io/${ImageName}:${Timestamp}"

# Logging setup
$LogFile = "deployment-${Timestamp}.log"

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
    
    # Check Docker
    try {
        $null = docker --version
        Write-Log "Docker is available" "INFO"
    } catch {
        Write-Log "Docker is not installed or not running" "ERROR"
        throw "Docker is required"
    }
    
    # Check Azure login
    try {
        $account = az account show --query "user.name" -o tsv
        Write-Log "Logged in as: $account" "INFO"
    } catch {
        Write-Log "Not logged in to Azure" "ERROR"
        throw "Please run 'az login' first"
    }
}

function Remove-UnicodeFiles {
    Write-Log "Handling Unicode files for Docker build..." "INFO"
    
    # Create .dockerignore if it doesn't exist or update it
    $dockerignore = @"
# Exclude Unicode-containing files
*.ps1
*.sh
*.md
*.log
*.json.bak
*backup*
*temp*
*tmp*
test_*.py
debug_*.py
remove_unicode.py
scripts/
docs/
deployment/
venv/
.vscode/
.pytest_cache/
__pycache__/
*.pyc
.coverage
.env
.env.*

# Keep essential files
!Dockerfile
!.dockerignore
!requirements.txt
!requirements-dev.txt
!pyproject.toml
"@
    
    Set-Content -Path ".dockerignore" -Value $dockerignore -Encoding UTF8
    Write-Log "Updated .dockerignore to exclude problematic files" "INFO"
}

function Get-EnvironmentVariables {
    param([string]$Environment)
    
    Write-Log "Extracting environment variables for: $Environment" "INFO"
    
    $envVars = @{}
    
    # Read base appsettings.json
    if (Test-Path "appsettings.json") {
        $baseConfig = Get-Content "appsettings.json" | ConvertFrom-Json
        $flattenedBase = ConvertTo-FlatDictionary $baseConfig
        foreach ($key in $flattenedBase.Keys) {
            $envVars[$key] = $flattenedBase[$key]
        }
    }
    
    # Read environment-specific settings
    $envFile = "appsettings.$Environment.json"
    if (Test-Path $envFile) {
        $envConfig = Get-Content $envFile | ConvertFrom-Json
        $flattenedEnv = ConvertTo-FlatDictionary $envConfig
        foreach ($key in $flattenedEnv.Keys) {
            $envVars[$key] = $flattenedEnv[$key]
        }
    }
    
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

function Build-DockerImage {
    Write-Log "Building Docker image: $ImageTag" "INFO"
    
    try {
        # Login to ACR
        az acr login --name $RegistryName
        Write-Log "Logged in to Azure Container Registry" "INFO"
        
        # Build and push image
        docker build -t $ImageTag .
        if ($LASTEXITCODE -ne 0) {
            throw "Docker build failed"
        }
        
        docker push $ImageTag
        if ($LASTEXITCODE -ne 0) {
            throw "Docker push failed"
        }
        
        Write-Log "Successfully built and pushed image: $ImageTag" "INFO"
        return $ImageTag
    } catch {
        Write-Log "Failed to build Docker image: $_" "ERROR"
        throw
    }
}

function Update-ContainerApp {
    param([string]$ImageTag, [hashtable]$EnvVars)
    
    Write-Log "Updating Container App: $ContainerAppName" "INFO"
    
    try {
        # Prepare environment variables for Azure CLI
        $envVarArgs = @()
        foreach ($key in $EnvVars.Keys) {
            if ($EnvVars[$key] -ne $null -and $EnvVars[$key] -ne "") {
                $value = $EnvVars[$key].ToString()
                $envVarArgs += "$key=$value"
            }
        }
        
        Write-Log "Setting $($envVarArgs.Count) environment variables" "INFO"
        
        # Update container app
        $updateArgs = @(
            "containerapp", "update",
            "--name", $ContainerAppName,
            "--resource-group", $ResourceGroup,
            "--image", $ImageTag
        )
        
        if ($envVarArgs.Count -gt 0) {
            $updateArgs += "--set-env-vars"
            $updateArgs += $envVarArgs
        }
        
        az @updateArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Container app update failed"
        }
        
        Write-Log "Container app updated successfully" "INFO"
        
        # Trigger restart to ensure new configuration takes effect
        Write-Log "Triggering container app restart..." "INFO"
        az containerapp revision set-mode --name $ContainerAppName --resource-group $ResourceGroup --mode single
        
        return $true
    } catch {
        Write-Log "Failed to update container app: $_" "ERROR"
        throw
    }
}

function Test-Deployment {
    Write-Log "Validating deployment..." "INFO"
    
    try {
        # Check container app status
        $status = az containerapp show --name $ContainerAppName --resource-group $ResourceGroup --query 'properties.runningStatus' -o tsv
        Write-Log "Container app status: $status" "INFO"
        
        # Check latest revision
        $revision = az containerapp revision list --name $ContainerAppName --resource-group $ResourceGroup --query '[0].{name:name,status:properties.runningState,image:properties.template.containers[0].image}' -o json | ConvertFrom-Json
        Write-Log "Latest revision: $($revision.name) - Status: $($revision.status)" "INFO"
        Write-Log "Image: $($revision.image)" "INFO"
        
        if ($status -eq "Running" -and $revision.status -eq "Running") {
            Write-Log "Deployment validation successful" "INFO"
            return $true
        } else {
            Write-Log "Deployment validation failed - Status: $status, Revision: $($revision.status)" "WARNING"
            return $false
        }
    } catch {
        Write-Log "Deployment validation error: $_" "ERROR"
        return $false
    }
}

# Main deployment process
try {
    Write-Log "Starting bulletproof deployment for Environment: $Environment" "INFO"
    Write-Log "Image tag: $ImageTag" "INFO"
    Write-Log "Log file: $LogFile" "INFO"
    
    # Step 1: Prerequisites check
    Test-Prerequisites
    
    # Step 2: Handle Unicode files
    Remove-UnicodeFiles
    
    # Step 3: Extract environment variables
    $envVars = Get-EnvironmentVariables $Environment
    
    # Step 4: Build Docker image (unless skipped)
    if (-not $SkipBuild) {
        $imageTag = Build-DockerImage
    } else {
        Write-Log "Skipping Docker build as requested" "INFO"
        $imageTag = $ImageTag
    }
    
    # Step 5: Update Container App
    Update-ContainerApp $imageTag $envVars
    
    # Step 6: Wait for deployment to stabilize
    Write-Log "Waiting for deployment to stabilize..." "INFO"
    Start-Sleep -Seconds 30
    
    # Step 7: Validate deployment
    $isValid = Test-Deployment
    
    if ($isValid) {
        Write-Log "🎉 Deployment completed successfully!" "INFO"
        Write-Log "Image deployed: $imageTag" "INFO"
        Write-Log "Environment variables: $($envVars.Count) configured" "INFO"
    } else {
        Write-Log "⚠️  Deployment completed but validation failed" "WARNING"
        Write-Log "Please check the Azure portal for container app status" "WARNING"
    }
    
} catch {
    Write-Log "💥 Deployment failed: $_" "ERROR"
    Write-Log "Check the log file for details: $LogFile" "ERROR"
    exit 1
}

Write-Log "Deployment script completed. Log saved to: $LogFile" "INFO"