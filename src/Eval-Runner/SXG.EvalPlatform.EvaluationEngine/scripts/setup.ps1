# PowerShell script for setting up the Python virtual environment and dependencies
# File: setup.ps1

param(
    [switch]$Clean,
    [switch]$Dev,
    [string]$PythonVersion = "3.11"
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Colors for output
$Green = "Green"
$Red = "Red"
$Yellow = "Yellow"
$Blue = "Blue"

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Test-Command {
    param([string]$Command)
    try {
        Get-Command $Command -ErrorAction Stop | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Main {
    $separator = "=" * 60
    Write-ColorOutput $separator $Blue
    Write-ColorOutput "SXG Evaluation Platform - Environment Setup" $Blue
    Write-ColorOutput $separator $Blue
    Write-ColorOutput ""

    # Get the project root (assume script is run from project root)
    $ProjectRoot = Get-Location
    $VenvPath = Join-Path $ProjectRoot ".venv"
    $RequirementsPath = Join-Path $ProjectRoot "requirements.txt"
    $DevRequirementsPath = Join-Path $ProjectRoot "requirements-dev.txt"
    
    Write-ColorOutput "Project root: $ProjectRoot" $Yellow
    Write-ColorOutput "Virtual environment: $VenvPath" $Yellow
    Write-ColorOutput ""

    # Check if Python is installed
    if (-not (Test-Command "python")) {
        Write-ColorOutput "ERROR: Python is not installed or not in PATH" $Red
        Write-ColorOutput "Please install Python $PythonVersion from https://www.python.org/" $Red
        exit 1
    }

    # Check Python version
    $PythonVersionOutput = python --version 2>&1
    Write-ColorOutput "Found Python: $PythonVersionOutput" $Green

    # Clean existing environment if requested
    if ($Clean -and (Test-Path $VenvPath)) {
        Write-ColorOutput "Cleaning existing virtual environment..." $Yellow
        Remove-Item $VenvPath -Recurse -Force
        Write-ColorOutput "Existing environment removed." $Green
    }

    # Create virtual environment if it doesn't exist
    if (-not (Test-Path $VenvPath)) {
        Write-ColorOutput "Creating virtual environment..." $Yellow
        python -m venv $VenvPath
        if ($LASTEXITCODE -ne 0) {
            Write-ColorOutput "ERROR: Failed to create virtual environment" $Red
            exit 1
        }
        Write-ColorOutput "Virtual environment created successfully." $Green
    } else {
        Write-ColorOutput "Virtual environment already exists." $Green
    }

    # Determine activation script path
    $ActivateScript = Join-Path $VenvPath "Scripts\Activate.ps1"
    if (-not (Test-Path $ActivateScript)) {
        Write-ColorOutput "ERROR: Virtual environment activation script not found" $Red
        exit 1
    }

    Write-ColorOutput "Activating virtual environment..." $Yellow
    
    # Activate virtual environment
    & $ActivateScript
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "ERROR: Failed to activate virtual environment" $Red
        exit 1
    }

    Write-ColorOutput "Virtual environment activated." $Green

    # Upgrade pip
    Write-ColorOutput "Upgrading pip..." $Yellow
    python -m pip install --upgrade pip
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "WARNING: Failed to upgrade pip" $Yellow
    } else {
        Write-ColorOutput "Pip upgraded successfully." $Green
    }

    # Install wheel for better package installation
    Write-ColorOutput "Installing wheel..." $Yellow
    python -m pip install wheel
    
    # Install production dependencies
    if (Test-Path $RequirementsPath) {
        Write-ColorOutput "Installing production dependencies..." $Yellow
        python -m pip install -r $RequirementsPath
        if ($LASTEXITCODE -ne 0) {
            Write-ColorOutput "ERROR: Failed to install production dependencies" $Red
            exit 1
        }
        Write-ColorOutput "Production dependencies installed successfully." $Green
    } else {
        Write-ColorOutput "WARNING: requirements.txt not found. Creating basic requirements..." $Yellow
        New-RequirementsFile $RequirementsPath
        python -m pip install -r $RequirementsPath
    }

    # Install development dependencies if requested
    if ($Dev) {
        if (Test-Path $DevRequirementsPath) {
            Write-ColorOutput "Installing development dependencies..." $Yellow
            python -m pip install -r $DevRequirementsPath
            if ($LASTEXITCODE -ne 0) {
                Write-ColorOutput "WARNING: Failed to install development dependencies" $Yellow
            } else {
                Write-ColorOutput "Development dependencies installed successfully." $Green
            }
        } else {
            Write-ColorOutput "WARNING: requirements-dev.txt not found. Creating basic dev requirements..." $Yellow
            New-DevRequirementsFile $DevRequirementsPath
            python -m pip install -r $DevRequirementsPath
        }
    }

    # Install the project in editable mode
    Write-ColorOutput "Installing project in editable mode..." $Yellow
    $PyProjectPath = Join-Path $ProjectRoot "pyproject.toml"
    if (Test-Path $PyProjectPath) {
        python -m pip install -e $ProjectRoot
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "Project installed in editable mode." $Green
        } else {
            Write-ColorOutput "WARNING: Failed to install project in editable mode" $Yellow
        }
    } else {
        Write-ColorOutput "INFO: No pyproject.toml found, skipping editable installation" $Yellow
    }

    # Verify installation
    Write-ColorOutput "Verifying installation..." $Yellow
    python -c "import sys; print(f'Python executable: {sys.executable}')"
    python -c "import eval_runner; print(f'eval_runner version: {eval_runner.__version__}')" 2>$null
    
    Write-ColorOutput ""
    $separator = "=" * 60
    Write-ColorOutput $separator $Blue
    Write-ColorOutput "Setup completed successfully!" $Green
    Write-ColorOutput $separator $Blue
    Write-ColorOutput ""
    Write-ColorOutput "To activate the environment manually, run:" $Yellow
    Write-ColorOutput "  .\.venv\Scripts\Activate.ps1" $Yellow
    Write-ColorOutput ""
    Write-ColorOutput "To run the evaluation runner:" $Yellow
    Write-ColorOutput "  python src\main.py" $Yellow
    Write-ColorOutput ""
    Write-ColorOutput "To run tests (if development dependencies installed):" $Yellow
    Write-ColorOutput "  pytest tests/" $Yellow
    Write-ColorOutput ""
}

