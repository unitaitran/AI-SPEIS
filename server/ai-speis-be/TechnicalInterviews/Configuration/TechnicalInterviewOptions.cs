namespace ai_speis_be.TechnicalInterviews.Configuration
{
    public sealed class TechnicalInterviewOptions
    {
        public string Provider { get; init; } = "external";
        public string SingleQuestionEvaluationProvider { get; init; } = "gemini";
        public string SingleQuestionEvaluationModel { get; init; } = string.Empty;
        public string Model { get; init; } = "gemini-3.5-flash";
        public string ApiKey { get; init; } = string.Empty;
        public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta/openai/";
        public int TimeoutSeconds { get; init; } = 30;
        public int MaxRetries { get; init; } = 2;
        public int CandidatePoolSize { get; init; } = 20;
        public int MaxTranscriptCharacters { get; init; } = 12_000;
        public string RubricVersion { get; init; } = "technical-rubric-v2";
        public string PracticeRubricVersion { get; init; } = "technical-rubric-v2";
        public string ScoringPolicyVersion { get; init; } = "technical-scoring-v2";
        public string QuestionPlanVersion { get; init; } = "technical-question-plan-v1";
        public string AdaptiveRuleVersion { get; init; } = "technical-rubric-bank-v2";
        public string BonusCalculationVersion { get; init; } = "technical-follow-up-bonus-v1";
        public int StandardMainQuestionCount { get; init; } = 3;
        public int RealtimeMainQuestionCount { get; init; } = 3;
        public decimal ClarificationRecoveryFactor { get; init; } = 0.75m;
        public bool ClarificationEndsMainQuestion { get; init; }
        public bool ReliabilityFollowUpEnabled { get; init; } = true;
        public int ReliabilityMinimumQuestionCount { get; init; } = 5;
        public int ReliabilityFollowUpLimit { get; init; } = 2;
        public int GlobalConcurrencyLimit { get; init; } = 10;
        public int EvaluationTimeoutMs { get; init; } = 120_000;
        public int EvaluationMaxRetries { get; init; } = 1;
        public int ProcessingStaleThresholdSeconds { get; init; } = 120;
        public string OllamaBaseUrl { get; init; } = "http://localhost:11434/v1/";
        public string OllamaModel { get; init; } = string.Empty;
        // Evaluation needs strict JSON. A fine-tuned question model may remain
        // configured as OllamaModel while a more reliable instruction model is
        // selected explicitly for answer evaluation.
        public string OllamaEvaluationModel { get; init; } = string.Empty;
        public decimal InputTokenCostPerMillion { get; init; }
        public decimal OutputTokenCostPerMillion { get; init; }

        public static TechnicalInterviewOptions FromConfiguration(IConfiguration configuration)
        {
            return new TechnicalInterviewOptions
            {
                Provider = Get(configuration, "TECHNICAL_INTERVIEW_AI_PROVIDER", "external"),
                SingleQuestionEvaluationProvider = Get(configuration, "SINGLE_QUESTION_EVALUATION_PROVIDER", "gemini"),
                SingleQuestionEvaluationModel = Get(configuration, "SINGLE_QUESTION_EVALUATION_MODEL", string.Empty),
                Model = Get(configuration, "TECHNICAL_INTERVIEW_AI_MODEL", "gemini-3.5-flash"),
                ApiKey = Get(configuration, "TECHNICAL_INTERVIEW_AI_API_KEY",
                    Get(configuration, "GEMINI_API_KEY",
                        Get(configuration, "GeminiAI:ApiKey",
                            Get(configuration, "GeminiAI__ApiKey", string.Empty)))),
                BaseUrl = EnsureTrailingSlash(Get(
                    configuration,
                    "TECHNICAL_INTERVIEW_AI_BASE_URL",
                    "https://generativelanguage.googleapis.com/v1beta/openai/")),
                OllamaBaseUrl = EnsureTrailingSlash(Get(
                    configuration,
                    "OLLAMA_BASE_URL",
                    "http://localhost:11434/v1/")),
                OllamaModel = Get(configuration, "OLLAMA_MODEL", string.Empty),
                OllamaEvaluationModel = Get(configuration, "OLLAMA_EVALUATION_MODEL", string.Empty),
                TimeoutSeconds = GetInt(configuration, "TECHNICAL_INTERVIEW_AI_TIMEOUT_SECONDS", 180, 5, 300),
                MaxRetries = GetInt(configuration, "TECHNICAL_INTERVIEW_AI_MAX_RETRIES", 2, 0, 5),
                CandidatePoolSize = GetInt(configuration, "TECHNICAL_INTERVIEW_CANDIDATE_POOL_SIZE", 20, 1, 100),
                MaxTranscriptCharacters = GetInt(configuration, "TECHNICAL_INTERVIEW_MAX_TRANSCRIPT_CHARACTERS", 12_000, 100, 50_000),
                RubricVersion = Get(configuration, "TECHNICAL_INTERVIEW_RUBRIC_VERSION", "technical-rubric-v2"),
                PracticeRubricVersion = Get(configuration, "TECHNICAL_INTERVIEW_PRACTICE_RUBRIC_VERSION", "technical-rubric-v2"),
                ScoringPolicyVersion = Get(configuration, "TECHNICAL_INTERVIEW_SCORING_POLICY_VERSION", "technical-scoring-v2"),
                QuestionPlanVersion = Get(configuration, "TECHNICAL_INTERVIEW_QUESTION_PLAN_VERSION", "technical-question-plan-v1"),
                AdaptiveRuleVersion = Get(configuration, "TECHNICAL_INTERVIEW_ADAPTIVE_RULE_VERSION", "technical-rubric-bank-v2"),
                BonusCalculationVersion = Get(configuration, "TECHNICAL_INTERVIEW_BONUS_CALCULATION_VERSION", "technical-follow-up-bonus-v1"),
                StandardMainQuestionCount = GetInt(configuration, "TECHNICAL_INTERVIEW_STANDARD_MAIN_QUESTION_COUNT", 3, 3, 3),
                RealtimeMainQuestionCount = GetInt(configuration, "TECHNICAL_INTERVIEW_REALTIME_MAIN_QUESTION_COUNT", 3, 1, 20),
                ClarificationRecoveryFactor = GetDecimal(configuration, "TECHNICAL_INTERVIEW_CLARIFICATION_RECOVERY_FACTOR", 0.75m, 0m, 1m),
                // The legacy switch is intentionally forced off: a clarification answer
                // may still require one or two evidence-grounded follow-ups.
                ClarificationEndsMainQuestion = false,
                ReliabilityFollowUpEnabled = GetBool(configuration, "TECHNICAL_INTERVIEW_RELIABILITY_FOLLOW_UP_ENABLED", true),
                ReliabilityMinimumQuestionCount = GetInt(configuration, "TECHNICAL_INTERVIEW_RELIABILITY_MINIMUM_COUNT", 5, 3, 20),
                ReliabilityFollowUpLimit = GetInt(configuration, "TECHNICAL_INTERVIEW_RELIABILITY_FOLLOW_UP_LIMIT", 2, 0, 6),
                GlobalConcurrencyLimit = GetInt(configuration, "TECHNICAL_AI_GLOBAL_CONCURRENCY_LIMIT", 10, 1, 100),
                EvaluationTimeoutMs = GetInt(configuration, "TECHNICAL_AI_EVALUATION_TIMEOUT_MS", 45_000, 1_000, 300_000),
                EvaluationMaxRetries = GetInt(configuration, "TECHNICAL_AI_EVALUATION_MAX_RETRIES", 1, 0, 3),
                ProcessingStaleThresholdSeconds = GetInt(configuration, "TECHNICAL_AI_PROCESSING_STALE_THRESHOLD_SECONDS", 120, 10, 3600),
                InputTokenCostPerMillion = GetDecimal(
                    configuration,
                    "TECHNICAL_AI_INPUT_TOKEN_COST_PER_MILLION",
                    0m,
                    0m,
                    1_000_000m),
                OutputTokenCostPerMillion = GetDecimal(
                    configuration,
                    "TECHNICAL_AI_OUTPUT_TOKEN_COST_PER_MILLION",
                    0m,
                    0m,
                    1_000_000m)
            };
        }

        private static string Get(IConfiguration configuration, string key, string fallback)
        {
            var value = configuration[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static int GetInt(
            IConfiguration configuration,
            string key,
            int fallback,
            int minimum,
            int maximum)
        {
            return int.TryParse(configuration[key], out var parsed)
                ? Math.Clamp(parsed, minimum, maximum)
                : fallback;
        }

        private static bool GetBool(IConfiguration configuration, string key, bool fallback)
        {
            return bool.TryParse(configuration[key], out var parsed) ? parsed : fallback;
        }

        private static decimal GetDecimal(
            IConfiguration configuration,
            string key,
            decimal fallback,
            decimal minimum,
            decimal maximum)
        {
            return decimal.TryParse(
                configuration[key],
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed)
                ? Math.Clamp(parsed, minimum, maximum)
                : fallback;
        }

        private static string EnsureTrailingSlash(string value)
        {
            return value.EndsWith('/') ? value : value + "/";
        }
    }
}
