using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Validation;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalAIResponseValidatorTests
{
    [Fact]
    public void ValidateEvaluation_AcceptsOnlyConfiguredDimensionsAndVerbatimEvidence()
    {
        var validator = new TechnicalAIResponseValidator();
        var response = TechnicalTestRubric.CreateEvaluation(4m, 4m, 3m, 3m, 4m);

        var result = validator.ValidateEvaluation(
            response,
            TechnicalTestRubric.Create(),
            new[] { new TechnicalAnswerContext("MAIN", "What is DI?", "Dependency injection separates construction from use.") });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateEvaluation_TreatsInvalidAiActionAsAuditDataInsteadOfCriticalFailure()
    {
        var validator = new TechnicalAIResponseValidator();
        var response = TechnicalTestRubric.CreateEvaluation(8m, 8m, 8m, 8m, 8m);
        response.Decision = "MODEL_INVENTED_ACTION";

        var result = validator.ValidateEvaluation(
            response,
            TechnicalTestRubric.Create(),
            new[] { new TechnicalAnswerContext("MAIN", "What is DI?", "Dependency injection separates construction from use.") });

        Assert.True(result.IsValid);
        Assert.Null(result.AiSuggestedDecision);
    }

    [Fact]
    public void ValidateEvaluation_RejectsEvidenceNotPresentInCandidateAnswer()
    {
        var validator = new TechnicalAIResponseValidator();
        var response = TechnicalTestRubric.CreateEvaluation(4m, 4m, 3m, 3m, 4m);
        response.DimensionEvaluations[0].Evidence = new List<string> { "invented evidence" };

        var result = validator.ValidateEvaluation(
            response,
            TechnicalTestRubric.Create(),
            new[] { new TechnicalAnswerContext("MAIN", "What is DI?", "Dependency injection separates construction from use.") });

        Assert.False(result.IsValid);
        Assert.Equal("EVIDENCE_NOT_IN_ANSWER", result.ErrorCode);
    }

    [Fact]
    public void IsValidSelection_RejectsQuestionOutsideCandidatePool()
    {
        var validator = new TechnicalAIResponseValidator();

        Assert.False(validator.IsValidSelection(99, new HashSet<int> { 1, 2, 3 }));
    }
}
