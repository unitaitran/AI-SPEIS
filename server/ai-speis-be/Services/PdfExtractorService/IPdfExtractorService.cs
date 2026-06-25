using System.Threading.Tasks;

namespace ai_speis_be.Services.PdfExtractorService
{
    public interface IPdfExtractorService
    {
        Task<(bool Success, string? Text, string? Error)> ExtractTextFromPdfAsync(string filePath);
    }
}
