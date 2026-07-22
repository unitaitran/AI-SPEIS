using System.Diagnostics;
using ai_speis_be.Models.Enums;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;

namespace ai_speis_be.TechnicalInterviews.Orchestration
{
    public sealed record TechnicalAITaskOutcome<T>(
        TechnicalAITaskStatus Status,
        AIProviderResult<T>? ProviderResult,
        DateTime StartedAt,
        DateTime CompletedAt,
        long LatencyMs,
        string? ErrorCode)
    {
        public bool IsFulfilled => Status == TechnicalAITaskStatus.Fulfilled
            && ProviderResult?.Success == true
            && ProviderResult.Data is not null;
    }

    public sealed record TechnicalParallelProcessingMetrics(
        long TotalProcessingLatencyMs,
        long SequentialEstimatedLatencyMs,
        long ParallelLatencySavingMs);

    public sealed record TechnicalParallelAIResults(
        TechnicalAITaskOutcome<TechnicalAIEvaluationResponse> Evaluation,
        TechnicalAITaskOutcome<TechnicalAIFeedbackDraftResponse> Feedback,
        TechnicalParallelProcessingMetrics Metrics);

    public interface ITechnicalAnswerParallelProcessor
    {
        Task<TechnicalParallelAIResults> ProcessAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken);
    }

    public sealed class TechnicalAnswerParallelProcessor : ITechnicalAnswerParallelProcessor
    {
        private readonly ITechnicalInterviewAIProviderResolver _providerResolver;
        private readonly TechnicalInterviewOptions _options;

        public TechnicalAnswerParallelProcessor(
            ITechnicalInterviewAIProviderResolver providerResolver,
            TechnicalInterviewOptions options)
        {
            _providerResolver = providerResolver;
            _options = options;
        }

        public async Task<TechnicalParallelAIResults> ProcessAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            var provider = _providerResolver.Resolve();
            var stopwatch = Stopwatch.StartNew();
            using var sessionGate = new SemaphoreSlim(
                _options.MaxParallelTasksPerSession,
                _options.MaxParallelTasksPerSession);

            TechnicalAITaskOutcome<TechnicalAIEvaluationResponse> evaluation;
            TechnicalAITaskOutcome<TechnicalAIFeedbackDraftResponse> feedback;
            using var feedbackStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            // These are true async I/O operations. Both are started before either is
            // awaited; Task.Run is intentionally not involved.
            var evaluationTask = RunSettledAsync(
                token => provider.EvaluateAnswerAsync(context, token),
                _options.EvaluationTimeoutMs,
                sessionGate,
                cancellationToken);
            var feedbackTask = RunSettledAsync(
                token => provider.GenerateFeedbackDraftAsync(context, token),
                _options.FeedbackTimeoutMs,
                sessionGate,
                cancellationToken,
                feedbackStop.Token);
            evaluation = await evaluationTask;
            if (!evaluation.IsFulfilled)
            {
                feedbackStop.Cancel();
                feedback = await feedbackTask;
            }
            else
            {
                if (!feedbackTask.IsCompleted)
                {
                    // Detailed feedback is non-critical. Do not let it extend the
                    // answer-to-next-question path once critical work is settled.
                    feedbackStop.Cancel();
                }
                feedback = await feedbackTask;
            }

            stopwatch.Stop();
            var sequentialEstimate = evaluation.LatencyMs + feedback.LatencyMs;
            return new TechnicalParallelAIResults(
                evaluation,
                feedback,
                new TechnicalParallelProcessingMetrics(
                    stopwatch.ElapsedMilliseconds,
                    sequentialEstimate,
                    Math.Max(0, sequentialEstimate - stopwatch.ElapsedMilliseconds)));
        }

        private static async Task<TechnicalAITaskOutcome<T>> RunSettledAsync<T>(
            Func<CancellationToken, Task<AIProviderResult<T>>> operation,
            int timeoutMs,
            SemaphoreSlim sessionGate,
            CancellationToken cancellationToken,
            CancellationToken speculativeStopToken = default)
        {
            var startedAt = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            await sessionGate.WaitAsync(cancellationToken);
            using var operationCts = speculativeStopToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, speculativeStopToken)
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            operationCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

            try
            {
                var providerResult = await operation(operationCts.Token)
                    .WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), cancellationToken);
                var status = Classify(providerResult);
                stopwatch.Stop();
                return new TechnicalAITaskOutcome<T>(
                    status,
                    providerResult,
                    startedAt,
                    DateTime.UtcNow,
                    stopwatch.ElapsedMilliseconds,
                    providerResult.ErrorCode);
            }
            catch (TimeoutException)
            {
                operationCts.Cancel();
                stopwatch.Stop();
                return FailedOutcome<T>(
                    TechnicalAITaskStatus.Timeout,
                    startedAt,
                    stopwatch.ElapsedMilliseconds,
                    "TIMEOUT");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                return FailedOutcome<T>(
                    TechnicalAITaskStatus.Timeout,
                    startedAt,
                    stopwatch.ElapsedMilliseconds,
                    "TIMEOUT");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                stopwatch.Stop();
                return FailedOutcome<T>(
                    TechnicalAITaskStatus.Rejected,
                    startedAt,
                    stopwatch.ElapsedMilliseconds,
                    "PROVIDER_EXCEPTION");
            }
            finally
            {
                sessionGate.Release();
            }
        }

        private static TechnicalAITaskStatus Classify<T>(AIProviderResult<T> result)
        {
            if (result.Success && result.Data is not null)
            {
                return TechnicalAITaskStatus.Fulfilled;
            }

            return result.ErrorCode switch
            {
                "TIMEOUT" => TechnicalAITaskStatus.Timeout,
                "MALFORMED_JSON" or "EMPTY_RESPONSE" => TechnicalAITaskStatus.InvalidOutput,
                _ => TechnicalAITaskStatus.Rejected
            };
        }

        private static TechnicalAITaskOutcome<T> FailedOutcome<T>(
            TechnicalAITaskStatus status,
            DateTime startedAt,
            long latencyMs,
            string errorCode)
        {
            return new TechnicalAITaskOutcome<T>(
                status,
                null,
                startedAt,
                DateTime.UtcNow,
                latencyMs,
                errorCode);
        }
    }
}
