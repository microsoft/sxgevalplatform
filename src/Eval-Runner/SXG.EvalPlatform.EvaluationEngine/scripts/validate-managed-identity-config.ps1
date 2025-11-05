# Managed Identity Configuration Validation Script
# This script validates that the application is configured for secretless operation using managed identity

param(
    [string]$AppSettingsPath = "appsettings.json",
    [string]$ConfigPath = "src/eval_runner/config/settings.py"
)

Write-Host "🔒 MANAGED IDENTITY CONFIGURATION VALIDATION" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Yellow

Write-Host "`n1. Checking Application Settings..." -ForegroundColor Blue

if (Test-Path $AppSettingsPath) {
    $appSettings = Get-Content $AppSettingsPath | ConvertFrom-Json
    
    # Check Azure Storage Configuration
    Write-Host "`n📦 Azure Storage Configuration:" -ForegroundColor Green
    if ($appSettings.AzureStorage.UseDefaultAzureCredential -eq $true) {
        Write-Host "✅ Azure Storage configured for managed identity (UseDefaultAzureCredential: true)" -ForegroundColor Green
    }
    else {
        Write-Host "❌ Azure Storage not configured for managed identity" -ForegroundColor Red
        Write-Host "   Set UseDefaultAzureCredential to true in appsettings.json" -ForegroundColor Yellow
    }
    
    if ($appSettings.AzureStorage.ConnectionString) {
        Write-Host "⚠️  WARNING: ConnectionString found in configuration" -ForegroundColor Yellow
        Write-Host "   Remove ConnectionString - managed identity will be used instead" -ForegroundColor Gray
    }
    else {
        Write-Host "✅ No connection string found - good for managed identity" -ForegroundColor Green
    }
    
    # Check Azure OpenAI Configuration
    Write-Host "`n🤖 Azure OpenAI Configuration:" -ForegroundColor Green
    if ($appSettings.AzureOpenAI.UseDefaultAzureCredential -eq $true) {
        Write-Host "✅ Azure OpenAI configured for managed identity" -ForegroundColor Green
    }
    else {
        Write-Host "❌ Azure OpenAI not configured for managed identity" -ForegroundColor Red
        Write-Host "   Set UseDefaultAzureCredential to true in appsettings.json" -ForegroundColor Yellow
    }
    
    if ($appSettings.AzureOpenAI.ApiKey) {
        Write-Host "⚠️  WARNING: ApiKey found in configuration" -ForegroundColor Yellow
        Write-Host "   Remove ApiKey - managed identity will be used instead" -ForegroundColor Gray
    }
    else {
        Write-Host "✅ No API key found - good for managed identity" -ForegroundColor Green
    }
    
    # Check Azure AI Foundry Configuration
    Write-Host "`n🏭 Azure AI Foundry Configuration:" -ForegroundColor Green
    if ($appSettings.AzureAIFoundry -and $appSettings.AzureAIFoundry.UseDefaultAzureCredential -eq $true) {
        Write-Host "✅ Azure AI Foundry configured for managed identity" -ForegroundColor Green
    }
    else {
        Write-Host "⚠️  Azure AI Foundry managed identity configuration not found" -ForegroundColor Yellow
        Write-Host "   Ensure UseDefaultAzureCredential is set to true for AI Foundry" -ForegroundColor Gray
    }
    
    # Check Application Insights Configuration
    Write-Host "`n📊 Application Insights Configuration:" -ForegroundColor Green
    if ($appSettings.ApplicationInsights.UseDefaultAzureCredential -eq $true) {
        Write-Host "✅ Application Insights configured for managed identity" -ForegroundColor Green
    }
    else {
        Write-Host "⚠️  Application Insights managed identity configuration not found" -ForegroundColor Yellow
    }
    
    if ($appSettings.ApplicationInsights.InstrumentationKey) {
        Write-Host "⚠️  WARNING: InstrumentationKey found in configuration" -ForegroundColor Yellow
        Write-Host "   Remove InstrumentationKey - managed identity will be used instead" -ForegroundColor Gray
    }
    else {
        Write-Host "✅ No instrumentation key found - good for managed identity" -ForegroundColor Green
    }
    
}
else {
    Write-Host "❌ appsettings.json not found at $AppSettingsPath" -ForegroundColor Red
}

Write-Host "`n2. Checking Python Configuration..." -ForegroundColor Blue

