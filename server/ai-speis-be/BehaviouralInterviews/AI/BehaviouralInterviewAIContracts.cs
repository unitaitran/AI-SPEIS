using System.Text.Json.Serialization;

namespace ai_speis_be.BehaviouralInterviews.AI
{
    public static class BehaviouralPromptVersions
    {
        public const string Selection = "behavioural-selection-v3";
        public const string Evaluation = "behavioural-evaluation-v4";
        public const string Summary = "behavioural-round-feedback-v2";
    }

    public sealed record BehaviouralAIQuestionCandidate(
        int QuestionId,
        string Content,
        string? Skill,
        string? Difficulty,
        string? ExperienceLevel);

    public sealed class BehaviouralAISelectionConstraints
    {
        public int RequiredQuestionCount { get; init; } = 3;
        public int MaximumQuestionsPerSkill { get; init; } = 1;
        public int MinimumCoveredSkills { get; init; } = 3;
        public IReadOnlyDictionary<string, int> DifficultyDistribution { get; init; } =
            new Dictionary<string, int>();

        /// <summary>
        /// Phân bổ nguồn câu hỏi theo CV-JD Match Score (Adaptive Question Generation Rubric):
        /// số câu khai thác kinh nghiệm trong CV vs số câu theo yêu cầu/tình huống của JD.
        /// </summary>
        public int CvFocusQuestionCount { get; init; }
        public int JdFocusQuestionCount { get; init; }
    }

    public sealed class BehaviouralAISelectionRequest
    {
        public string Language { get; init; } = string.Empty;
        public string JobRole { get; init; } = string.Empty;
        public string ExperienceLevel { get; init; } = string.Empty;
        public string? CompanyCategory { get; init; }
        public int? CvJdMatchScore { get; init; }
        public IReadOnlyList<string> RequiredSkills { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> NiceToHaveSkills { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> CvSkills { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> CvHighlights { get; init; } = Array.Empty<string>();
        public BehaviouralAISelectionConstraints Constraints { get; init; } = new();
        public IReadOnlyList<BehaviouralAIQuestionCandidate> Candidates { get; init; } = Array.Empty<BehaviouralAIQuestionCandidate>();
    }

    public sealed class BehaviouralAISelectedQuestion
    {
        public int QuestionId { get; set; }
        public int Order { get; set; }
        public string SelectionReason { get; set; } = string.Empty;
        public string EvaluationGoal { get; set; } = string.Empty;
    }

    public sealed class BehaviouralAISelectionResponse
    {
        public List<BehaviouralAISelectedQuestion> SelectedQuestions { get; set; } = new();
        public List<string> CoveredSkills { get; set; } = new();
        public string SelectionSummary { get; set; } = string.Empty;
    }

    public sealed record BehaviouralAnswerContext(
        string QuestionType,
        string Question,
        string Answer);

    public sealed class BehaviouralAIEvaluationRequest
    {
        public string RubricVersion { get; init; } = string.Empty;
        public object Rubric { get; init; } = new();
        public string JobRole { get; init; } = string.Empty;
        public string ExperienceLevel { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public string Skill { get; init; } = string.Empty;
        public string MainQuestion { get; init; } = string.Empty;
        public IReadOnlyList<string> ExpectedKeyPoints { get; init; } = Array.Empty<string>();
        public IReadOnlyDictionary<string, string> QuestionScoringRubric { get; init; } =
            new Dictionary<string, string>();
        public IReadOnlyList<BehaviouralAnswerContext> AnswerContext { get; init; } = Array.Empty<BehaviouralAnswerContext>();
        public int? AnswerDurationSeconds { get; init; }
        public int ClarificationsUsed { get; init; }
        public int FollowUpsUsed { get; init; }
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed class BehaviouralAIDimensionEvaluation
    {
        public string RubricCode { get; set; } = string.Empty; // e.g. SITUATION_TASK, ACTION, RESULT, COMPETENCY, COMMUNICATION
        public List<string> Evidence { get; set; } = new();
        public List<string> MissingEvidence { get; set; } = new();
        public decimal SuggestedScore { get; set; } // 0-10 (Evaluation Framework, thang thống nhất)
        public string ReasonSummary { get; set; } = string.Empty;
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed class BehaviouralAIEvaluationResponse
    {
        public List<BehaviouralAIDimensionEvaluation> DimensionEvaluations { get; set; } = new();
        public string AnswerStatus { get; set; } = string.Empty;
        public List<string> MissingAspects { get; set; } = new();
        public List<string> Evidence { get; set; } = new();
        public string RecommendedAction { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
    }

    public sealed class BehaviouralAIFinalSummaryRequest
    {
        public string RubricVersion { get; init; } = string.Empty;
        public string JobRole { get; init; } = string.Empty;
        public string ExperienceLevel { get; init; } = string.Empty;
        public string Language { get; init; } = string.Empty;
        public decimal OverallScore { get; init; }
        public string PerformanceBand { get; init; } = string.Empty;
        public IReadOnlyDictionary<string, decimal> SkillScores { get; init; } =
            new Dictionary<string, decimal>();
        public IReadOnlyDictionary<string, decimal> CriteriaAverages { get; init; } =
            new Dictionary<string, decimal>();
        public IReadOnlyList<object> MainQuestionResults { get; init; } = Array.Empty<object>();
        public IReadOnlyList<object> QuestionResults { get; init; } = Array.Empty<object>();
        public object? RelevantCvContext { get; init; }
        public object? RelevantJdContext { get; init; }
        public IReadOnlyList<string> RequiredSkills { get; init; } = Array.Empty<string>();
        public int? CvJdMatchScore { get; init; }
    }

    public sealed class BehaviouralAIFinalSummaryResponse
    {
        public string ExecutiveSummary { get; set; } = string.Empty;
        public List<string> CompetencyStrengths { get; set; } = new();
        public List<string> CompetencyGaps { get; set; } = new();
        public string LevelAssessment { get; set; } = string.Empty;
        public List<string> TopRecommendations { get; set; } = new();
    }

    public sealed class BehaviouralAIProviderResult<T>
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
