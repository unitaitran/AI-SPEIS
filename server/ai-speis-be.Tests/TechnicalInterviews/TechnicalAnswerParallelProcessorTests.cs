using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Orchestration;

namespace ai_speis_be.Tests.TechnicalInterviews;

public sealed class TechnicalAnswerParallelProcessorTests
{
    [Fact]
    public async Task ProcessAsync_StartsAllThreeTasksWithTheSameImmutableContext()
    {
        var provider = new BarrierProvider();
        var processor = CreateProcessor(provider);
        var context = TechnicalParallelTestData.CreateContext();

        var processingTask = processor.ProcessAsync(context, CancellationToken.None);
        await Task.WhenAll(
            provider.EvaluationStarted.Task,
            provider.FeedbackStarted.Task,
            provider.QuestionStarted.Task).WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(processingTask.IsCompleted);
        Assert.All(provider.ReceivedContexts, received => Assert.Same(context, received));

        provider.Release.TrySetResult();
        var results = await processingTask;

        Assert.True(results.Evaluation.IsFulfilled);
        Assert.True(results.Feedback.IsFulfilled);
        Assert.True(results.QuestionBundle.IsFulfilled);
    }

    [Fact]
    public async Task ProcessAsync_TotalLatencyTracksSlowestTaskInsteadOfSum()
    {
        var provider = new DelayedProvider(TimeSpan.FromMilliseconds(150));
        var processor = CreateProcessor(provider);

        var results = await processor.ProcessAsync(
            TechnicalParallelTestData.CreateContext(),
            CancellationToken.None);

        Assert.True(results.Metrics.SequentialEstimatedLatencyMs >= 400);
        Assert.True(results.Metrics.TotalProcessingLatencyMs < 300);
        Assert.True(results.Metrics.ParallelLatencySavingMs >= 100);
    }

    [Theory]
    [InlineData("feedback", "GEMINI_QUOTA_EXCEEDED", "REJECTED")]
    [InlineData("feedback", "MALFORMED_JSON", "INVALIDOUTPUT")]
    [InlineData("question", "TIMEOUT", "TIMEOUT")]
    public async Task ProcessAsync_SettlesPartialFailuresWithoutDiscardingEvaluation(
        string failingOperation,
        string errorCode,
        string expectedStatus)
    {
        var provider = new PartialFailureProvider(failingOperation, errorCode);
        var results = await CreateProcessor(provider).ProcessAsync(
            TechnicalParallelTestData.CreateContext(),
            CancellationToken.None);

        Assert.True(results.Evaluation.IsFulfilled);
        var actual = failingOperation == "feedback"
            ? results.Feedback.Status.ToString().ToUpperInvariant()
            : results.QuestionBundle.Status.ToString().ToUpperInvariant();
        Assert.Equal(expectedStatus, actual);
    }

    [Fact]
    public async Task ProcessAsync_FeedbackTimeoutDoesNotBlockEvaluationOrQuestionBundle()
    {
        var provider = new SlowFeedbackProvider();
        var processor = new TechnicalAnswerParallelProcessor(
            new FixedResolver(provider),
            new TechnicalInterviewOptions
            {
                ParallelProcessingEnabled = true,
                MaxParallelTasksPerSession = 3,
                EvaluationTimeoutMs = 1_000,
                FeedbackTimeoutMs = 50,
                QuestionTimeoutMs = 1_000
            });

        var results = await processor.ProcessAsync(
            TechnicalParallelTestData.CreateContext(),
            CancellationToken.None);

        Assert.True(results.Evaluation.IsFulfilled);
        Assert.Equal(ai_speis_be.Models.Enums.TechnicalAITaskStatus.Timeout, results.Feedback.Status);
        Assert.True(results.QuestionBundle.IsFulfilled);
        Assert.True(results.Metrics.TotalProcessingLatencyMs < 500);
    }

    private static TechnicalAnswerParallelProcessor CreateProcessor(ITechnicalInterviewAIProvider provider)
    {
        return new TechnicalAnswerParallelProcessor(
            new FixedResolver(provider),
            new TechnicalInterviewOptions
            {
                ParallelProcessingEnabled = true,
                MaxParallelTasksPerSession = 3,
                EvaluationTimeoutMs = 2_000,
                FeedbackTimeoutMs = 2_000,
                QuestionTimeoutMs = 2_000
            });
    }

