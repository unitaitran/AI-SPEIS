using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Helpers;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.InterviewCampaignRepo;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.InterviewSessionService
{
    public class InterviewSessionService : IInterviewSessionService
    {
        private readonly IInterviewCampaignRepository _repository;
        private readonly ApplicationDbContext _context;

        public InterviewSessionService(IInterviewCampaignRepository repository, ApplicationDbContext context)
        {
            _repository = repository;
            _context = context;
        }

        public async Task<(bool Success, string? ErrorMessage, InterviewCampaignDto? Campaign)> CreateSessionsAsync(int userId, CreateInterviewSessionRequest request)
        {
            // 1. Kiểm tra CV File
            var cvFile = await _context.CVFiles.FirstOrDefaultAsync(c => c.CVFileId == request.CVFileId && c.UserId == userId);
            if (cvFile == null)
            {
                return (false, "Không tìm thấy file CV.", null);
            }

            if (cvFile.Status != CVFileStatus.ConfirmationRequired)
            {
                return (false, "File CV chưa sẵn sàng để tạo phỏng vấn.", null);
            }

            var cvProfile = await _context.CVExtractedProfiles.FirstOrDefaultAsync(p => p.CVFileId == request.CVFileId);
            if (cvProfile == null)
            {
                return (false, "Không tìm thấy dữ liệu CV đã phân tích.", null);
            }

            // 2. Kiểm tra JD File
            var jdFile = await _context.JDFiles.FirstOrDefaultAsync(j => j.JDFileId == request.JDFileId && j.UserId == userId);
            if (jdFile == null)
            {
                return (false, "Không tìm thấy file JD.", null);
            }

            if (jdFile.Status != JDFileStatus.ConfirmationRequired && jdFile.Status != JDFileStatus.Confirmed)
            {
                return (false, "File JD chưa sẵn sàng để tạo phỏng vấn.", null);
            }

            var jdProfile = await _context.JDExtractedProfiles.FirstOrDefaultAsync(p => p.JDFileId == request.JDFileId);
            if (jdProfile == null)
            {
                return (false, "Không tìm thấy dữ liệu JD đã phân tích.", null);
            }

            // 3. Xác định các vòng phỏng vấn cần tạo dựa trên RoleTarget
            var availableRounds = RoleCategoryHelper.GetAvailableRounds(jdProfile.RoleTarget);
            var roundTypesToCreate = new List<InterviewRoundType>();

            // Lấy các vòng mặc định
            foreach (var roundName in availableRounds.AvailableRounds)
            {
                if (Enum.TryParse<InterviewRoundType>(roundName, out var roundEnum))
                {
                    roundTypesToCreate.Add(roundEnum);
                }
            }

            // Nếu là BA/Tester và chọn phỏng vấn thêm coding
            if (availableRounds.HasOptionalCoding && request.IncludeCoding)
            {
                roundTypesToCreate.Add(InterviewRoundType.Code);
            }

            if (!roundTypesToCreate.Any())
            {
                return (false, "Không xác định được vòng phỏng vấn nào khả dụng cho vị trí này.", null);
            }

            // Xác định độ khó dựa trên ExperienceLevel của JD Profile
            var difficulty = MapExperienceLevelToDifficulty(jdProfile.ExperienceLevel);

            // Xác định Mode của phỏng vấn (mặc định Practice)
            var mode = InterviewMode.Practice;
            if (!string.IsNullOrWhiteSpace(request.Mode) && Enum.TryParse<InterviewMode>(request.Mode, true, out var parsedMode))
            {
                mode = parsedMode;
            }

            // 4. Khởi tạo đợt phỏng vấn và các vòng phỏng vấn sử dụng Transaction để đảm bảo toàn vẹn dữ liệu
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var campaign = new InterviewCampaign
                {
                    UserId = userId,
                    CVExtractedProfileId = cvProfile.ExtractedProfileId,
                    JDExtractedProfileId = jdProfile.ExtractedProfileId,
                    Language = string.IsNullOrWhiteSpace(request.Language) ? "vi" : request.Language,
                    Mode = mode,
                    CreatedAt = DateTime.UtcNow
                };

                _context.InterviewCampaigns.Add(campaign);
                await _context.SaveChangesAsync(); // Lưu bảng cha trước để lấy ID

                foreach (var roundType in roundTypesToCreate)
                {
                    // Số lượng câu hỏi mặc định theo quy định từng vòng: Code = 10, các vòng khác = 5
                    int defaultQuestionCount = roundType == InterviewRoundType.Code ? 10 : 5;

                    var session = new InterviewSession
                    {
                        InterviewCampaignId = campaign.InterviewCampaignId,
                        InterviewRoundType = roundType,
                        Difficulty = difficulty,
                        QuestionCount = defaultQuestionCount,
                        Status = InterviewSessionStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.InterviewSessions.Add(session);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Load lại đợt phỏng vấn từ DB để đảm bảo không bị trùng lặp dữ liệu trong bộ nhớ (Change Tracker)
                var campaignDto = await GetCampaignByIdAsync(userId, campaign.InterviewCampaignId);
                return (true, null, campaignDto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Có lỗi xảy ra khi tạo luồng phỏng vấn: {ex.Message}", null);
            }
        }

        public async Task<InterviewSessionDto?> GetSessionByIdAsync(int userId, int sessionId)
        {
            var session = await _repository.GetSessionByIdAsync(sessionId);
            if (session == null || session.InterviewCampaign.UserId != userId)
            {
                return null;
            }

            return MapToResponse(session);
        }

        public async Task<InterviewCampaignDto?> GetCampaignByIdAsync(int userId, int campaignId)
        {
            var campaign = await _repository.GetCampaignByIdAsync(campaignId);
            if (campaign == null || campaign.UserId != userId)
            {
                return null;
            }

            return MapCampaignToResponse(campaign);
        }

        public async Task<IEnumerable<InterviewCampaignDto>> GetUserCampaignsAsync(int userId)
        {
            var campaigns = await _repository.GetCampaignsByUserIdAsync(userId);
            return campaigns.Select(MapCampaignToResponse).ToList();
        }

        public async Task<AvailableRoundsDto?> GetAvailableRoundsAsync(int userId, int jdId)
        {
            var jdFile = await _context.JDFiles.FirstOrDefaultAsync(j => j.JDFileId == jdId && j.UserId == userId);
            if (jdFile == null) return null;

            var jdProfile = await _context.JDExtractedProfiles.FirstOrDefaultAsync(p => p.JDFileId == jdId);
            if (jdProfile == null) return null;

            return RoleCategoryHelper.GetAvailableRounds(jdProfile.RoleTarget);
        }

        private InterviewCampaignDto MapCampaignToResponse(InterviewCampaign campaign)
        {
            return new InterviewCampaignDto
            {
                InterviewCampaignId = campaign.InterviewCampaignId,
                UserId = campaign.UserId,
                CVExtractedProfileId = campaign.CVExtractedProfileId,
                JDExtractedProfileId = campaign.JDExtractedProfileId,
                Language = campaign.Language,
                Mode = campaign.Mode.ToString(),
                CreatedAt = campaign.CreatedAt,
                UpdatedAt = campaign.UpdatedAt,
                Sessions = campaign.InterviewSessions.Select(MapToResponse).ToList()
            };
        }

        private InterviewSessionDto MapToResponse(InterviewSession session)
        {
            return new InterviewSessionDto
            {
                InterviewSessionId = session.InterviewSessionId,
                InterviewCampaignId = session.InterviewCampaignId,
                InterviewRoundType = session.InterviewRoundType.ToString(),
                Difficulty = session.Difficulty.ToString(),
                QuestionCount = session.QuestionCount,
                Status = session.Status.ToString(),
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt
            };
        }

        private QuestionDifficultyEnum MapExperienceLevelToDifficulty(string? experienceLevel)
        {
            if (string.IsNullOrWhiteSpace(experienceLevel))
            {
                return QuestionDifficultyEnum.Medium;
            }

            var normalized = experienceLevel.Trim().ToUpper();

            if (normalized.Contains("INTERN") || normalized.Contains("JUNIOR") || normalized.Contains("FRESHER"))
            {
                return QuestionDifficultyEnum.Easy;
            }

            if (normalized.Contains("SENIOR") || normalized.Contains("LEAD") || normalized.Contains("MANAGER") || normalized.Contains("PRINCIPAL") || normalized.Contains("EXPERT"))
            {
                return QuestionDifficultyEnum.Hard;
            }

            return QuestionDifficultyEnum.Medium;
        }
    }
}
