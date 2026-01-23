using ChunkHound.Core;

namespace ChunkHound.Tests.Helpers;

/// <summary>
/// Test data generator for creating realistic test files and chunks.
/// </summary>
public static class TestDataGenerator
{
    private static readonly Language[] _languages = { Language.CSharp, Language.Python, Language.JavaScript };

    /// <summary>
    /// Generates a list of test files with realistic code snippets.
    /// </summary>
    /// <param name="count">Number of files to generate.</param>
    /// <returns>List of generated files.</returns>
    public static List<Core.File> GenerateTestFiles(int count = 1000)
    {
        var files = new List<Core.File>();

        for (int i = 0; i < count; i++)
        {
            var language = _languages[i % _languages.Length];
            var path = $"src/test/file_{i}.{language.GetExtension()}";
            var content = GenerateCodeSnippet(language, i);
            var hash = Core.Utilities.HashUtility.ComputeContentHash(content);

            files.Add(new Core.File(
                path: path,
                mtime: DateTimeOffset.Now.ToUnixTimeSeconds(),
                language: language,
                sizeBytes: content.Length,
                contentHash: hash
            ));
        }

        return files;
    }

    /// <summary>
    /// Generates test chunks from a file.
    /// Creates mock chunks since UniversalParser is not yet implemented.
    /// </summary>
    /// <param name="file">The file to generate chunks for.</param>
    /// <param name="chunkCount">Number of chunks to generate.</param>
    /// <returns>List of generated chunks.</returns>
    public static List<Core.Chunk> GenerateTestChunks(Core.File file, int chunkCount = 5)
    {
        var chunks = new List<Core.Chunk>();
        var lines = file.Path.Contains("test") ? GenerateMockCodeLines(file.Language, chunkCount * 10) : new[] { "mock code" };
        var totalLines = lines.Length;

        for (int i = 0; i < chunkCount; i++)
        {
            var startLine = i * (totalLines / chunkCount) + 1;
            var endLine = Math.Min((i + 1) * (totalLines / chunkCount), totalLines);
            var code = string.Join("\n", lines.Skip(startLine - 1).Take(endLine - startLine + 1));

            var chunk = new Core.Chunk(
                symbol: $"Function{i}",
                startLine: startLine,
                endLine: endLine,
                code: code,
                chunkType: ChunkType.Function,
                fileId: file.Id ?? 0,
                language: file.Language,
                filePath: file.Path
            );

            chunks.Add(chunk);
        }

        return chunks;
    }

    /// <summary>
    /// Generates a realistic code snippet for the given language.
    /// </summary>
    private static string GenerateCodeSnippet(Language language, int index)
    {
        return language switch
        {
            Language.CSharp => $@"
public class Class{index}
{{
    private readonly int _value;

    public Class{index}(int value)
    {{
        _value = value;
    }}

    public int GetValue()
    {{
        return _value;
    }}

    public void Process()
    {{
        Console.WriteLine($""Processing {{_value}}"");
    }}
}}
",
            Language.Python => $@"
class Class{index}:
    def __init__(self, value):
        self._value = value

    def get_value(self):
        return self._value

    def process(self):
        print(f""Processing {{self._value}}"")
",
            Language.JavaScript => $@"
class Class{index} {{
    constructor(value) {{
        this._value = value;
    }}

    getValue() {{
        return this._value;
    }}

    process() {{
        console.log(`Processing ${{this._value}}`);
    }}
}}
",
            _ => $"// Mock code for {language.Value()} {index}"
        };
    }

    /// <summary>
    /// Generates mock code lines for chunking.
    /// </summary>
    private static string[] GenerateMockCodeLines(Language language, int lineCount)
    {
        var lines = new List<string>();
        for (int i = 0; i < lineCount; i++)
        {
            lines.Add($"{language.Value()} line {i + 1}");
        }
        return lines.ToArray();
    }


}