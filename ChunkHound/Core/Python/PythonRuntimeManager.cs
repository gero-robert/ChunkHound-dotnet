using System;
using System.IO;
using System.Runtime.InteropServices;
using Python.Runtime;

namespace ChunkHound.Core.Python
{
    /// <summary>
    /// Process-global singleton. Never duplicate init logic.
    /// This is the SINGLE initialization point for the entire process. Never call PythonEngine.Initialize() or ConfigurePythonEnvironment anywhere else.
    /// </summary>
    public static class PythonRuntimeManager
    {
        private static readonly object _lock = new();
        private static readonly Lazy<bool> _initialized = new(InitializeRuntime, LazyThreadSafetyMode.ExecutionAndPublication);
        private static nint _threadState;

        public static void EnsureInitialized()
        {
            _ = _initialized.Value;
        }

        public static bool IsAvailable => _initialized.Value;

        public static void Shutdown()
        {
            PythonEngine.EndAllowThreads(_threadState);
            PythonEngine.Shutdown();
        }

        private static bool InitializeRuntime()
        {
            lock (_lock)
            {
                string pythonHome = null;
                string pythonPath = null;
                string dllName = null;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    pythonHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "uv", "python", "cpython-3.12.12-windows-x86_64-none");
                    pythonPath = Path.Combine("python-deps", ".venv", "Lib", "site-packages");
                    dllName = "python312.dll";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    pythonHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "uv", "python", "cpython-3.12.12-linux-x86_64-gnu");
                    pythonPath = Path.Combine("python-deps", ".venv", "lib", "python3.12", "site-packages");
                    dllName = "libpython3.12.so";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    pythonHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "uv", "python", "cpython-3.12.12-macos-x86_64-none");
                    pythonPath = Path.Combine("python-deps", ".venv", "lib", "python3.12", "site-packages");
                    dllName = "libpython3.12.dylib";
                }

                // Overridable via environment variables
                pythonHome = Environment.GetEnvironmentVariable("CHUNKHOUND_PYTHONHOME") ?? pythonHome;
                pythonPath = Environment.GetEnvironmentVariable("CHUNKHOUND_PYTHONPATH") ?? pythonPath;

                if (!string.IsNullOrEmpty(pythonHome))
                {
                    Environment.SetEnvironmentVariable("PYTHONHOME", pythonHome);
                    if (!string.IsNullOrEmpty(dllName))
                    {
                        Runtime.PythonDLL = Path.Combine(pythonHome, dllName);
                    }
                }

                if (!string.IsNullOrEmpty(pythonPath))
                {
                    Environment.SetEnvironmentVariable("PYTHONPATH", pythonPath);
                }

                PythonEngine.Initialize();
                _threadState = PythonEngine.BeginAllowThreads();

                return true;
            }
        }
    }
}