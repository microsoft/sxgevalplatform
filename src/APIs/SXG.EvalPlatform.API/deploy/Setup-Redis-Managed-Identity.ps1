# Setup Redis Managed Identity Access
# This script configures Azure AD authentication for Redis and grants Managed Identity access

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-sxg-agent-evaluation-platform",
    
    [Parameter(Mandatory=$false)]
  [ValidateSet("Dev", "PPE", "Prod", "All")]
    [string]$Environment = "All"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Redis Managed Identity Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check Azure login
Write-Host "[1/6] Checking Azure CLI login..." -ForegroundColor Yellow
$loginCheck = az account show --output json 2>$null
if (-not $loginCheck) {
    Write-Host "? Not logged in. Please run: az login" -ForegroundColor Red
 exit 1
}

$currentSub = az account show --query name -o tsv
Write-Host "? Logged in to subscription: $currentSub" -ForegroundColor Green
Write-Host ""

# Define environments
$environments = @()

if ($Environment -eq "All" -or $Environment -eq "Dev") {
    $environments += @{
        Name = "Development"
        AppServiceName = "sxgevalapidev"
        RedisCacheName = "sxgagenteval"
    }
}

if ($Environment -eq "All" -or $Environment -eq "PPE") {
$environments += @{
 Name = "PPE"
        AppServiceName = "sxgevalapippe"
        RedisCacheName = "sxgagenteval"
    }
}

if ($Environment -eq "All" -or $Environment -eq "Prod") {
    $environments += @{
  Name = "Production"
  AppServiceName = "sxgevalapiprod"
        RedisCacheName = "evalplatformcacheprod"
    }
}

# Step 1: Enable Azure AD on Redis caches
Write-Host "[2/6] Enabling Azure AD authentication on Redis caches..." -ForegroundColor Yellow
Write-Host ""

$uniqueRedisCaches = $environments | Select-Object -ExpandProperty RedisCacheName -Unique

foreach ($redisCacheName in $uniqueRedisCaches) {
    Write-Host "  Configuring Redis: $redisCacheName" -ForegroundColor Cyan
  
    # Check if Redis exists
    $redisExists = az redis show --name $redisCacheName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null
    
    if (-not $redisExists) {
        Write-Host "  ??  Redis cache not found: $redisCacheName" -ForegroundColor Yellow
        Write-Host ""
        continue
    }
    
    # Enable Azure AD authentication
    Write-Host "    Enabling Azure AD authentication..." -ForegroundColor Gray
    az redis update `
        --name $redisCacheName `
        --resource-group $ResourceGroupName `
     --enable-non-ssl-port false `
        --set "redisConfiguration.aad-enabled=true" `
        --output none 2>$null
  
    # Verify
    $aadEnabled = az redis show --name $redisCacheName --resource-group $ResourceGroupName --query "redisConfiguration.aad-enabled" -o tsv 2>$null
    
    if ($aadEnabled -eq "true") {
Write-Host "  ? Azure AD authentication enabled on $redisCacheName" -ForegroundColor Green
    } else {
        Write-Host "  ??  Could not verify Azure AD status for $redisCacheName" -ForegroundColor Yellow
    }
 Write-Host ""
}

# Step 2: Enable Managed Identity on App Services
Write-Host "[3/6] Enabling Managed Identity on App Services..." -ForegroundColor Yellow
Write-Host ""

$managedIdentities = @{}

foreach ($env in $environments) {
    Write-Host "  Configuring App Service: $($env.AppServiceName)" -ForegroundColor Cyan
    
    # Check if App Service exists
  $appExists = az webapp show --name $env.AppServiceName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null
    
    if (-not $appExists) {
        Write-Host "  ??  App Service not found: $($env.AppServiceName)" -ForegroundColor Yellow
        Write-Host ""
   continue
    }
    
# Enable System-Assigned Managed Identity
    Write-Host "    Enabling System-Assigned Managed Identity..." -ForegroundColor Gray
    az webapp identity assign `
     --name $env.AppServiceName `
        --resource-group $ResourceGroupName `
        --output none
    
    # Get Principal ID
    $principalId = az webapp identity show `
        --name $env.AppServiceName `
  --resource-group $ResourceGroupName `
        --query principalId -o tsv
    
    if ($principalId) {
        Write-Host "  ? Managed Identity enabled: $principalId" -ForegroundColor Green
   $managedIdentities[$env.AppServiceName] = @{
            PrincipalId = $principalId
            RedisCacheName = $env.RedisCacheName
            Environment = $env.Name
        }
    } else {
        Write-Host "  ? Failed to get Managed Identity for $($env.AppServiceName)" -ForegroundColor Red
    }
    Write-Host ""
}

# Step 3: Get Redis Resource IDs
Write-Host "[4/6] Getting Redis cache resource IDs..." -ForegroundColor Yellow
Write-Host ""

$redisResourceIds = @{}

foreach ($redisCacheName in $uniqueRedisCaches) {
    $redisId = az redis show `
        --name $redisCacheName `
        --resource-group $ResourceGroupName `
        --query id -o tsv 2>$null
    
    if ($redisId) {
        $redisResourceIds[$redisCacheName] = $redisId
        Write-Host "  ? $redisCacheName - Resource ID obtained" -ForegroundColor Green
    } else {
        Write-Host "  ? Failed to get resource ID for $redisCacheName" -ForegroundColor Red
    }
}

