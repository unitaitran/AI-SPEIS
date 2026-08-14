using System.Text.Json;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class PublicQuestionDtoSecurityTests
{
    [Fact]
    public void QuestionResponseDto_DoesNotSerializeExpectedAnswer()
    {
        var dto = new QuestionResponseDto
        {
            QuestionId = 1,
            QuestionContent = "Question"
        };

        var json = JsonSerializer.Serialize(dto);

        Assert.DoesNotContain("ExpectedAnswer", json, StringComparison.OrdinalIgnoreCase);
    }
}
