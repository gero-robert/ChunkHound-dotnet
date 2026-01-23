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
4. **Avoid low-value tests**: Be cautious of unit tests that test trivial implementation details like constructors or property getters/setters, especially if they are redundant when covered by higher-level integration tests. Such tests may be prone to frequent changes due to syntax modifications and provide little value. Focus on tests that validate meaningful behavior or integration points.

### Example:
```csharp
/// <summary>
/// Tests that File.FromDict correctly parses a dictionary with all required fields,
/// including created_at and updated_at timestamps.
/// </summary>
[Test]
public void FromDict_WithValidData_CreatesFile()
{
    // Test implementation
}
```

### Rationale:
- **Prevents duplication**: Clear descriptions help identify overlapping tests.
- **Ensures value**: Forces consideration of whether the test adds meaningful coverage.
- **Maintainability**: Makes it easier to understand and refactor tests over time.

