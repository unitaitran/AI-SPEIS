namespace ai_speis_be.BehaviouralInterviews.Configuration
{
    public sealed class BehaviouralInterviewOptions
    {
        public const string SectionName = "BehaviouralInterviewAI";

        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/"; // Default to Gemini's OpenAI-compatible endpoint
        public string Model { get; set; } = "gemini-2.5-flash";
        public int MaxRetries { get; set; } = 3;
    }
}
