using ai_speis_be.TechnicalInterviews.Configuration;
using Microsoft.Extensions.Configuration;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalParallelOptionsTests
{
    [Fact]
    public void FromConfiguration_EnforcesParallelPairAndReadsIndependentTimeoutRetryBudgets()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TECHNICAL_AI_PARALLEL_PROCESSING_ENABLED"] = "false",
                ["TECHNICAL_AI_MAX_PARALLEL_TASKS_PER_SESSION"] = "2",
                ["TECHNICAL_AI_GLOBAL_CONCURRENCY_LIMIT"] = "7",
                ["TECHNICAL_AI_EVALUATION_TIMEOUT_MS"] = "16000",
                ["TECHNICAL_AI_FEEDBACK_TIMEOUT_MS"] = "9000",
                ["TECHNICAL_AI_EVALUATION_MAX_RETRIES"] = "1",
                ["TECHNICAL_AI_FEEDBACK_MAX_RETRIES"] = "0",
            })
            .Build();

        var options = TechnicalInterviewOptions.FromConfiguration(configuration);

        Assert.True(options.ParallelProcessingEnabled);
        Assert.Equal(2, options.MaxParallelTasksPerSession);
        Assert.Equal(7, options.GlobalConcurrencyLimit);
        Assert.Equal(16_000, options.EvaluationTimeoutMs);
        Assert.Equal(9_000, options.FeedbackTimeoutMs);
        Assert.Equal(1, options.EvaluationMaxRetries);
        Assert.Equal(0, options.FeedbackMaxRetries);
    }
}
