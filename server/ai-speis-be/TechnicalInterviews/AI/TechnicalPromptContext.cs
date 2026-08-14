using System.Collections.Immutable;

namespace ai_speis_be.TechnicalInterviews.AI
{
    public sealed record TechnicalRubricPromptDimension(string Code, string Name, string Description, decimal Weight);
    public sealed record TechnicalRubricPromptLevel(string Code, int Score, string Description);
    public sealed record TechnicalRubricPromptSnapshot(
        decimal MinimumScore,
        decimal MaximumScore,
        decimal EvidenceRequiredWhenScoreAbove,
        ImmutableArray<TechnicalRubricPromptDimension> Dimensions,
        ImmutableArray<TechnicalRubricPromptLevel> Levels);
}
