using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.QuestionService
{
    public enum QuestionOperationOutcome
    {
        Created,
        Updated,
        Deleted,
        Restored,
        PurgeRequested,
        QuestionNotFound,
        QuestionDeleted,
        AlreadyDeleted
    }

    public sealed record QuestionOperationResult(
        QuestionOperationOutcome Outcome,
        QuestionResponseDto? Question = null);

    public enum QuestionImportOutcome
    {
        Imported,
        InvalidFile
    }

    public sealed record QuestionImportOperationResult(
        QuestionImportOutcome Outcome,
        QuestionImportSummaryDto? Summary = null,
        string? ErrorMessage = null);
}
