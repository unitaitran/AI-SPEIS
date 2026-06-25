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

namespace ai_speis_be.Services.CVService
{
    public class CVService : ICVService
    {
        private readonly ICVRepository _cvRepository;
        private readonly IFileValidatorService _fileValidatorService;
        private readonly ICvParseQueue _cvParseQueue;
        private readonly ApplicationDbContext _dbContext;

        public CVService(
            ICVRepository cvRepository,
            IFileValidatorService fileValidatorService,
            ICvParseQueue cvParseQueue,
            ApplicationDbContext dbContext)
        {
            _cvRepository = cvRepository;
            _fileValidatorService = fileValidatorService;
            _cvParseQueue = cvParseQueue;
            _dbContext = dbContext;
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

                // 4.5. Soft delete previous active CV
                var activeCV = await _cvRepository.GetActiveCVByUserIdAsync(userId);
                if (activeCV != null)
                {
                    activeCV.Status = CVFileStatus.Archived;
                    activeCV.UpdatedAt = DateTime.Now;
                    await _cvRepository.UpdateCVAsync(activeCV);
                }

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

                return (true, null, MapToDto(savedCV));
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi hệ thống khi tải file: {ex.Message}", null);
            }
        }

        public async Task<IEnumerable<CVDto>> GetAllCVsAsync()
        {
            var cvs = await _cvRepository.GetAllCVAsync();
            return cvs.Select(MapToDto);
        }

        public async Task<CVDto?> GetCVByIdAsync(int id)
        {
            var cv = await _cvRepository.GetCVByIdAsync(id);
            return cv != null ? MapToDto(cv) : null;
        }

        public async Task<CVDto?> GetCVByUserIdAsync(int userId)
        {
            var cv = await _cvRepository.GetCVByUserIdAsync(userId);
            return cv != null ? MapToDto(cv) : null;
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteCVAsync(int id)
        {
            var cv = await _cvRepository.GetCVByIdAsync(id);
            if (cv == null)
            {
                return (false, "Không tìm thấy file CV.");
            }

            var cvDeleted = await _cvRepository.DeleteCVAsync(cv.CVFileId);
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
            cvFile.UpdatedAt = DateTime.Now;
            await _dbContext.SaveChangesAsync();

            // Push to background queue
            await _cvParseQueue.QueueCvParseAsync(new CvParseRequest(cvFileId, absolutePath));

            return (true, null);
        }

        public async Task<CvParseStatusResponse?> GetParseStatusAsync(int cvFileId)
        {
            var cvFile = await _dbContext.CVFiles.FindAsync(cvFileId);
            if (cvFile == null) return null;

            return new CvParseStatusResponse
            {
                CVFileId = cvFile.CVFileId,
                Status = cvFile.Status.ToString(),
                FileName = cvFile.FileName,
                UploadedAt = cvFile.UploadedAt
            };
        }

        public async Task<CvParsedDataResponse?> GetParsedDataAsync(int cvFileId)
        {
            var cvFile = await _dbContext.CVFiles.FindAsync(cvFileId);
            if (cvFile == null) return null;

            var profile = await _dbContext.CVExtractedProfiles
                .Include(e => e.Skills)
                .Include(e => e.Projects)
                .FirstOrDefaultAsync(e => e.CVFileId == cvFileId);

            if (profile == null) return null;

            return new CvParsedDataResponse
            {
                CVFileId = cvFile.CVFileId,
                Status = cvFile.Status.ToString(),
                RoleTarget = profile.RoleTarget,
                IsConfirmed = profile.IsConfirmed,
                CreatedAt = profile.CreatedAt,
                Education = string.IsNullOrEmpty(profile.Education) || profile.Education == "[]"
                    ? new List<EducationDto>()
                    : JsonSerializer.Deserialize<List<EducationDto>>(profile.Education, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<EducationDto>(),
                Experience = string.IsNullOrEmpty(profile.Experience) || profile.Experience == "[]"
                    ? new List<ExperienceDto>()
                    : JsonSerializer.Deserialize<List<ExperienceDto>>(profile.Experience, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ExperienceDto>(),
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

        public async Task<(bool Success, string? ErrorMessage)> ConfirmParsedDataAsync(int cvFileId, int userId, CvConfirmRequest request)
        {
            var cvFile = await _dbContext.CVFiles.FindAsync(cvFileId);
            if (cvFile == null)
                return (false, "Không tìm thấy file CV.");

            if (cvFile.UserId != userId)
                return (false, "Bạn không có quyền thao tác trên CV này.");

            if (cvFile.Status != CVFileStatus.ConfirmationRequired)
                return (false, $"CV đang ở trạng thái '{cvFile.Status}', không thể xác nhận.");

            // BR-27: Phải có ít nhất 1 skill
            if (request.Skills == null || request.Skills.Count == 0)
                return (false, "Phải có ít nhất 1 skill để xác nhận.");

            var profile = await _dbContext.CVExtractedProfiles
                .Include(e => e.Skills)
                .Include(e => e.Projects)
                .FirstOrDefaultAsync(e => e.CVFileId == cvFileId);

            if (profile == null)
                return (false, "Chưa có dữ liệu trích xuất cho CV này.");

            // Update profile with confirmed data
            profile.RoleTarget = request.RoleTarget;
            profile.Education = JsonSerializer.Serialize(request.Education);
            profile.Experience = JsonSerializer.Serialize(request.Experience);
            profile.IsConfirmed = true;
            profile.ConfirmedBy = userId;
            profile.ConfirmedAt = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;

            // Replace skills
            _dbContext.CVSkills.RemoveRange(profile.Skills);
            foreach (var skill in request.Skills)
            {
                profile.Skills.Add(new CVSkill
                {
                    SkillName = skill.SkillName,
                    Source = "USER",
                    Category = string.IsNullOrEmpty(skill.Category) ? "Other" : skill.Category,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Replace projects
            _dbContext.CVProjects.RemoveRange(profile.Projects);
            foreach (var project in request.Projects)
            {
                profile.Projects.Add(new CVProject
                {
                    ProjectName = project.ProjectName,
                    RoleDescription = project.RoleDescription,
                    TechnologyStack = project.TechnologyStack,
                    ProjectSummary = project.ProjectSummary,
                    Duration = project.Duration,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Update CV status
            cvFile.Status = CVFileStatus.Confirmed;
            cvFile.UpdatedAt = DateTime.Now;

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
