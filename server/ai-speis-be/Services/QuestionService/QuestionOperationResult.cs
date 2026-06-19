using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.QuestionService
{
    public enum QuestionOperationOutcome
    {
        Created,
        Updated,
        Deleted,
        QuestionNotFound,
        QuestionDeleted,
        AlreadyDeleted
    }

    public sealed record QuestionOperationResult(
        QuestionOperationOutcome Outcome,
        QuestionResponseDto? Question = null);
}
