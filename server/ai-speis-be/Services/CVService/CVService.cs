using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using ai_speis_be.DTOs.CvParsing;
using ai_speis_be.Repositories.CVRepo;
using ai_speis_be.Services.FileValidatorService;
using ai_speis_be.Services.BackgroundWorker;
using ai_speis_be.Helpers;
using ai_speis_be.Services.NotificationService;

namespace ai_speis_be.Services.CVService
{
    public class CVService : ICVService
    {
        private readonly ICVRepository _cvRepository;
        private readonly IFileValidatorService _fileValidatorService;
        private readonly ICvParseQueue _cvParseQueue;
        private readonly ApplicationDbContext _dbContext;
        private readonly INotificationEventPublisher _notificationPublisher;

        public CVService(
            ICVRepository cvRepository,
            IFileValidatorService fileValidatorService,
            ICvParseQueue cvParseQueue,
            ApplicationDbContext dbContext,
            INotificationEventPublisher notificationPublisher)
        {
            _cvRepository = cvRepository;
            _fileValidatorService = fileValidatorService;
            _cvParseQueue = cvParseQueue;
            _dbContext = dbContext;
            _notificationPublisher = notificationPublisher;
        }

        public async Task<(bool Success, string? ErrorMessage, CVDto? CVDto)> UploadCVAsync(int userId, IFormFile file)
        {
            // 1. Validate file using FileValidatorService
            var (isValid, validationError) = _fileValidatorService.ValidatePdf(file);
            if (!isValid)
            {
                return (false, validationError, null);
            }

            try
            {
                // 2. Prepare upload folder inside wwwroot
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "cvs");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // 3. Generate a unique file name to avoid collisions
                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName).ToLower()}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // 4. Save file to disk
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                // 4.5. Soft delete ALL previous active CVs 
                await _cvRepository.ArchiveAllActiveCVsByUserIdAsync(userId);

