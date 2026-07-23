using System.Collections.Immutable;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.DTOs;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalParallelPublicContractSecurityTests
{
    [Fact]
    public void SubmitResponse_DoesNotExposeInternalReferenceOrPerAnswerFeedbackFields()
    {
        var publicPropertyNames = typeof(TechnicalSubmitAnswerResponseDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("ExpectedAnswer", publicPropertyNames);
        Assert.DoesNotContain("KeyPoints", publicPropertyNames);
        Assert.DoesNotContain("RubricInternalRules", publicPropertyNames);
        Assert.DoesNotContain("RawGeminiResponse", publicPropertyNames);
        Assert.DoesNotContain("RawPrompt", publicPropertyNames);
        Assert.DoesNotContain("Feedback", publicPropertyNames);
        Assert.DoesNotContain("Strengths", publicPropertyNames);
        Assert.DoesNotContain("MissingPoints", publicPropertyNames);
    }

    [Fact]
    public void SubmitAndResultContractsExposeAdaptiveProgressWithoutLiveScores()
    {
        var submitProperties = typeof(TechnicalSubmitAnswerResponseDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var progressProperties = typeof(TechnicalInterviewProgressDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var resultProperties = typeof(TechnicalInterviewResultDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("ResolvedAction", submitProperties);
        Assert.Contains("Progress", submitProperties);
        Assert.DoesNotContain("Score", submitProperties);
        Assert.Contains("MainQuestionIndex", progressProperties);
        Assert.Contains("TotalMainQuestions", progressProperties);
        Assert.Contains("RequiredFollowUpCount", progressProperties);
        Assert.Contains("CompletedFollowUpCount", progressProperties);
        Assert.Contains("TechnicalScore", resultProperties);
        Assert.Contains("MainQuestionResults", resultProperties);
        Assert.DoesNotContain(
            typeof(TechnicalAIEvaluationResponse).GetProperties(),
            property => string.Equals(property.Name, "SelectedQuestionId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProcessingContext_CarriesPriorMissingEvidenceForSequentialFuGeneration()
    {
        var context = TechnicalParallelTestData.CreateContext() with
        {
            RemainingMissingEvidence = ImmutableArray.Create("Explain the scoped lifetime trade-off")
        };

        Assert.Equal("Explain the scoped lifetime trade-off", context.RemainingMissingEvidence.Single());
    }
}
