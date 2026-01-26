# ChunkHound Python Dependencies

This directory contains the Python dependencies required for ChunkHound's LanceDB integration using pythonnet.

## Setup

### Prerequisites
- [uv](https://github.com/astral-sh/uv) - Fast Python package installer
- Python 3.8+ (managed by uv)

### Installation

1. **Install dependencies using uv:**
   ```bash
   cd python-deps
   uv sync
   ```

   Or install directly:
   ```bash
   uv pip install lancedb pyarrow numpy
   ```

2. **Verify installation:**
   ```bash
   uv run python -c "import lancedb, pyarrow, numpy; print('All dependencies installed successfully')"
   ```

## Dependencies

- **lancedb** (0.27.0) - Vector database for AI applications
- **pyarrow** (23.0.0) - Apache Arrow Python bindings for data interchange
- **numpy** (2.4.1) - Fundamental package for array computing

## Usage

The Python environment is automatically detected by pythonnet when running ChunkHound. The virtual environment provides all necessary packages for the LanceDB integration.

### Running Tests

To run tests that require Python dependencies:

**Windows PowerShell:**
```powershell
.\run-tests-with-python.ps1
```

**Manual activation:**
```bash
# Activate virtual environment
python-deps\.venv\Scripts\activate

# Set Python path
set PYTHONPATH=python-deps\.venv\Lib\site-packages

# Run tests
dotnet test --filter LanceDBProvider
```

### CI/CD Integration

For GitHub Actions or other CI systems:

```yaml
- name: Setup Python
  uses: actions/setup-python@v4
  with:
    python-version: '3.12'

- name: Install uv
  run: pip install uv

- name: Install Python dependencies
  run: uv pip install lancedb pyarrow numpy

- name: Run tests
  run: dotnet test --filter LanceDBProvider
```

## Troubleshooting

### Python not found
If pythonnet cannot find Python, ensure the virtual environment is activated or PYTHONPATH is set correctly.

### Import errors
Verify all packages are installed:
```bash
uv run python -c "import lancedb; import pyarrow; import numpy"
```

### Version conflicts
If you encounter version conflicts, you can pin specific versions in `pyproject.toml` or use `uv.lock` to ensure reproducible builds.