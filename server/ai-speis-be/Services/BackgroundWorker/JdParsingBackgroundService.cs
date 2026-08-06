using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.GeminiAiParsingService;
using System.IO;
using System;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf;
using System.Text;
using ai_speis_be.Helpers;
using ai_speis_be.Services.NotificationService;

namespace ai_speis_be.Services.BackgroundWorker
{
    public class JdParsingBackgroundService : BackgroundService
    {
        private readonly IJdParseQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<JdParsingBackgroundService> _logger;

        public JdParsingBackgroundService(
            IJdParseQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<JdParsingBackgroundService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("JD Parsing Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var jdFileId = await _queue.DequeueAsync(stoppingToken);

                    // Xử lý file (phải tạo Scope vì BackgroundService là Singleton, DbContext là Scoped)
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var geminiService = scope.ServiceProvider.GetRequiredService<IGeminiAiParsingService>();

                    var jdFile = await dbContext.JDFiles.FindAsync(new object[] { jdFileId }, stoppingToken);

                    if (jdFile == null)
                    {
                        _logger.LogWarning($"JDFile with ID {jdFileId} not found.");
                        continue;
                    }

                    // Nếu InputType = File và RawText trống, tiến hành extract text từ file PDF
                    if (jdFile.InputType == JDInputType.File && string.IsNullOrEmpty(jdFile.RawText) && !string.IsNullOrEmpty(jdFile.FilePath))
                    {
                        try
                        {
                            var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", jdFile.FilePath.TrimStart('/'));
                            if (File.Exists(absolutePath))
                            {
                                jdFile.RawText = ExtractTextFromPdf(absolutePath);
                                await dbContext.SaveChangesAsync(stoppingToken);
                            }
                            else
                            {
                                throw new Exception($"File not found on disk: {absolutePath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            jdFile.Status = JDFileStatus.AnalysisFailed;
                            jdFile.ErrorMessage = "Failed to extract text from PDF file: " + ex.Message;
                            await dbContext.SaveChangesAsync(stoppingToken);
                            await PublishJdProcessingFailedAsync(jdFile, stoppingToken);
                            _logger.LogError(ex, $"Error extracting PDF for JD {jdFileId}");
                            continue; // Bỏ qua file này
                        }
                    }

                    var rawTextToParse = jdFile.RawText;

                    if (string.IsNullOrEmpty(rawTextToParse))
                    {
                        jdFile.Status = JDFileStatus.AnalysisFailed;
                        jdFile.ErrorMessage = "No text available for parsing.";
                        await dbContext.SaveChangesAsync(stoppingToken);
                        await PublishJdProcessingFailedAsync(jdFile, stoppingToken);
                        continue;
                    }

                    // Tiến hành gọi AI Parse
                    var (success, parsedData, rawResponse, error) = await geminiService.ParseJdTextAsync(rawTextToParse);

                    if (!success || parsedData == null)
                    {
                        jdFile.Status = JDFileStatus.AnalysisFailed;
                        jdFile.ErrorMessage = "AI parsing failed: " + error;
                        await dbContext.SaveChangesAsync(stoppingToken);
                        await PublishJdProcessingFailedAsync(jdFile, stoppingToken);
                        continue;
                    }

                    // AI Parse thành công nhưng tài liệu không phải là JD hoặc vị trí không thuộc 8 role hỗ trợ
                    if (!parsedData.IsValidJd || !RoleValidationHelper.IsSupportedRole(parsedData.RoleTarget, parsedData.JobTitle))
                    {
                        jdFile.Status = JDFileStatus.AnalysisFailed;
                        jdFile.ErrorMessage = !parsedData.IsValidJd
                            ? ("Document rejected by AI: " + (parsedData.InvalidReason ?? "Not a valid JD."))
                            : RoleValidationHelper.UnsupportedRoleErrorMessage;
                        await dbContext.SaveChangesAsync(stoppingToken);
                        await PublishJdProcessingFailedAsync(jdFile, stoppingToken);
                        continue;
                    }

                    // Lưu dữ liệu vào JDExtractedProfile
                    var extractedProfile = new JDExtractedProfile
                    {
                        JDFileId = jdFile.JDFileId,
                        JobTitle = parsedData.JobTitle,
                        ExperienceLevel = parsedData.ExperienceLevel,
                        RoleTarget = parsedData.RoleTarget,
                        RequiredSkills = JsonSerializer.Serialize(parsedData.RequiredSkills ?? new System.Collections.Generic.List<string>()),
                        NiceToHaveSkills = JsonSerializer.Serialize(parsedData.NiceToHaveSkills ?? new System.Collections.Generic.List<string>()),
                        Responsibilities = parsedData.Responsibilities,
                        CompanyCharacteristics = parsedData.CompanyCharacteristics,
                        ConfidenceScore = parsedData.JdConfidenceScore,
                        RawAiOutput = rawResponse,
                        IsConfirmed = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    dbContext.JDExtractedProfiles.Add(extractedProfile);
                    jdFile.Status = JDFileStatus.ConfirmationRequired; // Hoặc ANALYZED tuỳ theo design
                    jdFile.ErrorMessage = null;

                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation($"Successfully parsed JD {jdFileId}. Confidence: {parsedData.JdConfidenceScore}");
                }
                catch (OperationCanceledException)
                {
                    // Ignore, service is stopping
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred in JD Parsing Background Service.");
                }
            }
        }

        private string ExtractTextFromPdf(string pdfPath)
        {
            var sb = new StringBuilder();
            using var reader = new PdfReader(pdfPath);
            using var document = new PdfDocument(reader);

            for (int i = 1; i <= document.GetNumberOfPages(); i++)
            {
                var page = document.GetPage(i);
                var strategy = new SimpleTextExtractionStrategy();
                var text = PdfTextExtractor.GetTextFromPage(page, strategy);
                sb.AppendLine(text);
            }

            return sb.ToString();
        }

        private Task PublishJdProcessingFailedAsync(JDFile jdFile, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var publisher = scope.ServiceProvider.GetRequiredService<INotificationEventPublisher>();
            return publisher.PublishAsync(new NotificationEvent(
                jdFile.UserId, NotificationRecipientRole.USER, NotificationType.JD_PROCESSING_FAILED,
                NotificationCategory.PROFILE, NotificationSeverity.ERROR, "Job description processing failed",
                "We could not process the job description. Please review it and try again.",
                NotificationEntityType.JOB_DESCRIPTION, jdFile.JDFileId.ToString(), "/user/cv-management",
                $"JD_PROCESSING_FAILED:{jdFile.JDFileId}:1", new { jdFileId = jdFile.JDFileId }), cancellationToken);
        }
    }
}
