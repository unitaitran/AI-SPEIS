using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Orchestration;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalAnswerEvaluationProcessorTests
{
    [Fact]
    public async Task ProcessAsync_RunsOnlyEvaluation()
    {
        var provider = new BarrierProvider();
        var processor = CreateProcessor(provider);
        var context = TechnicalParallelTestData.CreateContext();

        var processing = processor.ProcessAsync(context, CancellationToken.None);
        await provider.EvaluationStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(processing.IsCompleted);
        Assert.Same(context, provider.Context);
        provider.Release.TrySetResult();

        var results = await processing;
        Assert.True(results.Evaluation.IsFulfilled);
        Assert.Equal(0, results.Metrics.ParallelLatencySavingMs);
    }

    [Fact]
    public async Task ProcessAsync_EvaluationTimeoutIsReported()
    {
        var provider = new SlowEvaluationProvider();
        var processor = new TechnicalAnswerEvaluationProcessor(
            new FixedResolver(provider),
            new TechnicalInterviewOptions
            {
                EvaluationTimeoutMs = 25,
            });

        var results = await processor.ProcessAsync(
            TechnicalParallelTestData.CreateContext(),
            CancellationToken.None);

        Assert.Equal(ai_speis_be.Models.Enums.TechnicalAITaskStatus.Timeout, results.Evaluation.Status);
    }

    private static TechnicalAnswerEvaluationProcessor CreateProcessor(ITechnicalInterviewAIProvider provider) =>
        new(new FixedResolver(provider), new TechnicalInterviewOptions
        {
            EvaluationTimeoutMs = 2_000,
        });

    private sealed class FixedResolver : ITechnicalInterviewAIProviderResolver
    {
        private readonly ITechnicalInterviewAIProvider _provider;
        public FixedResolver(ITechnicalInterviewAIProvider provider) => _provider = provider;
        public ITechnicalInterviewAIProvider Resolve() => _provider;
    }

    private abstract class FakeProvider : ITechnicalInterviewAIProvider
    {
        public string ProviderName => "fake";
        public Task<AIProviderResult<TechnicalAISelectionResponse>> SelectQuestionsAsync(
            TechnicalAISelectionRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public abstract Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken);
        public Task<AIProviderResult<TechnicalAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            TechnicalAIFinalSummaryRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        protected static AIProviderResult<T> Success<T>(T data) => new()
        {
            Success = true,
            Data = data,
            Model = "fake"
        };
    }

    private sealed class BarrierProvider : FakeProvider
    {
        public TaskCompletionSource EvaluationStarted { get; } = NewSignal();
        public TaskCompletionSource Release { get; } = NewSignal();
        public TechnicalAnswerProcessingContext? Context { get; private set; }

        public override async Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            Context = context;
            EvaluationStarted.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Success(TechnicalParallelTestData.CreateEvaluation());
        }
        private static TaskCompletionSource NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class SlowEvaluationProvider : FakeProvider
    {
        public override async Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Success(TechnicalParallelTestData.CreateEvaluation());
        }
    }
}
