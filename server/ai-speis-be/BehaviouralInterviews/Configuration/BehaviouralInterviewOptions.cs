namespace ai_speis_be.BehaviouralInterviews.Configuration
{
    public sealed class BehaviouralInterviewOptions
    {
        public const string SectionName = "BehaviouralInterviewAI";

        public string Provider { get; init; } = "external";
        public string ApiKey { get; init; } = string.Empty;
        public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta/openai/"; // Default to Gemini's OpenAI-compatible endpoint
        public string Model { get; init; } = "gemini-2.5-flash";
        public int MaxRetries { get; init; } = 3;
        public int TimeoutSeconds { get; init; } = 30;

        public static BehaviouralInterviewOptions FromConfiguration(IConfiguration configuration)
        {
            return new BehaviouralInterviewOptions
            {
                Provider = GetFirst(configuration, "external",
                    "BEHAVIOURAL_INTERVIEW_AI_PROVIDER", $"{SectionName}:Provider"),
                ApiKey = GetFirst(configuration, string.Empty,
                    "BEHAVIOURAL_INTERVIEW_AI_API_KEY", $"{SectionName}:ApiKey", "GeminiAI:ApiKey"),
                BaseUrl = EnsureTrailingSlash(GetFirst(
                    configuration,
                    "https://generativelanguage.googleapis.com/v1beta/openai/",
                    "BEHAVIOURAL_INTERVIEW_AI_BASE_URL", $"{SectionName}:BaseUrl")),
                Model = GetFirst(configuration, "gemini-2.5-flash",
                    "BEHAVIOURAL_INTERVIEW_AI_MODEL", $"{SectionName}:Model"),
                MaxRetries = GetInt(configuration, 3, 0, 5,
                    "BEHAVIOURAL_INTERVIEW_AI_MAX_RETRIES", $"{SectionName}:MaxRetries"),
                TimeoutSeconds = GetInt(configuration, 30, 5, 180,
                    "BEHAVIOURAL_INTERVIEW_AI_TIMEOUT_SECONDS", $"{SectionName}:TimeoutSeconds")
            };
        }

        private static string GetFirst(
            IConfiguration configuration,
            string fallback,
            params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = configuration[key];
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
            return fallback;
        }

        private static int GetInt(
            IConfiguration configuration,
            int fallback,
            int minimum,
            int maximum,
            params string[] keys)
        {
            return int.TryParse(GetFirst(configuration, string.Empty, keys), out var parsed)
                ? Math.Clamp(parsed, minimum, maximum)
                : fallback;
        }

        private static string EnsureTrailingSlash(string value)
        {
            return value.EndsWith('/') ? value : value + "/";
        }
    }
}
