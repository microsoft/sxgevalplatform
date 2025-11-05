# URGENT: Fix Container App ACR Authentication
# Admin must run this to fix deployment issue

Write-Host "🚨 URGENT FIX NEEDED - Container App Cannot Pull Images" -ForegroundColor Red
Write-Host "=======================================================" -ForegroundColor Yellow

Write-Host "`n❌ CURRENT ISSUE:" -ForegroundColor Red
Write-Host "Container app managed identity cannot pull images from ACR" -ForegroundColor White
Write-Host "Error: UNAUTHORIZED: authentication required" -ForegroundColor Gray

Write-Host "`n🎯 SOLUTION (Admin must run this):" -ForegroundColor Green
Write-Host "az role assignment create \" -ForegroundColor White
Write-Host "  --assignee-object-id 7a867d7d-43e1-4972-92c0-b875e678b7ef \" -ForegroundColor White
Write-Host "  --assignee-principal-type ServicePrincipal \" -ForegroundColor White
Write-Host "  --role `"AcrPull`" \" -ForegroundColor White
Write-Host "  --scope `"/subscriptions/d2ef7484-d847-4ca9-88be-d2d9f2a8a50f/resourceGroups/rg-sxg-agent-evaluation-platform/providers/Microsoft.ContainerRegistry/registries/evalplatformregistry`"" -ForegroundColor White

Write-Host "`n📋 STEP BY STEP:" -ForegroundColor Cyan
Write-Host "1. Copy the command above" -ForegroundColor White
Write-Host "2. Run it in PowerShell/Azure CLI with admin permissions" -ForegroundColor White
Write-Host "3. Wait 1-2 minutes for permissions to propagate" -ForegroundColor White
Write-Host "4. Retry the deployment command" -ForegroundColor White

Write-Host "`n✅ AFTER ADMIN RUNS THE COMMAND ABOVE:" -ForegroundColor Green
Write-Host "The deployment will work and you can proceed with:" -ForegroundColor White
Write-Host "az containerapp update --name eval-framework-app --resource-group rg-sxg-agent-evaluation-platform --image evalplatformregistry.azurecr.io/eval-framework-app:latest --set-env-vars MAX_DATASET_CONCURRENCY=3 MAX_METRICS_CONCURRENCY=8 EVALUATION_TIMEOUT=30 HTTP_POOL_CONNECTIONS=20 HTTP_POOL_MAXSIZE=10 HTTP_SESSION_LIFETIME=3600 AZURE_STORAGE_TIMEOUT=30 AZURE_STORAGE_CONNECT_TIMEOUT=60 ENABLE_PERFORMANCE_LOGGING=true" -ForegroundColor Gray

Write-Host "`n🔍 TO VERIFY (after admin runs the command):" -ForegroundColor Cyan
Write-Host "az role assignment list --assignee 7a867d7d-43e1-4972-92c0-b875e678b7ef --output table" -ForegroundColor Gray
Write-Host "(Should show AcrPull role)" -ForegroundColor Gray