using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using ai_speis_be.Helpers;
using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.InterviewCampaignRepo;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.InterviewSessionService
{
    public class InterviewSessionService : IInterviewSessionService
    {
        private static readonly TimeSpan PendingCampaignLifetime = TimeSpan.FromMinutes(30);
        private const int BasicInterviewQuota = 5;
        private const int PremiumInterviewQuota = 15;

        private readonly record struct QuotaMetadata(int Remaining, int Max, string PlanName);

        private readonly IInterviewCampaignRepository _repository;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InterviewSessionService> _logger;

        public InterviewSessionService(
            IInterviewCampaignRepository repository,
            ApplicationDbContext context,
            ILogger<InterviewSessionService> logger)
        {
            _repository = repository;
            _context = context;
            _logger = logger;
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

            var requestedRounds = mode == InterviewMode.Practice
                ? request.SelectedRounds ?? new List<string>()
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
                        request.QuestionCounts))
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

                var campaign = new InterviewCampaign
                {
                    UserId = userId,
                    CVExtractedProfileId = cvProfile.ExtractedProfileId,
                    JDExtractedProfileId = jdProfile.ExtractedProfileId,
                    Language = request.Language.Trim().ToLowerInvariant(),
                    Mode = mode,
                    DurationMinutes = request.DurationMinutes,
                    Status = InterviewCampaignStatus.Pending,
                    ExpiresAt = now.Add(PendingCampaignLifetime),
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
                        QuestionCount = GetQuestionCount(mode, roundType, request.QuestionCounts),
                        Status = InterviewSessionStatus.Pending,
                        CreatedAt = now
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var createdCampaign = await GetCampaignByIdAsync(userId, campaign.InterviewCampaignId);
                return (true, null, createdCampaign);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync();
                _logger.LogError(
                    exception,
                    "Không thể tạo campaign phỏng vấn cho User {UserId}, CV {CVFileId}, JD {JDFileId}.",
                    userId,
                    request.CVFileId,
                    request.JDFileId);
                return (false, "Không thể lưu cấu hình phỏng vấn. Vui lòng thử lại sau.", null);
            }
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

            if (session.Status != InterviewSessionStatus.Pending)
                return (false, $"Phiên phỏng vấn đang ở trạng thái '{session.Status}' và không thể bắt đầu.", null);
            if (!IsLiveCampaign(campaign))
                return (false, $"Campaign đang ở trạng thái '{campaign.Status}' và không thể bắt đầu.", null);
            if (campaign.InterviewSessions.Any(candidate =>
                candidate.InterviewSessionId != session.InterviewSessionId
                && candidate.Status == InterviewSessionStatus.Active))
            {
                return (false, "Campaign đã có một phiên đang hoạt động.", null);
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
                var existingQuota = await GetQuotaMetadataAsync(campaign.User, now);
                return (true, null, MapCampaignToResponse(campaign, existingQuota));
            }
            if (session.Status != InterviewSessionStatus.Active)
                return (false, "Chỉ có thể hoàn tất phiên đang hoạt động.", null);

            session.Status = InterviewSessionStatus.Completed;
            session.UpdatedAt = now;

            var nextSession = campaign.InterviewSessions
                .Where(candidate => !candidate.IsDeleted && candidate.Status == InterviewSessionStatus.Pending)
                .OrderBy(candidate => GetRoundOrder(candidate.InterviewRoundType))
                .ThenBy(candidate => candidate.InterviewSessionId)
                .FirstOrDefault();

            if (nextSession != null)
            {
                nextSession.Status = InterviewSessionStatus.Active;
                nextSession.UpdatedAt = now;
            }
            else
            {
                campaign.Status = InterviewCampaignStatus.Completed;
                campaign.CompletedAt = now;

                var quota = await GetQuotaMetadataAsync(campaign.User, now);
                if (quota.Remaining > 0)
                {
                    campaign.User.RemainingInterviewQuota = quota.Remaining - 1;
                    campaign.User.UpdatedAt = now;
                    quota = quota with { Remaining = campaign.User.RemainingInterviewQuota };
                }

                campaign.UpdatedAt = now;
                await _context.SaveChangesAsync();
                return (true, null, MapCampaignToResponse(campaign, quota));
            }

            campaign.UpdatedAt = now;
            await _context.SaveChangesAsync();
            var inProgressQuota = await GetQuotaMetadataAsync(campaign.User, now);
            return (true, null, MapCampaignToResponse(campaign, inProgressQuota));
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
            if (lifecycleChanged || previousRemainingQuota != user.RemainingInterviewQuota) await _context.SaveChangesAsync();

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
            return campaigns.Select(campaign => MapCampaignToResponse(campaign, quota)).ToList();
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

        private static bool MatchesConfiguration(
            InterviewCampaign campaign,
            int cvProfileId,
            int jdProfileId,
            string language,
            InterviewMode mode,
            int durationMinutes,
            IReadOnlyCollection<InterviewRoundType> roundTypes,
            IReadOnlyDictionary<string, int>? questionCounts)
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
                    .All(session => session.QuestionCount == GetQuestionCount(mode, session.InterviewRoundType, questionCounts));
        }

        private static int GetQuestionCount(
            InterviewMode mode,
            InterviewRoundType roundType,
            IReadOnlyDictionary<string, int>? questionCounts)
        {
            const int defaultQuestionCount = 5;
            const int defaultCodingQuestionCount = 3;
            // Adaptive Question Generation Rubric: Behavioral Interview luôn gồm 03 Main Questions
            const int defaultBehaviouralQuestionCount = 3;

            if (mode == InterviewMode.Practice && questionCounts != null)
            {
                var configuredCount = questionCounts.FirstOrDefault(item =>
                    string.Equals(item.Key, roundType.ToString(), StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(configuredCount.Key)) return configuredCount.Value;
            }

            return roundType switch
            {
                InterviewRoundType.Code => defaultCodingQuestionCount,
                InterviewRoundType.Behavior => defaultBehaviouralQuestionCount,
                InterviewRoundType.Technical => 3,
                _ => defaultQuestionCount
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

        private InterviewCampaignDto MapCampaignToResponse(InterviewCampaign campaign, QuotaMetadata? quota = null)
        {
            var metadata = quota ?? new QuotaMetadata(campaign.User?.RemainingInterviewQuota ?? 0, BasicInterviewQuota, "Basic");
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
                    .Select(MapToResponse)
                    .ToList()
            };
        }

        private static InterviewSessionDto MapToResponse(InterviewSession session)
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
                CompletedQuestionCount = session.TechnicalCompletedMainQuestionCount
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
            var isPremium = await _context.Payments.AnyAsync(payment =>
                payment.UserId == user.UserId && payment.Status == PaymentStatus.Paid);

            var maxQuota = isPremium ? PremiumInterviewQuota : BasicInterviewQuota;
            var normalizedRemaining = Math.Clamp(user.RemainingInterviewQuota, 0, maxQuota);
            if (user.RemainingInterviewQuota != normalizedRemaining)
            {
                user.RemainingInterviewQuota = normalizedRemaining;
                user.UpdatedAt = now;
            }

            return new QuotaMetadata(normalizedRemaining, maxQuota, isPremium ? "Premium" : "Basic");
        }
    }
}
