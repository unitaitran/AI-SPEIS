using ai_speis_be.AI.Json;

namespace ai_speis_be.Tests.AI;

public sealed class AiJsonRecoveryServiceTests
{
    private static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Deserialize_ValidJson_DoesNotRecover()
    {
        var result = new AiJsonRecoveryService().Deserialize<Dictionary<string, int>>("{\"value\":1}", Options);

        Assert.True(result.Success);
        Assert.Equal("NONE", result.Metadata.RecoveryStatus);
        Assert.Empty(result.Metadata.RecoveryFlags);
    }

    [Theory]
    [InlineData("Here is the result: {\"value\":1}")]
    [InlineData("{\"value\":1}\nHope this helps.")]
    [InlineData("```json\n{\"value\":1}\n```")]
    [InlineData("\uFEFF{\"value\":1}")]
    public void Deserialize_CommonModelFormatting_RecoversSafely(string raw)
    {
        var result = new AiJsonRecoveryService().Deserialize<Dictionary<string, int>>(raw, Options);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!["value"]);
        Assert.NotEqual("UNRECOVERABLE", result.Metadata.RecoveryStatus);
    }

    [Fact]
    public void Deserialize_TrailingComma_UsesTolerantParser()
    {
        var result = new AiJsonRecoveryService().Deserialize<Dictionary<string, int>>("{\"value\":1,}", Options);

        Assert.True(result.Success);
        Assert.Contains("JSON_RECOVERED_TOLERANT_PARSE", result.Metadata.RecoveryFlags);
    }

    [Fact]
    public void Deserialize_BracesInsideString_AreNotObjectBoundaries()
    {
        var result = new AiJsonRecoveryService().Deserialize<Dictionary<string, string>>(
            "Explanation {\"evidence\":\"candidate said {hello}\"} trailing", Options);

        Assert.True(result.Success);
        Assert.Equal("candidate said {hello}", result.Data!["evidence"]);
    }

    [Fact]
    public void Deserialize_TruncatedObject_IsUnrecoverable()
    {
        var result = new AiJsonRecoveryService().Deserialize<Dictionary<string, int>>("{\"value\":", Options);

        Assert.False(result.Success);
        Assert.Equal("UNRECOVERABLE", result.Metadata.RecoveryStatus);
    }
}
