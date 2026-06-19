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
                    cvFile.Status = CVFileStatus.AnalysisFailed;
                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogWarning("Failed to extract text from PDF: {FilePath}. Error: {Error}", request.FilePath, extractResult.Error);
                    return;
                }

                // 2. Parse JSON bằng AI
                var aiResult = await geminiParsing.ParseCvTextAsync(extractResult.Text);
                if (!aiResult.Success || aiResult.Data == null)
                {
                    cvFile.Status = CVFileStatus.AnalysisFailed;
                    await dbContext.SaveChangesAsync(stoppingToken);
                    _logger.LogWarning("Failed to parse CV with Gemini. Error: {Error}", aiResult.Error);
                    return;
                }

                var parsedData = aiResult.Data;

                // 3. Ánh xạ dữ liệu vào Database Entities
                var extractedProfile = new CVExtractedProfile
                {
                    CVFileId = cvFile.CVFileId,
                    RoleTarget = parsedData.RoleTarget,
                    Education = JsonSerializer.Serialize(parsedData.Education),
                    Experience = JsonSerializer.Serialize(parsedData.Experience),
                    RawAiOutput = aiResult.RawResponse,
                    IsConfirmed = false,
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var skill in parsedData.Skills)
                {
                    extractedProfile.Skills.Add(new CVSkill
                    {
                        SkillName = skill.SkillName,
                        Source = string.IsNullOrEmpty(skill.Source) ? "AI" : skill.Source,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                foreach (var project in parsedData.Projects)
                {
                    extractedProfile.Projects.Add(new CVProject
                    {
                        ProjectName = project.ProjectName,
                        RoleDescription = project.RoleDescription,
                        TechnologyStack = project.TechnologyStack,
                        ProjectSummary = project.ProjectSummary,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                dbContext.CVExtractedProfiles.Add(extractedProfile);
                
                // 4. Đổi trạng thái CV thành ConfirmationRequired
                cvFile.Status = CVFileStatus.ConfirmationRequired;

                await dbContext.SaveChangesAsync(stoppingToken);
                
                _logger.LogInformation("Successfully processed CVFileId: {CVFileId} and updated status to ConfirmationRequired", request.CVFileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing CVFileId: {CVFileId}", request.CVFileId);
                cvFile.Status = CVFileStatus.AnalysisFailed;
                await dbContext.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
