using System.Text.Json.Serialization;
using ai_speis_be.Models;

namespace ai_speis_be.Services.RagService
{
    public sealed record RagQuestionDto(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("question_text")] string QuestionText,
        [property: JsonPropertyName("skill")] string Skill,
        [property: JsonPropertyName("subskill")] string? Subskill,
        [property: JsonPropertyName("difficulty")] string Difficulty,
        [property: JsonPropertyName("language")] string Language,
        [property: JsonPropertyName("expected_answer")] string ExpectedAnswer,
        [property: JsonPropertyName("expected_key_points")] List<string>? ExpectedKeyPoints,
        [property: JsonPropertyName("clarification_question")] string? ClarificationQuestion,
        [property: JsonPropertyName("follow_up_1")] string? FollowUp1,
        [property: JsonPropertyName("follow_up_2")] string? FollowUp2,
        [property: JsonPropertyName("experience_level")] string? ExperienceLevel,
        [property: JsonPropertyName("is_active")] bool IsActive);

    public sealed record RagRetrievalResult(
        bool Success,
        IReadOnlyList<Question> Questions,
        string? ErrorCode,
        string? ErrorDetail);

    public interface IRagQuestionRetrievalClient
    {
        Task<RagRetrievalResult> RetrieveQuestionsAsync(
            string jobRole,
            string experienceLevel,
            IReadOnlyList<string> skills,
            string language,
            int count,
            string interviewType,
            CancellationToken cancellationToken);

        Task<RagRetrievalResult> RetrieveQuestionsAsync(
            string jobRole,
            string experienceLevel,
            IReadOnlyList<string> skills,
            string language,
            int count,
            CancellationToken cancellationToken);
    }
}
