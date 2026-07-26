using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Validation;
using System.Text.Json;

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
    public void EvaluationContract_DoesNotExposeAnAiDecisionOrQuestion()
    {
        var propertyNames = typeof(TechnicalAIEvaluationResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("Decision", propertyNames);
        Assert.DoesNotContain("AdaptiveDecision", propertyNames);
        Assert.DoesNotContain("SuggestedQuestion", propertyNames);
        Assert.Throws<JsonException>(() =>
        {
            JsonSerializer.Deserialize<TechnicalAIEvaluationResponse>(
                "{\"adaptiveDecision\":{\"recommendedAction\":\"FOLLOW_UP\"}}",
                new JsonSerializerOptions { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow });
        });
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
    public void ValidateEvaluation_AcceptsVietnameseEvidenceWithEquivalentUnicodeAndFormatting()
    {
        var validator = new TechnicalAIResponseValidator();
        var response = TechnicalTestRubric.CreateEvaluation(4m, 4m, 3m, 3m, 4m);
        const string normalizedEvidence =
            "neu trong no uu tien xu ly toan bo tac vu trong microtask queue truoc";
        foreach (var dimension in response.DimensionEvaluations)
        {
            dimension.Evidence = new List<string> { normalizedEvidence };
        }

        var result = validator.ValidateEvaluation(
            response,
            TechnicalTestRubric.Create(),
            new[]
            {
                new TechnicalAnswerContext(
                    "MAIN",
                    "Event Loop hoạt động như thế nào?",
                    "Nếu trống, nó ưu tiên xử lý toàn bộ tác vụ trong Microtask Queue trước.")
            });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateEvaluation_StillRejectsVietnameseParaphraseNotGroundedInAnswer()
    {
        var validator = new TechnicalAIResponseValidator();
        var response = TechnicalTestRubric.CreateEvaluation(4m, 4m, 3m, 3m, 4m);
        response.DimensionEvaluations[0].Evidence = new List<string>
        {
            "Promise callback luon chay truoc setTimeout"
        };

        var result = validator.ValidateEvaluation(
            response,
            TechnicalTestRubric.Create(),
            new[]
            {
                new TechnicalAnswerContext(
                    "MAIN",
                    "Event Loop hoạt động như thế nào?",
                    "Nếu trống, nó ưu tiên xử lý toàn bộ tác vụ trong Microtask Queue trước.")
            });

        Assert.False(result.IsValid);
        Assert.Equal("EVIDENCE_NOT_IN_ANSWER", result.ErrorCode);
    }

    [Fact]
    public void ValidateEvaluation_RejectsEvidenceContainingOnlyPunctuation()
    {
        var validator = new TechnicalAIResponseValidator();
        var response = TechnicalTestRubric.CreateEvaluation(4m, 4m, 3m, 3m, 4m);
        response.DimensionEvaluations[0].Evidence = new List<string> { "..." };

        var result = validator.ValidateEvaluation(
            response,
            TechnicalTestRubric.Create(),
            new[] { new TechnicalAnswerContext("MAIN", "What is DI?", "Dependency injection.") });

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
