namespace ai_speis_be.TechnicalInterviews.AI
{
    public static class TechnicalPromptVersions
    {
        public const string Selection = "technical-selection-v2";
        public const string Evaluation = "technical-evaluation-v3";
        public const string Feedback = "technical-feedback-v2";
        public const string QuestionBundle = "technical-question-bundle-v2";
        public const string Summary = "technical-summary-v1";
    }

    public sealed record TechnicalAIQuestionCandidate(
        int QuestionId,
        string Content,
        string Skill,
        string? Subskill,
        string Difficulty,
        string ExperienceLevel);

    public sealed class TechnicalAISelectionRequest
    {
        public string Language { get; init; } = string.Empty;
        public string JobRole { get; init; } = string.Empty;
        public string ExperienceLevel { get; init; } = string.Empty;
        public IReadOnlyList<string> SelectedSkills { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> AskedSkills { get; init; } = Array.Empty<string>();
        public IReadOnlyList<TechnicalAIQuestionCandidate> Candidates { get; init; } = Array.Empty<TechnicalAIQuestionCandidate>();
        public string? PlannedSourceType { get; init; }
        public string? TargetSkill { get; init; }
        public string? TargetSubskill { get; init; }
        public string? PlannedDifficulty { get; init; }
        public string? EvaluationObjective { get; init; }
    }

    public sealed class TechnicalAISelectionResponse
    {
        public int SelectedQuestionId { get; set; }
    }

    public sealed record TechnicalAnswerContext(
        string QuestionType,
        string Question,
        string Answer);

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

    public sealed class TechnicalAIEvaluationResponse
    {
        public List<TechnicalAIDimensionEvaluation> DimensionEvaluations { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> MissingPoints { get; set; } = new();
        public List<string> IncorrectClaims { get; set; } = new();
        public List<string> ImprovementSuggestions { get; set; } = new();
        public string Decision { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
    }

    public sealed class TechnicalAIFeedbackDraftResponse
    {
        public List<string> Strengths { get; set; } = new();
        public List<string> MissingPoints { get; set; } = new();
        public List<string> IncorrectClaims { get; set; } = new();
        public List<string> ImprovementSuggestions { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
    }

    public sealed class TechnicalAISubQuestionCandidate
    {
        public string Content { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public List<string> TargetRubricCodes { get; set; } = new();
    }

    public sealed class TechnicalAINextMainQuestionCandidate
    {
        public int SelectedQuestionId { get; set; }
    }

    public sealed class TechnicalAIQuestionBundleResponse
    {
        public TechnicalAISubQuestionCandidate? ClarificationCandidate { get; set; }
        public TechnicalAISubQuestionCandidate? FollowUpCandidate { get; set; }
        public TechnicalAINextMainQuestionCandidate? NextMainQuestionCandidate { get; set; }
    }

    public sealed class TechnicalAIFinalSummaryRequest
    {
        public string RubricVersion { get; init; } = string.Empty;
        public decimal OverallScore { get; init; }
        public string PerformanceBand { get; init; } = string.Empty;
        public IReadOnlyList<object> MainQuestionResults { get; init; } = Array.Empty<object>();
        public IReadOnlyList<object> SkillResults { get; init; } = Array.Empty<object>();
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