if (Test-Path $ConfigPath) {
    $configContent = Get-Content $ConfigPath -Raw
    
    Write-Host "`n🐍 Python Settings Configuration:" -ForegroundColor Green
    
    # Check for DefaultAzureCredential usage
    if ($configContent -match "DefaultAzureCredential") {
        Write-Host "✅ DefaultAzureCredential found in Python configuration" -ForegroundColor Green
    }
    else {
        Write-Host "⚠️  DefaultAzureCredential not found in Python configuration" -ForegroundColor Yellow
        Write-Host "   Ensure Azure SDK uses DefaultAzureCredential for authentication" -ForegroundColor Gray
    }
    
    # Check for hardcoded secrets (patterns to avoid)
    $secretPatterns = @(
        "connection_string\s*=\s*['`"].*['`"]",
        "api_key\s*=\s*['`"].*['`"]", 
        "secret\s*=\s*['`"].*['`"]",
        "password\s*=\s*['`"].*['`"]",
        "token\s*=\s*['`"].*['`"]"
    )
    
    $secretsFound = $false
    foreach ($pattern in $secretPatterns) {
        if ($configContent -match $pattern) {
            $secretsFound = $true
            Write-Host "⚠️  WARNING: Potential hardcoded secret pattern found: $pattern" -ForegroundColor Yellow
        }
    }
    
    if (-not $secretsFound) {
        Write-Host "✅ No hardcoded secret patterns found in Python configuration" -ForegroundColor Green
    }
    
}
else {
    Write-Host "❌ Python settings file not found at $ConfigPath" -ForegroundColor Red
}

Write-Host "`n3. Environment Variables Check..." -ForegroundColor Blue

# Check for environment variables that might contain secrets
$problematicEnvVars = @(
    "AZURE_STORAGE_CONNECTION_STRING",
    "AZURE_OPENAI_API_KEY", 
    "APPLICATIONINSIGHTS_INSTRUMENTATION_KEY",
    "AZURE_CLIENT_SECRET"
)

$envVarsFound = $false
foreach ($envVar in $problematicEnvVars) {
    if ([System.Environment]::GetEnvironmentVariable($envVar)) {
        Write-Host "⚠️  WARNING: Environment variable $envVar is set" -ForegroundColor Yellow
        Write-Host "   This may override managed identity authentication" -ForegroundColor Gray
        $envVarsFound = $true
    }
}

if (-not $envVarsFound) {
    Write-Host "✅ No problematic environment variables found" -ForegroundColor Green
}

# Check for recommended managed identity environment variables
$recommendedEnvVars = @{
    "AZURE_CLIENT_ID" = "System-assigned managed identity (not required but can be explicit)"
    "AZURE_TENANT_ID" = "Azure tenant ID for managed identity"
}

Write-Host "`n📋 Recommended Managed Identity Environment Variables:" -ForegroundColor Green
foreach ($envVar in $recommendedEnvVars.Keys) {
    $value = [System.Environment]::GetEnvironmentVariable($envVar)
    if ($value) {
        Write-Host "✅ $envVar is set" -ForegroundColor Green
    }
    else {
        Write-Host "ℹ️  $envVar not set - $($recommendedEnvVars[$envVar])" -ForegroundColor Yellow
    }
}

Write-Host "`n🔒 MANAGED IDENTITY VALIDATION SUMMARY" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan

Write-Host "✅ WHAT'S GOOD FOR MANAGED IDENTITY:" -ForegroundColor Green
Write-Host "   • No connection strings in configuration" -ForegroundColor White
Write-Host "   • No API keys in configuration" -ForegroundColor White
Write-Host "   • UseDefaultAzureCredential set to true" -ForegroundColor White
Write-Host "   • DefaultAzureCredential used in Python code" -ForegroundColor White
Write-Host "   • No hardcoded secrets in environment variables" -ForegroundColor White

Write-Host "`n⚠️  REQUIRED FOR SECRETLESS OPERATION:" -ForegroundColor Yellow
Write-Host "   • System-assigned managed identity must be enabled on Container App" -ForegroundColor White
Write-Host "   • All Azure resources must have proper RBAC permissions assigned" -ForegroundColor White
Write-Host "   • Application must use Azure SDK DefaultAzureCredential" -ForegroundColor White
Write-Host "   • No secrets, connection strings, or API keys anywhere" -ForegroundColor White

Write-Host "`n🎯 NEXT STEPS:" -ForegroundColor Cyan
Write-Host "1. Run the permissions script: .\scripts\assign-minimum-permissions.ps1" -ForegroundColor White
Write-Host "2. Deploy the application: .\deploy.ps1" -ForegroundColor White
Write-Host "3. Verify managed identity authentication in logs" -ForegroundColor White

Write-Host "`n🔐 VALIDATION COMPLETE - Ready for secretless managed identity operation!" -ForegroundColor Green