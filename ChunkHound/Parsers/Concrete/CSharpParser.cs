using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChunkHound.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using FileModel = ChunkHound.Core.File;

namespace ChunkHound.Parsers.Concrete;

/// <summary>
/// Parser for C# files using Roslyn syntax analysis.
/// Creates chunks for classes and methods with metadata.
/// </summary>
public class CSharpParser : IUniversalParser
{
    /// <summary>
    /// Parses a C# file into code chunks using Roslyn.
    /// </summary>
    /// <param name="file">The file to parse.</param>
    /// <returns>The parsed chunks.</returns>
    public async Task<List<Chunk>> ParseAsync(ChunkHound.Core.File file)
    {
        var content = await System.IO.File.ReadAllTextAsync(file.Path);
        var tree = CSharpSyntaxTree.ParseText(content, cancellationToken: System.Threading.CancellationToken.None);
        var root = await tree.GetRootAsync(System.Threading.CancellationToken.None);
        var chunks = new List<Chunk>();
        // Classes
        foreach (var cls in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var chunkContent = cls.ToFullString();
            var meta = ImmutableDictionary.CreateRange(new[] { KeyValuePair.Create("type", (object)"class"), KeyValuePair.Create("name", (object)cls.Identifier.ValueText) });
            chunks.Add(new Chunk(Guid.NewGuid().ToString(), 0, chunkContent, cls.GetLocation().GetLineSpan().StartLinePosition.Line, cls.GetLocation().GetLineSpan().EndLinePosition.Line, file.Language, ChunkType.Unknown, null, file.Path, meta));
        }
        // Methods
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var chunkContent = method.ToFullString();
            var meta = ImmutableDictionary.CreateRange(new[] { KeyValuePair.Create("type", (object)"method"), KeyValuePair.Create("name", (object)method.Identifier.ValueText), KeyValuePair.Create("return", (object)method.ReturnType.ToString()) });
            chunks.Add(new Chunk(Guid.NewGuid().ToString(), 0, chunkContent, method.GetLocation().GetLineSpan().StartLinePosition.Line, method.GetLocation().GetLineSpan().EndLinePosition.Line, file.Language, ChunkType.Unknown, null, file.Path, meta));
        }
        return chunks;
    }
}