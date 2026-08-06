using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.GeminiAiParsingService;
using ai_speis_be.Services.PdfExtractorService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ai_speis_be.Helpers;
using ai_speis_be.Services.NotificationService;

namespace ai_speis_be.Services.BackgroundWorker
{
    public class CvParsingBackgroundService : BackgroundService
    {
        private readonly ICvParseQueue _taskQueue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CvParsingBackgroundService> _logger;

        public CvParsingBackgroundService(
            ICvParseQueue taskQueue,
            IServiceScopeFactory scopeFactory,
            ILogger<CvParsingBackgroundService> logger)
        {
            _taskQueue = taskQueue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CV Parsing Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var request = await _taskQueue.DequeueAsync(stoppingToken);

                    _logger.LogInformation("Processing CV Parsing request for CVFileId: {CVFileId}", request.CVFileId);

                    await ProcessCvAsync(request, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Catch exception when stoppingToken is triggered
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing CV Parsing task.");
                }
            }

            _logger.LogInformation("CV Parsing Background Service is stopping.");
        }

        private async Task ProcessCvAsync(CvParseRequest request, CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pdfExtractor = scope.ServiceProvider.GetRequiredService<IPdfExtractorService>();
            var geminiParsing = scope.ServiceProvider.GetRequiredService<IGeminiAiParsingService>();

            var cvFile = await dbContext.CVFiles.FindAsync(new object[] { request.CVFileId }, stoppingToken);
            if (cvFile == null)
            {
                _logger.LogWarning("CVFile with ID {CVFileId} not found.", request.CVFileId);
                return;
            }

            try
            {
                // 1. Trích xuất text từ PDF
                var extractResult = await pdfExtractor.ExtractTextFromPdfAsync(request.FilePath);
                if (!extractResult.Success || string.IsNullOrWhiteSpace(extractResult.Text))
                {
                    await SetAnalysisFailedAsync(dbContext, cvFile, "Không thể trích xuất nội dung từ file PDF.", stoppingToken);
                    _logger.LogWarning("Failed to extract text from PDF: {FilePath}. Error: {Error}", request.FilePath, extractResult.Error);
                    return;
                }

                // 2. Pre-AI check: Nội dung quá ngắn → không phải CV có nội dung
                if (extractResult.Text.Trim().Length < 50)
                {
                    await SetAnalysisFailedAsync(dbContext, cvFile, "File PDF không chứa đủ nội dung để phân tích (ít hơn 50 ký tự).", stoppingToken);
                    _logger.LogWarning("PDF text too short for CVFileId: {CVFileId} ({Length} chars)", request.CVFileId, extractResult.Text.Trim().Length);
                    return;
                }

                // 3. Gọi AI: Classify + Score + Assess + Parse (1 lần gọi duy nhất)
                var aiResult = await geminiParsing.ParseCvTextAsync(extractResult.Text);
                if (!aiResult.Success || aiResult.Data == null)
                {
                    await SetAnalysisFailedAsync(dbContext, cvFile, $"Lỗi khi gọi AI phân tích: {aiResult.Error}", stoppingToken);
                    _logger.LogWarning("Failed to parse CV with Gemini. Error: {Error}", aiResult.Error);
                    return;
                }

                var parsedData = aiResult.Data;

                // 4. Xóa profile cũ nếu re-parse (tránh unique constraint violation)
                var existingProfile = await dbContext.CVExtractedProfiles
                    .Include(e => e.Skills)
                    .Include(e => e.Projects)
                    .FirstOrDefaultAsync(e => e.CVFileId == cvFile.CVFileId, stoppingToken);

                if (existingProfile != null)
                {
                    dbContext.CVSkills.RemoveRange(existingProfile.Skills);
                    dbContext.CVProjects.RemoveRange(existingProfile.Projects);
                    dbContext.CVExtractedProfiles.Remove(existingProfile);
                    await dbContext.SaveChangesAsync(stoppingToken);
                }

                // 5. Tạo profile mới với AI assessment
                var extractedProfile = new CVExtractedProfile
                {
                    CVFileId = cvFile.CVFileId,
                    RoleTarget = parsedData.RoleTarget,
                    Education = JsonSerializer.Serialize(parsedData.Education),
                    Experience = JsonSerializer.Serialize(parsedData.Experience),
                    RawAiOutput = aiResult.RawResponse,
                    OverallAssessment = parsedData.OverallAssessment,
                    Strengths = parsedData.Strengths,
                    Weaknesses = parsedData.Weaknesses,
                    ConfidenceScore = parsedData.CvConfidenceScore,
                    IsConfirmed = false,
                    CreatedAt = DateTime.UtcNow
                };

                // 6. Threshold Routing
                if (!parsedData.IsValidCv || parsedData.CvConfidenceScore < 0.50m)
                {
                    // REJECT: Không phải CV
                    extractedProfile.ErrorMessage = parsedData.InvalidReason ?? "File tải lên không phải là CV/resume hợp lệ.";
                    cvFile.Status = CVFileStatus.AnalysisFailed;
                    dbContext.CVExtractedProfiles.Add(extractedProfile);
                    await dbContext.SaveChangesAsync(stoppingToken);
                    await PublishCvProcessingFailedAsync(cvFile, stoppingToken);
                    _logger.LogWarning("CVFileId {CVFileId} rejected: confidence={Score}, reason={Reason}",
                        request.CVFileId, parsedData.CvConfidenceScore, extractedProfile.ErrorMessage);
                    return;
                }

                // 6.5. Role Validation: Reject CVs with unsupported roles
                if (!RoleValidationHelper.IsSupportedRole(parsedData.RoleTarget))
                {
                    extractedProfile.ErrorMessage = RoleValidationHelper.UnsupportedRoleErrorMessage;
                    cvFile.Status = CVFileStatus.AnalysisFailed;
                    dbContext.CVExtractedProfiles.Add(extractedProfile);
                    await dbContext.SaveChangesAsync(stoppingToken);
                    await PublishCvProcessingFailedAsync(cvFile, stoppingToken);
                    _logger.LogWarning("CVFileId {CVFileId} rejected: Role '{RoleTarget}' is not supported.",
                        request.CVFileId, parsedData.RoleTarget);
                    return;
                }

                if (parsedData.CvConfidenceScore < 0.80m)
                {
                    // WARNING: Parse nhưng cảnh báo
                    extractedProfile.ErrorMessage = "⚠️ Hệ thống không chắc chắn đây là CV. Vui lòng kiểm tra kỹ dữ liệu trích xuất.";
                    _logger.LogInformation("CVFileId {CVFileId} parsed with warning: confidence={Score}",
                        request.CVFileId, parsedData.CvConfidenceScore);
                }

                // 7. Map Skills & Projects (chỉ khi confidence >= 0.50)
                foreach (var skill in parsedData.Skills)
                {
                    if (!string.IsNullOrWhiteSpace(skill.SkillName))
                    {
                        extractedProfile.Skills.Add(new CVSkill
                        {
                            SkillName = skill.SkillName.Trim(),
                            Source = string.IsNullOrEmpty(skill.Source) ? "AI" : skill.Source,
                            Category = string.IsNullOrEmpty(skill.Category) ? "Other" : skill.Category,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                foreach (var project in parsedData.Projects)
                {
                    if (!string.IsNullOrWhiteSpace(project.ProjectName))
                    {
                        extractedProfile.Projects.Add(new CVProject
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

                dbContext.CVExtractedProfiles.Add(extractedProfile);

                // 8. Đổi trạng thái CV thành ConfirmationRequired
                cvFile.Status = CVFileStatus.ConfirmationRequired;
                cvFile.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(stoppingToken);

                _logger.LogInformation("Successfully processed CVFileId: {CVFileId} (confidence={Score}, skills={SkillCount}, projects={ProjectCount})",
                    request.CVFileId, parsedData.CvConfidenceScore, extractedProfile.Skills.Count, extractedProfile.Projects.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing CVFileId: {CVFileId}", request.CVFileId);
                await SetAnalysisFailedAsync(dbContext, cvFile, $"Lỗi hệ thống không mong muốn: {ex.Message}", stoppingToken);
            }
        }

        /// <summary>
        /// Helper: Set status = AnalysisFailed và lưu ErrorMessage vào profile.
        /// Wrap trong try-catch riêng để tránh swallow exception.
        /// </summary>
        private async Task SetAnalysisFailedAsync(ApplicationDbContext dbContext, CVFile cvFile, string errorMessage, CancellationToken ct)
        {
            try
            {
                cvFile.Status = CVFileStatus.AnalysisFailed;
                cvFile.UpdatedAt = DateTime.UtcNow;

                // Tạo hoặc cập nhật profile để lưu ErrorMessage
                var profile = await dbContext.CVExtractedProfiles
                    .FirstOrDefaultAsync(e => e.CVFileId == cvFile.CVFileId, ct);

                if (profile == null)
                {
                    profile = new CVExtractedProfile
                    {
                        CVFileId = cvFile.CVFileId,
                        ErrorMessage = errorMessage,
                        IsConfirmed = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    dbContext.CVExtractedProfiles.Add(profile);
                }
                else
                {
                    profile.ErrorMessage = errorMessage;
                    profile.UpdatedAt = DateTime.UtcNow;
                }

                await dbContext.SaveChangesAsync(ct);
                await PublishCvProcessingFailedAsync(cvFile, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save AnalysisFailed status for CVFileId: {CVFileId}", cvFile.CVFileId);
            }
        }

        private async Task PublishCvProcessingFailedAsync(CVFile cvFile, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var publisher = scope.ServiceProvider.GetRequiredService<INotificationEventPublisher>();
            await publisher.PublishAsync(new NotificationEvent(
                cvFile.UserId, NotificationRecipientRole.USER, NotificationType.CV_PROCESSING_FAILED,
                NotificationCategory.PROFILE, NotificationSeverity.ERROR, "CV processing failed",
                "We could not process your CV. Please review the file and upload it again.",
                NotificationEntityType.CV, cvFile.CVFileId.ToString(), "/user/cv-management",
                $"CV_PROCESSING_FAILED:{cvFile.CVFileId}:1", new { cvFileId = cvFile.CVFileId }), cancellationToken);
        }
    }
}
