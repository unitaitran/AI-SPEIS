using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Orchestration;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalAnswerParallelProcessorTests
{
    [Fact]
    public async Task EvaluateAsync_PerformsExactlyOneEvaluationOperation()
    {
        var provider = new BarrierProvider();
        var service = CreateService(provider);
        var context = TechnicalParallelTestData.CreateContext();

        var processing = service.EvaluateAsync(context, CancellationToken.None);
        await provider.EvaluationStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(processing.IsCompleted);
        Assert.Equal(1, provider.EvaluationCallCount);
        Assert.Same(context, provider.Context);
        provider.Release.TrySetResult();

        var result = await processing;
        Assert.True(result.Evaluation.IsFulfilled);
        Assert.Equal(1, provider.EvaluationCallCount);
    }

    [Fact]
    public async Task EvaluateAsync_TimeoutReturnsEvaluationFallbackStatus()
    {
        var service = new TechnicalAnswerEvaluationService(
            new FixedResolver(new SlowEvaluationProvider()),
            new TechnicalInterviewOptions { EvaluationTimeoutMs = 25 });

        var result = await service.EvaluateAsync(
            TechnicalParallelTestData.CreateContext(),
            CancellationToken.None);

        Assert.Equal(ai_speis_be.Models.Enums.TechnicalAITaskStatus.Timeout, result.Evaluation.Status);
        Assert.False(result.Evaluation.IsFulfilled);
    }

    private static TechnicalAnswerEvaluationService CreateService(ITechnicalInterviewAIProvider provider) =>
        new(new FixedResolver(provider), new TechnicalInterviewOptions { EvaluationTimeoutMs = 2_000 });

    private sealed class FixedResolver : ITechnicalInterviewAIProviderResolver
    {
        private readonly ITechnicalInterviewAIProvider _provider;
        public FixedResolver(ITechnicalInterviewAIProvider provider) => _provider = provider;
        public ITechnicalInterviewAIProvider Resolve() => _provider;
    }

    private abstract class FakeProvider : ITechnicalInterviewAIProvider
    {
        public string ProviderName => "fake";
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
        public int EvaluationCallCount { get; private set; }
        public TechnicalAnswerProcessingContext? Context { get; private set; }

        public override async Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            EvaluationCallCount++;
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
