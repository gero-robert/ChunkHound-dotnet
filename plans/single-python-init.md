TASK: Introduce Single Source of Truth PythonRuntimeManager (production-owned, cross-platform, GIL-safe)
Status: Production-ready, eliminates all GIL hangs in VS Code + CLI forever
Why this change (background for code reviewer):
Python.NET + LanceDB requires exactly one initialization of the Python interpreter per process. Previous duplicated code in LanceDbService and PythonTestHelper caused races under parallel test execution (VS Code Test Explorer vs dotnet test). Because tests execute production code in the same process, the manager must live in ChunkHound (not Tests). This makes Python interop thread-safe, cross-platform, and enforces the rule “never touch PythonEngine directly outside this class”.
Files to touch/create (current master paths — I reviewed latest commit):

NEW: ChunkHound/Core/Python/PythonRuntimeManager.cs (create Core/Python folder)
MODIFY: ChunkHound/Providers/LanceDbService.cs (remove old init, add GIL logs)
MODIFY: ChunkHound.Tests/Helpers/PythonTestHelper.cs
NEW/CREATE: CodingRules.md in repo root (or append if exists)
NEW: ChunkHound.Tests/test.runsettings
MODIFY: ChunkHound.Tests/ChunkHound.Tests.csproj

Exact step-by-step implementation (do in this order):
Step 0 – Background & Cross-platform support
PythonRuntimeManager must detect OS automatically:

Windows: uv Python + local venv (your current path)
Linux/macOS: Use RuntimeInformation → default to system Python or ~/.local/share/uv/python/ + venv site-packages.
Make paths overridable via environment variables (CHUNKHOUND_PYTHONHOME, CHUNKHOUND_PYTHONPATH) for CI/containers. Always call PythonEngine.BeginAllowThreads() after init.

Step 1 – Create the canonical manager
Create ChunkHound/Core/Python/PythonRuntimeManager.cs (static class):

private static readonly object _lock = new();
private static readonly Lazy<bool> _initialized = new(InitializeRuntime, LazyThreadSafetyMode.ExecutionAndPublication);
public static void EnsureInitialized() → forces init
public static bool IsAvailable => _initialized.Value;
public static void Shutdown() (for test cleanup)
Inside InitializeRuntime(): detect OS, set PYTHONHOME/PYTHONPATH/Runtime.PythonDLL, PythonEngine.Initialize(), PythonEngine.BeginAllowThreads().
Full XML docs + comment: “This is the SINGLE initialization point for the entire process. Never call PythonEngine.Initialize() or ConfigurePythonEnvironment anywhere else.”

Step 2 – Refactor LanceDbService

Delete InitializePythonEngine() and ConfigurePythonEnvironment() entirely.
In static ctor (or very first line of instance ctor): PythonRuntimeManager.EnsureInitialized();
For everyusing (Py.GIL()) block (there are ~5 currently):C#_logger.LogDebug("Acquiring Python GIL on thread {ThreadId}...", Thread.CurrentThread.ManagedThreadId);
using (Py.GIL())
{
    _logger.LogDebug("GIL acquired successfully");
    // original code
    _logger.LogDebug("Releasing Python GIL");
}
_logger.LogDebug("GIL released");
Update log messages to mention “PythonRuntimeManager ensured initialization”.

Step 3 – Refactor PythonTestHelper

Delete all env config, ConfigurePythonEnvironment, direct PythonEngine calls, GIL.
IsPythonAvailable() → return PythonRuntimeManager.IsAvailable;
Keep GetPythonSkipReason() unchanged.

Step 4 – Update CodingRules.md
Create CodingRules.md in repo root (or append to existing) with new section:
Markdown## Python Interop Rules (Python.NET + LanceDB)

- **Single initialization point**: Always call `PythonRuntimeManager.EnsureInitialized()` (never `PythonEngine.Initialize()` directly).  
- This manager lives in production (`ChunkHound/Core/Python/`) because the Python runtime is process-global.  
- Tests must only consume it — never re-initialize.  
- Every `using (Py.GIL())` must be wrapped with Debug logs (enter/acquire/release) for troubleshooting hangs.  
- Cross-platform support required (Windows/Linux/macOS) via OS detection + env var overrides.  

Reason: Prevents GIL deadlocks under parallel test runners (VS Code, xUnit). Production owns the contract.
Step 5 – Hardening for VS Code / parallel tests
Create ChunkHound.Tests/test.runsettings:
XML<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <MaxCpuCount>1</MaxCpuCount>
  </RunConfiguration>
</RunSettings>
Add to ChunkHound.Tests.csproj:
XML<ItemGroup>
  <None Update="test.runsettings">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
Step 6 – Final cleanup & verification

Global search & replace: remove every remaining direct PythonEngine.Initialize() or ConfigurePythonEnvironment call.
Replace them with: PythonRuntimeManager.EnsureInitialized(); (should be zero after this PR).
Keep existing [Collection("LanceDB")] + DisableParallelization = true on LanceDBProviderTests, UniversalParserTests, and your new local tests.
Add comment in PythonRuntimeManager: “Process-global singleton. Never duplicate init logic.”

Step 7 – Test

Full test suite in VS Code Tests tab → must finish cleanly (no hangs).
dotnet test
Your new local Python-initializing tests
Manually test on Linux/macOS VM (or GitHub Actions) to verify cross-platform paths.

Expected outcome

Zero duplication
Production owns Python contract
Stable in IDE + CLI + CI
Easy to extend (config injection, Python 3.13, etc.)