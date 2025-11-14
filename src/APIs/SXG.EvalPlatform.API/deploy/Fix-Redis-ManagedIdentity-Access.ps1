# Fix Redis Managed Identity Access for PPE
# This script configures Redis Cache to work with App Service Managed Identity
# REQUIRES: Azure Owner or User Access Administrator role

param(
    [Parameter(Mandatory=$false)]
    [string]$Environment = "PPE"
)

$ErrorActionPreference = "Continue"

# Configuration
$configs = @{
    Development = @{
        ResourceGroup = "rg-sxg-agent-evaluation-platform"
    AppServiceName = "sxgevalapidev"
        RedisName = "sxgagenteval"
    }
    PPE = @{
     ResourceGroup = "rg-sxg-agent-evaluation-platform"
        AppServiceName = "sxgevalapippe"
   RedisName = "sxgagenteval"
    }
}

$config = $configs[$Environment]

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "Fix Redis Managed Identity Access - $Environment" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "This script will:" -ForegroundColor Yellow
Write-Host "  1. Enable Microsoft Entra ID authentication on Redis Cache" -ForegroundColor White
Write-Host "  2. Create Access Policy Assignment for App Service Managed Identity" -ForegroundColor White
Write-Host ""
Write-Host "Prerequisites:" -ForegroundColor Yellow
Write-Host "  - You must have Owner or Contributor role on the resources" -ForegroundColor White
Write-Host "  - You must be logged in to Azure CLI (az login)" -ForegroundColor White
Write-Host ""

# Confirm
$confirm = Read-Host "Do you want to proceed? (yes/no)"
if ($confirm -ne "yes") {
    Write-Host "Aborted." -ForegroundColor Yellow
    exit 0
}
Write-Host ""

