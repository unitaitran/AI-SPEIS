using ai_speis_be.TechnicalInterviews.DTOs;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalParallelPublicContractSecurityTests
{
    [Fact]
    public void SubmitResponse_DoesNotExposeInternalReferenceOrDetailedFeedbackFields()
    {
        var publicPropertyNames = typeof(TechnicalSubmitAnswerResponseDto)
            .GetProperties()
            .Select(property => property.Name)
            .Concat(typeof(TechnicalFeedbackAcknowledgementDto)
                .GetProperties()
                .Select(property => property.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("ExpectedAnswer", publicPropertyNames);
        Assert.DoesNotContain("KeyPoints", publicPropertyNames);
        Assert.DoesNotContain("RubricInternalRules", publicPropertyNames);
        Assert.DoesNotContain("RawGeminiResponse", publicPropertyNames);
        Assert.DoesNotContain("RawPrompt", publicPropertyNames);
        Assert.DoesNotContain("Summary", typeof(TechnicalFeedbackAcknowledgementDto)
            .GetProperties().Select(property => property.Name));
    }
}
