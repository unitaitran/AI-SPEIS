using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.PdfExtractorService;
using ai_speis_be.Services.GeminiAiParsingService;
using ai_speis_be.Services.BackgroundWorker;

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

        [HttpPost("test-background-parsing")]
        public async Task<IActionResult> TestBackgroundParsing(
            IFormFile file, 
            [FromServices] ApplicationDbContext dbContext,
            [FromServices] ICvParseQueue queue)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "File trống." });

            var tempPath = Path.GetTempFileName() + ".pdf";
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Tạo dummy user nếu chưa có để pass foreign key
            var testUser = await dbContext.Users.FirstOrDefaultAsync();
            if (testUser == null) return BadRequest("Không có user trong DB để tạo CVFile.");

            var cvFile = new CVFile
            {
                UserId = testUser.UserId,
                FileName = file.FileName,
                FilePath = tempPath,
                FileSize = file.Length,
                FileType = "application/pdf",
                Status = CVFileStatus.Pending,
                UploadedAt = System.DateTime.UtcNow
            };

            dbContext.CVFiles.Add(cvFile);
            await dbContext.SaveChangesAsync();

            // Đẩy vào background queue
            await queue.QueueCvParseAsync(new CvParseRequest(cvFile.CVFileId, tempPath));

            return Ok(new { Message = "Đã lưu DB và đưa vào queue xử lý ngầm.", CVFileId = cvFile.CVFileId });
        }

        [HttpGet("check-status/{cvFileId}")]
        public async Task<IActionResult> CheckStatus(int cvFileId, [FromServices] ApplicationDbContext dbContext)
        {
            var cvFile = await dbContext.CVFiles
                .FirstOrDefaultAsync(c => c.CVFileId == cvFileId);

            if (cvFile == null) return NotFound("Không tìm thấy CVFile");

            var profile = await dbContext.CVExtractedProfiles
                .Include(e => e.Skills)
                .Include(e => e.Projects)
                .FirstOrDefaultAsync(e => e.CVFileId == cvFileId);

            return Ok(new 
            { 
                CVFileId = cvFile.CVFileId,
                Status = cvFile.Status.ToString(),
                Profile = profile
            });
        }
    }
}
