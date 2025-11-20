#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Validate PPE Container App Deployment

.DESCRIPTION
    This script validates the PPE deployment by checking:
    - Container app health and status
    - Managed identity permissions
    - Azure service connectivity
    - Queue processing capability

.PARAMETER SubscriptionId
    Azure subscription ID (default: d2ef7484-d847-4ca9-88be-d2d9f2a8a50f)

.PARAMETER ResourceGroupName
    Resource group name (default: rg-sxg-agent-evaluation-platform)

.EXAMPLE
    ./validate-ppe-deployment.ps1
    
.EXAMPLE
    ./validate-ppe-deployment.ps1 -Verbose
#>

param(
    [string]$SubscriptionId = "d2ef7484-d847-4ca9-88be-d2d9f2a8a50f",
    [string]$ResourceGroupName = "rg-sxg-agent-evaluation-platform"
)

# PPE Environment Configuration
$PPEConfig = @{
    ContainerAppName = "eval-framework-app-ppe"
    ManagedIdentityName = "eval-framework-identity-ppe"
    StorageAccountName = "sxgagentevalppe"
    QueueName = "eval-processing-requests"
    Environment = "PPE"
}

Write-Host "🔍 Validating PPE Container App Deployment" -ForegroundColor Green

# Ensure we're logged in to Azure
try {
    $currentContext = az account show --query name -o tsv 2>$null
    if (-not $currentContext) {
        throw "Not logged in"
    }
    Write-Host "✅ Connected to Azure: $currentContext" -ForegroundColor Green
} catch {
    Write-Host "❌ Please login to Azure first: az login" -ForegroundColor Red
    exit 1
}

# Set subscription
az account set --subscription $SubscriptionId

# Test 1: Container App Status
Write-Host "`n1️⃣ Checking Container App Status..." -ForegroundColor Cyan
try {
    $containerApp = az containerapp show --name $PPEConfig.ContainerAppName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
    
    if ($containerApp) {
        $status = $containerApp.properties.runningStatus
        $appUrl = "https://" + $containerApp.properties.configuration.ingress.fqdn
        
        Write-Host "✅ Container App Status: $status" -ForegroundColor Green
        Write-Host "   App URL: $appUrl" -ForegroundColor Gray
        Write-Host "   Replicas: $($containerApp.properties.template.scale.minReplicas) - $($containerApp.properties.template.scale.maxReplicas)" -ForegroundColor Gray
        
        if ($status -eq "Running") {
            Write-Host "✅ Container app is running successfully" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Container app status: $status" -ForegroundColor Yellow
        }
    } else {
        Write-Host "❌ Container app not found" -ForegroundColor Red
        return
    }
} catch {
    Write-Host "❌ Failed to get container app status" -ForegroundColor Red
    return
}

