using System.Text.Json.Serialization;

namespace ai_speis_be.TechnicalInterviews.AI
{
    public static class TechnicalPromptVersions
    {
        public const string Evaluation = "technical-evaluation-rubric-v7";
        public const string Summary = "technical-round-feedback-v2";
    }

    public sealed record TechnicalAnswerContext(
        string QuestionType,
        string Question,
        string Answer);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed class TechnicalAIDimensionEvaluation
    {
        public string RubricCode { get; set; } = string.Empty;
        public List<string> Evidence { get; set; } = new();
        public List<string> MissingEvidence { get; set; } = new();
        public List<string> IncorrectClaims { get; set; } = new();
        public decimal SuggestedScore { get; set; }
        public string SuggestedLevel { get; set; } = string.Empty;
        public string ReasonSummary { get; set; } = string.Empty;
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed class TechnicalAIEvaluationPayload
    {
        public string AnswerQuality { get; set; } = string.Empty;
        public List<TechnicalAIDimensionEvaluation> DimensionEvaluations { get; set; } = new();
        public List<string> Evidence { get; set; } = new();
        public List<string> MissingPoints { get; set; } = new();
        public List<string> IncorrectClaims { get; set; } = new();
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed class TechnicalAIEvaluationResponse
    {
        public TechnicalAIEvaluationPayload Evaluation { get; set; } = new();
        public decimal Confidence { get; set; }

        // Source-compatible aliases for existing scoring code and legacy tests. They
        // are ignored by JSON so the provider contract remains the evaluation-only v6 schema.
        [System.Text.Json.Serialization.JsonIgnore]
        public List<TechnicalAIDimensionEvaluation> DimensionEvaluations
        {
            get => Evaluation.DimensionEvaluations;
            set => Evaluation.DimensionEvaluations = value ?? new();
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public List<string> MissingPoints
        {
            get => Evaluation.MissingPoints;
            set => Evaluation.MissingPoints = value ?? new();
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public List<string> IncorrectClaims
        {
            get => Evaluation.IncorrectClaims;
            set => Evaluation.IncorrectClaims = value ?? new();
        }

    }

    public sealed class TechnicalAIFinalSummaryRequest
    {
        public string RubricVersion { get; init; } = string.Empty;
        public decimal OverallScore { get; init; }
        public string PerformanceBand { get; init; } = string.Empty;
        public IReadOnlyList<object> MainQuestionResults { get; init; } = Array.Empty<object>();
        public IReadOnlyList<object> SkillResults { get; init; } = Array.Empty<object>();
        public object? RelevantCvContext { get; init; }
        public object? RelevantJdContext { get; init; }
        public IReadOnlyList<string> RequiredSkills { get; init; } = Array.Empty<string>();
        public int? CvJdMatchScore { get; init; }
    }

    public sealed class TechnicalAIFinalSummaryResponse
    {
        public string Summary { get; set; } = string.Empty;
        public List<string> Strengths { get; set; } = new();
        public List<string> AreasForImprovement { get; set; } = new();
        public List<string> RecommendedNextSteps { get; set; } = new();
    }

    public sealed class AIProviderResult<T>
    {
        public bool Success { get; init; }
        public T? Data { get; init; }
        public string Model { get; init; } = string.Empty;
        public long LatencyMs { get; init; }
        public int? InputTokens { get; init; }
        public int? OutputTokens { get; init; }
        public string? ErrorCode { get; init; }
        public int RetryCount { get; init; }
        public DateTime StartedAt { get; init; } = DateTime.UtcNow;
        public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
    }
}
