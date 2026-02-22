# Sample Markdown File for Benchmarking

This is a sample markdown file used for benchmarking the ChunkHound parsers.

## Section 1: Introduction

ChunkHound is a tool for parsing and chunking code files. It supports multiple programming languages and file formats.

### Subsection 1.1: Features

- **Parsing**: Supports various file types including Markdown, YAML, Vue, and code files
- **Chunking**: Intelligent splitting of content into manageable chunks
- **Embedding**: Integration with embedding providers for semantic search

## Section 2: Technical Details

The parser uses recursive chunking algorithms to maintain semantic boundaries while enforcing maximum chunk sizes.

### Code Example

```csharp
public class Example
{
    public void Method()
    {
        Console.WriteLine("Hello, World!");
    }
}
```

## Section 3: Configuration

Configuration is handled through `.chunkhound.json` files that specify parsing parameters.

### YAML Configuration Example

```yaml
maxChunkSize: 1000
overlap: 100
embedding:
  provider: "openai"
  model: "text-embedding-ada-002"
```

## Section 4: Performance

Performance benchmarks compare .NET implementation against Python reference implementation.

- Parse time
- Chunk count
- Embedding time
- Memory usage

## Conclusion

This concludes the sample markdown file for benchmarking purposes.