                // 5. Save metadata to database
                var cvFile = new CVFile
                {
                    UserId = userId,
                    FileName = file.FileName, // Keep original file name for display
                    FilePath = $"/uploads/cvs/{uniqueFileName}", // Web-accessible relative path
                    FileSize = file.Length,
                    FileType = file.ContentType,
                    Status = CVFileStatus.Pending,
                    UploadedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                var savedCV = await _cvRepository.AddCVAsync(cvFile);
                await PublishUploadNotificationAsync(savedCV);

                return (true, null, MapToDto(savedCV));
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi hệ thống khi tải file: {ex.Message}", null);
            }
        }

        private async Task PublishUploadNotificationAsync(CVFile cvFile)
        {
            try
            {
                await _notificationPublisher.PublishAsync(new NotificationEvent(
                    cvFile.UserId, NotificationRecipientRole.USER, NotificationType.CV_UPLOADED,
                    NotificationCategory.PROFILE, NotificationSeverity.SUCCESS, "CV uploaded",
                    "Your CV was uploaded successfully and is ready to be processed.",
                    NotificationEntityType.CV, cvFile.CVFileId.ToString(), "/user/cv-management",
                    $"CV_UPLOADED:{cvFile.CVFileId}:{cvFile.UserId}", new { cvFileId = cvFile.CVFileId }));
            }
            catch { }
        }

        public async Task<PagedResultDto<CVDto>> GetAllCVsAsync(CVQueryParameters query)
        {
            // 1. Gọi Repo để lấy danh sách Entity phân trang
            var pagedCVFiles = await _cvRepository.GetAllCVAsync(query);
            // 2. Map từng Entity CVFile sang CVDto
            var cvDtos = pagedCVFiles.Items.Select(MapToDto).ToList();
            // 3. Trả về DTO phân trang
            return new PagedResultDto<CVDto>
            {
                Items = cvDtos,
                PageNumber = pagedCVFiles.PageNumber,
                PageSize = pagedCVFiles.PageSize,
                TotalItems = pagedCVFiles.TotalItems
            };
        }

        public async Task<CVDto?> GetCVByIdAsync(int id)
        {
            var cv = await _cvRepository.GetCVByIdAsync(id);
            return cv != null ? MapToDto(cv) : null;
        }

        public async Task<PagedResultDto<CVDto>> GetCVByUserIdAsync(int userId, CVQueryParameters query)
        {
            var pagedCVs = await _cvRepository.GetCVByUserIdAsync(userId, query);
            var cvDtos = pagedCVs.Items.Select(MapToDto).ToList();

            return new PagedResultDto<CVDto>
            {
                Items = cvDtos,
                PageNumber = pagedCVs.PageNumber,
                PageSize = pagedCVs.PageSize,
                TotalItems = pagedCVs.TotalItems
            };
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteCVAsync(int id)
        {
            var cv = await _cvRepository.GetCVByIdAsync(id);
            if (cv == null)
            {
                return (false, "Không tìm thấy file CV.");
            }

            var cvDeleted = await _cvRepository.DeleteCVAsync(cv.CVFileId);
            if (!cvDeleted)
            {
                return (false, "Không thể xóa file CV. Vui lòng thử lại.");
            }
            return (true, null);
        }

        public async Task<CVDto?> GetMyCVAsync(int userId)
        { 
            var cv = await _cvRepository.GetActiveCVByUserIdAsync(userId);
            return cv != null ? MapToDto(cv) : null;
        }

        // ===================== CV PARSING METHODS (Step 7) =====================

        public async Task<(bool Success, string? ErrorMessage)> TriggerParseAsync(int cvFileId, int userId)
        {
            var cvFile = await _dbContext.CVFiles.FindAsync(cvFileId);
            if (cvFile == null)
                return (false, "Không tìm thấy file CV.");

            if (cvFile.UserId != userId)
                return (false, "Bạn không có quyền thao tác trên CV này.");

            if (cvFile.Status != CVFileStatus.Pending && cvFile.Status != CVFileStatus.AnalysisFailed)
                return (false, $"CV đang ở trạng thái '{cvFile.Status}', không thể parse lại.");

            // Build absolute path from relative web path
            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", cvFile.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
                return (false, "File CV không tồn tại trên server.");

            // Update status to Processing
            cvFile.Status = CVFileStatus.Processing;
            cvFile.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            // Push to background queue
            await _cvParseQueue.QueueCvParseAsync(new CvParseRequest(cvFileId, absolutePath));

            return (true, null);
        }

        public async Task<CvParseStatusResponse?> GetParseStatusAsync(int cvFileId, int userId)
        {
            var cvFile = await _dbContext.CVFiles.FindAsync(cvFileId);
            if (cvFile == null || cvFile.UserId != userId) return null;

            string? errorMessage = null;
            if (cvFile.Status == CVFileStatus.AnalysisFailed || cvFile.Status == CVFileStatus.ConfirmationRequired)
            {
                var profile = await _dbContext.CVExtractedProfiles
                    .Where(p => p.CVFileId == cvFileId)
                    .Select(p => new { p.ErrorMessage })
                    .FirstOrDefaultAsync();
                
                errorMessage = profile?.ErrorMessage;
            }

            return new CvParseStatusResponse
            {
                CVFileId = cvFile.CVFileId,
                Status = cvFile.Status.ToString(),
                FileName = cvFile.FileName,
                UploadedAt = cvFile.UploadedAt,
                ErrorMessage = errorMessage
            };
        }

        public async Task<CvParsedDataResponse?> GetParsedDataAsync(int cvFileId, int userId)
        {
            var cvFile = await _dbContext.CVFiles.FindAsync(cvFileId);
            if (cvFile == null || cvFile.UserId != userId) return null;

            var profile = await _dbContext.CVExtractedProfiles
                .Include(e => e.Skills)
                .Include(e => e.Projects)
                .FirstOrDefaultAsync(e => e.CVFileId == cvFileId);

            if (profile == null) return null;

            // Safe JSON deserialization with try-catch
            List<EducationDto> education;
            List<ExperienceDto> experience;
            try
            {
                var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                education = string.IsNullOrEmpty(profile.Education) || profile.Education == "[]"
                    ? new List<EducationDto>()
                    : JsonSerializer.Deserialize<List<EducationDto>>(profile.Education, jsonOpts) ?? new List<EducationDto>();
                experience = string.IsNullOrEmpty(profile.Experience) || profile.Experience == "[]"
                    ? new List<ExperienceDto>()
                    : JsonSerializer.Deserialize<List<ExperienceDto>>(profile.Experience, jsonOpts) ?? new List<ExperienceDto>();
            }
            catch (JsonException)
            {
                education = new List<EducationDto>();
                experience = new List<ExperienceDto>();
            }

            return new CvParsedDataResponse
            {
                CVFileId = cvFile.CVFileId,
                Status = cvFile.Status.ToString(),
                RoleTarget = profile.RoleTarget,
                IsConfirmed = profile.IsConfirmed,
                CreatedAt = profile.CreatedAt,
                OverallAssessment = profile.OverallAssessment,
                Strengths = profile.Strengths,
                Weaknesses = profile.Weaknesses,
                ConfidenceScore = profile.ConfidenceScore,
                ErrorMessage = profile.ErrorMessage,
                Education = education,
                Experience = experience,
                Skills = profile.Skills.Select(s => new CvSkillResponse
                {
                    CVSkillId = s.CVSkillId,
                    SkillName = s.SkillName,
                    Source = s.Source,
                    Category = s.Category
                }).ToList(),
                Projects = profile.Projects.Select(p => new CvProjectResponse
                {
                    CVProjectId = p.CVProjectId,
                    ProjectName = p.ProjectName,
                    RoleDescription = p.RoleDescription,
                    TechnologyStack = p.TechnologyStack,
                    ProjectSummary = p.ProjectSummary,
                    Duration = p.Duration
                }).ToList()
            };
        }

        private static readonly HashSet<string> ValidCategories = new(StringComparer.OrdinalIgnoreCase)
            { "Language", "Framework", "Database", "Tool", "Cloud", "Other" };

        public async Task<(bool Success, string? ErrorMessage)> ConfirmParsedDataAsync(int cvFileId, int userId, CvConfirmRequest request)
        {
            var cvFile = await _dbContext.CVFiles.FindAsync(cvFileId);
            if (cvFile == null)
                return (false, "Không tìm thấy file CV.");

            if (cvFile.UserId != userId)
                return (false, "Bạn không có quyền thao tác trên CV này.");

            if (cvFile.Status != CVFileStatus.ConfirmationRequired)
                return (false, $"CV đang ở trạng thái '{cvFile.Status}', không thể xác nhận.");

            // === VALIDATION ===
            if (string.IsNullOrWhiteSpace(request.RoleTarget))
                return (false, "Vị trí ứng tuyển (RoleTarget) không được để trống.");

            if (request.Skills == null || request.Skills.Count == 0)
                return (false, "Phải có ít nhất 1 skill để xác nhận.");

            // Validate từng skill
            foreach (var skill in request.Skills)
            {
                if (string.IsNullOrWhiteSpace(skill.SkillName))
                    return (false, "Tên skill không được để trống.");
                if (!string.IsNullOrEmpty(skill.Category) && !ValidCategories.Contains(skill.Category))
                    return (false, $"Category '{skill.Category}' không hợp lệ. Chỉ chấp nhận: Language, Framework, Database, Tool, Cloud, Other.");
            }

            // Validate roleTarget
            if (!RoleValidationHelper.IsSupportedRole(request.RoleTarget))
            {
                return (false, RoleValidationHelper.UnsupportedRoleErrorMessage);
            }

            // Validate từng project
            if (request.Projects != null)
            {
                foreach (var project in request.Projects)
                {
                    if (string.IsNullOrWhiteSpace(project.ProjectName))
                        return (false, "Tên project không được để trống.");
                }
            }

            var profile = await _dbContext.CVExtractedProfiles
                .Include(e => e.Skills)
                .Include(e => e.Projects)
                .FirstOrDefaultAsync(e => e.CVFileId == cvFileId);

            if (profile == null)
                return (false, "Chưa có dữ liệu trích xuất cho CV này.");

            // Update profile with confirmed data
            profile.RoleTarget = request.RoleTarget.Trim();
            profile.Education = JsonSerializer.Serialize(request.Education);
            profile.Experience = JsonSerializer.Serialize(request.Experience);
            profile.IsConfirmed = true;
            profile.ConfirmedBy = userId;
            profile.ConfirmedAt = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;

            // Replace skills (auto-deduplicate by SkillName, case-insensitive)
            _dbContext.CVSkills.RemoveRange(profile.Skills);
            var seenSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var skill in request.Skills)
            {
                var trimmedName = skill.SkillName.Trim();
                if (seenSkills.Add(trimmedName)) // Only add if not duplicate
                {
                    profile.Skills.Add(new CVSkill
                    {
                        SkillName = trimmedName,
                        Source = "USER",
                        Category = string.IsNullOrEmpty(skill.Category) ? "Other" : skill.Category,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            // Replace projects
            _dbContext.CVProjects.RemoveRange(profile.Projects);
            if (request.Projects != null)
            {
                foreach (var project in request.Projects)
                {
                    profile.Projects.Add(new CVProject
                    {
                        ProjectName = project.ProjectName.Trim(),
                        RoleDescription = project.RoleDescription,
                        TechnologyStack = project.TechnologyStack,
                        ProjectSummary = project.ProjectSummary,
                        Duration = project.Duration,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            // Update CV status
            cvFile.Status = CVFileStatus.Confirmed;
            cvFile.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return (true, null);
        }

        private CVDto MapToDto(CVFile cv)
        {
            return new CVDto
            {
                CVFileId = cv.CVFileId,
                UserId = cv.UserId,
                FileName = cv.FileName,
                FilePath = cv.FilePath,
                FileSize = cv.FileSize,
                FileType = cv.FileType,
                Status = cv.Status,
                UploadedAt = cv.UploadedAt,
                UpdatedAt = cv.UpdatedAt
            };
        }
    }
}