function New-RequirementsFile {
    param([string]$Path)
    
    $Content = @"
# Azure SDK packages
azure-storage-queue==12.8.0
azure-storage-blob==12.19.0
azure-identity==1.15.0

# HTTP client
aiohttp==3.9.1

# Data processing
pydantic==2.5.2

# Logging and utilities
structlog==23.2.0
python-json-logger==2.0.7

# Optional: For advanced metrics
scikit-learn==1.3.2
numpy==1.24.3
"@
    
    $Content | Out-File -FilePath $Path -Encoding UTF8
    Write-ColorOutput "Created basic requirements.txt" $Green
}

function New-DevRequirementsFile {
    param([string]$Path)
    
    $Content = @"
# Testing
pytest==7.4.3
pytest-asyncio==0.21.1
pytest-cov==4.1.0
pytest-mock==3.12.0

# Code quality
black==23.11.0
flake8==6.1.0
mypy==1.7.1
isort==5.12.0

# Development utilities
pre-commit==3.6.0
ipython==8.18.1
jupyter==1.0.0

# Documentation
sphinx==7.2.6
sphinx-rtd-theme==1.3.0
"@
    
    $Content | Out-File -FilePath $Path -Encoding UTF8
    Write-ColorOutput "Created basic requirements-dev.txt" $Green
}

# Run main function
try {
    Main
}
catch {
    Write-ColorOutput "ERROR: $_" $Red
    exit 1
}