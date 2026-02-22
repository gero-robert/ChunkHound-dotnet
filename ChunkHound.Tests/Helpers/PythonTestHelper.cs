using System;
using ChunkHound.Core.Python;

namespace ChunkHound.Core.Tests.Helpers;

/// <summary>
/// Helper class for testing Python-dependent functionality.
/// </summary>
public static class PythonTestHelper
{
    /// <summary>
    /// Checks if Python runtime is available for testing.
    /// </summary>
    public static bool IsPythonAvailable()
    {
        return PythonRuntimeManager.IsAvailable;
    }

    /// <summary>
    /// Gets the skip reason for Python-dependent tests.
    /// </summary>
    public static string GetPythonSkipReason()
    {
        return "Python runtime not available. Install Python 3.12+ and run: pip install lancedb pyarrow numpy";
    }
}