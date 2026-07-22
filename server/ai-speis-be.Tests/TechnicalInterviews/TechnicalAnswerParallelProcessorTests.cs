using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Orchestration;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalAnswerParallelProcessorTests
{
    [Fact]
    public async Task ProcessAsync_StartsEvaluationDecisionAndFeedbackBeforeAwaitingEither()
    {
        var provider = new BarrierProvider();
        var processor = CreateProcessor(provider);
        var context = TechnicalParallelTestData.CreateContext();

        var processing = processor.ProcessAsync(context, CancellationToken.None);
        await Task.WhenAll(provider.EvaluationStarted.Task, provider.FeedbackStarted.Task)
            .WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(processing.IsCompleted);
        Assert.All(provider.Contexts, item => Assert.Same(context, item));
        provider.Release.TrySetResult();

        var results = await processing;
        Assert.True(results.Evaluation.IsFulfilled);
        Assert.True(results.Feedback.IsFulfilled);
    }

    [Fact]
    public async Task ProcessAsync_FeedbackTimeoutDoesNotDiscardCriticalEvaluationDecision()
    {
        var provider = new SlowFeedbackProvider();
        var processor = new TechnicalAnswerParallelProcessor(
            new FixedResolver(provider),
            new TechnicalInterviewOptions
            {
                ParallelProcessingEnabled = true,
                MaxParallelTasksPerSession = 2,
                EvaluationTimeoutMs = 1_000,
                FeedbackTimeoutMs = 25
            });

        var results = await processor.ProcessAsync(
            TechnicalParallelTestData.CreateContext(),
            CancellationToken.None);

        Assert.True(results.Evaluation.IsFulfilled);
        Assert.Equal(ai_speis_be.Models.Enums.TechnicalAITaskStatus.Timeout, results.Feedback.Status);
    }

    private static TechnicalAnswerParallelProcessor CreateProcessor(ITechnicalInterviewAIProvider provider) =>
        new(new FixedResolver(provider), new TechnicalInterviewOptions
        {
            ParallelProcessingEnabled = true,
            MaxParallelTasksPerSession = 2,
            EvaluationTimeoutMs = 2_000,
            FeedbackTimeoutMs = 2_000
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
        public abstract Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken);
        public abstract Task<AIProviderResult<TechnicalAIFeedbackDraftResponse>> GenerateFeedbackDraftAsync(
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
        public TaskCompletionSource FeedbackStarted { get; } = NewSignal();
        public TaskCompletionSource Release { get; } = NewSignal();
        public List<TechnicalAnswerProcessingContext> Contexts { get; } = new();

        public override async Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            Contexts.Add(context);
            EvaluationStarted.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Success(TechnicalParallelTestData.CreateEvaluation());
        }

        public override async Task<AIProviderResult<TechnicalAIFeedbackDraftResponse>> GenerateFeedbackDraftAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            Contexts.Add(context);
            FeedbackStarted.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Success(TechnicalParallelTestData.CreateFeedback());
        }

        private static TaskCompletionSource NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class SlowFeedbackProvider : FakeProvider
    {
        public override Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken) => Task.FromResult(
                Success(TechnicalParallelTestData.CreateEvaluation()));

        public override async Task<AIProviderResult<TechnicalAIFeedbackDraftResponse>> GenerateFeedbackDraftAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Success(TechnicalParallelTestData.CreateFeedback());
        }
    }
}