# Check Azure CLI login
Write-Host "[Step 1/7] Verifying Azure CLI login..." -ForegroundColor Yellow
try {
    $accountJson = az account show 2>&1 | Out-String
    
    # Extract JSON from output (ignore warnings)
    if ($accountJson -match '\{[\s\S]*\}') {
     $jsonPart = $matches[0]
        $account = $jsonPart | ConvertFrom-Json
   Write-Host "  ✓ Logged in as: $($account.user.name)" -ForegroundColor Green
        Write-Host "  ✓ Subscription: $($account.name)" -ForegroundColor Green
    Write-Host "  ✓ Subscription ID: $($account.id)" -ForegroundColor Green
    } else {
        Write-Host "  ✗ NOT logged in to Azure CLI" -ForegroundColor Red
 Write-Host "  Run: az login" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "  ✗ Failed to check Azure CLI login" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Get App Service Managed Identity
Write-Host "[Step 2/7] Getting App Service Managed Identity..." -ForegroundColor Yellow
$principalId = $null
$appService = $null
try {
    Write-Host "  Checking if App Service exists..." -ForegroundColor Cyan
  $appServiceJson = az webapp show --name $config.AppServiceName --resource-group $config.ResourceGroup 2>&1 | Out-String
 
 # Extract JSON from output (ignore warnings)
if ($appServiceJson -match '\{[\s\S]*\}') {
        $jsonPart = $matches[0]
        $appService = $jsonPart | ConvertFrom-Json
        Write-Host "  ✓ App Service found: $($appService.name)" -ForegroundColor Green
        Write-Host "  ✓ Default hostname: $($appService.defaultHostName)" -ForegroundColor Green
    } else {
    Write-Host "  ✗ App Service not found" -ForegroundColor Red
        Write-Host "  Please verify:" -ForegroundColor Yellow
      Write-Host "    App Service name: $($config.AppServiceName)" -ForegroundColor White
      Write-Host "  Resource Group: $($config.ResourceGroup)" -ForegroundColor White
  exit 1
    }
    
    # Check Managed Identity
    if ($appService.identity -and $appService.identity.principalId) {
        $principalId = $appService.identity.principalId
        Write-Host "  ✓ Managed Identity already enabled" -ForegroundColor Green
        Write-Host "  ✓ Principal ID: $principalId" -ForegroundColor Green
    } else {
    Write-Host "  ℹ Managed Identity not enabled. Enabling now..." -ForegroundColor Cyan
        
        $identityJson = az webapp identity assign --name $config.AppServiceName --resource-group $config.ResourceGroup 2>&1 | Out-String
 
        if ($identityJson -match '\{[\s\S]*\}') {
            $jsonPart = $matches[0]
            $identity = $jsonPart | ConvertFrom-Json
            $principalId = $identity.principalId
Write-Host "  ✓ Managed Identity enabled successfully" -ForegroundColor Green
            Write-Host "  ✓ Principal ID: $principalId" -ForegroundColor Green
    
        Write-Host "  ⏳ Waiting 15 seconds for identity to propagate..." -ForegroundColor Cyan
            Start-Sleep -Seconds 15
 } else {
   Write-Host "  ✗ Failed to enable Managed Identity" -ForegroundColor Red
            exit 1
    }
 }
} catch {
    Write-Host "  ✗ Unexpected error while checking App Service" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Get Redis Cache details
Write-Host "[Step 3/7] Getting Redis Cache details..." -ForegroundColor Yellow
$redis = $null
try {
 $redisJson = az redis show --name $config.RedisName --resource-group $config.ResourceGroup 2>&1 | Out-String
    
    # Extract JSON from output (ignore warnings)
    if ($redisJson -match '\{[\s\S]*\}') {
        $jsonPart = $matches[0]
        $redis = $jsonPart | ConvertFrom-Json
     Write-Host "  ✓ Redis Cache: $($redis.name)" -ForegroundColor Green
     Write-Host "  ✓ Host: $($redis.hostName)" -ForegroundColor Green
        Write-Host "  ✓ SSL Port: $($redis.sslPort)" -ForegroundColor Green
        Write-Host "  ✓ SKU: $($redis.sku.name)" -ForegroundColor Green
    } else {
     Write-Host "  ✗ Redis Cache not found" -ForegroundColor Red
        exit 1
 }
} catch {
    Write-Host "  ✗ Failed to get Redis Cache details" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Check Microsoft Entra ID Authentication
Write-Host "[Step 4/7] Checking Microsoft Entra ID Authentication..." -ForegroundColor Yellow
try {
    $policiesJson = az redis access-policy-assignment list --name $config.RedisName --resource-group $config.ResourceGroup 2>&1 | Out-String
    
    if ($policiesJson -match '\[[\s\S]*\]') {
        $jsonPart = $matches[0]
        $policies = $jsonPart | ConvertFrom-Json
   Write-Host "  ✓ Microsoft Entra ID authentication is enabled" -ForegroundColor Green
 Write-Host "  ✓ Existing access policies: $($policies.Count)" -ForegroundColor Green
    } elseif ($policiesJson -match "not supported") {
        Write-Host "  ✗ Microsoft Entra ID authentication is NOT enabled" -ForegroundColor Red
        Write-Host ""
   Write-Host "  MANUAL STEP REQUIRED:" -ForegroundColor Red
      Write-Host "  1. Go to Azure Portal → https://portal.azure.com" -ForegroundColor White
        Write-Host "  2. Navigate to: Redis Cache → $($config.RedisName)" -ForegroundColor White
   Write-Host "  3. Go to: Settings → Authentication" -ForegroundColor White
        Write-Host "  4. Enable Microsoft Entra Authentication" -ForegroundColor White
        Write-Host "  5. Click Save" -ForegroundColor White
        Write-Host ""
      $continue = Read-Host "Have you completed this step? (yes/no)"
if ($continue -ne "yes") {
    Write-Host "  Aborted." -ForegroundColor Yellow
  exit 1
      }
    } else {
   Write-Host "  ⚠ Unable to determine Entra ID status" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ⚠ Could not check access policies" -ForegroundColor Yellow
}
Write-Host ""

# Create Access Policy Assignment
Write-Host "[Step 5/7] Creating Access Policy Assignment..." -ForegroundColor Yellow
try {
    $policiesJson = az redis access-policy-assignment list --name $config.RedisName --resource-group $config.ResourceGroup 2>&1 | Out-String
    
    $existingPolicy = $null
    if ($policiesJson -match '\[[\s\S]*\]') {
     $jsonPart = $matches[0]
        $policies = $jsonPart | ConvertFrom-Json
        $existingPolicy = $policies | Where-Object { $_.objectId -eq $principalId }
    }
    
    if ($existingPolicy) {
        Write-Host "  ℹ Access policy assignment already exists" -ForegroundColor Cyan
        Write-Host "    Policy Name: $($existingPolicy.name)" -ForegroundColor Gray
 Write-Host "    Access Policy: $($existingPolicy.accessPolicyName)" -ForegroundColor Gray
        Write-Host "  ✓ Skipping creation" -ForegroundColor Green
    } else {
      Write-Host "  ℹ Creating new access policy assignment..." -ForegroundColor Cyan

        $policyName = "$($config.AppServiceName)-policy"
        
 $createPolicyJson = az redis access-policy-assignment create --name $policyName --resource-group $config.ResourceGroup --redis-name $config.RedisName --object-id $principalId --object-id-alias $config.AppServiceName --access-policy-name "Data Owner" 2>&1 | Out-String
  
        if ($createPolicyJson -match '\{[\s\S]*\}') {
    Write-Host "  ✓ Access policy assignment created successfully" -ForegroundColor Green
            Write-Host "    Policy Name: $policyName" -ForegroundColor Gray
       Write-Host "    Access Policy: Data Owner" -ForegroundColor Gray
            Write-Host "  Object ID: $principalId" -ForegroundColor Gray
        } else {
            Write-Host "  ✗ Failed to create access policy assignment" -ForegroundColor Red
     Write-Host ""
    Write-Host "  MANUAL STEP REQUIRED:" -ForegroundColor Red
         Write-Host "  1. Go to Azure Portal → Redis Cache → $($config.RedisName)" -ForegroundColor White
            Write-Host "  2. Go to: Settings → Data Access Configuration" -ForegroundColor White
            Write-Host "  3. Click: + Add → New Redis User" -ForegroundColor White
            Write-Host "  4. Configure:" -ForegroundColor White
            Write-Host "     - Name: $policyName" -ForegroundColor Gray
            Write-Host "- Principal: $($config.AppServiceName)" -ForegroundColor Gray
       Write-Host "     - Access Policy: Data Owner" -ForegroundColor Gray
   Write-Host "  5. Click Save" -ForegroundColor White
            Write-Host ""
            
 $manualConfirm = Read-Host "Have you completed this step? (yes/no)"
   if ($manualConfirm -ne "yes") {
    Write-Host "  Aborted." -ForegroundColor Yellow
   exit 1
  }
      }
    }
} catch {
    Write-Host "  ✗ Unexpected error creating access policy" -ForegroundColor Red
    Write-Host "  Error: $_" -ForegroundColor Red
}
Write-Host ""

# Verify configuration
Write-Host "[Step 6/7] Verifying configuration..." -ForegroundColor Yellow
try {
    $verifyJson = az redis access-policy-assignment list --name $config.RedisName --resource-group $config.ResourceGroup 2>&1 | Out-String

    if ($verifyJson -match '\[[\s\S]*\]') {
        $jsonPart = $matches[0]
        $policies = $jsonPart | ConvertFrom-Json
      $appServicePolicy = $policies | Where-Object { $_.objectId -eq $principalId }
     
        if ($appServicePolicy) {
            Write-Host "  ✓ Configuration verified successfully" -ForegroundColor Green
        Write-Host "    App Service has access policy assignment" -ForegroundColor Gray
    Write-Host "    Policy: $($appServicePolicy.accessPolicyName)" -ForegroundColor Gray
        } else {
 Write-Host "⚠ Warning: Could not verify access policy assignment" -ForegroundColor Yellow
 }
} else {
        Write-Host "  ⚠ Could not verify policies" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ⚠ Could not verify configuration" -ForegroundColor Yellow
}
Write-Host ""

# Restart App Service
Write-Host "[Step 7/7] Restarting App Service..." -ForegroundColor Yellow
try {
    az webapp restart --name $config.AppServiceName --resource-group $config.ResourceGroup 2>&1 | Out-Null
    
Write-Host "  ✓ App Service restart initiated" -ForegroundColor Green
    Write-Host "  ⏳ Waiting 30 seconds for app to start..." -ForegroundColor Cyan
    Start-Sleep -Seconds 30
} catch {
    Write-Host "  ⚠ Could not restart App Service" -ForegroundColor Yellow
}
Write-Host ""

# Summary
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "Configuration Complete!" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor White
Write-Host "  ✓ App Service: $($config.AppServiceName)" -ForegroundColor Green
Write-Host "  ✓ Managed Identity Principal ID: $principalId" -ForegroundColor Green
Write-Host "  ✓ Redis Cache: $($config.RedisName)" -ForegroundColor Green
Write-Host "  ✓ Access Policy: Configured" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Wait 2-3 minutes for Azure to propagate changes" -ForegroundColor White
Write-Host ""
Write-Host "  2. Test the connection:" -ForegroundColor White
Write-Host "  .\Test-Redis-ManagedIdentity.ps1 -Environment $Environment" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Check health endpoint:" -ForegroundColor White
if ($appService) {
    Write-Host "     https://$($appService.defaultHostName)/api/v1/health/detailed" -ForegroundColor Gray
}
Write-Host ""
Write-Host "Expected Result:" -ForegroundColor Cyan
Write-Host '  "name": "Cache (Redis)", "status": "Healthy"' -ForegroundColor Green
Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
