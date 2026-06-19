using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ai_speis_be.Services.PdfExtractorService;
using ai_speis_be.Services.GeminiAiParsingService;

namespace ai_speis_be.Controllers
{
    // LƯU Ý: ĐÂY LÀ FILE CHỈ DÙNG ĐỂ TEST, VUI LÒNG XÓA ĐI TRƯỚC KHI COMMIT HOẶC LÊN PRODUCTION.
    [ApiController]
    [Route("api/[controller]")]
    public class TestPdfController : ControllerBase
    {
        private readonly IPdfExtractorService _pdfExtractor;
        private readonly IGeminiAiParsingService _geminiAiParsingService;

        public TestPdfController(IPdfExtractorService pdfExtractor, IGeminiAiParsingService geminiAiParsingService)
        {
            _pdfExtractor = pdfExtractor;
            _geminiAiParsingService = geminiAiParsingService;
        }

        [HttpPost("test-extract")]
        public async Task<IActionResult> TestExtractPdf(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "File trống." });

            var tempPath = Path.GetTempFileName() + ".pdf";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var result = await _pdfExtractor.ExtractTextFromPdfAsync(tempPath);
                
                if (result.Success)
                    return Ok(new { Text = result.Text, Length = result.Text?.Length });
                
                return BadRequest(new { Error = result.Error });
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
        }

        [HttpPost("test-ai-parsing")]
        public async Task<IActionResult> TestAiParsing(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "File trống." });

            var tempPath = Path.GetTempFileName() + ".pdf";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // 1. Trích xuất text từ PDF (Step 4)
                var extractResult = await _pdfExtractor.ExtractTextFromPdfAsync(tempPath);
                
                if (!extractResult.Success || string.IsNullOrWhiteSpace(extractResult.Text))
                    return BadRequest(new { Error = extractResult.Error ?? "Không thể trích xuất text." });
                
                // 2. Gọi Gemini AI để parse text (Step 5)
                var aiResult = await _geminiAiParsingService.ParseCvTextAsync(extractResult.Text);

                if (aiResult.Success)
                    return Ok(new { 
                        Data = aiResult.Data, 
                        RawAiResponse = aiResult.RawResponse 
                    });
                
                return StatusCode(500, new { Error = aiResult.Error, RawAiResponse = aiResult.RawResponse });
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
        }
    }
}
