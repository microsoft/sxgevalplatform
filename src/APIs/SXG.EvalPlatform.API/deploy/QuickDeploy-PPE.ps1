# Quick PPE Deployment Script
# Run this from the deploy directory to deploy to PPE

Write-Host "Starting PPE Deployment..." -ForegroundColor Green
Write-Host ""

# Change to deploy directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

# Run the deployment
.\Deploy-To-Azure-PPE.ps1

Write-Host ""
Write-Host "Deployment script completed!" -ForegroundColor Green
