# Environment Configuration Validation Script
# Validates all environment-specific appsettings files

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Local", "Development", "PPE", "Production", "All")]
    [string]$Environment = "All"
)

function Test-JsonFile {
    param(
        [string]$FilePath,
        [string]$EnvironmentName
    )
    
    Write-Host "🔍 Validating $EnvironmentName configuration: $FilePath" -ForegroundColor Cyan
    
    # Check if file exists
    if (-not (Test-Path $FilePath)) {
        Write-Host "   ❌ File not found: $FilePath" -ForegroundColor Red
        return $false
    }
    
    try {
        # Test JSON syntax
        $config = Get-Content $FilePath -Raw | ConvertFrom-Json
        Write-Host "   ✅ JSON syntax is valid" -ForegroundColor Green
        
        # Validate required sections
        $requiredSections = @("AzureStorage", "ApiEndpoints", "AzureOpenAI", "AzureAI", "Evaluation", "ApplicationInsights", "Logging")
        $valid = $true
        
        foreach ($section in $requiredSections) {
            if ($config.PSObject.Properties.Name -contains $section) {
                Write-Host "   ✅ Section '$section' found" -ForegroundColor Green
            } else {
                Write-Host "   ❌ Missing required section: $section" -ForegroundColor Red
                $valid = $false
            }
        }
        
        # Validate specific properties
        Write-Host "   📋 Configuration summary:"
        
        # Azure Storage validation
        if ($config.AzureStorage) {
            Write-Host "      Storage Account: $($config.AzureStorage.AccountName)"
            Write-Host "      Queue Name: $($config.AzureStorage.QueueName)"
            Write-Host "      Managed Identity: $($config.AzureStorage.UseManagedIdentity)"
        }
        
        # API Endpoints validation
        if ($config.ApiEndpoints) {
            Write-Host "      Base URL: $($config.ApiEndpoints.BaseUrl)"
        }
        
        # Azure OpenAI validation
        if ($config.AzureOpenAI) {
            Write-Host "      OpenAI Resource: $($config.AzureOpenAI.ResourceName)"
            Write-Host "      Deployment: $($config.AzureOpenAI.DeploymentName)"
        }
        
        # Performance settings validation
        if ($config.Evaluation) {
            Write-Host "      Max Parallel Prompts: $($config.Evaluation.MaxParallelPrompts)"
            Write-Host "      Max Parallel Metrics: $($config.Evaluation.MaxParallelMetrics)"
        }
        
        return $valid
        
    } catch {
        Write-Host "   ❌ JSON parsing failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Test-EnvironmentConfiguration {
    param([string]$EnvName)
    
    $filePath = "appsettings.$EnvName.json"
    $isValid = Test-JsonFile -FilePath $filePath -EnvironmentName $EnvName
    
    if ($isValid) {
        Write-Host "🎉 $EnvName configuration is valid!" -ForegroundColor Green
    } else {
        Write-Host "💥 $EnvName configuration has issues!" -ForegroundColor Red
    }
    
    Write-Host ""
    return $isValid
}

# Main validation logic
Write-Host "🔧 SXG Evaluation Engine - Environment Configuration Validation" -ForegroundColor Magenta
Write-Host "=============================================================" -ForegroundColor Magenta
Write-Host ""

$allValid = $true

if ($Environment -eq "All") {
    $environments = @("Local", "Development", "PPE", "Production")
    
    foreach ($env in $environments) {
        $envValid = Test-EnvironmentConfiguration -EnvName $env
        if (-not $envValid) { $allValid = $false }
    }
    
} else {
    $allValid = Test-EnvironmentConfiguration -EnvName $Environment
}

# Summary
Write-Host "=============================================================" -ForegroundColor Magenta
if ($allValid) {
    Write-Host "🎉 All validations passed! Configurations are ready for deployment." -ForegroundColor Green
} else {
    Write-Host "💥 Some validations failed. Please fix the issues before deployment." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "🚀 Ready to deploy? Use: .\deploy-environment.ps1 -Environment <env-name>" -ForegroundColor Green