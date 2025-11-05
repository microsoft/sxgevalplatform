# Validate deployment configuration script
param(
    [string]$ParametersFile = "deployment\container-app-parameters.json"
)

Write-Host "🔍 Validating deployment configuration..." -ForegroundColor Blue

$validationErrors = @()
$warnings = @()

# Check if parameters file exists
if (-not (Test-Path $ParametersFile)) {
    $validationErrors += "Parameters file not found: $ParametersFile"
}
else {
    # Read and parse parameters file
    try {
        $parametersContent = Get-Content $ParametersFile -Raw | ConvertFrom-Json
        
        # Check for placeholder values
        $parameters = $parametersContent.parameters
        
        if ($parameters.azureStorageAccountName.value -eq "REPLACE_WITH_STORAGE_ACCOUNT_NAME") {
            $validationErrors += "Azure Storage account name contains placeholder value"
        }
        
        if ($parameters.evaluationApiBaseUrl.value -eq "REPLACE_WITH_ACTUAL_API_BASE_URL") {
            $validationErrors += "Evaluation API base URL contains placeholder value"
        }
        
        if ($parameters.azureTenantId.value -eq "REPLACE_WITH_TENANT_ID") {
            $validationErrors += "Azure Tenant ID contains placeholder value"
        }
        
        if ($parameters.azureSubscriptionId.value -eq "REPLACE_WITH_SUBSCRIPTION_ID") {
            $validationErrors += "Azure Subscription ID contains placeholder value"
        }
        
        # Validate storage account name format
        if ($parameters.azureStorageAccountName.value -match "[^a-z0-9]") {
            $warnings += "Azure Storage account name should only contain lowercase letters and numbers"
        }
        
        # Validate API URL format
        if ($parameters.evaluationApiBaseUrl.value -notmatch "^https?://") {
            $warnings += "Evaluation API base URL should start with http:// or https://"
        }
        
        # Check Application Insights connection string
        if ($parameters.applicationInsightsConnectionString.value -notmatch "InstrumentationKey=") {
            $warnings += "Application Insights connection string may not be in correct format"
        }
        
        # Validate resource allocations for performance optimizations
        $cpuCores = [double]$parameters.cpuCores.value
        $memoryGi = [double]($parameters.memorySize.value -replace "Gi", "")
        
        if ($cpuCores -lt 2.0) {
            $warnings += "CPU allocation ($($parameters.cpuCores.value)) may be insufficient for optimized concurrent processing. Recommended: 2.0 or higher"
        }
        
        if ($memoryGi -lt 4.0) {
            $warnings += "Memory allocation ($($parameters.memorySize.value)) may be insufficient for optimized concurrent processing. Recommended: 4.0Gi or higher"
        }
        
        Write-Host "✅ Parameters file structure is valid" -ForegroundColor Green
        Write-Host "✅ Performance optimization settings validated" -ForegroundColor Green
        
    }
    catch {
        $validationErrors += "Failed to parse parameters file: $($_.Exception.Message)"
    }
}

# Check if required files exist
$requiredFiles = @(
    "deployment\container-app-template.json",
    "Dockerfile",
    "requirements.txt",
    "src\main.py"
)

foreach ($file in $requiredFiles) {
    if (-not (Test-Path $file)) {
        $validationErrors += "Required file missing: $file"
    }
}

# Check Azure CLI
try {
    $null = Get-Command az -ErrorAction Stop
    Write-Host "✅ Azure CLI is available" -ForegroundColor Green
}
catch {
    $validationErrors += "Azure CLI is not installed or not in PATH"
}

# Docker not required - using ACR build
Write-Host "✅ Using Azure Container Registry build (Docker not required)" -ForegroundColor Green

# Check Azure login status
try {
    $null = az account show 2>&1 | Out-Null
    Write-Host "✅ Logged into Azure" -ForegroundColor Green
}
catch {
    $warnings += "Not logged into Azure - run 'az login' before deploying"
}

# Display results
Write-Host "`n📊 Validation Results:" -ForegroundColor Blue

if ($validationErrors.Count -eq 0) {
    Write-Host "✅ No critical errors found!" -ForegroundColor Green
}
else {
    Write-Host "❌ Critical errors found:" -ForegroundColor Red
    foreach ($errorMsg in $validationErrors) {
        Write-Host "   • $errorMsg" -ForegroundColor Red
    }
}

if ($warnings.Count -gt 0) {
    Write-Host "`n⚠️  Warnings:" -ForegroundColor Yellow
    foreach ($warningMsg in $warnings) {
        Write-Host "   • $warningMsg" -ForegroundColor Yellow
    }
}

if ($validationErrors.Count -eq 0) {
    Write-Host "`n🚀 Ready to deploy! Run: .\deploy.ps1" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "`n🛑 Fix errors before deploying" -ForegroundColor Red
    exit 1
}
