namespace ai_speis_be.TechnicalInterviews.AI
{
    public static class TechnicalPromptVersions
    {
        public const string Selection = "technical-selection-v1";
        public const string Evaluation = "technical-evaluation-v1";
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
    }

    public sealed class TechnicalAISelectionResponse
    {
        public int SelectedQuestionId { get; set; }
    }

    public sealed record TechnicalAnswerContext(
        string QuestionType,
        string Question,
        string Answer);

    public sealed class TechnicalAIEvaluationRequest
    {
        public string RubricVersion { get; init; } = string.Empty;
        public object Rubric { get; init; } = new();
        public string JobRole { get; init; } = string.Empty;
        public string ExperienceLevel { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public string MainQuestion { get; init; } = string.Empty;
        public string ExpectedAnswer { get; init; } = string.Empty;
        public string ExpectedKeyPoints { get; init; } = string.Empty;
        public string QuestionSpecificRubric { get; init; } = string.Empty;
        public IReadOnlyList<TechnicalAnswerContext> AnswerContext { get; init; } = Array.Empty<TechnicalAnswerContext>();
        public string CvContext { get; init; } = string.Empty;
        public string JdContext { get; init; } = string.Empty;
        public int ClarificationsUsed { get; init; }
        public int FollowUpsUsed { get; init; }
    }

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

    public sealed class TechnicalAINextQuestion
    {
        public string Content { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public List<string> TargetRubricCodes { get; set; } = new();
        public List<string> TargetMissingEvidence { get; set; } = new();
    }

    public sealed class TechnicalAIEvaluationResponse
    {
        public List<TechnicalAIDimensionEvaluation> DimensionEvaluations { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public List<string> MissingPoints { get; set; } = new();
        public List<string> IncorrectClaims { get; set; } = new();
        public List<string> ImprovementSuggestions { get; set; } = new();
        public string Decision { get; set; } = string.Empty;
        public TechnicalAINextQuestion? NextQuestion { get; set; }
        public decimal Confidence { get; set; }
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
    }
}
