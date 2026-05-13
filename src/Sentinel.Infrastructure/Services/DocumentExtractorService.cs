using Sentinel.Application;
using UglyToad.PdfPig;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;




namespace Sentinel.Infrastructure.Services;

public class DocumentExtractorService : IDocumentExtractor
{
    public async Task<string> ExtractTextAsync(Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf"  => await ExtractPdfAsync(stream),
            ".docx" => await ExtractDocxAsync(stream),
            _       => throw new NotSupportedException($"File type '{ext}' is not supported.")
        };
    }
  
    private static async Task<string> ExtractPdfAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.Position = 0;

        using var pdf = PdfDocument.Open(ms);
        return string.Join("\n", pdf.GetPages()
            .Select(page => string.Join(" ", page.GetWords().Select(w => w.Text))));
    }

    private static async Task<string> ExtractDocxAsync(Stream stream)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.Position = 0;

        using var doc = WordprocessingDocument.Open(ms, false);
        var paragraphs = doc.MainDocumentPart?.Document?.Body?.Elements<Paragraph>()
                         ?? Enumerable.Empty<Paragraph>();
        return string.Join("\n", paragraphs
            .Select(p => p.InnerText)
            .Where(t => !string.IsNullOrWhiteSpace(t)));

    }


}