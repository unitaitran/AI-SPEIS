using ai_speis_be.Models;
using ai_speis_be.Tests.Helpers;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalAttemptConstraintTests
{
    [Fact]
    public void Model_HasUniqueRootTypeSequenceConstraintToPreventDuplicateSubAttempts()
    {
        using var context = TestDbContextFactory.Create();
        var entity = context.Model.FindEntityType(typeof(TechnicalQuestionAttempt));

        var index = entity!.GetIndexes().Single(candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(TechnicalQuestionAttempt.RootMainAttemptId),
                nameof(TechnicalQuestionAttempt.QuestionType),
                nameof(TechnicalQuestionAttempt.SequenceWithinMain)
            }));

        Assert.True(index.IsUnique);
    }
}