Write-Host ""

# Step 4: Assign "Redis Cache Contributor" role
Write-Host "[5/6] Assigning 'Redis Cache Contributor' role to Managed Identities..." -ForegroundColor Yellow
Write-Host ""

$successCount = 0
$failCount = 0

foreach ($appServiceName in $managedIdentities.Keys) {
    $identity = $managedIdentities[$appServiceName]
    $redisCacheName = $identity.RedisCacheName
    $principalId = $identity.PrincipalId
    $environment = $identity.Environment
    
    Write-Host "  Assigning role for $environment ($appServiceName)" -ForegroundColor Cyan
    
    if (-not $redisResourceIds.ContainsKey($redisCacheName)) {
        Write-Host "  ??  Redis resource ID not found for $redisCacheName" -ForegroundColor Yellow
        $failCount++
        Write-Host ""
        continue
}
    
$redisId = $redisResourceIds[$redisCacheName]
    
    # Check if role already assigned
    $existingRole = az role assignment list `
        --assignee $principalId `
 --scope $redisId `
        --query "[?roleDefinitionName=='Redis Cache Contributor']" -o tsv 2>$null
    
    if ($existingRole) {
        Write-Host "  ??  Role already assigned to $environment" -ForegroundColor Cyan
        $successCount++
    } else {
    # Assign role
        Write-Host "    Assigning 'Redis Cache Contributor' role..." -ForegroundColor Gray
        $roleAssignment = az role assignment create `
   --assignee $principalId `
         --role "Redis Cache Contributor" `
         --scope $redisId `
     --output none 2>&1
        
        if ($LASTEXITCODE -eq 0) {
   Write-Host "  ? Role assigned successfully for $environment" -ForegroundColor Green
            $successCount++
   } else {
      Write-Host "  ? Failed to assign role for $environment" -ForegroundColor Red
            Write-Host "     Error: $roleAssignment" -ForegroundColor Gray
          $failCount++
        }
    }
    Write-Host ""
}

# Step 5: Verify role assignments
Write-Host "[6/6] Verifying role assignments..." -ForegroundColor Yellow
Write-Host ""

foreach ($appServiceName in $managedIdentities.Keys) {
    $identity = $managedIdentities[$appServiceName]
    $redisCacheName = $identity.RedisCacheName
    $principalId = $identity.PrincipalId
    $environment = $identity.Environment
    
    if (-not $redisResourceIds.ContainsKey($redisCacheName)) {
 continue
    }
    
    $redisId = $redisResourceIds[$redisCacheName]
    
    Write-Host "  $environment ($appServiceName):" -ForegroundColor Cyan
    
    $roles = az role assignment list `
    --assignee $principalId `
   --scope $redisId `
   --query "[].roleDefinitionName" -o tsv 2>$null
    
    if ($roles) {
     foreach ($role in $roles) {
   Write-Host "    ? $role" -ForegroundColor Green
        }
    } else {
        Write-Host "    ? No roles assigned" -ForegroundColor Red
    }
    Write-Host ""
}

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Setup Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Environments configured: $($environments.Count)" -ForegroundColor Cyan
Write-Host "Redis caches configured: $($uniqueRedisCaches.Count)" -ForegroundColor Cyan
Write-Host "Successful role assignments: $successCount" -ForegroundColor Green
Write-Host "Failed role assignments: $failCount" -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Red" })
Write-Host ""

if ($failCount -eq 0 -and $successCount -gt 0) {
    Write-Host "? SETUP COMPLETE!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Yellow
    Write-Host "  1. Deploy application with updated code" -ForegroundColor White
    Write-Host "  2. Verify Redis connection in Application Insights" -ForegroundColor White
    Write-Host "  3. Monitor cache operations in logs" -ForegroundColor White
} else {
    Write-Host "??  SETUP INCOMPLETE" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please review errors above and re-run the script." -ForegroundColor White
}

Write-Host ""
Write-Host "Completed at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
