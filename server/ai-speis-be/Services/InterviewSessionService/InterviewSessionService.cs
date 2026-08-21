using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ai_speis_be.CampaignResults;
using ai_speis_be.Helpers;
using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.InterviewCampaignRepo;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Scoring;
using Microsoft.EntityFrameworkCore;
using ai_speis_be.Services.RewardService;
using ai_speis_be.Services.SubscriptionService;
using ai_speis_be.Services.NotificationService;

namespace ai_speis_be.Services.InterviewSessionService
{
    public class InterviewSessionService : IInterviewSessionService
    {
        private static readonly TimeSpan PendingCampaignLifetime = TimeSpan.FromHours(2);
        private static readonly JsonSerializerOptions ResultJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
        private const int BasicInterviewQuota = 3;
        private const int PremiumInterviewQuota = 15;

        private readonly record struct QuotaMetadata(int Remaining, int Max, string PlanName);

        private readonly IInterviewCampaignRepository _repository;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InterviewSessionService> _logger;
        private readonly ISubscriptionService _subscriptionService;
        private readonly IRewardService _rewardService;
        private readonly IConfiguration? _configuration;
        private readonly INotificationEventPublisher? _notificationPublisher;
        private readonly ai_speis_be.Services.CodingService.Selection.ICodingQuestionSelectionService _codingSelectionService;

        // Kept for existing unit-test and internal construction sites; runtime DI uses
        // the full constructor below.
        public InterviewSessionService(
            IInterviewCampaignRepository repository,
            ApplicationDbContext context,
            ILogger<InterviewSessionService> logger)
            : this(
                repository,
                context,
                logger,
                new SubscriptionService.SubscriptionService(context),
                new RewardService.RewardService(context),
                null,
                null,
                null)
        {
        }

        public InterviewSessionService(
            IInterviewCampaignRepository repository,
            ApplicationDbContext context,
            ILogger<InterviewSessionService> logger,
            ISubscriptionService subscriptionService,
            IRewardService rewardService,
            IConfiguration? configuration = null,
            INotificationEventPublisher? notificationPublisher = null,
            ai_speis_be.Services.CodingService.Selection.ICodingQuestionSelectionService? codingSelectionService = null)
        {
            _repository = repository;
            _context = context;
            _logger = logger;
            _subscriptionService = subscriptionService;
            _rewardService = rewardService;
            _configuration = configuration;
            _notificationPublisher = notificationPublisher;
            _codingSelectionService = codingSelectionService ?? new ai_speis_be.Services.CodingService.Selection.CodingQuestionSelectionService(
                context,
                new LoggerFactory().CreateLogger<ai_speis_be.Services.CodingService.Selection.CodingQuestionSelectionService>());
        }

        public async Task<(bool Success, string? ErrorMessage, InterviewCampaignDto? Campaign)> CreateSessionsAsync(
            int userId,
            CreateInterviewSessionRequest request)
        {
            var cvFile = await _context.CVFiles
                .FirstOrDefaultAsync(file => file.CVFileId == request.CVFileId && file.UserId == userId);
            if (cvFile == null) return (false, "Không tìm thấy file CV.", null);
            if (cvFile.Status != CVFileStatus.Confirmed)
                return (false, "CV phải được xác nhận trước khi tạo phỏng vấn.", null);

            var cvProfile = await _context.CVExtractedProfiles
                .FirstOrDefaultAsync(profile => profile.CVFileId == request.CVFileId);
            if (cvProfile == null) return (false, "Không tìm thấy dữ liệu CV đã phân tích.", null);
            if (!cvProfile.IsConfirmed)
                return (false, "Dữ liệu CV chưa được người dùng xác nhận.", null);

            var jdFile = await _context.JDFiles
                .FirstOrDefaultAsync(file => file.JDFileId == request.JDFileId && file.UserId == userId);
            if (jdFile == null) return (false, "Không tìm thấy file JD.", null);
            if (jdFile.Status != JDFileStatus.ConfirmationRequired
                && jdFile.Status != JDFileStatus.Confirmed)
            {
                return (false, "JD phải được phân tích hoàn tất trước khi tạo phỏng vấn.", null);
            }

            var jdProfile = await _context.JDExtractedProfiles
                .FirstOrDefaultAsync(profile => profile.JDFileId == request.JDFileId);
            if (jdProfile == null) return (false, "Không tìm thấy dữ liệu JD đã phân tích.", null);

            if (!Enum.TryParse<InterviewMode>(request.Mode, true, out var mode)
                || !Enum.GetNames(typeof(InterviewMode)).Contains(request.Mode, StringComparer.OrdinalIgnoreCase))
            {
                return (false, "Chế độ phỏng vấn không hợp lệ.", null);
            }

            var availableRounds = RoleCategoryHelper.GetAvailableRounds(jdProfile.RoleTarget);
            var selectableRounds = new HashSet<string>(availableRounds.AvailableRounds, StringComparer.OrdinalIgnoreCase);
            if (availableRounds.HasOptionalCoding) selectableRounds.Add(InterviewRoundType.Code.ToString());

            var requestedRounds = request.SelectedRounds != null && request.SelectedRounds.Count > 0
                ? request.SelectedRounds
                : availableRounds.AvailableRounds;
            var roundTypesToCreate = new List<InterviewRoundType>();

            foreach (var roundName in requestedRounds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!selectableRounds.Contains(roundName))
                    return (false, $"Vòng phỏng vấn '{roundName}' không khả dụng cho vị trí này.", null);

                if (Enum.TryParse<InterviewRoundType>(roundName, true, out var roundType))
                    roundTypesToCreate.Add(roundType);
            }

            if (mode == InterviewMode.RealTest
                && availableRounds.HasOptionalCoding
                && request.IncludeCoding
                && !roundTypesToCreate.Contains(InterviewRoundType.Code))
            {
                roundTypesToCreate.Add(InterviewRoundType.Code);
            }

            roundTypesToCreate = roundTypesToCreate
                .Distinct()
                .OrderBy(GetRoundOrder)
                .ToList();

            if (roundTypesToCreate.Count == 0)
                return (false, "Không xác định được vòng phỏng vấn nào khả dụng cho vị trí này.", null);

            string? technicalProvider = null;
            if (roundTypesToCreate.Contains(InterviewRoundType.Technical)
                && !string.IsNullOrWhiteSpace(request.AiProvider))
            {
                try
                {
                    technicalProvider = TechnicalInterviewAIProviderResolver.Normalize(request.AiProvider);
                }
                catch (InvalidOperationException)
                {
                    return (false, $"Technical AI provider '{request.AiProvider}' is not supported. Use 'gemini' or 'ollama'.", null);
                }
            }

            var difficulty = MapExperienceLevelToDifficulty(jdProfile.ExperienceLevel);
            var now = DateTime.UtcNow;
            var readyNotifications = new List<NotificationEvent>();
            InterviewCampaignDto? createdCampaign = null;
            var transactionCommitted = false;

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(candidate => candidate.UserId == userId);
                if (user == null)
                    return (false, "Không tìm thấy người dùng.", null);

                var quota = await GetQuotaMetadataAsync(user, now);
                if (quota.Remaining != user.RemainingInterviewQuota)
                {
                    user.RemainingInterviewQuota = quota.Remaining;
                    user.UpdatedAt = now;
                    await _context.SaveChangesAsync();
                }

                var liveCampaigns = await _context.InterviewCampaigns
                    .Include(campaign => campaign.InterviewSessions.Where(session => !session.IsDeleted))
                    .Where(campaign => campaign.UserId == userId
                        && !campaign.IsDeleted
                        && (campaign.Status == InterviewCampaignStatus.Pending
                            || campaign.Status == InterviewCampaignStatus.Active))
                    .OrderByDescending(campaign => campaign.CreatedAt)
                    .ToListAsync();

                var lifecycleChanged = false;
                foreach (var liveCampaign in liveCampaigns)
                    lifecycleChanged |= ExpireIfDue(liveCampaign, user, now);

                if (lifecycleChanged) await _context.SaveChangesAsync();

                var existingCampaign = liveCampaigns.FirstOrDefault(IsLiveCampaign);
                if (existingCampaign != null)
                {
                    if (MatchesConfiguration(
                        existingCampaign,
                        cvProfile.ExtractedProfileId,
                        jdProfile.ExtractedProfileId,
                        request.Language,
                        mode,
                        request.DurationMinutes,
                        roundTypesToCreate,
                        request.QuestionCounts,
                        technicalProvider,
                        _configuration))
                    {
                        await transaction.CommitAsync();
                        return (true, null, MapCampaignToResponse(existingCampaign, quota));
                    }

                    await transaction.CommitAsync();
                    return (false, "Bạn đang có một campaign chưa kết thúc. Hãy tiếp tục hoặc hủy campaign đó trước khi tạo cấu hình mới.", null);
                }

                if (quota.Remaining <= 0)
                {
                    await transaction.CommitAsync();
                    return (false, "Bạn đã hết lượt phỏng vấn.", null);
                }

                bool isOnlyCoding = roundTypesToCreate.Count > 0 && roundTypesToCreate.All(r => r == InterviewRoundType.Code);

                var campaign = new InterviewCampaign
                {
                    UserId = userId,
                    CVExtractedProfileId = cvProfile.ExtractedProfileId,
                    JDExtractedProfileId = jdProfile.ExtractedProfileId,
                    Language = request.Language.Trim().ToLowerInvariant(),
                    Mode = mode,
                    DurationMinutes = request.DurationMinutes,
                    Status = isOnlyCoding ? InterviewCampaignStatus.Active : InterviewCampaignStatus.Pending,
                    StartedAt = isOnlyCoding ? now : null,
                    ExpiresAt = isOnlyCoding ? now.AddMinutes(request.DurationMinutes) : now.Add(PendingCampaignLifetime),
                    QuotaRefunded = false,
                    CreatedAt = now
                };

                _context.InterviewCampaigns.Add(campaign);
                await _context.SaveChangesAsync();

