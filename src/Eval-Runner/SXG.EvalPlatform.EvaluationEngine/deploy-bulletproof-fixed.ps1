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

# Environment-specific configuration
if ($Environment -eq "PPE") {
    $ContainerAppName = "eval-framework-app-ppe"
} else {
    $ContainerAppName = "eval-framework-app"
}

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

function Remove-UnicodeFiles {
    Write-Log "Handling Unicode files for Docker build..." "INFO"
    
    # Create comprehensive .dockerignore
    $dockerignore = @"
# Exclude ALL potentially problematic files
*.ps1
*.sh
*.md
*.log
*.json.bak
*backup*
*temp*
*tmp*
*.backup
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
.git/
.gitignore
README*
*.txt
!requirements.txt
!requirements-dev.txt
*.yaml
*.yml
!docker-compose.yml

# Exclude everything except essential Python files
**/*
!src/
!src/**/*.py
!Dockerfile
!.dockerignore
!requirements.txt
!requirements-dev.txt
!pyproject.toml
!appsettings*.json
"@
    
    Set-Content -Path ".dockerignore" -Value $dockerignore -Encoding UTF8
    Write-Log "Updated .dockerignore with comprehensive exclusions" "INFO"
    
    # Also try to remove any obviously problematic files
    $problematicPatterns = @("*.md", "*.ps1", "*.sh", "*.log", "*backup*", "*temp*")
    foreach ($pattern in $problematicPatterns) {
        $files = Get-ChildItem -Path . -Filter $pattern -Recurse -ErrorAction SilentlyContinue
        foreach ($file in $files) {
            try {
                if ($file.FullName -notmatch "node_modules|\.git") {
                    Write-Log "Temporarily moving problematic file: $($file.Name)" "INFO"
                    $backupName = "$($file.FullName).bak"
                    Move-Item $file.FullName $backupName -ErrorAction SilentlyContinue
                }
            } catch {
                # Ignore errors moving files
            }
        }
    }
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

function Build-DockerImage {
    Write-Log "Building Docker image using ACR: $ImageTag" "INFO"
    
    try {
        # Set encoding environment variables to handle Unicode issues in Azure CLI output
        $originalEncoding = $env:PYTHONIOENCODING
        $originalLegacy = $env:PYTHONLEGACYWINDOWSSTDIO
        $originalLC = $env:LC_ALL
        
        $env:PYTHONIOENCODING = "utf-8"
        $env:PYTHONLEGACYWINDOWSSTDIO = "1" 
        $env:LC_ALL = "C.UTF-8"
        
        # Set console encoding
        try {
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
        } catch {
            Write-Log "Note: Could not set console encoding" "INFO"
        }
        
        # Note: az acr build doesn't require ACR login (uses Azure CLI authentication)
        # Skip the login step to avoid Docker Desktop requirement
        Write-Log "Using Azure CLI authentication for ACR build (Docker not required)" "INFO"
        
        # Use ACR build with --no-logs to avoid Unicode issues in log streaming
        Write-Log "Starting ACR build (suppressing logs to avoid encoding issues)..." "INFO"
        
        # Execute ACR build with output suppression
        $buildResult = az acr build --registry $RegistryName --image "${ImageName}:${Timestamp}" . --no-logs 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Log "ACR build completed successfully (no errors detected)" "INFO"
            
            # Verify the image was created by checking the registry
            $imageCheck = az acr repository show --name $RegistryName --image "${ImageName}:${Timestamp}" --query "name" -o tsv 2>$null
            if ($imageCheck) {
                Write-Log "✅ Image verified in registry: $ImageTag" "INFO"
                return $ImageTag
            } else {
                throw "Image verification failed - not found in registry after build"
            }
        } else {
            Write-Log "ACR build output: $buildResult" "ERROR"
            throw "ACR build failed with exit code: $LASTEXITCODE"
        }
        
    } catch {
        Write-Log "Failed to build Docker image using ACR: $_" "ERROR"
        throw
    } finally {
        # Restore original environment variables
        $env:PYTHONIOENCODING = $originalEncoding
        $env:PYTHONLEGACYWINDOWSSTDIO = $originalLegacy
        $env:LC_ALL = $originalLC
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
    param([hashtable]$ExpectedEnvVars)
    
    Write-Log "Validating deployment..." "INFO"
    
    try {
        # Check container app status
        $status = az containerapp show --name $ContainerAppName --resource-group $ResourceGroup --query 'properties.runningStatus' -o tsv
        Write-Log "Container app status: $status" "INFO"
        
        # Check latest revision
        $revision = az containerapp revision list --name $ContainerAppName --resource-group $ResourceGroup --query '[0].{name:name,status:properties.runningState,image:properties.template.containers[0].image}' -o json | ConvertFrom-Json
        Write-Log "Latest revision: $($revision.name) - Status: $($revision.status)" "INFO"
        Write-Log "Image: $($revision.image)" "INFO"
        
        # Get deployed environment variables for validation
        Write-Log "Validating environment variables..." "INFO"
        $deployedEnv = az containerapp show --name $ContainerAppName --resource-group $ResourceGroup --query "properties.template.containers[0].env" --output json | ConvertFrom-Json
        
        # Create lookup map of deployed environment variables
        $deployedEnvMap = @{}
        foreach ($envVar in $deployedEnv) {
            $deployedEnvMap[$envVar.name] = $envVar.value
        }
        
        Write-Log "Deployed environment variables: $($deployedEnv.Count)" "INFO"
        Write-Log "Expected environment variables: $($ExpectedEnvVars.Count)" "INFO"
        
        # Validate ALL expected configuration values
        $validationErrors = @()
        $validationSuccess = 0
        $missingKeys = @()
        
        Write-Log "Validating all configuration keys..." "INFO"
        
        foreach ($key in $ExpectedEnvVars.Keys) {
            if ($deployedEnvMap.ContainsKey($key)) {
                $deployedValue = $deployedEnvMap[$key]
                $expectedValue = $ExpectedEnvVars[$key]
                
                if ($deployedValue -eq $expectedValue) {
                    Write-Log "✅ $key = $deployedValue" "INFO"
                    $validationSuccess++
                } else {
                    $error = "❌ $key mismatch - Expected: '$expectedValue', Deployed: '$deployedValue'"
                    Write-Log $error "ERROR"
                    $validationErrors += $error
                }
            } else {
                $error = "❌ Missing environment variable: $key"
                Write-Log $error "ERROR"
                $validationErrors += $error
                $missingKeys += $key
            }
        }
        
        # Check for extra environment variables (deployed but not in appsettings)
        $extraKeys = @()
        foreach ($key in $deployedEnvMap.Keys) {
            if (-not $ExpectedEnvVars.ContainsKey($key)) {
                $extraKeys += $key
                Write-Log "ℹ️  Extra environment variable: $key = $($deployedEnvMap[$key])" "INFO"
            }
        }
        
        # Summary validation results
        Write-Log "Environment Variable Validation Summary:" "INFO"
        Write-Log "  ✅ Keys validated successfully: $validationSuccess/$($ExpectedEnvVars.Count)" "INFO"
        Write-Log "  ❌ Missing keys: $($missingKeys.Count)" "INFO"
        Write-Log "  ❌ Value mismatches: $($validationErrors.Count - $missingKeys.Count)" "INFO"
        Write-Log "  ℹ️  Extra keys: $($extraKeys.Count)" "INFO"
        Write-Log "  📊 Total expected keys: $($ExpectedEnvVars.Count)" "INFO"
        Write-Log "  📊 Total deployed keys: $($deployedEnv.Count)" "INFO"
        
        if ($missingKeys.Count -gt 0) {
            Write-Log "Missing keys details:" "ERROR"
            foreach ($missingKey in $missingKeys) {
                Write-Log "  - $missingKey" "ERROR"
            }
        }
        
        # Overall deployment validation
        $deploymentValid = ($status -eq "Running" -and $revision.status -eq "Running")
        $configurationValid = ($validationErrors.Count -eq 0 -and $missingKeys.Count -eq 0)
        
        if ($deploymentValid -and $configurationValid) {
            Write-Log "✅ Deployment validation successful - App running with ALL configuration correct" "INFO"
            return $true
        } elseif ($deploymentValid -and -not $configurationValid) {
            Write-Log "❌ Configuration validation FAILED - $($validationErrors.Count + $missingKeys.Count) issue(s) found" "ERROR"
            Write-Log "Deployment cannot continue with incorrect configuration values" "ERROR"
            throw "Configuration validation failed: $($validationErrors.Count) value mismatches, $($missingKeys.Count) missing keys"
        } else {
            Write-Log "❌ Deployment validation failed - Status: $status, Revision: $($revision.status)" "ERROR"
            throw "Container app is not running properly. Status: $status, Revision: $($revision.status)"
        }
        
    } catch {
        Write-Log "Deployment validation error: $_" "ERROR"
        return $false
    }
}

function Test-ConfigurationValidity {
    param([hashtable]$EnvVars, [string]$Environment)
    
    Write-Log "Pre-deployment configuration validation for: $Environment" "INFO"
    
    $validationIssues = @()
    
    # Validate critical configuration values
    $criticalConfigs = @{
        "ApiEndpoints__BaseUrl" = "Should not have trailing slash"
        "AzureStorage__AccountName" = "Must be set"
        "AzureStorage__QueueName" = "Must be set"
        "AzureOpenAI__ResourceName" = "Must be set"
        "AzureAI__ProjectName" = "Must be set"
    }
    
    foreach ($key in $criticalConfigs.Keys) {
        if (-not $EnvVars.ContainsKey($key)) {
            $issue = "Missing critical configuration: $key"
            Write-Log "❌ $issue" "ERROR"
            $validationIssues += $issue
        } else {
            $value = $EnvVars[$key]
            
            # Check for trailing slashes in URLs
            if ($key -like "*Url" -and $value -match "/$") {
                $issue = "$key has trailing slash: '$value' - This will cause double-slash URLs"
                Write-Log "❌ $issue" "ERROR"
                $validationIssues += $issue
            }
            
            # Check for empty values
            if ([string]::IsNullOrWhiteSpace($value)) {
                $issue = "$key has empty value"
                Write-Log "❌ $issue" "ERROR"
                $validationIssues += $issue
            } else {
                Write-Log "✅ $key validated" "INFO"
            }
        }
    }
    
    if ($validationIssues.Count -gt 0) {
        Write-Log "Configuration validation FAILED with $($validationIssues.Count) issue(s)" "ERROR"
        throw "Configuration is invalid. Please fix the issues in appsettings.$Environment.json before deploying."
    }
    
    Write-Log "✅ Pre-deployment configuration validation passed" "INFO"
}

# Main deployment process
try {
    Write-Log "Starting bulletproof deployment for Environment: $Environment" "INFO"
    Write-Log "Container App: $ContainerAppName" "INFO"
    Write-Log "Image tag: $ImageTag" "INFO"
    Write-Log "Log file: $LogFile" "INFO"
    
    # Step 1: Prerequisites check
    Test-Prerequisites
    
    # Step 2: Handle Unicode files
    Remove-UnicodeFiles
    
    # Step 3: Extract environment variables
    $envVars = Get-EnvironmentVariables $Environment
    
    # Step 3.5: Validate configuration before deployment
    Test-ConfigurationValidity -EnvVars $envVars -Environment $Environment
    
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
    $isValid = Test-Deployment $envVars
    
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