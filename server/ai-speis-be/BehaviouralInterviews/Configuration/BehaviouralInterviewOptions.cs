namespace ai_speis_be.BehaviouralInterviews.Configuration
{
    public sealed class BehaviouralInterviewOptions
    {
        public const string SectionName = "BehaviouralInterviewAI";

        public string Provider { get; init; } = "external";
        public string ApiKey { get; init; } = string.Empty;
        public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta/openai/"; // Default to Gemini's OpenAI-compatible endpoint
        public string Model { get; init; } = "gemini-3.5-flash";
        public int MaxRetries { get; init; } = 3;
        public int TimeoutSeconds { get; init; } = 90;
        public decimal InputTokenCostPerMillion { get; init; }
        public decimal OutputTokenCostPerMillion { get; init; }

        public static BehaviouralInterviewOptions FromConfiguration(IConfiguration configuration)
        {
            return new BehaviouralInterviewOptions
            {
                Provider = Get(configuration, "BEHAVIOURAL_INTERVIEW_AI_PROVIDER", "external"),
                ApiKey = Get(configuration, "BEHAVIOURAL_INTERVIEW_AI_API_KEY", string.Empty),
                BaseUrl = EnsureTrailingSlash(Get(
                    configuration,
                    "BEHAVIOURAL_INTERVIEW_AI_BASE_URL",
                    "https://generativelanguage.googleapis.com/v1beta/openai/")),
                Model = Get(configuration, "BEHAVIOURAL_INTERVIEW_AI_MODEL", "gemini-3.5-flash"),
                MaxRetries = GetInt(configuration, "BEHAVIOURAL_INTERVIEW_AI_MAX_RETRIES", 3, 0, 5),
                TimeoutSeconds = GetInt(configuration, "BEHAVIOURAL_INTERVIEW_AI_TIMEOUT_SECONDS", 90, 5, 180),
                InputTokenCostPerMillion = GetDecimal(
                    configuration,
                    "BEHAVIOURAL_INTERVIEW_AI_INPUT_TOKEN_COST_PER_MILLION",
                    0m,
                    0m,
                    1_000_000m),
                OutputTokenCostPerMillion = GetDecimal(
                    configuration,
                    "BEHAVIOURAL_INTERVIEW_AI_OUTPUT_TOKEN_COST_PER_MILLION",
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
