namespace Sentinel.Application;

public interface IDocumentExtractor
{
    Task<string> ExtractTextAsync(Stream stream, string fileName);
}