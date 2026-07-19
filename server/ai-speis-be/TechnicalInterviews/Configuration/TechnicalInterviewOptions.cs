namespace ai_speis_be.TechnicalInterviews.Configuration
{
    public sealed class TechnicalInterviewOptions
    {
        public string Provider { get; init; } = "external";
        public string Model { get; init; } = "gemini-2.5-flash";
        public string ApiKey { get; init; } = string.Empty;
        public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta/openai/";
        public int TimeoutSeconds { get; init; } = 30;
        public int MaxRetries { get; init; } = 2;
        public int CandidatePoolSize { get; init; } = 20;
        public int MaxTranscriptCharacters { get; init; } = 12_000;
        public string RubricVersion { get; init; } = "technical-rubric-v1";
        public string ScoringPolicyVersion { get; init; } = "technical-scoring-v1";

        public static TechnicalInterviewOptions FromConfiguration(IConfiguration configuration)
        {
            return new TechnicalInterviewOptions
            {
                Provider = Get(configuration, "TECHNICAL_INTERVIEW_AI_PROVIDER", "external"),
                Model = Get(configuration, "TECHNICAL_INTERVIEW_AI_MODEL", "gemini-2.5-flash"),
                ApiKey = Get(configuration, "TECHNICAL_INTERVIEW_AI_API_KEY", string.Empty),
                BaseUrl = EnsureTrailingSlash(Get(
                    configuration,
                    "TECHNICAL_INTERVIEW_AI_BASE_URL",
                    "https://generativelanguage.googleapis.com/v1beta/openai/")),
                TimeoutSeconds = GetInt(configuration, "TECHNICAL_INTERVIEW_AI_TIMEOUT_SECONDS", 30, 5, 180),
                MaxRetries = GetInt(configuration, "TECHNICAL_INTERVIEW_AI_MAX_RETRIES", 2, 0, 5),
                CandidatePoolSize = GetInt(configuration, "TECHNICAL_INTERVIEW_CANDIDATE_POOL_SIZE", 20, 1, 100),
                MaxTranscriptCharacters = GetInt(configuration, "TECHNICAL_INTERVIEW_MAX_TRANSCRIPT_CHARACTERS", 12_000, 100, 50_000),
                RubricVersion = Get(configuration, "TECHNICAL_INTERVIEW_RUBRIC_VERSION", "technical-rubric-v1"),
                ScoringPolicyVersion = Get(configuration, "TECHNICAL_INTERVIEW_SCORING_POLICY_VERSION", "technical-scoring-v1")
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

        private static string EnsureTrailingSlash(string value)
        {
            return value.EndsWith('/') ? value : value + "/";
        }
    }
}
