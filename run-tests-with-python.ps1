# PowerShell script to run ChunkHound tests with Python dependencies
# This script activates the uv-managed Python virtual environment and runs the tests

param(
    [string]$TestFilter = "LanceDBProvider"
)

Write-Host "Setting up Python environment for ChunkHound tests..." -ForegroundColor Green

# Check if uv virtual environment exists
$venvPath = "python-deps\.venv"
if (!(Test-Path $venvPath)) {
    Write-Host "Python virtual environment not found. Installing dependencies..." -ForegroundColor Yellow
    & uv venv python-deps
    & uv pip install --python python-deps\.venv lancedb pyarrow numpy
}

# Activate the virtual environment
$activateScript = Join-Path $venvPath "Scripts\Activate.ps1"
if (Test-Path $activateScript) {
    Write-Host "Activating Python virtual environment..." -ForegroundColor Green
    & $activateScript
} else {
    Write-Host "Could not find activation script at $activateScript" -ForegroundColor Red
    exit 1
}

# Environment variables will be set by TestHelper
Write-Host "Python version:" -ForegroundColor Green
& python --version

# Run the tests
Write-Host "Running tests with filter: $TestFilter" -ForegroundColor Green
& dotnet test --filter $TestFilter --verbosity normal

Write-Host "Tests completed." -ForegroundColor Green