    private sealed class FixedResolver : ITechnicalInterviewAIProviderResolver
    {
        private readonly ITechnicalInterviewAIProvider _provider;

        public FixedResolver(ITechnicalInterviewAIProvider provider) => _provider = provider;

        public ITechnicalInterviewAIProvider Resolve() => _provider;
    }

    private abstract class FakeProviderBase : ITechnicalInterviewAIProvider
    {
        public string ProviderName => "fake";

        public virtual Task<AIProviderResult<TechnicalAISelectionResponse>> SelectQuestionAsync(
            TechnicalAISelectionRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public abstract Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken);

        public abstract Task<AIProviderResult<TechnicalAIFeedbackDraftResponse>> GenerateFeedbackDraftAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken);

        public abstract Task<AIProviderResult<TechnicalAIQuestionBundleResponse>> GenerateQuestionBundleAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken);

        public virtual Task<AIProviderResult<TechnicalAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            TechnicalAIFinalSummaryRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        protected static AIProviderResult<T> Success<T>(T data) => new()
        {
            Success = true,
            Data = data,
            Model = "fake-gemini"
        };

        protected static AIProviderResult<T> Failure<T>(string errorCode) => new()
        {
            Success = false,
            ErrorCode = errorCode,
            Model = "fake-gemini"
        };
    }

    private sealed class BarrierProvider : FakeProviderBase
    {
        public TaskCompletionSource EvaluationStarted { get; } = NewSignal();
        public TaskCompletionSource FeedbackStarted { get; } = NewSignal();
        public TaskCompletionSource QuestionStarted { get; } = NewSignal();
        public TaskCompletionSource Release { get; } = NewSignal();
        public List<TechnicalAnswerProcessingContext> ReceivedContexts { get; } = new();

        public override async Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            ReceivedContexts.Add(context);
            EvaluationStarted.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Success(TechnicalParallelTestData.CreateEvaluation());
        }

        public override async Task<AIProviderResult<TechnicalAIFeedbackDraftResponse>> GenerateFeedbackDraftAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            ReceivedContexts.Add(context);
            FeedbackStarted.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Success(TechnicalParallelTestData.CreateFeedback());
        }

        public override async Task<AIProviderResult<TechnicalAIQuestionBundleResponse>> GenerateQuestionBundleAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            ReceivedContexts.Add(context);
            QuestionStarted.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Success(TechnicalParallelTestData.CreateBundle());
        }

        private static TaskCompletionSource NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class DelayedProvider : FakeProviderBase
    {
        private readonly TimeSpan _delay;

        public DelayedProvider(TimeSpan delay) => _delay = delay;

        public override async Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return Success(TechnicalParallelTestData.CreateEvaluation());
        }

        public override async Task<AIProviderResult<TechnicalAIFeedbackDraftResponse>> GenerateFeedbackDraftAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return Success(TechnicalParallelTestData.CreateFeedback());
        }

        public override async Task<AIProviderResult<TechnicalAIQuestionBundleResponse>> GenerateQuestionBundleAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken);
            return Success(TechnicalParallelTestData.CreateBundle());
        }
    }

    private sealed class PartialFailureProvider : FakeProviderBase
    {
        private readonly string _operation;
        private readonly string _errorCode;

        public PartialFailureProvider(string operation, string errorCode)
        {
            _operation = operation;
            _errorCode = errorCode;
        }

        public override Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken) => Task.FromResult(
                Success(TechnicalParallelTestData.CreateEvaluation()));

        public override Task<AIProviderResult<TechnicalAIFeedbackDraftResponse>> GenerateFeedbackDraftAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken) => Task.FromResult(
                _operation == "feedback"
                    ? Failure<TechnicalAIFeedbackDraftResponse>(_errorCode)
                    : Success(TechnicalParallelTestData.CreateFeedback()));

        public override Task<AIProviderResult<TechnicalAIQuestionBundleResponse>> GenerateQuestionBundleAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken) => Task.FromResult(
                _operation == "question"
                    ? Failure<TechnicalAIQuestionBundleResponse>(_errorCode)
                    : Success(TechnicalParallelTestData.CreateBundle()));
    }

    private sealed class SlowFeedbackProvider : FakeProviderBase
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

        public override Task<AIProviderResult<TechnicalAIQuestionBundleResponse>> GenerateQuestionBundleAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken) => Task.FromResult(
                Success(TechnicalParallelTestData.CreateBundle()));
    }
}
