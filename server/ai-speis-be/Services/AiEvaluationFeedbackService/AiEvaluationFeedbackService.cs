using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.NotificationService;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ai_speis_be.Services.AiEvaluationFeedbackService
{
    public sealed class AiEvaluationFeedbackService : IAiEvaluationFeedbackService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAdminNotificationPublisher? _adminNotificationPublisher;
        private readonly ILogger<AiEvaluationFeedbackService>? _logger;

        public AiEvaluationFeedbackService(
            ApplicationDbContext context,
            IAdminNotificationPublisher? adminNotificationPublisher = null,
            ILogger<AiEvaluationFeedbackService>? logger = null)
        {
            _context = context;
            _adminNotificationPublisher = adminNotificationPublisher;
            _logger = logger;
        }

        public async Task<AiEvaluationFeedbackOperationResult<AiEvaluationFeedbackDto>> CreateAsync(
            int userId,
            CreateAiEvaluationFeedbackRequest request,
            CancellationToken cancellationToken)
        {
            var reason = request.Reason.Trim().ToUpperInvariant();
            if (!AiEvaluationFeedbackReasons.All.Contains(reason))
            {
                return BadRequest("INVALID_FEEDBACK_REASON", "Select a supported feedback reason.");
            }

            if (!Enum.TryParse<AiEvaluationFeedbackType>(request.EvaluationType, true, out var evaluationType))
            {
                return BadRequest("INVALID_EVALUATION_TYPE", "Evaluation type must be Technical or Behavioral.");
            }

            var session = await _context.InterviewSessions
                .AsNoTracking()
                .Include(item => item.InterviewCampaign)
                .SingleOrDefaultAsync(item => item.InterviewSessionId == request.InterviewSessionId, cancellationToken);

            if (session is null)
            {
                return new(AiEvaluationFeedbackOperationStatus.NotFound, ErrorCode: "INTERVIEW_SESSION_NOT_FOUND", Message: "Interview session was not found.");
            }

            if (session.InterviewCampaign.UserId != userId)
            {
                return new(AiEvaluationFeedbackOperationStatus.Forbidden, ErrorCode: "FEEDBACK_ACCESS_DENIED", Message: "This interview session does not belong to the current user.");
            }

            if (session.Status != InterviewSessionStatus.Completed)
            {
                return BadRequest("INTERVIEW_NOT_COMPLETED", "Feedback can only be submitted for a completed interview session.");
            }

            var expectedRound = evaluationType == AiEvaluationFeedbackType.Technical
                ? InterviewRoundType.Technical
                : InterviewRoundType.Behavior;
            if (session.InterviewRoundType != expectedRound)
            {
                return BadRequest("EVALUATION_TYPE_MISMATCH", "Evaluation type does not match the interview round.");
            }

            var roundEvaluationExists = evaluationType == AiEvaluationFeedbackType.Technical
                ? await _context.TechnicalRoundResults.AsNoTracking().AnyAsync(
                    item => item.InterviewSessionId == request.InterviewSessionId
                        && item.AiExecutiveSummary != null,
                    cancellationToken)
                : await _context.BehaviourRoundResults.AsNoTracking().AnyAsync(
                    item => item.InterviewSessionId == request.InterviewSessionId
                        && item.AiExecutiveSummary != null,
                    cancellationToken);

            if (!roundEvaluationExists)
            {
                return new(AiEvaluationFeedbackOperationStatus.NotFound, ErrorCode: "ROUND_EVALUATION_NOT_FOUND", Message: "The AI evaluation for this interview round is not available.");
            }

            var entity = new AiEvaluationFeedback
            {
                UserId = userId,
                InterviewSessionId = request.InterviewSessionId,
                EvaluationType = evaluationType.ToString(),
                Reason = reason,
                Explanation = request.Explanation.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.AiEvaluationFeedbacks.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            await PublishAdminReviewNotificationAsync(entity, cancellationToken);

            var dto = await GetDtoQuery()
                .SingleAsync(item => item.FeedbackId == entity.AiEvaluationFeedbackId, cancellationToken);
            return new(AiEvaluationFeedbackOperationStatus.Created, dto);
        }

        public async Task<IReadOnlyList<AiEvaluationFeedbackDto>> GetMineAsync(int userId, CancellationToken cancellationToken)
        {
            return await GetDtoQuery()
                .Where(item => item.UserId == userId)
                .OrderByDescending(item => item.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        private static readonly Dictionary<string, string[]> ReasonDisplayLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            [AiEvaluationFeedbackReasons.IncorrectScore] = ["Điểm đánh giá chưa chính xác", "Incorrect score"],
            [AiEvaluationFeedbackReasons.InaccurateFeedback] = ["Nhận xét chưa chính xác", "Inaccurate feedback"],
            [AiEvaluationFeedbackReasons.MissingContext] = ["Thiếu bằng chứng hoặc ngữ cảnh", "Missing evidence or context"],
            [AiEvaluationFeedbackReasons.Hallucination] = ["AI đưa ra thông tin không có căn cứ", "Hallucinated information"],
            [AiEvaluationFeedbackReasons.BiasOrUnfairness] = ["Đánh giá thiên vị hoặc không công bằng", "Biased or unfair evaluation"],
            [AiEvaluationFeedbackReasons.UnclearExplanation] = ["Giải thích chưa rõ ràng", "Unclear explanation"],
            [AiEvaluationFeedbackReasons.OffensiveOrInappropriate] = ["Nội dung không phù hợp", "Offensive or inappropriate content"],
            [AiEvaluationFeedbackReasons.Other] = ["Khác", "Other"]
        };

        public async Task<PagedResult<AiEvaluationFeedbackDto>> GetAdminPageAsync(
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
        {
            pageNumber = Math.Max(pageNumber, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var query = _context.AiEvaluationFeedbacks.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                var matchedReasons = ReasonDisplayLabels
                    .Where(pair => pair.Key.Contains(term, StringComparison.OrdinalIgnoreCase)
                                || pair.Value.Any(label => label.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    .Select(pair => pair.Key)
                    .ToList();

                query = query.Where(item =>
                    item.User.FullName.Contains(term)
                    || item.User.Email.Contains(term)
                    || item.Explanation.Contains(term)
                    || item.Reason.Contains(term)
                    || matchedReasons.Contains(item.Reason));
            }

            var totalItems = await query.CountAsync(cancellationToken);
            var pagedQuery = query
                .OrderByDescending(item => item.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);
            var items = await ProjectToDto(pagedQuery)
                .ToListAsync(cancellationToken);

            return new PagedResult<AiEvaluationFeedbackDto>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems
            };
        }

        public async Task<AiEvaluationFeedbackOperationResult<AiEvaluationFeedbackDto>> GetAdminDetailAsync(
            int feedbackId,
            CancellationToken cancellationToken)
        {
            var item = await GetDtoQuery().SingleOrDefaultAsync(feedback => feedback.FeedbackId == feedbackId, cancellationToken);
            if (item is null)
            {
                return new(AiEvaluationFeedbackOperationStatus.NotFound, ErrorCode: "FEEDBACK_NOT_FOUND", Message: "Feedback was not found.");
            }

            await PopulateRoundEvaluationAsync(item, cancellationToken);
            return new(AiEvaluationFeedbackOperationStatus.Ok, item);
        }

        private IQueryable<AiEvaluationFeedbackDto> GetDtoQuery()
        {
            return ProjectToDto(_context.AiEvaluationFeedbacks.AsNoTracking());
        }

        private static IQueryable<AiEvaluationFeedbackDto> ProjectToDto(IQueryable<AiEvaluationFeedback> query)
        {
            return query.Select(item => new AiEvaluationFeedbackDto
                {
                    FeedbackId = item.AiEvaluationFeedbackId,
                    UserId = item.UserId,
                    UserName = item.User.FullName,
                    UserEmail = item.User.Email,
                    InterviewSessionId = item.InterviewSessionId,
                    EvaluationType = item.EvaluationType,
                    Reason = item.Reason,
                    Explanation = item.Explanation,
                    CreatedAt = item.CreatedAt,
                    UpdatedAt = item.UpdatedAt
                });
        }

        private async Task PopulateRoundEvaluationAsync(AiEvaluationFeedbackDto feedback, CancellationToken cancellationToken)
        {
            RoundEvaluation? evaluation;
            if (feedback.EvaluationType.Equals(nameof(AiEvaluationFeedbackType.Technical), StringComparison.OrdinalIgnoreCase))
            {
                evaluation = await _context.TechnicalRoundResults
                    .AsNoTracking()
                    .Where(item => item.InterviewSessionId == feedback.InterviewSessionId)
                    .Select(item => new RoundEvaluation(item.AiExecutiveSummary, item.AiStrengths, item.AiGaps))
                    .SingleOrDefaultAsync(cancellationToken);
            }
            else
            {
                evaluation = await _context.BehaviourRoundResults
                    .AsNoTracking()
                    .Where(item => item.InterviewSessionId == feedback.InterviewSessionId)
                    .Select(item => new RoundEvaluation(item.AiExecutiveSummary, item.AiStrengths, item.AiGaps))
                    .SingleOrDefaultAsync(cancellationToken);
            }

            feedback.AiExecutiveSummary = evaluation?.ExecutiveSummary;
            feedback.AiStrengths = ParseStringList(evaluation?.StrengthsJson);
            feedback.AiGaps = ParseStringList(evaluation?.GapsJson);
        }

        private static List<string> ParseStringList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json)
                    ?.Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .ToList() ?? [];
            }
            catch (JsonException)
            {
                return [json.Trim()];
            }
        }

        private static AiEvaluationFeedbackOperationResult<AiEvaluationFeedbackDto> BadRequest(string code, string message)
            => new(AiEvaluationFeedbackOperationStatus.BadRequest, ErrorCode: code, Message: message);

        private async Task PublishAdminReviewNotificationAsync(
            AiEvaluationFeedback feedback,
            CancellationToken cancellationToken)
        {
            if (_adminNotificationPublisher is null) return;

            try
            {
                await _adminNotificationPublisher.PublishAsync(new AdminNotificationEvent(
                    feedback.UserId,
                    NotificationType.AI_EVALUATION_REQUIRES_REVIEW,
                    NotificationCategory.AI_EVALUATION,
                    NotificationSeverity.WARNING,
                    "AI evaluation feedback requires review",
                    $"A user reported an AI evaluation: {feedback.Reason}.",
                    NotificationEntityType.AI_EVALUATION,
                    feedback.AiEvaluationFeedbackId.ToString(),
                    "/admin/ai-feedback",
                    $"AI_EVALUATION_REQUIRES_REVIEW:{feedback.AiEvaluationFeedbackId}",
                    new Dictionary<string, object?>
                    {
                        ["feedbackId"] = feedback.AiEvaluationFeedbackId,
                        ["interviewSessionId"] = feedback.InterviewSessionId,
                        ["evaluationType"] = feedback.EvaluationType,
                        ["reason"] = feedback.Reason
                    }), cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                _logger?.LogError(
                    exception,
                    "Could not publish admin review notification for AI evaluation feedback {FeedbackId}.",
                    feedback.AiEvaluationFeedbackId);
            }
        }

        private sealed record RoundEvaluation(string? ExecutiveSummary, string? StrengthsJson, string? GapsJson);
    }
}
