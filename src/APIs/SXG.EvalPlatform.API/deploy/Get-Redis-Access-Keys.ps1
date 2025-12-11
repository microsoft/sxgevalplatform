# Get Redis Access Keys for All Environments
# Run this script to get the access keys needed for deployment

param(
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-sxg-agent-evaluation-platform"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Get Redis Access Keys" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check Azure login
Write-Host "[1/2] Checking Azure CLI login..." -ForegroundColor Yellow
$loginCheck = az account show --output json 2>$null
if (-not $loginCheck) {
 Write-Host "? Not logged in. Please run: az login" -ForegroundColor Red
  exit 1
}
Write-Host "? Logged in" -ForegroundColor Green
Write-Host ""

# Get keys for each environment
Write-Host "[2/2] Retrieving Redis access keys..." -ForegroundColor Yellow
Write-Host ""

$environments = @(
 @{ Name = "Development"; RedisCacheName = "sxgagenteval" },
    @{ Name = "PPE"; RedisCacheName = "sxgagenteval" },
    @{ Name = "Production"; RedisCacheName = "evalplatformcacheprod" }
)

foreach ($env in $environments) {
    Write-Host "Environment: $($env.Name)" -ForegroundColor Cyan
    Write-Host "Redis Cache: $($env.RedisCacheName)" -ForegroundColor Gray
    
    # Check if Redis exists
    $rediExists = az redis show --name $env.RedisCacheName --resource-group $ResourceGroupName --query "name" -o tsv 2>$null
 
    if (-not $redisExists) {
        Write-Host "  ??  Redis cache NOT found: $($env.RedisCacheName)" -ForegroundColor Yellow
  Write-Host ""
        continue
    }
    
    # Get primary key (only once for shared cache)
    if ($env.RedisCacheName -eq "sxgagenteval" -and $env.Name -eq "PPE") {
  Write-Host "  ??  Using same key as Development (shared cache)" -ForegroundColor Cyan
Write-Host ""
        continue
    }
    
  $primaryKey = az redis list-keys --name $env.RedisCacheName --resource-group $ResourceGroupName --query primaryKey -o tsv 2>$null
    
if ($primaryKey) {
        Write-Host "  ? Primary Key: $primaryKey" -ForegroundColor Green
        
        # Build connection string
  $connectionString = "$($env.RedisCacheName).redis.cache.windows.net:6380,password=$primaryKey,ssl=True,abortConnect=False"
      Write-Host "  ?? Connection String:" -ForegroundColor Cyan
        Write-Host "     $connectionString" -ForegroundColor White
      Write-Host ""
        
        # Save to clipboard option
Write-Host "  ?? Save this connection string for deployment script:" -ForegroundColor Yellow
        Write-Host "     Cache__Redis__Endpoint=$connectionString" -ForegroundColor White
        Write-Host ""
 } else {
  Write-Host "  ? Failed to retrieve access key" -ForegroundColor Red
        Write-Host ""
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Next Steps" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Update deployment scripts with connection strings above" -ForegroundColor White
Write-Host "2. Set Cache__Redis__UseManagedIdentity=false in deployment scripts" -ForegroundColor White
Write-Host "3. Deploy to each environment" -ForegroundColor White
Write-Host "4. Verify Redis connection in Application Insights (no more exceptions)" -ForegroundColor White
Write-Host ""
Write-Host "Example for PPE deployment script:" -ForegroundColor Yellow
Write-Host '  "Cache__Redis__Endpoint=evalplatformcacheppe.redis.cache.windows.net:6380,password=YOUR_KEY_HERE,ssl=True,abortConnect=False",' -ForegroundColor Gray
Write-Host '  "Cache__Redis__UseManagedIdentity=false",' -ForegroundColor Gray
Write-Host ""