                foreach (var roundType in roundTypesToCreate)
                {
                    _context.InterviewSessions.Add(new InterviewSession
                    {
                        InterviewCampaignId = campaign.InterviewCampaignId,
                        InterviewRoundType = roundType,
                        Difficulty = difficulty,
                        QuestionCount = GetQuestionCount(mode, roundType, request.QuestionCounts, _configuration),
                        Status = (isOnlyCoding && roundType == InterviewRoundType.Code) ? InterviewSessionStatus.Active : InterviewSessionStatus.Pending,
                        TechnicalRuntimeVersion = roundType == InterviewRoundType.Technical ? "V2" : null,
                        // Behavioral still uses this historical field to pin its provider.
                        // Technical V2 persists provider metadata on its canonical records instead.
                        TechnicalAiProvider = !string.IsNullOrWhiteSpace(request.AiProvider)
                            ? request.AiProvider
                            : technicalProvider,
                        CreatedAt = now
                    });
                }

                await _context.SaveChangesAsync();
                foreach (var createdSession in _context.InterviewSessions.Local.Where(item => item.InterviewCampaignId == campaign.InterviewCampaignId))
                {
                    readyNotifications.Add(new NotificationEvent(
                        userId, NotificationRecipientRole.USER, NotificationType.INTERVIEW_SESSION_READY,
                        NotificationCategory.INTERVIEW, NotificationSeverity.INFO, "Your interview is ready",
                        $"Your {GetRoundDisplayName(createdSession.InterviewRoundType)} Interview is ready to begin.",
                        NotificationEntityType.INTERVIEW_SESSION, createdSession.InterviewSessionId.ToString(), "/user/interview/setup",
                        $"INTERVIEW_SESSION_READY:{createdSession.InterviewSessionId}:{userId}",
                        new { sessionId = createdSession.InterviewSessionId, roundType = ToContractRoundType(createdSession.InterviewRoundType) }));
                }
                var createdQuota = await GetQuotaMetadataAsync(campaign.User, now);
                createdCampaign = MapCampaignToResponse(campaign, createdQuota);
                await transaction.CommitAsync();
                transactionCommitted = true;
            }
            catch (Exception exception)
            {
                if (!transactionCommitted)
                    await transaction.RollbackAsync();
                _logger.LogError(
                    exception,
                    "Không thể tạo campaign phỏng vấn cho User {UserId}, CV {CVFileId}, JD {JDFileId}.",
                    userId,
                    request.CVFileId,
                    request.JDFileId);
                return (false, "Không thể lưu cấu hình phỏng vấn. Vui lòng thử lại sau.", null);
            }

            foreach (var notification in readyNotifications)
                await PublishSafelyAsync(notification);

