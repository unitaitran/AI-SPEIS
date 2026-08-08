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

    public sealed record TechnicalEvaluationProcessingMetrics(
        long TotalProcessingLatencyMs,
        long SequentialEstimatedLatencyMs,
        long ParallelLatencySavingMs);

    public sealed record TechnicalAnswerEvaluationProcessingResult(
        TechnicalAITaskOutcome<TechnicalAIEvaluationResponse> Evaluation,
        TechnicalEvaluationProcessingMetrics Metrics);

    public interface ITechnicalAnswerEvaluationProcessor
    {
        Task<TechnicalAnswerEvaluationProcessingResult> ProcessAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken);
    }

    public sealed class TechnicalAnswerEvaluationProcessor : ITechnicalAnswerEvaluationProcessor
    {
        private readonly ITechnicalInterviewAIProviderResolver _providerResolver;
        private readonly TechnicalInterviewOptions _options;

        public TechnicalAnswerEvaluationProcessor(
            ITechnicalInterviewAIProviderResolver providerResolver,
            TechnicalInterviewOptions options)
        {
            _providerResolver = providerResolver;
            _options = options;
        }

        public async Task<TechnicalAnswerEvaluationProcessingResult> ProcessAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            var provider = _providerResolver.Resolve();
            var stopwatch = Stopwatch.StartNew();
            var evaluation = await RunSettledAsync(
                token => provider.EvaluateAnswerAsync(context, token),
                _options.EvaluationTimeoutMs,
                cancellationToken);

            stopwatch.Stop();
            var sequentialEstimate = evaluation.LatencyMs;
            return new TechnicalAnswerEvaluationProcessingResult(
                evaluation,
                new TechnicalEvaluationProcessingMetrics(
                    stopwatch.ElapsedMilliseconds,
                    sequentialEstimate,
                    0));
        }

        private static async Task<TechnicalAITaskOutcome<T>> RunSettledAsync<T>(
            Func<CancellationToken, Task<AIProviderResult<T>>> operation,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            var startedAt = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
                "MALFORMED_JSON" or "MALFORMED_JSON_UNRECOVERABLE" or "EMPTY_RESPONSE" => TechnicalAITaskStatus.InvalidOutput,
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