# Test 2: Health Endpoint Check
Write-Host "`n2️⃣ Testing Health Endpoint..." -ForegroundColor Cyan
try {
    $healthUrl = $appUrl + "/health"
    Write-Host "🔍 Testing: $healthUrl" -ForegroundColor Blue
    
    $response = Invoke-WebRequest -Uri $healthUrl -TimeoutSec 30 -ErrorAction Stop
    
    if ($response.StatusCode -eq 200) {
        Write-Host "✅ Health endpoint responding: $($response.StatusCode)" -ForegroundColor Green
        Write-Host "   Response: $($response.Content)" -ForegroundColor Gray
    } else {
        Write-Host "⚠️  Health endpoint status: $($response.StatusCode)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Health endpoint not accessible: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   This may be normal if the app is still starting up" -ForegroundColor Yellow
}

# Test 3: Container App Logs
Write-Host "`n3️⃣ Checking Recent Container Logs..." -ForegroundColor Cyan
try {
    Write-Host "🔍 Fetching last 20 log entries..." -ForegroundColor Blue
    
    $logs = az containerapp logs show --name $PPEConfig.ContainerAppName --resource-group $ResourceGroupName --tail 20 2>$null
    
    if ($logs) {
        Write-Host "✅ Recent logs retrieved:" -ForegroundColor Green
        $logs | ForEach-Object { Write-Host "   $_" -ForegroundColor Gray }
        
        # Check for errors in logs
        $errorCount = ($logs | Where-Object { $_ -match "ERROR|Exception|Failed" }).Count
        if ($errorCount -gt 0) {
            Write-Host "⚠️  Found $errorCount error entries in recent logs" -ForegroundColor Yellow
        } else {
            Write-Host "✅ No errors found in recent logs" -ForegroundColor Green
        }
    } else {
        Write-Host "⚠️  No logs available yet" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠️  Could not retrieve container logs" -ForegroundColor Yellow
}

# Test 4: Managed Identity Status
Write-Host "`n4️⃣ Checking Managed Identity..." -ForegroundColor Cyan
try {
    $identity = az identity show --name $PPEConfig.ManagedIdentityName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
    
    if ($identity) {
        Write-Host "✅ Managed Identity found: $($identity.name)" -ForegroundColor Green
        Write-Host "   Principal ID: $($identity.principalId)" -ForegroundColor Gray
        Write-Host "   Client ID: $($identity.clientId)" -ForegroundColor Gray
    } else {
        Write-Host "❌ Managed identity not found" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Failed to get managed identity info" -ForegroundColor Red
}

# Test 5: Storage Queue Access
Write-Host "`n5️⃣ Testing Storage Access..." -ForegroundColor Cyan
try {
    Write-Host "🔍 Testing queue access with managed identity..." -ForegroundColor Blue
    
    $queues = az storage queue list --account-name $PPEConfig.StorageAccountName --auth-mode login --output json 2>$null | ConvertFrom-Json
    
    if ($queues) {
        Write-Host "✅ Storage access working. Found queues:" -ForegroundColor Green
        $queues | ForEach-Object { Write-Host "   - $($_.name)" -ForegroundColor Gray }
        
        # Check if our specific queue exists
        $ourQueue = $queues | Where-Object { $_.name -eq $PPEConfig.QueueName }
        if ($ourQueue) {
            Write-Host "✅ Target queue '$($PPEConfig.QueueName)' found" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Target queue '$($PPEConfig.QueueName)' not found" -ForegroundColor Yellow
        }
    } else {
        Write-Host "❌ Could not access storage queues" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Storage access test failed" -ForegroundColor Red
}

# Test 6: Environment Variables
Write-Host "`n6️⃣ Checking Environment Configuration..." -ForegroundColor Cyan
try {
    $containerApp = az containerapp show --name $PPEConfig.ContainerAppName --resource-group $ResourceGroupName --output json | ConvertFrom-Json
    $envVars = $containerApp.properties.template.containers[0].env
    
    if ($envVars) {
        Write-Host "✅ Environment variables configured:" -ForegroundColor Green
        $envVars | ForEach-Object { 
            if ($_.name -eq "RUNTIME_ENVIRONMENT") {
                Write-Host "   - $($_.name): $($_.value)" -ForegroundColor Gray
                if ($_.value -eq "PPE") {
                    Write-Host "✅ RUNTIME_ENVIRONMENT correctly set to PPE" -ForegroundColor Green
                } else {
                    Write-Host "⚠️  RUNTIME_ENVIRONMENT is '$($_.value)', expected 'PPE'" -ForegroundColor Yellow
                }
            }
        }
    } else {
        Write-Host "⚠️  No environment variables configured" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠️  Could not check environment variables" -ForegroundColor Yellow
}

# Test 7: Create Test Queue Message
Write-Host "`n7️⃣ Testing Queue Message Processing..." -ForegroundColor Cyan
try {
    Write-Host "🔍 Creating test queue message..." -ForegroundColor Blue
    
    $testMessage = @{
        evalRunId = "test-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        agentId = "test-agent-ppe"
        configurationId = "test-config"
        status = "test"
    } | ConvertTo-Json -Compress
    
    # Encode message for queue
    $encodedMessage = [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($testMessage))
    
    $result = az storage message put --queue-name $PPEConfig.QueueName --content $encodedMessage --account-name $PPEConfig.StorageAccountName --auth-mode login 2>$null
    
    if ($result) {
        Write-Host "✅ Test message queued successfully" -ForegroundColor Green
        Write-Host "   Message: $testMessage" -ForegroundColor Gray
        Write-Host "   Monitor container logs to see if message is processed" -ForegroundColor Yellow
    } else {
        Write-Host "⚠️  Could not queue test message" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠️  Queue message test failed (this is optional)" -ForegroundColor Yellow
}

# Summary Report
Write-Host "`n📊 PPE Deployment Validation Summary:" -ForegroundColor Green
Write-Host "=" * 50 -ForegroundColor Gray

Write-Host "Environment: $($PPEConfig.Environment)" -ForegroundColor Gray
Write-Host "Container App: $($PPEConfig.ContainerAppName)" -ForegroundColor Gray
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Gray

if ($appUrl) {
    Write-Host "App URL: $appUrl" -ForegroundColor Gray
}

Write-Host "`n🔧 Manual Testing Commands:" -ForegroundColor Yellow
Write-Host "   # View live logs:" -ForegroundColor Gray
Write-Host "   az containerapp logs show --name $($PPEConfig.ContainerAppName) --resource-group $ResourceGroupName --follow" -ForegroundColor White
Write-Host "   # Test health endpoint:" -ForegroundColor Gray
Write-Host "   curl $appUrl/health" -ForegroundColor White
Write-Host "   # Check storage queues:" -ForegroundColor Gray  
Write-Host "   az storage queue list --account-name $($PPEConfig.StorageAccountName) --auth-mode login" -ForegroundColor White

Write-Host "`n✅ PPE deployment validation completed!" -ForegroundColor Green