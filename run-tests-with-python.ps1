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

# Set environment variables for pythonnet
$pythonHome = Join-Path $venvPath "Lib"
$pythonPath = Join-Path $venvPath "Lib\site-packages"
$env:PYTHONHOME = $pythonHome
$env:PYTHONPATH = $pythonPath

Write-Host "PYTHONHOME set to: $env:PYTHONHOME" -ForegroundColor Green
Write-Host "PYTHONPATH set to: $env:PYTHONPATH" -ForegroundColor Green
Write-Host "Python version:" -ForegroundColor Green
& python --version

# Run the tests
Write-Host "Running tests with filter: $TestFilter" -ForegroundColor Green
& dotnet test --filter $TestFilter --verbosity normal

Write-Host "Tests completed." -ForegroundColor Green