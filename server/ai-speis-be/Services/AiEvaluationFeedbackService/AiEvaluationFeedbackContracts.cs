using System.ComponentModel.DataAnnotations;
using ai_speis_be.Models;

namespace ai_speis_be.Services.AiEvaluationFeedbackService
{
    public static class AiEvaluationFeedbackReasons
    {
        public const string IncorrectScore = "INCORRECT_SCORE";
        public const string InaccurateFeedback = "INACCURATE_FEEDBACK";
        public const string MissingContext = "MISSING_CONTEXT";
        public const string Hallucination = "HALLUCINATION";
        public const string BiasOrUnfairness = "BIAS_OR_UNFAIRNESS";
        public const string UnclearExplanation = "UNCLEAR_EXPLANATION";
        public const string OffensiveOrInappropriate = "OFFENSIVE_OR_INAPPROPRIATE";
        public const string Other = "OTHER";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            IncorrectScore,
            InaccurateFeedback,
            MissingContext,
            Hallucination,
            BiasOrUnfairness,
            UnclearExplanation,
            OffensiveOrInappropriate,
            Other
        };
    }

    public sealed class CreateAiEvaluationFeedbackRequest
    {
        [Range(1, int.MaxValue)]
        public int InterviewSessionId { get; set; }

        [Required]
        [MaxLength(30)]
        public string EvaluationType { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Reason { get; set; } = string.Empty;

        [Required]
        [StringLength(1000, MinimumLength = 10)]
        public string Explanation { get; set; } = string.Empty;
    }

    public sealed class AiEvaluationFeedbackDto
    {
        public int FeedbackId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public int InterviewSessionId { get; set; }
        public string EvaluationType { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Title => Reason;
        public string Explanation { get; set; } = string.Empty;
        public string? AiExecutiveSummary { get; set; }
        public List<string> AiStrengths { get; set; } = new();
        public List<string> AiGaps { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public enum AiEvaluationFeedbackOperationStatus
    {
        Ok,
        Created,
        BadRequest,
        NotFound,
        Forbidden
    }

    public sealed record AiEvaluationFeedbackOperationResult<T>(
        AiEvaluationFeedbackOperationStatus Status,
        T? Value = default,
        string? ErrorCode = null,
        string? Message = null);

    public interface IAiEvaluationFeedbackService
    {
        Task<AiEvaluationFeedbackOperationResult<AiEvaluationFeedbackDto>> CreateAsync(
            int userId,
            CreateAiEvaluationFeedbackRequest request,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<AiEvaluationFeedbackDto>> GetMineAsync(int userId, CancellationToken cancellationToken);

        Task<PagedResult<AiEvaluationFeedbackDto>> GetAdminPageAsync(
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken);

        Task<AiEvaluationFeedbackOperationResult<AiEvaluationFeedbackDto>> GetAdminDetailAsync(
            int feedbackId,
            CancellationToken cancellationToken);
    }
}
