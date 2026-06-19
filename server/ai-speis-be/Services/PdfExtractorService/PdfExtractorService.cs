using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace ai_speis_be.Services.PdfExtractorService
{
    public class PdfExtractorService : IPdfExtractorService
    {
        public async Task<(bool Success, string? Text, string? Error)> ExtractTextFromPdfAsync(string filePath)
        {
            return await Task.Run<(bool Success, string? Text, string? Error)>(() =>
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        return (false, null, "File không tồn tại.");
                    }

                    using var pdfReader = new PdfReader(filePath);
                    using var pdfDocument = new PdfDocument(pdfReader);
                    var textBuilder = new StringBuilder();

                    for (int page = 1; page <= pdfDocument.GetNumberOfPages(); page++)
                    {
                        var strategy = new SimpleTextExtractionStrategy();
                        var currentText = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(page), strategy);
                        textBuilder.AppendLine(currentText);
                    }

                    string extractedText = textBuilder.ToString().Trim();

                    // Yêu cầu NFR: validate độ dài văn bản extract được (ít nhất 50 ký tự)
                    if (string.IsNullOrWhiteSpace(extractedText) || extractedText.Length < 50)
                    {
                        return (false, null, "Nội dung trích xuất từ PDF quá ngắn hoặc PDF chỉ chứa hình ảnh (không có text).");
                    }

                    return (true, extractedText, null);
                }
                catch (Exception ex)
                {
                    return (false, null, $"Lỗi khi trích xuất text từ PDF: {ex.Message}");
                }
            });
        }
    }
}
