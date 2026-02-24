namespace ChunkHound.Core;

public sealed record IndexingOptions
{
    public string TempPath { get; init; } = System.IO.Path.GetTempPath();
    // future: int MaxWorkers etc.
    public static IndexingOptions FromDict(Dictionary<string, object> dict) => throw new NotImplementedException();
}