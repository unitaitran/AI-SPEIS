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
using ai_speis_be.TechnicalInterviews.DTOs;
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
            INotificationEventPublisher? notificationPublisher = null)
        {
            _repository = repository;
            _context = context;
            _logger = logger;
            _subscriptionService = subscriptionService;
            _rewardService = rewardService;
            _configuration = configuration;
            _notificationPublisher = notificationPublisher;
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
                        TechnicalAiProvider = !string.IsNullOrWhiteSpace(request.AiProvider) ? request.AiProvider : null,
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
                    other.Status = InterviewSessionStatus.Completed;
                    other.UpdatedAt = now;
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

            session.Status = InterviewSessionStatus.Completed;
            session.UpdatedAt = now;
            var quota = await AdvanceCampaignAsync(campaign, now);
            await PublishRoundCompletionAsync(userId, session, campaign);
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
                var summary = DeserializeOrDefault<TechnicalFinalSummaryDto>(technicalSession.TechnicalSummaryJson);
                var evaluationJson = await _context.TechnicalAnswerEvaluations
                    .Where(evaluation => evaluation.Attempt.InterviewSessionId == technicalSession.InterviewSessionId
                        && evaluation.Attempt.QuestionType == TechnicalAttemptType.Main)
                    .Select(evaluation => evaluation.ScoringBreakdownJson)
                    .ToListAsync();

                var dimensionScores = evaluationJson
                    .SelectMany(json => DeserializeList<TechnicalDimensionScore>(json))
                    .GroupBy(dimension => dimension.RubricCode, StringComparer.OrdinalIgnoreCase);
                foreach (var group in dimensionScores)
                {
                    technicalDimensions[group.Key] = CampaignResultCalculator.Round(
                        group.Average(dimension => dimension.FinalScore));
                }

                var completedMainAttempts = await _context.TechnicalQuestionAttempts
                    .Where(a => a.InterviewSessionId == technicalSession.InterviewSessionId
                        && a.QuestionType == TechnicalAttemptType.Main
                        && a.FinalMainScore.HasValue)
                    .ToListAsync();

                var calculatedScore = completedMainAttempts.Count > 0
                    ? CampaignResultCalculator.Round(completedMainAttempts.Average(a => a.FinalMainScore!.Value))
                    : 0m;
                var score = calculatedScore > 0m
                    ? calculatedScore
                    : CampaignResultCalculator.Round(technicalSession.TechnicalFinalScore ?? 0m);

                if (score > 0m && technicalSession.TechnicalFinalScore != score)
                {
                    technicalSession.TechnicalFinalScore = score;
                    technicalSession.TechnicalPerformanceBand = CampaignResultCalculator.GetPerformanceBand(score);
                    await _context.SaveChangesAsync();
                }
                rounds.Add(new CampaignRoundResultDto
                {
                    InterviewSessionId = technicalSession.InterviewSessionId,
                    RoundType = InterviewRoundType.Technical.ToString(),
                    Score = score,
                    PerformanceBand = string.IsNullOrWhiteSpace(technicalSession.TechnicalPerformanceBand)
                        ? CampaignResultCalculator.GetPerformanceBand(score)
                        : technicalSession.TechnicalPerformanceBand,
                    EvaluatedItemCount = technicalSession.TechnicalCompletedMainQuestionCount,
                    Summary = summary.Summary,
                    Strengths = summary.Strengths,
                    AreasForImprovement = summary.AreasForImprovement,
                    Recommendations = summary.RecommendedNextSteps
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
                    Strengths = DeserializeStringList(result?.AiStrengths),
                    AreasForImprovement = DeserializeStringList(result?.AiGaps),
                    Recommendations = DeserializeStringList(result?.AiRecommendations)
                });
            }

            var codingSession = campaign.InterviewSessions.FirstOrDefault(session =>
                !session.IsDeleted && session.InterviewRoundType == InterviewRoundType.Code);
            if (codingSession != null)
            {
                var submissions = await _context.CodingSubmissions
                    .AsNoTracking()
                    .Include(submission => submission.CodingQuestion)
                    .Where(submission => submission.InterviewSessionId == codingSession.InterviewSessionId)
                    .ToListAsync();

                var questionResults = submissions
                    .GroupBy(submission => submission.CodingQuestionId)
                    .Select(group => group
                        .OrderByDescending(submission => CampaignResultCalculator.GetCodingScore(
                            submission.PassedTestCases, submission.TotalTestCases))
                        .ThenByDescending(submission => submission.TotalTestCases == 0
                            ? 0m
                            : (decimal)submission.PassedTestCases / submission.TotalTestCases)
                        .ThenByDescending(submission => submission.CreatedAt)
                        .First())
                    .Select(submission => new CodingQuestionResultDto
                    {
                        CodingQuestionId = submission.CodingQuestionId,
                        Title = submission.CodingQuestion?.Title ?? $"Coding question {submission.CodingQuestionId}",
                        Score = CampaignResultCalculator.GetCodingScore(
                            submission.PassedTestCases, submission.TotalTestCases),
                        PassRate = submission.TotalTestCases == 0
                            ? 0m
                            : CampaignResultCalculator.Round(
                                (decimal)submission.PassedTestCases / submission.TotalTestCases * 100m),
                        PassedTestCases = submission.PassedTestCases,
                        TotalTestCases = submission.TotalTestCases
                    })
                    .OrderBy(item => item.CodingQuestionId)
                    .ToList();

                var score = questionResults.Count == 0
                    ? 0m
                    : CampaignResultCalculator.Round(questionResults.Average(item => item.Score));
                var isVietnamese = string.Equals(campaign.Language, "vi", StringComparison.OrdinalIgnoreCase);
                rounds.Add(new CampaignRoundResultDto
                {
                    InterviewSessionId = codingSession.InterviewSessionId,
                    RoundType = InterviewRoundType.Code.ToString(),
                    Score = score,
                    PerformanceBand = CampaignResultCalculator.GetPerformanceBand(score),
                    EvaluatedItemCount = questionResults.Count,
                    Summary = isVietnamese
                        ? $"Điểm Coding được tính từ tỷ lệ test case vượt qua của {questionResults.Count} bài đã nộp."
                        : $"The Coding score is calculated from passed test cases across {questionResults.Count} submitted problems.",
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
                var skillHistory = skillScores
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
            var title = campaignId.HasValue ? $"Phỏng vấn #{campaignId.Value}" : (sessionId.HasValue ? $"Phiên #{sessionId.Value}" : "Đánh giá phỏng vấn");

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
            var hasScores = await _context.UserSkillScores.AsNoTracking().AnyAsync(s => s.UserId == userId && s.Score > 0);
            if (hasScores) return;

            var userCampaigns = await _context.InterviewCampaigns
                .AsNoTracking()
                .Where(c => c.UserId == userId && !c.IsDeleted)
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
                where c.UserId == userId && !s.IsDeleted && !c.IsDeleted
                orderby s.CreatedAt
                select new
                {
                    s.InterviewSessionId,
                    s.InterviewCampaignId,
                    s.CreatedAt,
                    s.TechnicalFinalScore,
                    Attempts = s.TechnicalQuestionAttempts
                        .Where(a => a.FinalMainScore != null || a.RawScore != null || a.InitialMainScore != null)
                        .Select(a => (decimal?)(a.FinalMainScore ?? a.RawScore ?? a.InitialMainScore))
                        .ToList()
                }
            ).ToListAsync();

            foreach (var session in userSessions)
            {
                decimal? scoreVal = session.TechnicalFinalScore;
                if (!scoreVal.HasValue || scoreVal.Value <= 0)
                {
                    var validAttemptScores = session.Attempts.Where(a => a.HasValue && a.Value > 0).Select(a => a!.Value).ToList();
                    if (validAttemptScores.Count > 0)
                    {
                        scoreVal = Math.Round(validAttemptScores.Average(), 1);
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

            return campaigns
                .Select(campaign => MapCampaignToResponse(campaign, quota, behaviourCompletedCounts))
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
                .Where(candidate => !candidate.IsDeleted && candidate.Status == InterviewSessionStatus.Pending)
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
                await PublishSafelyAsync(new NotificationEvent(
                    userId, NotificationRecipientRole.USER, NotificationType.ALL_INTERVIEW_ROUNDS_COMPLETED,
                    NotificationCategory.INTERVIEW, NotificationSeverity.SUCCESS, "Interview completed",
                    "You have completed all required interview rounds.", NotificationEntityType.INTERVIEW_RESULT,
                    campaign.InterviewCampaignId.ToString(), "/user/interview/campaign-result",
                    $"ALL_INTERVIEW_ROUNDS_COMPLETED:{campaign.InterviewCampaignId}:{userId}"));
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
                    (Technical("ACCURACY"), 0.35m, "Technical Accuracy"),
                    (Technical("TECHNICAL_DEPTH"), 0.25m, "Technical Depth"),
                    (Technical("APPLICATION"), 0.15m, "Technical Application"),
                    (coding, 0.25m, "Coding")),
                Metric("COMMUNICATION_SKILLS", "Communication Skills",
                    (Technical("COMMUNICATION"), 0.40m, "Technical Communication"),
                    (Behavioural("communication"), 0.60m, "Behavioral Communication")),
                Metric("CV_UNDERSTANDING", "CV Understanding",
                    (Technical("APPLICATION"), 0.30m, "Technical Application"),
                    (Technical("REASONING"), 0.30m, "Technical Reasoning"),
                    (Behavioural("action"), 0.40m, "Behavioral Action & Ownership")),
                Metric("PROBLEM_SOLVING", "Problem Solving",
                    (coding, 0.35m, "Coding"),
                    (Technical("TECHNICAL_DEPTH"), 0.35m, "Technical Depth"),
                    (Technical("REASONING"), 0.30m, "Technical Reasoning"))
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

        private InterviewCampaignDto MapCampaignToResponse(
            InterviewCampaign campaign,
            QuotaMetadata? quota = null,
            IReadOnlyDictionary<int, int>? behaviourCompletedCounts = null)
        {
            var metadata = quota ?? new QuotaMetadata(campaign.User?.RemainingInterviewQuota ?? 0, BasicInterviewQuota, "Free");
            return new InterviewCampaignDto
            {
                InterviewCampaignId = campaign.InterviewCampaignId,
                UserId = campaign.UserId,
                CVExtractedProfileId = campaign.CVExtractedProfileId,
                JDExtractedProfileId = campaign.JDExtractedProfileId,
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
                Sessions = campaign.InterviewSessions
                    .Where(session => !session.IsDeleted)
                    .OrderBy(session => GetRoundOrder(session.InterviewRoundType))
                    .ThenBy(session => session.InterviewSessionId)
                    .Select(session => MapToResponse(
                        session,
                        behaviourCompletedCounts is not null
                        && behaviourCompletedCounts.TryGetValue(session.InterviewSessionId, out var completedCount)
                            ? completedCount
                            : null))
                    .ToList()
            };
        }

        private static InterviewSessionDto MapToResponse(
            InterviewSession session,
            int? behaviourCompletedMainQuestionCount = null)
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
                CompletedQuestionCount = session.InterviewRoundType == InterviewRoundType.Behavior
                    ? behaviourCompletedMainQuestionCount ?? 0
                    : session.TechnicalCompletedMainQuestionCount
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