            return (true, null, createdCampaign);
        }

        public async Task<(bool Success, string? ErrorMessage, InterviewCampaignDto? Campaign)> StartSessionAsync(
            int userId,
            int sessionId)
        {
            var session = await GetOwnedSessionWithCampaignAsync(userId, sessionId);
            if (session == null) return (false, "Không tìm thấy phiên phỏng vấn.", null);

            var campaign = session.InterviewCampaign;
            var now = DateTime.UtcNow;
            if (ExpireIfDue(campaign, campaign.User, now))
            {
                await _context.SaveChangesAsync();
                return (false, "Campaign đã hết hạn.", null);
            }

            if (session.Status == InterviewSessionStatus.Active
                && campaign.Status == InterviewCampaignStatus.Active)
            {
                if (EnsureActiveCampaignTiming(campaign, now))
                    await _context.SaveChangesAsync();
                var activeQuota = await GetQuotaMetadataAsync(campaign.User, now);
                return (true, null, MapCampaignToResponse(campaign, activeQuota));
            }

            // Recover sessions started by older round-specific flows that activated the
            // round without advancing the parent campaign lifecycle.
            if (session.Status == InterviewSessionStatus.Active
                && campaign.Status == InterviewCampaignStatus.Pending
                && !campaign.InterviewSessions.Any(candidate =>
                    candidate.InterviewSessionId != session.InterviewSessionId
                    && candidate.Status == InterviewSessionStatus.Active))
            {
                campaign.Status = InterviewCampaignStatus.Active;
                campaign.StartedAt ??= now;
                campaign.ExpiresAt = campaign.StartedAt.Value.AddMinutes(campaign.DurationMinutes);
                campaign.UpdatedAt = now;
                session.UpdatedAt = now;
                await _context.SaveChangesAsync();
                var recoveredQuota = await GetQuotaMetadataAsync(campaign.User, now);
                return (true, null, MapCampaignToResponse(campaign, recoveredQuota));
            }

            if (session.Status != InterviewSessionStatus.Pending)
                return (false, $"Phiên phỏng vấn đang ở trạng thái '{session.Status}' và không thể bắt đầu.", null);
            if (!IsLiveCampaign(campaign))
                return (false, $"Campaign đang ở trạng thái '{campaign.Status}' và không thể bắt đầu.", null);
            var activeOtherSessions = campaign.InterviewSessions
                .Where(candidate => candidate.InterviewSessionId != session.InterviewSessionId
                    && candidate.Status == InterviewSessionStatus.Active)
                .ToList();

            if (activeOtherSessions.Any())
            {
                foreach (var other in activeOtherSessions)
                {
                    if (GetRoundOrder(other.InterviewRoundType) < GetRoundOrder(session.InterviewRoundType))
                    {
                        other.Status = InterviewSessionStatus.Completed;
                        other.UpdatedAt = now;
                    }
                }
            }

            if (campaign.Status == InterviewCampaignStatus.Pending)
            {
                campaign.Status = InterviewCampaignStatus.Active;
                campaign.StartedAt = now;
                campaign.ExpiresAt = now.AddMinutes(campaign.DurationMinutes);
            }

            session.Status = InterviewSessionStatus.Active;
            session.UpdatedAt = now;
            campaign.UpdatedAt = now;
            await _context.SaveChangesAsync();
            var startedQuota = await GetQuotaMetadataAsync(campaign.User, now);
            return (true, null, MapCampaignToResponse(campaign, startedQuota));
        }

        public async Task<(bool Success, string? ErrorMessage, InterviewCampaignDto? Campaign)> CompleteSessionAsync(
            int userId,
            int sessionId)
        {
            var session = await GetOwnedSessionWithCampaignAsync(userId, sessionId);
            if (session == null) return (false, "Không tìm thấy phiên phỏng vấn.", null);

            var campaign = session.InterviewCampaign;
            var now = DateTime.UtcNow;
            if (ExpireIfDue(campaign, campaign.User, now))
            {
                await _context.SaveChangesAsync();
                return (false, "Campaign đã hết hạn.", null);
            }

            if (session.Status == InterviewSessionStatus.Completed)
            {
                var recoveredQuota = await AdvanceCampaignAsync(campaign, now);
                return (true, null, MapCampaignToResponse(campaign, recoveredQuota));
            }
            if (session.Status != InterviewSessionStatus.Active && session.Status != InterviewSessionStatus.Pending)
                return (false, "Chỉ có thể hoàn tất phiên đang hoạt động.", null);

            if (campaign.Status == InterviewCampaignStatus.Pending)
            {
                campaign.Status = InterviewCampaignStatus.Active;
                campaign.StartedAt = now;
                campaign.ExpiresAt = now.AddMinutes(campaign.DurationMinutes);
            }

            if (session.InterviewRoundType == InterviewRoundType.Code)
            {
                await EnsureCodingSubmissionsExistAsync(session);
            }

            session.Status = InterviewSessionStatus.Completed;
            session.UpdatedAt = now;
            var quota = await AdvanceCampaignAsync(campaign, now);
            await PublishRoundCompletionAsync(userId, session, campaign);
            return (true, null, MapCampaignToResponse(campaign, quota));
        }

        public async Task<(bool Success, string? ErrorMessage, InterviewCampaignDto? Campaign)> FinishCampaignAsync(
            int userId,
            int campaignId)
        {
            var campaign = await GetOwnedCampaignAsync(userId, campaignId);
            if (campaign == null) return (false, "Không tìm thấy đợt phỏng vấn.", null);

            var now = DateTime.UtcNow;
            foreach (var session in campaign.InterviewSessions.Where(s => !s.IsDeleted && s.Status != InterviewSessionStatus.Completed))
            {
                session.Status = InterviewSessionStatus.Cancelled;
                session.UpdatedAt = now;
            }

            if (campaign.Status != InterviewCampaignStatus.Completed)
            {
                campaign.Status = InterviewCampaignStatus.Completed;
                campaign.CompletedAt = now;
                campaign.UpdatedAt = now;
            }

            await _context.SaveChangesAsync();
            var quota = await GetQuotaMetadataAsync(campaign.User, now);
            return (true, null, MapCampaignToResponse(campaign, quota));
        }

        public async Task<(bool Success, string? ErrorMessage, InterviewCampaignDto? Campaign)> CancelCampaignAsync(
            int userId,
            int campaignId)
        {
            var campaign = await GetOwnedCampaignAsync(userId, campaignId);
            if (campaign == null) return (false, "Không tìm thấy campaign phỏng vấn.", null);
            if (campaign.Status == InterviewCampaignStatus.Cancelled)
            {
                var existingQuota = await GetQuotaMetadataAsync(campaign.User, DateTime.UtcNow);
                return (true, null, MapCampaignToResponse(campaign, existingQuota));
            }
            if (campaign.Status == InterviewCampaignStatus.Completed || campaign.Status == InterviewCampaignStatus.Expired)
                return (false, $"Campaign ở trạng thái '{campaign.Status}' và không thể hủy.", null);

            var now = DateTime.UtcNow;
            campaign.Status = InterviewCampaignStatus.Cancelled;
            campaign.CancelledAt = now;
            campaign.UpdatedAt = now;
            CancelOpenSessions(campaign, now);
            RefundUnusedQuota(campaign, campaign.User);
            await _context.SaveChangesAsync();
            var quota = await GetQuotaMetadataAsync(campaign.User, now);
            return (true, null, MapCampaignToResponse(campaign, quota));
        }

        public async Task<(bool Success, string? ErrorMessage, InterviewCampaignDto? Campaign)> ExpireCampaignAsync(
            int userId,
            int campaignId)
        {
            var campaign = await GetOwnedCampaignAsync(userId, campaignId);
            if (campaign == null) return (false, "Không tìm thấy campaign phỏng vấn.", null);
            if (campaign.Status == InterviewCampaignStatus.Expired)
            {
                var existingQuota = await GetQuotaMetadataAsync(campaign.User, DateTime.UtcNow);
                return (true, null, MapCampaignToResponse(campaign, existingQuota));
            }
            if (!ExpireIfDue(campaign, campaign.User, DateTime.UtcNow))
                return (false, "Campaign chưa đến thời điểm hết hạn.", null);

            await _context.SaveChangesAsync();
            await PublishCampaignExpiredAsync(userId, campaign);
            var quota = await GetQuotaMetadataAsync(campaign.User, DateTime.UtcNow);
            return (true, null, MapCampaignToResponse(campaign, quota));
        }

        public async Task<InterviewQuotaDto?> GetQuotaAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(candidate => candidate.UserId == userId);
            if (user == null) return null;

            var now = DateTime.UtcNow;
            var previousRemainingQuota = user.RemainingInterviewQuota;
            var quota = await GetQuotaMetadataAsync(user, now);

            var liveCampaigns = await _context.InterviewCampaigns
                .Include(campaign => campaign.InterviewSessions.Where(session => !session.IsDeleted))
                .Where(campaign => campaign.UserId == userId
                    && !campaign.IsDeleted
                    && (campaign.Status == InterviewCampaignStatus.Pending
                        || campaign.Status == InterviewCampaignStatus.Active))
                .ToListAsync();

            var lifecycleChanged = false;
            foreach (var campaign in liveCampaigns)
                lifecycleChanged |= ExpireIfDue(campaign, user, now);
            if (lifecycleChanged || _context.ChangeTracker.HasChanges()) await _context.SaveChangesAsync();

            return new InterviewQuotaDto
            {
                RemainingInterviewQuota = user.RemainingInterviewQuota,
                MaxInterviewQuota = quota.Max,
                PlanName = quota.PlanName,
            };
        }

        public async Task<InterviewSessionDto?> GetSessionByIdAsync(int userId, int sessionId)
        {
            var session = await GetOwnedSessionWithCampaignAsync(userId, sessionId);
            if (session == null) return null;
            var now = DateTime.UtcNow;
            var lifecycleChanged = EnsureActiveCampaignTiming(session.InterviewCampaign, now);
            lifecycleChanged |= ExpireIfDue(session.InterviewCampaign, session.InterviewCampaign.User, now);
            if (lifecycleChanged) await _context.SaveChangesAsync();
            return MapToResponse(session);
        }

        public async Task<InterviewCampaignDto?> GetCampaignByIdAsync(int userId, int campaignId)
        {
            var campaign = await GetOwnedCampaignAsync(userId, campaignId);
            if (campaign == null) return null;
            var now = DateTime.UtcNow;
            var lifecycleChanged = EnsureActiveCampaignTiming(campaign, now);
            lifecycleChanged |= ExpireIfDue(campaign, campaign.User, now);
            if (lifecycleChanged) await _context.SaveChangesAsync();
            var quota = await GetQuotaMetadataAsync(campaign.User, now);
            return MapCampaignToResponse(campaign, quota);
        }

        public async Task<CampaignInterviewResultDto?> GetCampaignResultAsync(int userId, int campaignId)
        {
            var campaign = await GetOwnedCampaignAsync(userId, campaignId);
            if (campaign == null || campaign.Status != InterviewCampaignStatus.Completed) return null;

            var rounds = new List<CampaignRoundResultDto>();
            var technicalDimensions = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var behaviouralCriteria = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            var technicalSession = campaign.InterviewSessions.FirstOrDefault(session =>
                !session.IsDeleted && session.InterviewRoundType == InterviewRoundType.Technical);
            if (technicalSession != null)
            {
                var v2Result = await _context.TechnicalRoundResults
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.InterviewSessionId == technicalSession.InterviewSessionId);
                var v2Questions = await _context.TechnicalSessionQuestions
                    .AsNoTracking()
                    .Include(item => item.Answer)
                    .Where(item => item.TechnicalQuestionSet.InterviewSessionId == technicalSession.InterviewSessionId
                        && item.QuestionType == TechnicalSessionQuestionType.Main)
                    .ToListAsync();
                var v2Score = CampaignResultCalculator.Round(v2Result?.OverallScore ?? 0m);
                foreach (var dimension in DeserializeTechnicalV2Dimensions(v2Questions.Select(item => item.Answer?.AiCriteriaDetailJson)))
                {
                    technicalDimensions[dimension.Key] = CampaignResultCalculator.Round(dimension.Value);
                }
                rounds.Add(new CampaignRoundResultDto
                {
                    InterviewSessionId = technicalSession.InterviewSessionId,
                    RoundType = InterviewRoundType.Technical.ToString(),
                    Score = v2Score,
                    PerformanceBand = CampaignResultCalculator.GetPerformanceBand(v2Score),
                    EvaluatedItemCount = v2Questions.Count(item => item.Answer?.FinalQuestionScore.HasValue == true),
                    Summary = v2Result?.AiExecutiveSummary ?? string.Empty,
                    LevelAssessment = v2Result?.AiLevelAssessment,
                    Strengths = DeserializeStringList(v2Result?.AiStrengths),
                    AreasForImprovement = DeserializeStringList(v2Result?.AiGaps),
                    Recommendations = DeserializeStringList(v2Result?.AiRecommendations),
                    FinalFeedbackStatus = v2Result?.FinalFeedbackStatus
                });
            }

            var behaviourSession = campaign.InterviewSessions.FirstOrDefault(session =>
                !session.IsDeleted && session.InterviewRoundType == InterviewRoundType.Behavior);
            if (behaviourSession != null)
            {
                var result = await _context.BehaviourRoundResults
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.InterviewSessionId == behaviourSession.InterviewSessionId);
                behaviouralCriteria = DeserializeDictionary(result?.CriteriaAveragesJson);
                var score = CampaignResultCalculator.Round(result?.OverallScore ?? 0m);
                rounds.Add(new CampaignRoundResultDto
                {
                    InterviewSessionId = behaviourSession.InterviewSessionId,
                    RoundType = InterviewRoundType.Behavior.ToString(),
                    Score = score,
                    PerformanceBand = CampaignResultCalculator.GetPerformanceBand(score),
                    EvaluatedItemCount = await _context.BehaviourSessionQuestions.CountAsync(question =>
                        question.BehaviourQuestionSet.InterviewSessionId == behaviourSession.InterviewSessionId
                        && question.QuestionType == BehaviourQuestionType.Main),
                    Summary = result?.AiExecutiveSummary ?? string.Empty,
                    LevelAssessment = result?.AiLevelAssessment,
                    Strengths = DeserializeStringList(result?.AiStrengths),
                    AreasForImprovement = DeserializeStringList(result?.AiGaps),
                    Recommendations = DeserializeStringList(result?.AiRecommendations)
                });
            }

            var codingSession = campaign.InterviewSessions.FirstOrDefault(session =>
                !session.IsDeleted && session.InterviewRoundType == InterviewRoundType.Code);
            if (codingSession != null)
            {
                var assignedQuestions = await _codingSelectionService.SelectCodingQuestionsAsync(codingSession);
                var totalAssignedCount = assignedQuestions.Count > 0
                    ? assignedQuestions.Count
                    : (codingSession.QuestionCount > 0 ? codingSession.QuestionCount : 3);

                var submissions = await _context.CodingSubmissions
                    .AsNoTracking()
                    .Include(submission => submission.CodingQuestion)
                    .Where(submission => submission.InterviewSessionId == codingSession.InterviewSessionId)
                    .ToListAsync();

                var submissionGrouped = submissions
                    .GroupBy(submission => submission.CodingQuestionId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(s => CampaignResultCalculator.GetCodingScore(s.PassedTestCases, s.TotalTestCases))
                              .ThenByDescending(s => s.TotalTestCases == 0 ? 0m : (decimal)s.PassedTestCases / s.TotalTestCases)
                              .ThenByDescending(s => s.CreatedAt)
                              .First());

                var questionResults = new List<CodingQuestionResultDto>();
                int submittedCount = 0;

                if (assignedQuestions.Count > 0)
                {
                    foreach (var q in assignedQuestions)
                    {
                        if (submissionGrouped.TryGetValue(q.CodingQuestionId, out var sub))
                        {
                            submittedCount++;
                            questionResults.Add(new CodingQuestionResultDto
                            {
                                CodingQuestionId = sub.CodingQuestionId,
                                Title = q.Title ?? sub.CodingQuestion?.Title ?? $"Coding question {sub.CodingQuestionId}",
                                Score = CampaignResultCalculator.GetCodingScore(sub.PassedTestCases, sub.TotalTestCases),
                                PassRate = sub.TotalTestCases == 0
                                    ? 0m
                                    : CampaignResultCalculator.Round((decimal)sub.PassedTestCases / sub.TotalTestCases * 100m),
                                PassedTestCases = sub.PassedTestCases,
                                TotalTestCases = sub.TotalTestCases
                            });
                        }
                        else
                        {
                            var totalTc = q.TestCases?.Count ?? 0;
                            questionResults.Add(new CodingQuestionResultDto
                            {
                                CodingQuestionId = q.CodingQuestionId,
                                Title = q.Title ?? $"Coding question {q.CodingQuestionId}",
                                Score = 0m,
                                PassRate = 0m,
                                PassedTestCases = 0,
                                TotalTestCases = totalTc
                            });
                        }
                    }
                }
                else
                {
                    foreach (var sub in submissionGrouped.Values)
                    {
                        submittedCount++;
                        questionResults.Add(new CodingQuestionResultDto
                        {
                            CodingQuestionId = sub.CodingQuestionId,
                            Title = sub.CodingQuestion?.Title ?? $"Coding question {sub.CodingQuestionId}",
                            Score = CampaignResultCalculator.GetCodingScore(sub.PassedTestCases, sub.TotalTestCases),
                            PassRate = sub.TotalTestCases == 0
                                ? 0m
                                : CampaignResultCalculator.Round((decimal)sub.PassedTestCases / sub.TotalTestCases * 100m),
                            PassedTestCases = sub.PassedTestCases,
                            TotalTestCases = sub.TotalTestCases
                        });
                    }
                }

                questionResults = questionResults.OrderBy(item => item.CodingQuestionId).ToList();
                var totalCount = Math.Max(totalAssignedCount, questionResults.Count);
                var score = totalCount == 0
                    ? 0m
                    : CampaignResultCalculator.Round(questionResults.Sum(item => item.Score) / totalCount);

                var isVietnamese = string.Equals(campaign.Language, "vi", StringComparison.OrdinalIgnoreCase);
                var unsubmittedCount = totalCount - submittedCount;
                var summaryText = isVietnamese
                    ? (unsubmittedCount > 0
                        ? $"Điểm Coding được tính từ tỷ lệ test case vượt qua của tất cả {totalCount} bài trong phiên ({submittedCount} bài đã nộp, {unsubmittedCount} bài chưa nộp)."
                        : $"Điểm Coding được tính từ tỷ lệ test case vượt qua của tất cả {totalCount} bài đã nộp.")
                    : (unsubmittedCount > 0
                        ? $"The Coding score is calculated across all {totalCount} assigned problems ({submittedCount} submitted, {unsubmittedCount} unsubmitted)."
                        : $"The Coding score is calculated from passed test cases across {totalCount} assigned problems.");

                rounds.Add(new CampaignRoundResultDto
                {
                    InterviewSessionId = codingSession.InterviewSessionId,
                    RoundType = InterviewRoundType.Code.ToString(),
                    Score = score,
                    PerformanceBand = CampaignResultCalculator.GetPerformanceBand(score),
                    EvaluatedItemCount = totalCount,
                    Summary = summaryText,
                    Strengths = score >= 6.5m
                        ? new List<string> { isVietnamese ? "Khả năng hiện thực hoá lời giải bằng code đạt mức khá trở lên." : "Coding execution met or exceeded the good-performance threshold." }
                        : new List<string>(),
                    AreasForImprovement = score < 6.5m
                        ? new List<string> { isVietnamese ? "Tăng độ chính xác của lời giải và tỷ lệ test case vượt qua." : "Improve solution correctness and the test-case pass rate." }
                        : new List<string>(),
                    Recommendations = score < 8m
                        ? new List<string> { isVietnamese ? "Luyện thêm edge case, độ phức tạp và kiểm thử trước khi nộp." : "Practise edge cases, complexity analysis, and pre-submission testing." }
                        : new List<string>(),
                    CodingQuestions = questionResults
                });
            }

            var overallScore = CampaignResultCalculator.ApplyRoundWeights(rounds);
            var metrics = BuildDashboardMetrics(technicalDimensions, behaviouralCriteria, rounds);
            var feedback = BuildCampaignFeedback(campaign.Language, overallScore, rounds);

            try
            {
                campaign.DashboardMetricsJson = JsonSerializer.Serialize(metrics);
                campaign.OverallScore = overallScore;

                if (campaign.Mode == InterviewMode.RealTest)
                {
                    campaign.ProfessionalKnowledge = metrics.FirstOrDefault(m => string.Equals(m.Code, "PROFESSIONAL_KNOWLEDGE", StringComparison.OrdinalIgnoreCase))?.Score;
                    campaign.CommunicationSkill = metrics.FirstOrDefault(m => string.Equals(m.Code, "COMMUNICATION_SKILLS", StringComparison.OrdinalIgnoreCase))?.Score;
                    campaign.CvUnderstanding = metrics.FirstOrDefault(m => string.Equals(m.Code, "CV_UNDERSTANDING", StringComparison.OrdinalIgnoreCase))?.Score;
                    campaign.ProblemSolving = metrics.FirstOrDefault(m => string.Equals(m.Code, "PROBLEM_SOLVING", StringComparison.OrdinalIgnoreCase))?.Score;
                }
                else
                {
                    campaign.ProfessionalKnowledge = null;
                    campaign.CommunicationSkill = null;
                    campaign.CvUnderstanding = null;
                    campaign.ProblemSolving = null;
                }

                campaign.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await SyncSkillScoresToDbAsync(campaign.UserId, campaign.InterviewCampaignId, null, metrics, campaign.CompletedAt ?? DateTime.UtcNow);
                await _rewardService.AwardInterviewPointsAsync(campaign.UserId, campaign.InterviewCampaignId, overallScore);
            }
            catch { }

            return new CampaignInterviewResultDto
            {
                InterviewCampaignId = campaign.InterviewCampaignId,
                Status = campaign.Status.ToString(),
                Language = campaign.Language,
                OverallScore = overallScore,
                PerformanceBand = CampaignResultCalculator.GetPerformanceBand(overallScore),
                CompletedAt = AsUtc(campaign.CompletedAt),
                Rounds = rounds.OrderBy(round => GetRoundOrder(
                    Enum.Parse<InterviewRoundType>(round.RoundType))).ToList(),
                DashboardMetrics = metrics,
                Feedback = feedback
            };
        }

        public async Task<List<CampaignDashboardMetricDto>> GetUserCapabilitiesAsync(int userId)
        {
            await EnsureUserSkillScoresBackfilledAsync(userId);

            var skillScores = await _context.UserSkillScores
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.EvaluatedAt)
                .ToListAsync();

            var realTestCampaigns = await _context.InterviewCampaigns
                .AsNoTracking()
                .Include(c => c.JDExtractedProfile)
                .Where(c => c.UserId == userId && !c.IsDeleted && c.Mode == InterviewMode.RealTest && c.Status == InterviewCampaignStatus.Completed)
                .OrderBy(c => c.CompletedAt ?? c.CreatedAt)
                .ToListAsync();

            var skillDefinitions = new[]
            {
                ("PROFESSIONAL_KNOWLEDGE", "Professional Knowledge", new[] { "Technical Depth", "Coding" }),
                ("COMMUNICATION_SKILLS", "Communication Skills", new[] { "Technical Communication", "Behavioral Communication" }),
                ("CV_UNDERSTANDING", "CV Understanding", new[] { "Behavioral Action & Ownership" }),
                ("PROBLEM_SOLVING", "Problem Solving", new[] { "Coding", "Technical Depth" }),
            };

            var resultList = new List<CampaignDashboardMetricDto>();

            foreach (var (code, name, sources) in skillDefinitions)
            {
                var skillHistory = new List<SkillHistoryPointDto>();

                if (realTestCampaigns.Count > 0)
                {
                    foreach (var c in realTestCampaigns)
                    {
                        decimal? val = code switch
                        {
                            "PROFESSIONAL_KNOWLEDGE" => c.ProfessionalKnowledge,
                            "COMMUNICATION_SKILLS" => c.CommunicationSkill,
                            "CV_UNDERSTANDING" => c.CvUnderstanding,
                            "PROBLEM_SOLVING" => c.ProblemSolving,
                            _ => null
                        };

                        if (!val.HasValue || val.Value <= 0)
                        {
                            var matchScore = skillScores.FirstOrDefault(s => s.InterviewCampaignId == c.InterviewCampaignId && string.Equals(s.SkillCode, code, StringComparison.OrdinalIgnoreCase))?.Score;
                            if (matchScore.HasValue && matchScore.Value > 0) val = matchScore.Value;
                        }

                        if (val.HasValue && val.Value > 0)
                        {
                            var jobTitle = c.JDExtractedProfile?.JobTitle ?? c.JDExtractedProfile?.RoleTarget;
                            var title = !string.IsNullOrWhiteSpace(jobTitle) ? $"{jobTitle} — Phỏng vấn mô phỏng" : $"Phỏng vấn #{c.InterviewCampaignId}";
                            skillHistory.Add(new SkillHistoryPointDto
                            {
                                SessionId = c.InterviewCampaignId,
                                Title = title,
                                Score = val.Value,
                                Date = c.CompletedAt ?? c.CreatedAt
                            });
                        }
                    }
                }

                if (skillHistory.Count == 0)
                {
                    skillHistory = skillScores
                        .Where(s => string.Equals(s.SkillCode, code, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(s => s.EvaluatedAt)
                        .Select(s => new SkillHistoryPointDto
                        {
                            SessionId = s.InterviewSessionId ?? s.InterviewCampaignId ?? 0,
                            Title = !string.IsNullOrWhiteSpace(s.SessionTitle) ? s.SessionTitle : $"Phỏng vấn #{s.InterviewCampaignId ?? s.InterviewSessionId}",
                            Score = s.Score,
                            Date = s.EvaluatedAt
                        })
                        .ToList();
                }

                decimal? latestScore = skillHistory.Count > 0 ? skillHistory.Last().Score : null;

                resultList.Add(new CampaignDashboardMetricDto
                {
                    Code = code,
                    Name = name,
                    Score = latestScore,
                    Sources = sources.ToList(),
                    History = skillHistory
                });
            }

            return resultList;
        }

        private async Task SyncSkillScoresToDbAsync(
            int userId,
            int? campaignId,
            int? sessionId,
            List<CampaignDashboardMetricDto> metrics,
            DateTime? evaluatedAt = null)
        {
            if (metrics == null || metrics.Count == 0) return;
            var timestamp = evaluatedAt ?? DateTime.UtcNow;

            var title = "Đánh giá phỏng vấn";
            if (campaignId.HasValue)
            {
                var camp = await _context.InterviewCampaigns
                    .AsNoTracking()
                    .Include(c => c.JDExtractedProfile)
                    .FirstOrDefaultAsync(c => c.InterviewCampaignId == campaignId.Value);

                if (camp != null)
                {
                    var jobTitle = camp.JDExtractedProfile?.JobTitle ?? camp.JDExtractedProfile?.RoleTarget;
                    var modeStr = camp.Mode == InterviewMode.RealTest ? "RealTest" : "Practice";
                    title = !string.IsNullOrWhiteSpace(jobTitle) ? $"{jobTitle} — {modeStr}" : $"Phỏng vấn #{campaignId.Value}";
                }
                else
                {
                    title = $"Phỏng vấn #{campaignId.Value}";
                }
            }
            else if (sessionId.HasValue)
            {
                title = $"Phiên #{sessionId.Value}";
            }

            foreach (var metric in metrics)
            {
                if (!metric.Score.HasValue || metric.Score.Value <= 0) continue;

                var existing = await _context.UserSkillScores.FirstOrDefaultAsync(s =>
                    s.UserId == userId
                    && s.SkillCode == metric.Code
                    && s.InterviewCampaignId == campaignId
                    && s.InterviewSessionId == sessionId);

                if (existing != null)
                {
                    existing.Score = metric.Score.Value;
                    existing.SessionTitle = title;
                    existing.EvaluatedAt = timestamp;
                }
                else
                {
                    _context.UserSkillScores.Add(new UserSkillScore
                    {
                        UserId = userId,
                        InterviewCampaignId = campaignId,
                        InterviewSessionId = sessionId,
                        SkillCode = metric.Code,
                        SkillName = metric.Name ?? metric.Code,
                        Score = metric.Score.Value,
                        SessionTitle = title,
                        EvaluatedAt = timestamp,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            try
            {
                await _context.SaveChangesAsync();
            }
            catch { }
        }

        private async Task EnsureUserSkillScoresBackfilledAsync(int userId)
        {
            var practiceCampaignIds = await _context.InterviewCampaigns
                .AsNoTracking()
                .Where(c => c.UserId == userId && c.Mode == InterviewMode.Practice)
                .Select(c => c.InterviewCampaignId)
                .ToListAsync();

            if (practiceCampaignIds.Count > 0)
            {
                var practiceScores = await _context.UserSkillScores
                    .Where(s => s.UserId == userId && s.InterviewCampaignId.HasValue && practiceCampaignIds.Contains(s.InterviewCampaignId.Value))
                    .ToListAsync();

                if (practiceScores.Count > 0)
                {
                    _context.UserSkillScores.RemoveRange(practiceScores);
                    await _context.SaveChangesAsync();
                }
            }

            var existingScores = await _context.UserSkillScores
                .Where(s => s.UserId == userId && s.InterviewCampaignId.HasValue)
                .ToListAsync();

            if (existingScores.Count > 0)
            {
                var campIds = existingScores.Select(s => s.InterviewCampaignId!.Value).Distinct().ToList();
                var campaigns = await _context.InterviewCampaigns
                    .AsNoTracking()
                    .Include(c => c.JDExtractedProfile)
                    .Where(c => campIds.Contains(c.InterviewCampaignId))
                    .ToDictionaryAsync(c => c.InterviewCampaignId);

                var titleUpdated = false;
                foreach (var score in existingScores)
                {
                    if (score.InterviewCampaignId.HasValue && campaigns.TryGetValue(score.InterviewCampaignId.Value, out var c))
                    {
                        var jobTitle = c.JDExtractedProfile?.JobTitle ?? c.JDExtractedProfile?.RoleTarget;
                        var modeStr = c.Mode == InterviewMode.RealTest ? "RealTest" : "Practice";
                        var newTitle = !string.IsNullOrWhiteSpace(jobTitle) ? $"{jobTitle} — {modeStr}" : $"Phỏng vấn #{c.InterviewCampaignId}";
                        if (score.SessionTitle != newTitle)
                        {
                            score.SessionTitle = newTitle;
                            titleUpdated = true;
                        }
                    }
                }
                if (titleUpdated)
                {
                    try { await _context.SaveChangesAsync(); } catch { }
                }
            }

            var hasScores = await _context.UserSkillScores.AsNoTracking().AnyAsync(s => s.UserId == userId && s.Score > 0);
            if (hasScores) return;

            var userCampaigns = await _context.InterviewCampaigns
                .AsNoTracking()
                .Include(c => c.JDExtractedProfile)
                .Where(c => c.UserId == userId && !c.IsDeleted && c.Mode == InterviewMode.RealTest)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            foreach (var campaign in userCampaigns)
            {
                List<CampaignDashboardMetricDto>? metrics = null;
                if (!string.IsNullOrWhiteSpace(campaign.DashboardMetricsJson))
                {
                    try
                    {
                        metrics = JsonSerializer.Deserialize<List<CampaignDashboardMetricDto>>(campaign.DashboardMetricsJson);
                    }
                    catch { }
                }

                if (metrics == null || !metrics.Any(m => m.Score.HasValue && m.Score.Value > 0))
                {
                    var res = await GetCampaignResultAsync(userId, campaign.InterviewCampaignId);
                    metrics = res?.DashboardMetrics;
                }

                if (metrics != null && metrics.Any(m => m.Score.HasValue && m.Score.Value > 0))
                {
                    await SyncSkillScoresToDbAsync(
                        userId,
                        campaign.InterviewCampaignId,
                        null,
                        metrics,
                        campaign.CompletedAt ?? campaign.CreatedAt);
                }
            }

            var hasScoresNow = await _context.UserSkillScores.AsNoTracking().AnyAsync(s => s.UserId == userId && s.Score > 0);
            if (hasScoresNow) return;

            // Deep Fallback: Query all evaluated interview sessions for this user
            var userSessions = await (
                from s in _context.InterviewSessions.AsNoTracking()
                join c in _context.InterviewCampaigns.AsNoTracking() on s.InterviewCampaignId equals c.InterviewCampaignId
                where c.UserId == userId
                    && s.InterviewRoundType == InterviewRoundType.Technical
                    && !s.IsDeleted
                    && !c.IsDeleted
                orderby s.CreatedAt
                select new
                {
                    s.InterviewSessionId,
                    s.InterviewCampaignId,
                    s.CreatedAt,
                    s.InterviewRoundType,
                    V2Score = s.TechnicalRoundResult != null ? s.TechnicalRoundResult.OverallScore : null,
                    V2Answers = s.TechnicalQuestionSet != null
                        ? s.TechnicalQuestionSet.Questions
                            .Where(q => q.QuestionType == TechnicalSessionQuestionType.Main && q.Answer != null)
                            .Select(q => q.Answer!.FinalQuestionScore)
                            .ToList()
                        : new List<decimal?>()
                }
            ).ToListAsync();

            foreach (var session in userSessions)
            {
                decimal? scoreVal = session.InterviewRoundType == InterviewRoundType.Technical
                    ? session.V2Score
                    : null;
                if (!scoreVal.HasValue || scoreVal.Value <= 0)
                {
                    var validScores = session.InterviewRoundType == InterviewRoundType.Technical
                        ? session.V2Answers.Where(a => a.HasValue && a.Value > 0).Select(a => a!.Value).ToList()
                        : new List<decimal>();
                    if (validScores.Count > 0)
                    {
                        scoreVal = Math.Round(validScores.Average(), 1);
                    }
                }

                if (scoreVal.HasValue && scoreVal.Value > 0)
                {
                    var sessionMetrics = new List<CampaignDashboardMetricDto>
                    {
                        new() { Code = "PROFESSIONAL_KNOWLEDGE", Name = "Professional Knowledge", Score = scoreVal.Value },
                        new() { Code = "COMMUNICATION_SKILLS", Name = "Communication Skills", Score = scoreVal.Value },
                        new() { Code = "CV_UNDERSTANDING", Name = "CV Understanding", Score = scoreVal.Value },
                        new() { Code = "PROBLEM_SOLVING", Name = "Problem Solving", Score = scoreVal.Value },
                    };
                    await SyncSkillScoresToDbAsync(userId, session.InterviewCampaignId, session.InterviewSessionId, sessionMetrics, session.CreatedAt);
                }
            }
        }

        public async Task<InterviewCampaignDto?> GetActiveCampaignAsync(int userId)
        {
            var campaigns = await _context.InterviewCampaigns
                .Include(campaign => campaign.User)
                .Include(campaign => campaign.InterviewSessions.Where(session => !session.IsDeleted))
                    .ThenInclude(session => session.TechnicalQuestionSet)
                        .ThenInclude(set => set!.Questions)
                            .ThenInclude(question => question.Answer)
                .Where(campaign => campaign.UserId == userId
                    && !campaign.IsDeleted
                    && (campaign.Status == InterviewCampaignStatus.Pending
                        || campaign.Status == InterviewCampaignStatus.Active))
                .OrderByDescending(campaign => campaign.UpdatedAt ?? campaign.CreatedAt)
                .ToListAsync();

            var now = DateTime.UtcNow;
            var lifecycleChanged = false;
            foreach (var campaign in campaigns)
            {
                lifecycleChanged |= EnsureActiveCampaignTiming(campaign, now);
                lifecycleChanged |= ExpireIfDue(campaign, campaign.User, now);
            }

            if (lifecycleChanged) await _context.SaveChangesAsync();

            var activeCampaign = campaigns.FirstOrDefault(IsLiveCampaign);
            if (activeCampaign == null) return null;

            var quota = await GetQuotaMetadataAsync(activeCampaign.User, now);
            return MapCampaignToResponse(activeCampaign, quota);
        }

        public async Task<IEnumerable<InterviewCampaignDto>> GetUserCampaignsAsync(int userId)
        {
            var campaigns = (await _repository.GetCampaignsByUserIdAsync(userId)).ToList();
            var user = campaigns.FirstOrDefault()?.User
                ?? await _context.Users.FirstOrDefaultAsync(candidate => candidate.UserId == userId);
            if (user == null) return Array.Empty<InterviewCampaignDto>();

            var now = DateTime.UtcNow;
            var lifecycleChanged = false;
            foreach (var campaign in campaigns)
                lifecycleChanged |= ExpireIfDue(campaign, user, now);
            if (lifecycleChanged) await _context.SaveChangesAsync();
            var quota = await GetQuotaMetadataAsync(user, now);
            var behaviourCompletedCounts = await GetBehaviourCompletedMainQuestionCountsAsync(
                campaigns.Select(campaign => campaign.InterviewCampaignId));
            var codingTestcaseCounts = await GetCodingTestcaseCountsAsync(
                campaigns.Select(campaign => campaign.InterviewCampaignId));

            return campaigns
                .Select(campaign => MapCampaignToResponse(campaign, quota, behaviourCompletedCounts, codingTestcaseCounts))
                .ToList();
        }

        public async Task<AvailableRoundsDto?> GetAvailableRoundsAsync(int userId, int jdId)
        {
            var jdFile = await _context.JDFiles
                .FirstOrDefaultAsync(file => file.JDFileId == jdId && file.UserId == userId);
            if (jdFile == null
                || (jdFile.Status != JDFileStatus.ConfirmationRequired
                    && jdFile.Status != JDFileStatus.Confirmed))
            {
                return null;
            }

            var jdProfile = await _context.JDExtractedProfiles
                .FirstOrDefaultAsync(profile => profile.JDFileId == jdId);
            if (jdProfile == null) return null;

            var result = RoleCategoryHelper.GetAvailableRounds(jdProfile.RoleTarget);
            result.Difficulty = MapExperienceLevelToDifficulty(jdProfile.ExperienceLevel).ToString();
            return result;
        }

        private async Task<InterviewCampaign?> GetOwnedCampaignAsync(int userId, int campaignId)
        {
            var campaign = await _repository.GetCampaignByIdAsync(campaignId);
            return campaign?.UserId == userId ? campaign : null;
        }

        private async Task<InterviewSession?> GetOwnedSessionWithCampaignAsync(int userId, int sessionId)
        {
            var session = await _context.InterviewSessions
                .Include(candidate => candidate.InterviewCampaign)
                    .ThenInclude(campaign => campaign.User)
                .Include(candidate => candidate.InterviewCampaign)
                    .ThenInclude(campaign => campaign.InterviewSessions.Where(item => !item.IsDeleted))
                .Include(candidate => candidate.TechnicalQuestionSet)
                    .ThenInclude(set => set!.Questions)
                        .ThenInclude(question => question.Answer)
                .FirstOrDefaultAsync(candidate => candidate.InterviewSessionId == sessionId
                    && !candidate.IsDeleted
                    && !candidate.InterviewCampaign.IsDeleted);
            return session?.InterviewCampaign.UserId == userId ? session : null;
        }

        private static bool IsLiveCampaign(InterviewCampaign campaign) =>
            campaign.Status == InterviewCampaignStatus.Pending || campaign.Status == InterviewCampaignStatus.Active;

        private async Task<QuotaMetadata> AdvanceCampaignAsync(InterviewCampaign campaign, DateTime now)
        {
            // A completed round may be retried after a transient failure. If another round is
            // already active, the lifecycle transition has already succeeded and is idempotent.
            if (campaign.InterviewSessions.Any(candidate =>
                !candidate.IsDeleted && candidate.Status == InterviewSessionStatus.Active))
            {
                if (campaign.Status == InterviewCampaignStatus.Pending)
                {
                    campaign.Status = InterviewCampaignStatus.Active;
                    campaign.StartedAt ??= now;
                    campaign.ExpiresAt = campaign.StartedAt.Value.AddMinutes(campaign.DurationMinutes);
                }
                campaign.UpdatedAt = now;
                await _context.SaveChangesAsync();
                return await GetQuotaMetadataAsync(campaign.User, now);
            }

            var nextSession = campaign.InterviewSessions
                .Where(candidate => !candidate.IsDeleted
                    && candidate.Status != InterviewSessionStatus.Completed
                    && candidate.Status != InterviewSessionStatus.Cancelled)
                .OrderBy(candidate => GetRoundOrder(candidate.InterviewRoundType))
                .ThenBy(candidate => candidate.InterviewSessionId)
                .FirstOrDefault();

            if (nextSession != null)
            {
                nextSession.Status = InterviewSessionStatus.Active;
                nextSession.UpdatedAt = now;
                if (campaign.Status == InterviewCampaignStatus.Pending)
                {
                    campaign.StartedAt ??= now;
                    campaign.ExpiresAt = campaign.StartedAt.Value.AddMinutes(campaign.DurationMinutes);
                }
                campaign.Status = InterviewCampaignStatus.Active;
                campaign.UpdatedAt = now;
                await _context.SaveChangesAsync();
                return await GetQuotaMetadataAsync(campaign.User, now);
            }

            var quota = await GetQuotaMetadataAsync(campaign.User, now);
            if (campaign.Status != InterviewCampaignStatus.Completed)
            {
                campaign.Status = InterviewCampaignStatus.Completed;
                campaign.CompletedAt = now;
                campaign.UpdatedAt = now;
                var consumed = await _subscriptionService.ConsumeCampaignQuotaAsync(
                    campaign.User,
                    campaign.InterviewCampaignId,
                    now);
                quota = new QuotaMetadata(consumed.Remaining, consumed.Limit, consumed.PlanCode == "PREMIUM" ? "Premium" : "Free");
            }

            await _context.SaveChangesAsync();
            return quota;
        }

        private async Task PublishRoundCompletionAsync(int userId, InterviewSession session, InterviewCampaign campaign)
        {
            if (_notificationPublisher is not null)
                await _notificationPublisher.UpdateActionStatusAsync(userId, NotificationRecipientRole.USER, NotificationEntityType.INTERVIEW_SESSION, session.InterviewSessionId.ToString(), NotificationActionStatus.COMPLETED);
            await PublishSafelyAsync(new NotificationEvent(
                userId, NotificationRecipientRole.USER, NotificationType.INTERVIEW_ROUND_COMPLETED,
                NotificationCategory.INTERVIEW, NotificationSeverity.SUCCESS,
                $"{GetRoundDisplayName(session.InterviewRoundType)} Interview completed",
                $"Your {GetRoundDisplayName(session.InterviewRoundType)} Interview has been completed successfully.",
                NotificationEntityType.INTERVIEW_ROUND, session.InterviewSessionId.ToString(), "/user/interview-history",
                $"INTERVIEW_ROUND_COMPLETED:{session.InterviewSessionId}:{userId}",
                new { sessionId = session.InterviewSessionId, roundId = session.InterviewSessionId, roundType = ToContractRoundType(session.InterviewRoundType) },
                NotificationActionStatus.COMPLETED));
            if (campaign.Status == InterviewCampaignStatus.Completed)
            {
                var earnedPoints = await _rewardService.AwardInterviewPointsAsync(userId, campaign.InterviewCampaignId, campaign.OverallScore ?? 6.0m);
                await PublishSafelyAsync(new NotificationEvent(
                    userId, NotificationRecipientRole.USER, NotificationType.ALL_INTERVIEW_ROUNDS_COMPLETED,
                    NotificationCategory.INTERVIEW, NotificationSeverity.SUCCESS,
                    "Interview completed",
                    $"You have completed all required interview rounds and earned +{earnedPoints} reward points!",
                    NotificationEntityType.INTERVIEW_RESULT,
                    campaign.InterviewCampaignId.ToString(),
                    $"/user/interview/campaign-result/{campaign.InterviewCampaignId}",
                    $"ALL_INTERVIEW_ROUNDS_COMPLETED:{campaign.InterviewCampaignId}:{userId}",
                    new { campaignId = campaign.InterviewCampaignId, earnedPoints, points = earnedPoints, overallScore = campaign.OverallScore }));
            }
        }

        private async Task EnsureCodingSubmissionsExistAsync(InterviewSession session)
        {
            if (session.InterviewRoundType != InterviewRoundType.Code) return;

            try
            {
                var assignedQuestions = await _codingSelectionService.SelectCodingQuestionsAsync(session);
                if (assignedQuestions.Count == 0) return;

                var existingSubQIds = await _context.CodingSubmissions
                    .Where(s => s.InterviewSessionId == session.InterviewSessionId)
                    .Select(s => s.CodingQuestionId)
                    .Distinct()
                    .ToListAsync();

                var addedAny = false;
                foreach (var q in assignedQuestions)
                {
                    if (!existingSubQIds.Contains(q.CodingQuestionId))
                    {
                        var template = q.CodingQuestionTemplates?.FirstOrDefault();
                        var langId = template?.LanguageId ?? 51;
                        var starterCode = template?.TemplateCode
                            ?? q.StarterCode
                            ?? $"// Auto-submitted code for {q.Title ?? "Coding Question"}\npublic class Solution {{ public void {q.FunctionName ?? "solution"}() {{ }} }}";

                        var testCasesCount = await _context.TestCases
                            .CountAsync(tc => tc.CodingQuestionId == q.CodingQuestionId);
                        int totalTc = testCasesCount > 0 ? testCasesCount : 1;

                        var fallbackSubmission = new CodingSubmission
                        {
                            InterviewSessionId = session.InterviewSessionId,
                            CodingQuestionId = q.CodingQuestionId,
                            SourceCode = starterCode,
                            LanguageId = langId,
                            Status = "Completed",
                            TotalTestCases = totalTc,
                            PassedTestCases = 0,
                            MaxTimeMs = 0,
                            MaxMemoryKb = 0,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.CodingSubmissions.Add(fallbackSubmission);
                        addedAny = true;
                    }
                }

                if (addedAny)
                {
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tự động tạo bản ghi nộp bài cho phiên Coding {SessionId}", session.InterviewSessionId);
            }
        }

        private async Task PublishCampaignExpiredAsync(int userId, InterviewCampaign campaign)
        {
            if (_notificationPublisher is not null)
            {
                foreach (var session in campaign.InterviewSessions.Where(item => !item.IsDeleted))
                    await _notificationPublisher.UpdateActionStatusAsync(userId, NotificationRecipientRole.USER, NotificationEntityType.INTERVIEW_SESSION, session.InterviewSessionId.ToString(), NotificationActionStatus.EXPIRED);
            }
            await PublishSafelyAsync(new NotificationEvent(
                userId, NotificationRecipientRole.USER, NotificationType.INTERVIEW_SESSION_EXPIRED,
                NotificationCategory.INTERVIEW, NotificationSeverity.WARNING, "Interview session expired",
                "Your interview session has expired and can no longer be resumed.",
                NotificationEntityType.INTERVIEW_SESSION, campaign.InterviewCampaignId.ToString(), "/user/interview-history",
                $"INTERVIEW_SESSION_EXPIRED:{campaign.InterviewCampaignId}:{userId}", null,
                NotificationActionStatus.EXPIRED));
        }

        private async Task PublishSafelyAsync(NotificationEvent notificationEvent)
        {
            if (_notificationPublisher is null) return;
            try { await _notificationPublisher.PublishAsync(notificationEvent); }
            catch (Exception exception) { _logger.LogError(exception, "Notification publication failed for {NotificationType}.", notificationEvent.Type); }
        }

        private static string GetRoundDisplayName(InterviewRoundType roundType) => roundType switch
        {
            InterviewRoundType.Behavior => "Behavioral",
            InterviewRoundType.Technical => "Technical",
            InterviewRoundType.Code => "Coding",
            _ => roundType.ToString()
        };

        private static string ToContractRoundType(InterviewRoundType roundType) => roundType switch
        {
            InterviewRoundType.Behavior => "BEHAVIORAL",
            InterviewRoundType.Technical => "TECHNICAL",
            InterviewRoundType.Code => "CODING",
            _ => roundType.ToString().ToUpperInvariant()
        };

        private static bool MatchesConfiguration(
            InterviewCampaign campaign,
            int cvProfileId,
            int jdProfileId,
            string language,
            InterviewMode mode,
            int durationMinutes,
            IReadOnlyCollection<InterviewRoundType> roundTypes,
            IReadOnlyDictionary<string, int>? questionCounts,
            string? technicalProvider = null,
            IConfiguration? configuration = null)
        {
            var existingRounds = campaign.InterviewSessions
                .Where(session => !session.IsDeleted)
                .Select(session => session.InterviewRoundType)
                .Distinct()
                .OrderBy(GetRoundOrder)
                .ToArray();
            var requestedRounds = roundTypes.Distinct().OrderBy(GetRoundOrder).ToArray();

            return campaign.CVExtractedProfileId == cvProfileId
                && campaign.JDExtractedProfileId == jdProfileId
                && string.Equals(campaign.Language, language.Trim(), StringComparison.OrdinalIgnoreCase)
                && campaign.Mode == mode
                && campaign.DurationMinutes == durationMinutes
                && existingRounds.SequenceEqual(requestedRounds)
                && (!requestedRounds.Contains(InterviewRoundType.Technical)
                    || string.IsNullOrWhiteSpace(technicalProvider)
                    || campaign.InterviewSessions
                        .Where(session => !session.IsDeleted && session.InterviewRoundType == InterviewRoundType.Technical)
                        .All(session => string.Equals(session.TechnicalAiProvider, technicalProvider, StringComparison.OrdinalIgnoreCase)))
                && campaign.InterviewSessions
                    .Where(session => !session.IsDeleted)
                    .All(session => session.QuestionCount == GetQuestionCount(mode, session.InterviewRoundType, questionCounts, configuration));
        }

        private static int GetQuestionCount(
            InterviewMode mode,
            InterviewRoundType roundType,
            IReadOnlyDictionary<string, int>? questionCounts,
            IConfiguration? configuration = null)
        {
            if (mode == InterviewMode.Practice && questionCounts != null)
            {
                var configuredCount = questionCounts.FirstOrDefault(item =>
                    string.Equals(item.Key, roundType.ToString(), StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(configuredCount.Key)) return configuredCount.Value;
            }

            int GetConfigInt(string envKey, string configKey, int fallback)
            {
                if (configuration != null)
                {
                    var val = configuration[envKey] ?? configuration[configKey];
                    if (int.TryParse(val, out var parsed) && parsed > 0) return parsed;
                }
                var envVal = Environment.GetEnvironmentVariable(envKey);
                if (int.TryParse(envVal, out var envParsed) && envParsed > 0) return envParsed;
                return fallback;
            }

            return roundType switch
            {
                InterviewRoundType.Technical => GetConfigInt(
                    "TECHNICAL_INTERVIEW_REALTIME_MAIN_QUESTION_COUNT",
                    "TechnicalInterviewAI:RealtimeMainQuestionCount",
                    3),
                InterviewRoundType.Behavior => GetConfigInt(
                    "BEHAVIOURAL_INTERVIEW_REALTIME_MAIN_QUESTION_COUNT",
                    "BehaviouralInterviewAI:RealtimeMainQuestionCount",
                    3),
                InterviewRoundType.Code => GetConfigInt(
                    "CODING_INTERVIEW_REALTIME_QUESTION_COUNT",
                    "CodingInterview:RealtimeQuestionCount",
                    3),
                _ => 3
            };
        }

        private static bool ExpireIfDue(InterviewCampaign campaign, User user, DateTime now)
        {
            if (!IsLiveCampaign(campaign) || !campaign.ExpiresAt.HasValue || campaign.ExpiresAt.Value > now)
                return false;

            campaign.Status = InterviewCampaignStatus.Expired;
            campaign.UpdatedAt = now;
            CancelOpenSessions(campaign, now);
            RefundUnusedQuota(campaign, user);
            return true;
        }

        private static bool EnsureActiveCampaignTiming(InterviewCampaign campaign, DateTime now)
        {
            if (campaign.Status != InterviewCampaignStatus.Active || campaign.DurationMinutes <= 0)
                return false;

            var changed = false;
            var initializedStart = false;
            if (!campaign.StartedAt.HasValue)
            {
                campaign.StartedAt = now;
                changed = true;
                initializedStart = true;
            }

            var configuredDeadline = campaign.StartedAt.Value.AddMinutes(campaign.DurationMinutes);
            if (initializedStart || !campaign.ExpiresAt.HasValue || campaign.ExpiresAt.Value > configuredDeadline)
            {
                campaign.ExpiresAt = configuredDeadline;
                changed = true;
            }

            if (changed) campaign.UpdatedAt = now;
            return changed;
        }

        private static void CancelOpenSessions(InterviewCampaign campaign, DateTime now)
        {
            foreach (var session in campaign.InterviewSessions.Where(item =>
                item.Status == InterviewSessionStatus.Pending || item.Status == InterviewSessionStatus.Active))
            {
                session.Status = InterviewSessionStatus.Cancelled;
                session.UpdatedAt = now;
            }
        }

        private static void RefundUnusedQuota(InterviewCampaign campaign, User user)
        {
            // Quota is consumed on completed campaigns only, so there is nothing to refund.
            campaign.QuotaRefunded = true;
        }

        private static List<CampaignDashboardMetricDto> BuildDashboardMetrics(
            IReadOnlyDictionary<string, decimal> technical,
            IReadOnlyDictionary<string, decimal> behavioural,
            IReadOnlyCollection<CampaignRoundResultDto> rounds)
        {
            decimal? Technical(string code) => technical.TryGetValue(code, out var value) ? value : null;
            decimal? TechnicalAny(params string[] codes) => codes.Select(Technical).FirstOrDefault(value => value.HasValue);
            decimal? Behavioural(string code) => behavioural.TryGetValue(code, out var value) ? value : null;
            var coding = rounds.FirstOrDefault(round =>
                string.Equals(round.RoundType, InterviewRoundType.Code.ToString(), StringComparison.OrdinalIgnoreCase))?.Score;

            CampaignDashboardMetricDto Metric(
                string code,
                string name,
                params (decimal? Score, decimal Weight, string Source)[] components) => new()
                {
                    Code = code,
                    Name = name,
                    Score = CampaignResultCalculator.CalculateMetric(components),
                    Sources = components
                        .Where(component => component.Score.HasValue)
                        .Select(component => component.Source)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                };

            return new List<CampaignDashboardMetricDto>
            {
                Metric("PROFESSIONAL_KNOWLEDGE", "Professional Knowledge",
                    (TechnicalAny("ACCURACY"), 0.35m, "Technical Accuracy"),
                    (TechnicalAny("TECHNICAL_DEPTH"), 0.25m, "Technical Depth"),
                    (TechnicalAny("APPLICATION"), 0.15m, "Application"),
                    (coding, 0.25m, "Coding")),
                Metric("COMMUNICATION_SKILLS", "Communication Skills",
                    (TechnicalAny("COMMUNICATION"), 0.40m, "Technical Communication"),
                    (Behavioural("communication"), 0.60m, "Behavioral Communication")),
                Metric("CV_UNDERSTANDING", "CV Understanding",
                    (TechnicalAny("APPLICATION"), 0.30m, "Application"),
                    (TechnicalAny("REASONING"), 0.30m, "Reasoning"),
                    (Behavioural("action"), 0.40m, "Behavioral Action & Ownership")),
                Metric("PROBLEM_SOLVING", "Problem Solving",
                    (coding, 0.35m, "Coding"),
                    (TechnicalAny("TECHNICAL_DEPTH"), 0.35m, "Technical Depth"),
                    (TechnicalAny("REASONING"), 0.30m, "Reasoning"))
            };
        }

        private static CampaignFinalFeedbackDto BuildCampaignFeedback(
            string language,
            decimal overallScore,
            IReadOnlyCollection<CampaignRoundResultDto> rounds)
        {
            var isVietnamese = string.Equals(language, "vi", StringComparison.OrdinalIgnoreCase);
            var strengths = DistinctItems(rounds.SelectMany(round => round.Strengths));
            var improvements = DistinctItems(rounds.SelectMany(round => round.AreasForImprovement));
            var recommendations = DistinctItems(rounds.SelectMany(round => round.Recommendations));
            var strongestRound = rounds.OrderByDescending(round => round.Score).FirstOrDefault();

            if (strengths.Count == 0 && strongestRound != null)
            {
                strengths.Add(isVietnamese
                    ? $"Vòng {strongestRound.RoundType} là phần thể hiện tốt nhất với {strongestRound.Score:0.00}/10."
                    : $"{strongestRound.RoundType} was the strongest round at {strongestRound.Score:0.00}/10.");
            }

            if (improvements.Count == 0)
            {
                improvements.Add(isVietnamese
                    ? "Tiếp tục tăng độ sâu của bằng chứng và tính nhất quán giữa các vòng."
                    : "Continue improving evidence depth and consistency across rounds.");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add(isVietnamese
                    ? "Ưu tiên luyện tập tiêu chí có điểm dashboard thấp nhất trong lần phỏng vấn tiếp theo."
                    : "Prioritise the lowest dashboard metric in the next interview practice session.");
            }

            return new CampaignFinalFeedbackDto
            {
                ExecutiveSummary = isVietnamese
                    ? $"Campaign đã hoàn tất với điểm tổng hợp {overallScore:0.00}/10 trên {rounds.Count} vòng phỏng vấn. Kết quả dùng trọng số rubric và chỉ tổng hợp các vòng đã chọn."
                    : $"The campaign finished with an aggregate score of {overallScore:0.00}/10 across {rounds.Count} interview rounds. The result uses rubric weights and includes only selected rounds.",
                Strengths = strengths.Take(6).ToList(),
                AreasForImprovement = improvements.Take(6).ToList(),
                Recommendations = recommendations.Take(6).ToList()
            };
        }

        private static List<string> DistinctItems(IEnumerable<string?> values) => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        private static T DeserializeOrDefault<T>(string? json) where T : new()
        {
            if (string.IsNullOrWhiteSpace(json)) return new T();
            try
            {
                return JsonSerializer.Deserialize<T>(json, ResultJsonOptions) ?? new T();
            }
            catch (JsonException)
            {
                return new T();
            }
        }

        private static List<T> DeserializeList<T>(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<T>();
            try
            {
                return JsonSerializer.Deserialize<List<T>>(json, ResultJsonOptions) ?? new List<T>();
            }
            catch (JsonException)
            {
                return new List<T>();
            }
        }

        private static List<string> DeserializeStringList(string? json) => DeserializeList<string>(json);

        private static Dictionary<string, decimal> DeserializeTechnicalV2Dimensions(IEnumerable<string?> jsonValues)
        {
            var values = new Dictionary<string, List<decimal>>(StringComparer.OrdinalIgnoreCase);
            foreach (var json in jsonValues)
            {
                if (string.IsNullOrWhiteSpace(json)) continue;
                try
                {
                    using var document = JsonDocument.Parse(json);
                    if (document.RootElement.ValueKind != JsonValueKind.Array) continue;
                    foreach (var item in document.RootElement.EnumerateArray())
                    {
                        if (!item.TryGetProperty("rubricCode", out var code)
                            || !item.TryGetProperty("suggestedScore", out var score)
                            || string.IsNullOrWhiteSpace(code.GetString())
                            || !score.TryGetDecimal(out var parsed)) continue;
                        var key = code.GetString()!;
                        if (!values.TryGetValue(key, out var list))
                        {
                            list = new List<decimal>();
                            values[key] = list;
                        }
                        list.Add(parsed);
                    }
                }
                catch (JsonException)
                {
                    // A malformed answer detail must not break campaign history.
                }
            }

            return values.ToDictionary(item => item.Key, item => item.Value.Average(), StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, decimal> DeserializeDictionary(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var values = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, ResultJsonOptions)
                    ?? new Dictionary<string, decimal>();
                return new Dictionary<string, decimal>(values, StringComparer.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private async Task<IReadOnlyDictionary<int, int>> GetBehaviourCompletedMainQuestionCountsAsync(
            IEnumerable<int> campaignIds)
        {
            var ids = campaignIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, int>();

            return await _context.BehaviourSessionQuestions
                .AsNoTracking()
                .Where(question => ids.Contains(question.BehaviourQuestionSet.InterviewSession.InterviewCampaignId)
                    && question.QuestionType == BehaviourQuestionType.Main
                    && question.Status == BehaviourQuestionStatus.Answered)
                .GroupBy(question => question.BehaviourQuestionSet.InterviewSessionId)
                .ToDictionaryAsync(group => group.Key, group => group.Count());
        }

        private async Task<IReadOnlyDictionary<int, (int passed, int total)>> GetCodingTestcaseCountsAsync(
            IEnumerable<int> campaignIds)
        {
            var ids = campaignIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, (int passed, int total)>();

            var submissions = await _context.CodingSubmissions
                .AsNoTracking()
                .Where(sub => ids.Contains(sub.InterviewSession.InterviewCampaignId))
                .ToListAsync();

            return submissions
                .GroupBy(sub => sub.InterviewSessionId)
                .ToDictionary(
                    group => group.Key,
                    group => {
                        var bestSubmissions = group.GroupBy(s => s.CodingQuestionId)
                            .Select(g => g.OrderByDescending(s => s.PassedTestCases)
                                          .ThenByDescending(s => s.TotalTestCases)
                                          .First());
                        return (passed: bestSubmissions.Sum(s => s.PassedTestCases), total: bestSubmissions.Sum(s => s.TotalTestCases));
                    });
        }

        private InterviewCampaignDto MapCampaignToResponse(
            InterviewCampaign campaign,
            QuotaMetadata? quota = null,
            IReadOnlyDictionary<int, int>? behaviourCompletedCounts = null,
            IReadOnlyDictionary<int, (int passed, int total)>? codingTestcaseCounts = null)
        {
            var metadata = quota ?? new QuotaMetadata(
                campaign.User?.RemainingInterviewQuota ?? 0,
                BasicInterviewQuota,
                "Free");

            var jobTitle = campaign.JDExtractedProfile?.JobTitle 
                ?? campaign.JDExtractedProfile?.RoleTarget
                ?? campaign.CVExtractedProfile?.RoleTarget;

            return new InterviewCampaignDto
            {
                InterviewCampaignId = campaign.InterviewCampaignId,
                UserId = campaign.UserId,
                CVExtractedProfileId = campaign.CVExtractedProfileId,
                JDExtractedProfileId = campaign.JDExtractedProfileId,
                JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle,
                Language = campaign.Language,
                Mode = campaign.Mode.ToString(),
                DurationMinutes = campaign.DurationMinutes,
                Status = campaign.Status.ToString(),
                StartedAt = AsUtc(campaign.StartedAt),
                ExpiresAt = AsUtc(campaign.ExpiresAt),
                CompletedAt = AsUtc(campaign.CompletedAt),
                CancelledAt = AsUtc(campaign.CancelledAt),
                RemainingInterviewQuota = metadata.Remaining,
                MaxInterviewQuota = metadata.Max,
                PlanName = metadata.PlanName,
                CreatedAt = AsUtc(campaign.CreatedAt),
                UpdatedAt = AsUtc(campaign.UpdatedAt),
                OverallScore = campaign.OverallScore,
                ProfessionalKnowledge = campaign.ProfessionalKnowledge,
                CommunicationSkill = campaign.CommunicationSkill,
                CvUnderstanding = campaign.CvUnderstanding,
                ProblemSolving = campaign.ProblemSolving,
                Sessions = campaign.InterviewSessions
                    .Where(session => !session.IsDeleted)
                    .OrderBy(session => GetRoundOrder(session.InterviewRoundType))
                    .ThenBy(session => session.InterviewSessionId)
                    .Select(session => MapToResponse(
                        session,
                        behaviourCompletedCounts is not null
                        && behaviourCompletedCounts.TryGetValue(session.InterviewSessionId, out var completedCount)
                            ? completedCount
                            : null,
                        codingTestcaseCounts is not null
                        && codingTestcaseCounts.TryGetValue(session.InterviewSessionId, out var testcaseCount)
                            ? testcaseCount
                            : null))
                    .ToList()
            };
        }

        private static InterviewSessionDto MapToResponse(
            InterviewSession session,
            int? behaviourCompletedMainQuestionCount = null,
            (int passed, int total)? codingTestcaseCount = null)
        {
            return new InterviewSessionDto
            {
                InterviewSessionId = session.InterviewSessionId,
                InterviewCampaignId = session.InterviewCampaignId,
                InterviewRoundType = session.InterviewRoundType.ToString(),
                Difficulty = session.Difficulty.ToString(),
                QuestionCount = session.QuestionCount,
                Status = session.Status.ToString(),
                CreatedAt = AsUtc(session.CreatedAt),
                UpdatedAt = AsUtc(session.UpdatedAt),
                CompletedQuestionCount = session.InterviewRoundType switch
                {
                    InterviewRoundType.Behavior => behaviourCompletedMainQuestionCount ?? 0,
                    InterviewRoundType.Technical => session.TechnicalQuestionSet?.Questions.Count(question =>
                        question.QuestionType == TechnicalSessionQuestionType.Main
                        && question.Answer?.FinalQuestionScore.HasValue == true) ?? 0,
                    _ => 0
                },
                PassedTestCases = session.InterviewRoundType == InterviewRoundType.Code ? codingTestcaseCount?.passed : null,
                TotalTestCases = session.InterviewRoundType == InterviewRoundType.Code ? codingTestcaseCount?.total : null
            };
        }

        private static DateTime AsUtc(DateTime value) =>
            value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static DateTime? AsUtc(DateTime? value) =>
            value.HasValue ? AsUtc(value.Value) : null;

        private static int GetRoundOrder(InterviewRoundType roundType) => roundType switch
        {
            InterviewRoundType.Behavior => 0,
            InterviewRoundType.Technical => 1,
            InterviewRoundType.Code => 2,
            _ => int.MaxValue
        };

        private static QuestionDifficultyEnum MapExperienceLevelToDifficulty(string? experienceLevel)
        {
            if (string.IsNullOrWhiteSpace(experienceLevel)) return QuestionDifficultyEnum.Medium;
            var normalized = experienceLevel.Trim().ToUpperInvariant();
            if (normalized.Contains("INTERN") || normalized.Contains("JUNIOR") || normalized.Contains("FRESHER"))
                return QuestionDifficultyEnum.Easy;
            if (normalized.Contains("SENIOR") || normalized.Contains("LEAD") || normalized.Contains("MANAGER")
                || normalized.Contains("PRINCIPAL") || normalized.Contains("EXPERT"))
                return QuestionDifficultyEnum.Hard;
            return QuestionDifficultyEnum.Medium;
        }

        private async Task<QuotaMetadata> GetQuotaMetadataAsync(User user, DateTime now)
        {
            var quota = await _subscriptionService.GetQuotaAsync(user, now);
            return new QuotaMetadata(
                quota.Remaining,
                quota.Limit,
                quota.PlanCode == "PREMIUM" ? "Premium" : "Free");
        }
    }
}
