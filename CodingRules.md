# Coding Rules for ChunkHound

## Rule: Field Name Constants for String References

When referencing field names as strings in code (e.g., in serialization methods like `FromDict` or `ToDict`), always use a single source of truth for the field name to ensure consistency and maintainability.
Other repeating strings cross methods should be defined in costs.

### Guidelines:
1. **Prefer the original field name**: If the string matches the exact field name (case-sensitive), reference it directly from the field's definition if possible.
2. **Use constants for derived or transformed names**: If the string representation differs from the field name (e.g., snake_case vs PascalCase, or abbreviated forms), define a `const` string for the field name and use that constant in all methods that reference this field as a string.
3. **Avoid magic strings**: Never hardcode field name strings directly in methods. Always use the constant or derive from the field name.
4. **Reflection fallback**: Dont use reflection to access the field name, prefer defining a const instead.
5. **Naming convention for const fields**: All const fields should use the ALL_CAPS_WITH_UNDERSCORES naming convention, e.g., THIS_IS_A_CONST.

### Example:
In the `File.cs` class, the fields `CreatedAt` and `UpdatedAt` are serialized as `"created_at"` and `"updated_at"` (snake_case). Instead of hardcoding these strings in `FromDict` and `ToDict` methods, define constants:

```csharp
private const string CREATED_AT_FIELD = "created_at";
private const string UPDATED_AT_FIELD = "updated_at";
```

Then use these constants in the methods.

## Rule: Test Comments for Use Cases

Each test method must include a comment describing the specific use cases it tests. This ensures that tests provide clear value and helps prevent duplication by making the purpose of each test explicit.

### Guidelines:
1. **Mandatory comments**: Every test method should start with a comment explaining what use cases or scenarios it covers.
2. **Avoid redundancy**: Before writing a new test, review existing tests to ensure it doesn't duplicate coverage.
3. **Value-driven**: Focus on tests that validate meaningful behavior, not just code paths.
4. **Test Value Assessment Framework**: Before writing a test, evaluate:
   - **Business Value**: Does this test validate a business rule, invariant, or user requirement?
   - **Integration Coverage**: Is this behavior already covered by integration or E2E tests?
   - **Change Frequency**: How often does this code change? (Avoid testing volatile implementation details)
   - **Failure Impact**: What would break if this test fails? (High impact = high value)

### What NOT to Test (Low-Value Examples):
- **Constructor assignment tests**: Testing that `new Object(param)` assigns `param` to a property
- **Simple property getters/setters**: Testing that `obj.Property` returns the assigned value
- **Trivial calculations**: Testing `end - start + 1` without business context
- **Basic enum logic**: Testing `IsCodeChunk()` returns true for code types (unless it has complex business logic)
- **Range checks**: Testing `ContainsLine(line)` for obvious cases without edge cases

### What TO Test (High-Value Examples):
- **Validation logic**: Constructor throws on invalid parameters (business rules)
- **Serialization/Deserialization**: `FromDict`/`ToDict` correctly handles data transformation
- **Immutability**: `WithId()` creates new instance without mutating original
- **Complex business logic**: Multi-step calculations with business significance
- **Edge cases**: Boundary conditions that could cause real bugs
- **Integration points**: How components interact, especially across module boundaries

### Test Categories by Value:
- **Critical**: Validation, serialization, immutability, error handling
- **High**: Complex algorithms, state transitions, integration behaviors
- **Medium**: Edge cases, boundary conditions, error scenarios
- **Low**: Basic assignments, trivial getters, obvious calculations
- **None**: Tests that duplicate integration coverage or test volatile internals

### Example of High-Value Test:
```csharp
/// <summary>
/// Tests that Chunk constructor validates line ranges according to business rules:
/// start line must be positive and end line must be >= start line.
/// This prevents invalid chunks that could break parsing logic.
/// </summary>
[Test]
public void Constructor_InvalidLineRange_ThrowsValidationException()
{
    // Test implementation - validates business invariant
}
```

### Example of Low-Value Test (AVOID):
```csharp
/// <summary>
/// Tests that LineCount property returns the correct number of lines.
/// </summary>
[Test]
public void LineCount_ReturnsCorrectCount() // DON'T WRITE THIS
{
    var chunk = new Chunk("test", 5, 15, "code", ChunkType.Function, 1, Language.CSharp);
    Assert.Equal(11, chunk.LineCount); // 15 - 5 + 1 - trivial calculation
}
```

### Rationale:
- **Prevents duplication**: Clear descriptions help identify overlapping tests.
- **Ensures value**: Forces consideration of whether the test adds meaningful coverage.
- **Maintainability**: Makes it easier to understand and refactor tests over time.
- **Cost Efficiency**: Avoids wasting time on tests that don't catch real bugs.

## Python Interop Rules (Python.NET + LanceDB)

- **Single initialization point**: Always call `PythonRuntimeManager.EnsureInitialized()` (never `PythonEngine.Initialize()` directly).  
- This manager lives in production (`ChunkHound/Core/Python/`) because the Python runtime is process-global.  
- Tests must only consume it — never re-initialize.  
- Every `using (Py.GIL())` must be wrapped with Debug logs (enter/acquire/release) for troubleshooting hangs.  
- Cross-platform support required (Windows/Linux/macOS) via OS detection + env var overrides.  

Reason: Prevents GIL deadlocks under parallel test runners (VS Code, xUnit). Production owns the contract.

