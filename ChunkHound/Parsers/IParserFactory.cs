namespace ChunkHound.Parsers;

public interface IParserFactory
{
    IChunkParser GetParser(string fileExtension);
}