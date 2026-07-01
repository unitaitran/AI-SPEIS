using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.JDRepo;
using ai_speis_be.Services.FileValidatorService;

namespace ai_speis_be.Services.JDService
{
    public class JDService : IJDService
    {
        private readonly IJDRepository _jdRepository;
        private readonly IFileValidatorService _fileValidatorService;
        private readonly ApplicationDbContext _context;

        public JDService(IJDRepository jdRepository, IFileValidatorService fileValidatorService, ApplicationDbContext context)
        {
            _jdRepository = jdRepository;
            _fileValidatorService = fileValidatorService;
            _context = context;
        }

        // ===================== DELETE =====================

        public async Task<(bool Success, string? ErrorMessage)> DeleteJDAsync(int id)
        {
            // Controller đã verify tồn tại và phân quyền → gọi thẳng repo, không query lại
            var deleted = await _jdRepository.DeleteJDAsync(id);
            if (deleted == null)
            {
                return (false, "Không tìm thấy JD");
            }
            return (true, null);
        }

        // ===================== GET =====================

        public async Task<PagedResultDto<JDDto>> GetAllJDsAsync(JDQueryParameters query)
        {
            var pagedJDFiles = await _jdRepository.GetAllJDAsync(query);
            var jdDtos = pagedJDFiles.Items.Select(MapToDto).ToList();
            return new PagedResultDto<JDDto>
            {
                Items = jdDtos,
                PageNumber = pagedJDFiles.PageNumber,
                PageSize = pagedJDFiles.PageSize,
                TotalItems = pagedJDFiles.TotalItems
            };
        }

        public async Task<JDDto?> GetJDByIdAsync(int id)
        {
            var jdFile = await _jdRepository.GetJDByIdAsync(id);
            return jdFile != null ? MapToDto(jdFile) : null;
        }

        public async Task<PagedResultDto<JDDto>> GetJDByUserIdAsync(int userId, JDQueryParameters query)
        {
            var pagedJDFiles = await _jdRepository.GetJDByUserIdAsync(userId, query);
            var jdDtos = pagedJDFiles.Items.Select(MapToDto).ToList();
            return new PagedResultDto<JDDto>
            {
                Items = jdDtos,
                PageNumber = pagedJDFiles.PageNumber,
                PageSize = pagedJDFiles.PageSize,
                TotalItems = pagedJDFiles.TotalItems
            };
        }

        // ===================== UPLOAD FILE =====================

        public async Task<(bool Success, string? ErrorMessage, JDDto? JDDto)> UploadJDAsync(int userId, IFormFile file)
        {
            var (isValid, validationError) = _fileValidatorService.ValidatePdf(file);
            if (!isValid)
            {
                return (false, validationError, null);
            }

            try
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "jds");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName).ToLower()}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                var jdFile = new JDFile
                {
                    UserId = userId,
                    InputType = JDInputType.File,
                    // RawText = null → background worker sẽ extract PDF và điền sau
                    FileName = file.FileName,
                    FilePath = $"/uploads/jds/{uniqueFileName}",
                    FileSize = file.Length,
                    FileType = file.ContentType,
                    Status = JDFileStatus.Pending,
                    UploadedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                var savedJD = await _jdRepository.AddJDAsync(jdFile);
                return (true, null, MapToDto(savedJD));
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi hệ thống khi tải file: {ex.Message}", null);
            }
        }

        // ===================== SUBMIT TEXT =====================

        public async Task<(bool Success, string? ErrorMessage, JDDto? JDDto)> SubmitJDTextAsync(int userId, string rawText)
        {
            try
            {
                var jdFile = new JDFile
                {
                    UserId = userId,
                    InputType = JDInputType.Text,
                    RawText = rawText.Trim(),
                    // FileName, FilePath, FileSize, FileType đều null — không có file
                    Status = JDFileStatus.Pending,
                    UploadedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                var saved = await _jdRepository.AddJDAsync(jdFile);
                return (true, null, MapToDto(saved));
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi hệ thống khi lưu JD: {ex.Message}", null);
            }
        }

        // ===================== HELPER =====================

        private JDDto MapToDto(JDFile jd)
        {
            return new JDDto
            {
                JDFileId = jd.JDFileId,
                UserId = jd.UserId,
                InputType = jd.InputType,
                RawText = jd.RawText,
                FileName = jd.FileName,
                FilePath = jd.FilePath,
                FileSize = jd.FileSize,
                FileType = jd.FileType,
                Status = jd.Status,
                UploadedAt = jd.UploadedAt,
                UpdatedAt = jd.UpdatedAt
            };
        }
    }
}
