using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.JDRepo;
using ai_speis_be.Services.FileValidatorService;
using ai_speis_be.Services.BackgroundWorker;
using ai_speis_be.DTOs.JdParsing;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ai_speis_be.Services.GeminiAiParsingService;
using ai_speis_be.Helpers;
using ai_speis_be.Services.NotificationService;

namespace ai_speis_be.Services.JDService
{
    public class JDService : IJDService
    {
        private readonly IJDRepository _jdRepository;
        private readonly IFileValidatorService _fileValidatorService;
        private readonly ApplicationDbContext _context;
        private readonly IJdParseQueue _jdParseQueue;
        private readonly IGeminiAiParsingService _aiParsingService;
        private readonly INotificationEventPublisher _notificationPublisher;

        public JDService(IJDRepository jdRepository, IFileValidatorService fileValidatorService, ApplicationDbContext context, IJdParseQueue jdParseQueue, IGeminiAiParsingService aiParsingService, INotificationEventPublisher notificationPublisher)
        {
            _jdRepository = jdRepository;
            _fileValidatorService = fileValidatorService;
            _context = context;
            _jdParseQueue = jdParseQueue;
            _aiParsingService = aiParsingService;
            _notificationPublisher = notificationPublisher;
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
                await PublishUploadNotificationAsync(savedJD);
                return (true, null, MapToDto(savedJD));
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi hệ thống khi tải file: {ex.Message}", null);
            }
        }

        // ===================== SUBMIT TEXT =====================

        public async Task<(bool Success, string? ErrorMessage, JDDto? JDDto)> SubmitJDTextAsync(int userId, string fileName, string rawText)
        {
            try
            {
                var jdFile = new JDFile
                {
                    UserId = userId,
                    InputType = JDInputType.Text,
                    RawText = rawText.Trim(),
                    FileName = fileName,
                    // FilePath, FileSize, FileType đều null — không có file
                    Status = JDFileStatus.Pending,
                    UploadedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                var saved = await _jdRepository.AddJDAsync(jdFile);
                await PublishUploadNotificationAsync(saved);
                return (true, null, MapToDto(saved));
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi hệ thống khi lưu JD: {ex.Message}", null);
            }
        }

        private async Task PublishUploadNotificationAsync(JDFile jdFile)
        {
            try
            {
                await _notificationPublisher.PublishAsync(new NotificationEvent(
                    jdFile.UserId, NotificationRecipientRole.USER, NotificationType.JD_UPLOADED,
                    NotificationCategory.PROFILE, NotificationSeverity.SUCCESS, "Job description uploaded",
                    "Your job description was uploaded successfully and is ready to be processed.",
                    NotificationEntityType.JOB_DESCRIPTION, jdFile.JDFileId.ToString(), "/user/cv-management",
                    $"JD_UPLOADED:{jdFile.JDFileId}:{jdFile.UserId}", new { jdFileId = jdFile.JDFileId }));
            }
            catch { }
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

        // ===================== AI PARSING =====================

        public async Task<bool> TriggerParseAsync(int userId, int jdId)
        {
            var jdFile = await _context.JDFiles.FirstOrDefaultAsync(j => j.JDFileId == jdId && j.UserId == userId);
            if (jdFile == null) return false;

            if (jdFile.Status == JDFileStatus.Processing || jdFile.Status == JDFileStatus.Confirmed)
                return false; // Already parsing or done

            // Delete old profile if exists
            var oldProfile = await _context.JDExtractedProfiles.FirstOrDefaultAsync(p => p.JDFileId == jdId);
            if (oldProfile != null)
            {
                _context.JDExtractedProfiles.Remove(oldProfile);
            }

            jdFile.Status = JDFileStatus.Processing;
            jdFile.ErrorMessage = null;
            await _context.SaveChangesAsync();

            await _jdParseQueue.QueueJdForParsingAsync(jdId);
            return true;
        }

        public async Task<object?> GetParseStatusAsync(int userId, int jdId)
        {
            var jdFile = await _context.JDFiles.FirstOrDefaultAsync(j => j.JDFileId == jdId && j.UserId == userId);
            if (jdFile == null) return null;

            return new
            {
                Status = jdFile.Status.ToString(),
                ErrorMessage = jdFile.ErrorMessage
            };
        }

        public async Task<JdParsedDataResponse?> GetParsedDataAsync(int userId, int jdId)
        {
            var jdFile = await _context.JDFiles.FirstOrDefaultAsync(j => j.JDFileId == jdId && j.UserId == userId);
            if (jdFile == null) return null;

            var profile = await _context.JDExtractedProfiles.FirstOrDefaultAsync(p => p.JDFileId == jdId);

            return new JdParsedDataResponse
            {
                ExtractedProfileId = profile?.ExtractedProfileId ?? 0,
                JDFileId = jdFile.JDFileId,
                FileName = jdFile.FileName,
                RawText = jdFile.RawText,
                InputType = jdFile.InputType.ToString(),
                JobTitle = profile?.JobTitle,
                ExperienceLevel = profile?.ExperienceLevel,
                RoleTarget = profile?.RoleTarget,
                RequiredSkills = profile != null && !string.IsNullOrEmpty(profile.RequiredSkills) 
                    ? (JsonSerializer.Deserialize<List<string>>(profile.RequiredSkills) ?? new List<string>()) 
                    : new List<string>(),
                NiceToHaveSkills = profile != null && !string.IsNullOrEmpty(profile.NiceToHaveSkills) 
                    ? (JsonSerializer.Deserialize<List<string>>(profile.NiceToHaveSkills) ?? new List<string>()) 
                    : new List<string>(),
                Responsibilities = profile?.Responsibilities,
                CompanyCharacteristics = profile?.CompanyCharacteristics,
                ConfidenceScore = profile?.ConfidenceScore,
                WarningMessage = (profile != null && profile.ConfidenceScore < 0.80m) ? "Confidence score is low. Please verify the extracted data carefully." : null
            };
        }

        public async Task<bool> ConfirmParsedDataAsync(int userId, int jdId, JdConfirmRequest request)
        {
            var jdFile = await _context.JDFiles.FirstOrDefaultAsync(j => j.JDFileId == jdId && j.UserId == userId);
            if (jdFile == null) return false;

            var profile = await _context.JDExtractedProfiles.FirstOrDefaultAsync(p => p.JDFileId == jdId);
            if (profile == null) return false;

            if (!RoleValidationHelper.IsSupportedRole(request.RoleTarget, request.JobTitle)) return false;

            profile.JobTitle = request.JobTitle;
            profile.ExperienceLevel = request.ExperienceLevel;
            profile.RoleTarget = request.RoleTarget;
            profile.RequiredSkills = JsonSerializer.Serialize(request.RequiredSkills);
            profile.NiceToHaveSkills = JsonSerializer.Serialize(request.NiceToHaveSkills);
            profile.Responsibilities = request.Responsibilities;
            profile.CompanyCharacteristics = request.CompanyCharacteristics;
            
            profile.IsConfirmed = true;
            profile.ConfirmedBy = userId;
            profile.ConfirmedAt = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;

            jdFile.Status = JDFileStatus.Confirmed;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<CvJdMatchResultResponse?> MatchCvToJdAsync(int userId, int jdId, int cvId)
        {
            // 1. Verify CV and JD belong to User
            var cvFile = await _context.CVFiles.FirstOrDefaultAsync(c => c.CVFileId == cvId && c.UserId == userId);
            var jdFile = await _context.JDFiles.FirstOrDefaultAsync(j => j.JDFileId == jdId && j.UserId == userId);

            if (cvFile == null || jdFile == null) return null;

            // 2. Check if a cached result already exists
            var existing = await _context.FastCheckResults
                .FirstOrDefaultAsync(fc => fc.UserId == userId && fc.CVFileId == cvId && fc.JDFileId == jdId);

            if (existing != null)
            {
                return new CvJdMatchResultResponse
                {
                    Success = true,
                    MatchScore = existing.MatchScore,
                    SuitabilityLevel = existing.SuitabilityLevel,
                    MatchingSkills = DeserializeJsonList(existing.MatchingSkillsJson),
                    MissingSkills = DeserializeJsonList(existing.MissingSkillsJson),
                    Advice = existing.Advice
                };
            }

            // 3. Fetch Parsed Profiles
            var cvProfile = await _context.CVExtractedProfiles
                .Include(p => p.Skills)
                .Include(p => p.Projects)
                .FirstOrDefaultAsync(p => p.CVFileId == cvId);

            var jdProfile = await _context.JDExtractedProfiles.FirstOrDefaultAsync(p => p.JDFileId == jdId);

            if (cvProfile == null || jdProfile == null) return null;

            // 4. Serialize into JSON for AI Context
            var cvJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                cvProfile.RoleTarget,
                cvProfile.OverallAssessment,
                cvProfile.Strengths,
                cvProfile.Weaknesses,
                cvProfile.Experience,
                cvProfile.Education,
                Skills = cvProfile.Skills.Select(s => s.SkillName).ToList(),
                Projects = cvProfile.Projects.Select(p => p.ProjectSummary).ToList()
            });

            var jdJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                jdProfile.JobTitle,
                jdProfile.ExperienceLevel,
                jdProfile.RequiredSkills,
                jdProfile.NiceToHaveSkills,
                jdProfile.Responsibilities,
                jdProfile.CompanyCharacteristics
            });

