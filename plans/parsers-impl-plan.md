```markdown
# ChunkHound .NET Recursive Parser Integration Plan

## NuGet Packages (pure managed only)
Add to `ChunkHound/ChunkHound.csproj` (existing project file at repo root):

```xml
<ItemGroup>
  <PackageReference Include="YamlDotNet" Version="16.1.3" />
  <PackageReference Include="Markdig" Version="0.41.0" />
  <PackageReference Include="HtmlAgilityPack" Version="1.11.72" />
</ItemGroup>
```

## Model Updates (zero breaking)
- `ChunkHound/Core/ChunkType.cs` (existing): Add any missing values to match Python (e.g. `Vue`, `YamlTemplate` if not present). Keep all current enum members untouched.
- `ChunkHound/Core/Chunk.cs` (existing): No changes required. `IReadOnlyList<Chunk>` output contract (properties: `Content`, `Metadata`, `Type`, `FilePath`, line/offset info) remains identical for IndexingCoordinator/BatchProcessor/embedding pipeline.

## New Folders/Files (exact paths)
Create under existing project root:
- `ChunkHound/Parsers/`
  - `ChunkHound/Parsers/IChunkParser.cs` (new abstraction)
  - `ChunkHound/Parsers/IParserFactory.cs` (new abstraction)
  - `ChunkHound/Parsers/ParserFactory.cs` (port of `chunkhound/parsers/parser_factory.py`)
  - `ChunkHound/Parsers/RecursiveChunkSplitter.cs` (full port of `chunkhound/parsers/chunk_splitter.py` + `chunkhound/parsers/universal_engine.py` with chunk-size-enforcement)
  - `ChunkHound/Parsers/YamlTemplateSanitizer.cs` (port of `chunkhound/parsers/yaml_template_sanitizer.py`)
- `ChunkHound/Parsers/Concrete/`
  - `ChunkHound/Parsers/Concrete/RapidYamlParser.cs` (port of `chunkhound/parsers/rapid_yaml_parser.py`)
  - `ChunkHound/Parsers/Concrete/VueChunkParser.cs` (port of `chunkhound/parsers/vue_parser.py`)
  - `ChunkHound/Parsers/Concrete/MarkdownParser.cs`
  - `ChunkHound/Parsers/Concrete/CodeChunkParser.cs`
  - `ChunkHound/Parsers/Concrete/UniversalTextParser.cs`

## Abstractions
- `IChunkParser.cs` (in `ChunkHound/Parsers/`):
  ```csharp
  public interface IChunkParser
  {
      bool CanHandle(string fileExtension);
      Task<IReadOnlyList<Chunk>> ParseAsync(string content, string filePath);
  }
  ```
- `IParserFactory.cs` (in `ChunkHound/Parsers/`):
  ```csharp
  public interface IParserFactory
  {
      IChunkParser GetParser(string fileExtension);
  }
  ```

## Core Logic
- `RecursiveChunkSplitter.cs`: Implements recursive splitting with semantic boundaries (headings, functions, sections) + strict max-chunk-size enforcement from `.chunkhound.json`. Takes pre-parsed chunks or raw text; outputs final `IReadOnlyList<Chunk>`.

## Updated Services/UniversalParser.cs (existing file)
Update `ChunkHound/Services/UniversalParser.cs` (minimal internal changes):
- Update constructor to accept `IParserFactory` and `RecursiveChunkSplitter` parameters (in addition to existing `ILogger<UniversalParser>` and `ILanguageConfigProvider`).
- Add private readonly fields:
  ```csharp
  private readonly IParserFactory _factory;
  private readonly RecursiveChunkSplitter _splitter;
  ```
- Keep the existing `_config` / `ILanguageConfigProvider` field untouched (strongly-typed, from .chunkhound.json).
- Keep all existing public methods/properties for zero breaking.
- Replace core method:
  ```csharp
  public async Task<IReadOnlyList<Chunk>> ParseFileAsync(string filePath)
  {
      // single string filePath input contract
      var content = await File.ReadAllTextAsync(filePath);
      var ext = Path.GetExtension(filePath).ToLowerInvariant();
      
      var parser = _factory.GetParser(ext);
      var initialChunks = await parser.ParseAsync(content, filePath);
      
      // reuse existing typed config (MaxChunkSize + Overlap)
      return _splitter.Split(initialChunks, _config.MaxChunkSize, _config.Overlap);
  }
  ```

## Exact DI Registration Location
In existing `ChunkHound/Program.cs` (inside `WebApplication.CreateBuilder` / `builder.Services` block, after any existing parser registrations):

```csharp
builder.Services.AddSingleton<IParserFactory, ParserFactory>();
builder.Services.AddSingleton<RecursiveChunkSplitter>();
// UniversalParser registration remains unchanged - now receives new deps internally
```

## Exact Wiring Location in the Flow
No changes to:
- `ChunkHound/Services/IndexingCoordinator.cs`
- `ChunkHound/Workers/BatchProcessor.cs`
- Any worker/coordinator that already calls `IUniversalParser.ParseFileAsync(filePath)` or equivalent.

Existing DI already supplies updated `UniversalParser`; output `IReadOnlyList<Chunk>` flows unchanged into embedding pipeline and `.chunkhound.json` config.

## Testing Notes
- Add tests to `ChunkHound.Tests/Parsers/` mirroring Python branch `split/chunk-size-enforcement`.
- Round-trip validation: same filePath → identical chunk structure/content as Python reference.
- Test suite: Markdown, YAML (sanitized), Vue, code files; enforce max size/overlap; edge cases (empty, huge files).
- Run full end-to-end indexing against existing embedding pipeline to confirm zero regression.

## Implementation Order for Roo Code / Grok Code Fast Orchestrator
1. Add NuGet packages + rebuild (`ChunkHound/ChunkHound.csproj`).
2. Update `ChunkHound/Core/ChunkType.cs`.
3. Create `ChunkHound/Parsers/IChunkParser.cs` + `IParserFactory.cs` + `ParserFactory.cs`.
4. Implement `ChunkHound/Parsers/RecursiveChunkSplitter.cs` (core port first).
5. Implement concrete parsers (`RapidYamlParser.cs`, `VueChunkParser.cs`, etc.) using exact Python reference files.
6. Add `YamlTemplateSanitizer.cs`.
7. Update `ChunkHound/Services/UniversalParser.cs`.
8. Add DI registration in `ChunkHound/Program.cs`.
9. Full integration test run (indexing + embedding).
10. Benchmark vs Python; optimize splitter if needed.

**This plan ensures 100% backward compatibility, pure managed .NET, and exact match to Python parsers.**
```