using System;
using System.IO;
using Python.Runtime;

namespace ChunkHound.Core.Tests.Helpers;

/// <summary>
/// Helper class for testing Python-dependent functionality.
/// </summary>
public static class PythonTestHelper
{
    private static bool? _isPythonAvailable;

    /// <summary>
    /// Checks if Python runtime is available for testing.
    /// </summary>
    public static bool IsPythonAvailable()
    {
        if (_isPythonAvailable.HasValue)
            return _isPythonAvailable.Value;

        try
        {
            // Configure Python environment before initialization
            ConfigurePythonEnvironment();

            if (!PythonEngine.IsInitialized)
            {
                PythonEngine.Initialize();
                _isPythonAvailable = true;
            }
            else
            {
                _isPythonAvailable = true;
            }

            // Try to import a basic module to ensure Python is working
            using (Py.GIL())
            {
                // Ensure the venv site-packages is in sys.path
                var venvPath = @"e:\dev\github\chunkhound-dotnet\python-deps\.venv";
                var pythonPath = Path.Combine(venvPath, "Lib", "site-packages");

                dynamic sys = Py.Import("sys");
                sys.path.insert(0, pythonPath.ToString());
                Console.WriteLine($"TestHelper: Added {pythonPath} to sys.path");

                // If we get here, Python is working
            }

            return true;
        }
        catch (Exception)
        {
            _isPythonAvailable = false;
            return false;
        }
    }

    /// <summary>
    /// Configures Python environment variables for virtual environment support.
    /// </summary>
    private static void ConfigurePythonEnvironment()
    {
        // Force use the venv's Python environment
        var venvPath = @"e:\dev\github\chunkhound-dotnet\python-deps\.venv";
        var pythonHome = venvPath;
        var pythonPath = Path.Combine(venvPath, "Lib", "site-packages");

        // Set environment variables for pythonnet
        Environment.SetEnvironmentVariable("PYTHONHOME", pythonHome);
        Environment.SetEnvironmentVariable("PYTHONPATH", pythonPath);

        // Set the Python DLL path for pythonnet (must be set before PythonEngine.Initialize)
        // For uv-managed venvs, the DLL is in the uv python installation directory
        var pythonDllPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "uv", "python", "cpython-3.12.12-windows-x86_64-none", "python312.dll");

        if (System.IO.File.Exists(pythonDllPath))
        {
            Runtime.PythonDLL = pythonDllPath;
            Console.WriteLine($"TestHelper: Set Python DLL to: {pythonDllPath}");
            Console.WriteLine($"TestHelper: Set PYTHONHOME to: {pythonHome}");
            Console.WriteLine($"TestHelper: Set PYTHONPATH to: {pythonPath}");
        }
    }

    /// <summary>
    /// Gets the skip reason for Python-dependent tests.
    /// </summary>
    public static string GetPythonSkipReason()
    {
        return "Python runtime not available. Install Python 3.12+ and run: pip install lancedb pyarrow numpy";
    }
}