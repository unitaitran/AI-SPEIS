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
                "{\"adaptiveDecision\":{\"recommendedAction\":\"FOLLOW_UP\"}}");
        });
    }

    [Fact]
    public void ValidateEvaluation_RejectsAnswerQualityOutsideBackendRuleVocabulary()
    {
        var validator = new TechnicalAIResponseValidator();
        var response = TechnicalTestRubric.CreateEvaluation(8m, 8m, 8m, 8m, 8m);
        response.Evaluation.AnswerQuality = "MODEL_INVENTED_QUALITY";

        var result = validator.ValidateEvaluation(
            response,
            TechnicalTestRubric.Create(),
            new[] { new TechnicalAnswerContext("MAIN", "What is DI?", "Dependency injection separates construction from use.") });

        Assert.False(result.IsValid);
        Assert.Equal("INVALID_ANSWER_QUALITY", result.ErrorCode);
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

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.01")]
    [InlineData("85")]
    public void ValidateEvaluation_RejectsConfidenceOutsideZeroToOne(string confidenceValue)
    {
        var validator = new TechnicalAIResponseValidator();
        var response = TechnicalTestRubric.CreateEvaluation(4m, 4m, 3m, 3m, 4m);
        response.Confidence = decimal.Parse(
            confidenceValue,
            System.Globalization.CultureInfo.InvariantCulture);

        var result = validator.ValidateEvaluation(
            response,
            TechnicalTestRubric.Create(),
            new[] { new TechnicalAnswerContext("MAIN", "What is DI?", "Dependency injection separates construction from use.") });

        Assert.False(result.IsValid);
        Assert.Equal("INVALID_CONFIDENCE", result.ErrorCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.85")]
    [InlineData("1")]
    public void ValidateEvaluation_AcceptsConfidenceWithinZeroToOne(string confidenceValue)
    {
        var validator = new TechnicalAIResponseValidator();
        var response = TechnicalTestRubric.CreateEvaluation(4m, 4m, 3m, 3m, 4m);
        response.Confidence = decimal.Parse(
            confidenceValue,
            System.Globalization.CultureInfo.InvariantCulture);

        var result = validator.ValidateEvaluation(
            response,
            TechnicalTestRubric.Create(),
            new[] { new TechnicalAnswerContext("MAIN", "What is DI?", "Dependency injection separates construction from use.") });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void IsValidSelection_RejectsQuestionOutsideCandidatePool()
    {
        var validator = new TechnicalAIResponseValidator();

        Assert.False(validator.IsValidSelection(99, new HashSet<int> { 1, 2, 3 }));
    }
}
