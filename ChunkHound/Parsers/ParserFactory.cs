using System.Collections.Generic;
using ChunkHound.Core;

namespace ChunkHound.Parsers;

/// <summary>
/// Factory for creating chunk parsers based on file extensions.
/// This is a port of the Python parser_factory.py, adapted for C# with dependency injection.
/// </summary>
public class ParserFactory : IParserFactory
{
    private readonly IReadOnlyList<IChunkParser> _parsers;

    /// <summary>
    /// Initializes a new instance of the ParserFactory.
    /// </summary>
    /// <param name="parsers">Collection of available chunk parsers injected via DI.</param>
    public ParserFactory(IEnumerable<IChunkParser> parsers)
    {
        _parsers = parsers.ToList();
    }

    /// <summary>
    /// Gets the appropriate parser for the given file extension.
    /// </summary>
    /// <param name="fileExtension">The file extension (e.g., ".cs", ".vue").</param>
    /// <returns>The chunk parser that can handle the extension.</returns>
    /// <exception cref="NotSupportedException">Thrown when no parser can handle the extension.</exception>
    public IChunkParser GetParser(string fileExtension)
    {
        var ext = fileExtension.ToLowerInvariant();
        foreach (var parser in _parsers)
        {
            if (parser.CanHandle(ext))
            {
                return parser;
            }
        }
        throw new NotSupportedException($"No parser available for file extension '{fileExtension}'.");
    }
}