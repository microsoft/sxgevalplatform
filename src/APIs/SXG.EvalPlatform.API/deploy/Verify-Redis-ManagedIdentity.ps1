# Redis Managed Identity Configuration Verifier
# This script verifies all prerequisites for Redis with Managed Identity

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Development", "PPE", "Production")]
    [string]$Environment
)

$ErrorActionPreference = "Stop"

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
    Production = @{
        ResourceGroup = "sxg-eval-platform-prod"
    AppServiceName = "sxgevalapiprod"
   RedisName = "evalplatformcacheprod"
    }
}

$config = $configs[$Environment]

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Redis Managed Identity Verification" -ForegroundColor Cyan
Write-Host "Environment: $Environment" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Azure CLI Login
Write-Host "? Test 1: Verifying Azure CLI login..." -ForegroundColor Yellow
try {
    $accountJson = az account show 2>&1 | Out-String
    if ($accountJson -match '\{[\s\S]*\}') {
        $jsonPart = $matches[0]
        $account = $jsonPart | ConvertFrom-Json
        Write-Host "  ? Logged in as: $($account.user.name)" -ForegroundColor Green
        Write-Host "  ? Subscription: $($account.name)" -ForegroundColor Green
    } else {
        Write-Host "  ? NOT logged in to Azure CLI" -ForegroundColor Red
        Write-Host "    Run: az login" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "  ? NOT logged in to Azure CLI" -ForegroundColor Red
    Write-Host "    Run: az login" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Test 2: Redis Cache exists and configuration
Write-Host "? Test 2: Verifying Redis Cache configuration..." -ForegroundColor Yellow
try {
    $redisJson = az redis show --name $config.RedisName --resource-group $config.ResourceGroup 2>&1 | Out-String
    
    if ($redisJson -match '\{[\s\S]*\}') {
        $jsonPart = $matches[0]
        $redis = $jsonPart | ConvertFrom-Json
        Write-Host "  ? Redis Cache found: $($redis.name)" -ForegroundColor Green
        Write-Host "  ? Host: $($redis.hostName)" -ForegroundColor Green
        Write-Host "  ? Port (SSL): $($redis.sslPort)" -ForegroundColor Green
        Write-Host "  ? Redis Version: $($redis.redisVersion)" -ForegroundColor Green
        Write-Host "  ? Minimum TLS Version: $($redis.minimumTlsVersion)" -ForegroundColor Green
        
        if ($redis.enableNonSslPort -eq $false) {
          Write-Host "  ? Non-SSL port: Disabled (secure)" -ForegroundColor Green
    } else {
            Write-Host "  ? Non-SSL port: Enabled (consider disabling)" -ForegroundColor Yellow
    }
    } else {
     Write-Host "  ? Redis Cache not found" -ForegroundColor Red
   exit 1
    }
} catch {
    Write-Host "  ? Redis Cache not found or not accessible" -ForegroundColor Red
    Write-Host "    Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Test 3: App Service exists and Managed Identity
Write-Host "? Test 3: Verifying App Service and Managed Identity..." -ForegroundColor Yellow
try {
    $appServiceJson = az webapp show --name $config.AppServiceName --resource-group $config.ResourceGroup 2>&1 | Out-String
    
    if ($appServiceJson -match '\{[\s\S]*\}') {
      $jsonPart = $matches[0]
        $appService = $jsonPart | ConvertFrom-Json
        Write-Host "  ? App Service found: $($appService.name)" -ForegroundColor Green
        Write-Host "  ? Location: $($appService.location)" -ForegroundColor Green
        Write-Host "  ? Default hostname: $($appService.defaultHostName)" -ForegroundColor Green
        
    if ($appService.identity -and $appService.identity.principalId) {
    $principalId = $appService.identity.principalId
      Write-Host "  ? Managed Identity: Enabled" -ForegroundColor Green
        Write-Host "  ? Principal ID: $principalId" -ForegroundColor Green
        } else {
         Write-Host "  ? Managed Identity: NOT Enabled" -ForegroundColor Red
      Write-Host "    Run: az webapp identity assign --name $($config.AppServiceName) --resource-group $($config.ResourceGroup)" -ForegroundColor Yellow
          exit 1
        }
    } else {
        Write-Host "  ? App Service not found" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "  ? App Service not found or not accessible" -ForegroundColor Red
    Write-Host "    Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Test 4: Access Policy Assignments (New Entra ID method)
Write-Host "? Test 4: Verifying Redis Access Policy Assignments..." -ForegroundColor Yellow
try {
    $policiesJson = az redis access-policy-assignment list --name $config.RedisName --resource-group $config.ResourceGroup 2>&1 | Out-String
    
    if ($policiesJson -match '\[[\s\S]*\]') {
        $jsonPart = $matches[0]
        $policies = $jsonPart | ConvertFrom-Json
        
     Write-Host "  ? Access policies found: $($policies.Count)" -ForegroundColor Green
        
    $appServicePolicy = $policies | Where-Object { $_.objectId -eq $principalId }
  
        if ($appServicePolicy) {
            Write-Host "  ? App Service has access policy assignment" -ForegroundColor Green
        Write-Host "    Policy Name: $($appServicePolicy.name)" -ForegroundColor Green
            Write-Host "    Access Policy: $($appServicePolicy.accessPolicyName)" -ForegroundColor Green
  Write-Host "    Object ID: $($appServicePolicy.objectId)" -ForegroundColor Green
        } else {
   Write-Host "  ? App Service does NOT have access policy assignment" -ForegroundColor Red
          Write-Host "    Principal ID not found in access policies: $principalId" -ForegroundColor Yellow
            Write-Host ""
   Write-Host "  RUN THIS COMMAND:" -ForegroundColor Red
            Write-Host "  .\Fix-Redis-ManagedIdentity-Access.ps1 -Environment $Environment" -ForegroundColor White
            exit 1
        }
    } else {
   Write-Host "  ? Could not retrieve access policies" -ForegroundColor Red
        Write-Host "  Response: $policiesJson" -ForegroundColor Gray
      exit 1
    }
} catch {
    Write-Host "  ? Failed to check access policies" -ForegroundColor Red
    Write-Host "    Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Test 5: Network connectivity
Write-Host "? Test 5: Verifying network configuration..." -ForegroundColor Yellow
try {
    $firewallJson = az redis firewall-rules list --name $config.RedisName --resource-group $config.ResourceGroup 2>&1 | Out-String
    
    if ($firewallJson -match '\[[\s\S]*\]') {
 $jsonPart = $matches[0]
        $firewallRules = $jsonPart | ConvertFrom-Json
        
        if ($firewallRules.Count -gt 0) {
            Write-Host "  ? Firewall rules configured: $($firewallRules.Count)" -ForegroundColor Green
   foreach ($rule in $firewallRules) {
       Write-Host "    - $($rule.name): $($rule.startIP) - $($rule.endIP)" -ForegroundColor Green
            }
         
       # Check for Azure Services rule
        $azureRule = $firewallRules | Where-Object { $_.startIP -eq "0.0.0.0" -and $_.endIP -eq "0.0.0.0" }
            if ($azureRule) {
    Write-Host "  ? Azure Services firewall rule exists (allows App Service)" -ForegroundColor Green
       } else {
                Write-Host "  ? No Azure Services firewall rule found" -ForegroundColor Yellow
     Write-Host "    App Service may not be able to connect" -ForegroundColor Yellow
        }
        } else {
            Write-Host "  ? No firewall rules configured" -ForegroundColor Yellow
         Write-Host "    Note: If using VNet integration, this is OK" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "  ? Could not check firewall rules" -ForegroundColor Yellow
}

Write-Host ""

# Test 6: App Settings
Write-Host "? Test 6: Verifying App Service configuration..." -ForegroundColor Yellow
try {
    $settingsJson = az webapp config appsettings list --name $config.AppServiceName --resource-group $config.ResourceGroup 2>&1 | Out-String
    
    if ($settingsJson -match '\[[\s\S]*\]') {
   $jsonPart = $matches[0]
        $appSettings = $jsonPart | ConvertFrom-Json
        
        $cacheProvider = $appSettings | Where-Object { $_.name -eq "Cache__Provider" }
  if ($cacheProvider) {
            if ($cacheProvider.value -eq "Redis") {
                Write-Host "  ? Cache Provider: Redis" -ForegroundColor Green
      } else {
     Write-Host "  ? Cache Provider: $($cacheProvider.value) (expected: Redis)" -ForegroundColor Yellow
     }
        } else {
     Write-Host "  ? Cache__Provider not set in App Settings" -ForegroundColor Cyan
            Write-Host "    Using value from appsettings.json" -ForegroundColor Gray
        }
    
        $redisEndpoint = $appSettings | Where-Object { $_.name -eq "Cache__Redis__Endpoint" }
        if ($redisEndpoint) {
   $expectedEndpoint = "$($redis.hostName):$($redis.sslPort)"
       if ($redisEndpoint.value -eq $expectedEndpoint) {
         Write-Host "  ? Redis Endpoint: $($redisEndpoint.value)" -ForegroundColor Green
        } else {
          Write-Host "  ? Redis Endpoint: $($redisEndpoint.value)" -ForegroundColor Yellow
  Write-Host "    Expected: $expectedEndpoint" -ForegroundColor Yellow
      }
        } else {
   Write-Host "  ? Cache__Redis__Endpoint not set in App Settings" -ForegroundColor Cyan
            Write-Host "    Using value from appsettings.json" -ForegroundColor Gray
}
        
     $apiEnv = $appSettings | Where-Object { $_.name -eq "ApiSettings__Environment" }
        if ($apiEnv) {
          Write-Host "  ? ApiSettings__Environment: $($apiEnv.value)" -ForegroundColor Green
    } else {
 Write-Host "  ? ApiSettings__Environment not set in App Settings" -ForegroundColor Cyan
        }
    }
} catch {
    Write-Host "  ? Could not verify app settings" -ForegroundColor Yellow
}

Write-Host ""

# Summary
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Verification Summary" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration Details:" -ForegroundColor White
Write-Host "  Resource Group: $($config.ResourceGroup)" -ForegroundColor White
Write-Host "  App Service: $($config.AppServiceName)" -ForegroundColor White
Write-Host "  Redis Cache: $($config.RedisName)" -ForegroundColor White
Write-Host "  Redis Endpoint: $($redis.hostName):$($redis.sslPort)" -ForegroundColor White
Write-Host "  Managed Identity: $principalId" -ForegroundColor White
Write-Host ""

Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Deploy the application to $Environment" -ForegroundColor White
Write-Host "   .\Deploy-To-Azure-PPE.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Wait 2-3 minutes for deployment to complete" -ForegroundColor White
Write-Host ""
Write-Host "3. Test the health endpoint:" -ForegroundColor White
Write-Host "   .\Test-Redis-ManagedIdentity.ps1 -Environment $Environment" -ForegroundColor Gray
Write-Host ""

Write-Host "? Verification complete!" -ForegroundColor Green