            // 5. Call AI Matching
            var (success, result, raw, error) = await _aiParsingService.EvaluateCvAgainstJdAsync(cvJson, jdJson);
            
            if (!success || result == null)
            {
                return new CvJdMatchResultResponse
                {
                    Success = false,
                    ErrorMessage = error ?? "Lỗi phân tích AI."
                };
            }

            // 6. Save result to database
            var fastCheckEntity = new FastCheckResult
            {
                UserId = userId,
                CVFileId = cvId,
                JDFileId = jdId,
                MatchScore = result.MatchScore,
                SuitabilityLevel = result.SuitabilityLevel,
                MatchingSkillsJson = System.Text.Json.JsonSerializer.Serialize(result.MatchingSkills),
                MissingSkillsJson = System.Text.Json.JsonSerializer.Serialize(result.MissingSkills),
                Advice = result.Advice,
                RawAiResponseJson = raw,
                CreatedAt = DateTime.UtcNow
            };

            _context.FastCheckResults.Add(fastCheckEntity);
            await _context.SaveChangesAsync();

            return result;
        }

        public async Task<List<FastCheckResultDto>> GetFastCheckResultsAsync(int userId)
        {
            var results = await _context.FastCheckResults
                .Where(fc => fc.UserId == userId)
                .OrderByDescending(fc => fc.CreatedAt)
                .ToListAsync();

            return results.Select(fc => new FastCheckResultDto
            {
                FastCheckResultId = fc.FastCheckResultId,
                CVFileId = fc.CVFileId,
                JDFileId = fc.JDFileId,
                MatchScore = fc.MatchScore,
                SuitabilityLevel = fc.SuitabilityLevel,
                MatchingSkills = DeserializeJsonList(fc.MatchingSkillsJson),
                MissingSkills = DeserializeJsonList(fc.MissingSkillsJson),
                Advice = fc.Advice,
                CreatedAt = fc.CreatedAt,
                Success = true
            }).ToList();
        }

        private static List<string> DeserializeJsonList(string json)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
