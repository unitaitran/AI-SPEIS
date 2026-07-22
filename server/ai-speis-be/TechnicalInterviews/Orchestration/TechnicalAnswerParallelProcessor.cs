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

    public sealed record TechnicalAnswerEvaluationMetrics(long EvaluationLatencyMs);

    public sealed record TechnicalAnswerEvaluationResult(
        TechnicalAITaskOutcome<TechnicalAIEvaluationResponse> Evaluation,
        TechnicalAnswerEvaluationMetrics Metrics);

    /// <summary>
    /// Evaluation-only boundary used on the answer-to-next-question critical path.
    /// It deliberately performs exactly one AI operation and never creates feedback.
    /// </summary>
    public interface ITechnicalAnswerEvaluationService
    {
        Task<TechnicalAnswerEvaluationResult> EvaluateAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken);
    }

    public sealed class TechnicalAnswerEvaluationService : ITechnicalAnswerEvaluationService
    {
        private readonly ITechnicalInterviewAIProviderResolver _providerResolver;
        private readonly TechnicalInterviewOptions _options;

        public TechnicalAnswerEvaluationService(
            ITechnicalInterviewAIProviderResolver providerResolver,
            TechnicalInterviewOptions options)
        {
            _providerResolver = providerResolver;
            _options = options;
        }

        public async Task<TechnicalAnswerEvaluationResult> EvaluateAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            var startedAt = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            operationCts.CancelAfter(TimeSpan.FromMilliseconds(_options.EvaluationTimeoutMs));

            TechnicalAITaskOutcome<TechnicalAIEvaluationResponse> outcome;
            try
            {
                var result = await _providerResolver.Resolve()
                    .EvaluateAnswerAsync(context, operationCts.Token)
                    .WaitAsync(TimeSpan.FromMilliseconds(_options.EvaluationTimeoutMs), cancellationToken);
                stopwatch.Stop();
                outcome = new TechnicalAITaskOutcome<TechnicalAIEvaluationResponse>(
                    Classify(result),
                    result,
                    startedAt,
                    DateTime.UtcNow,
                    stopwatch.ElapsedMilliseconds,
                    result.ErrorCode);
            }
            catch (TimeoutException)
            {
                operationCts.Cancel();
                stopwatch.Stop();
                outcome = Failed(startedAt, stopwatch.ElapsedMilliseconds, TechnicalAITaskStatus.Timeout, "TIMEOUT");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                outcome = Failed(startedAt, stopwatch.ElapsedMilliseconds, TechnicalAITaskStatus.Timeout, "TIMEOUT");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                stopwatch.Stop();
                outcome = Failed(startedAt, stopwatch.ElapsedMilliseconds, TechnicalAITaskStatus.Rejected, "PROVIDER_EXCEPTION");
            }

            return new TechnicalAnswerEvaluationResult(
                outcome,
                new TechnicalAnswerEvaluationMetrics(outcome.LatencyMs));
        }

        private static TechnicalAITaskStatus Classify(AIProviderResult<TechnicalAIEvaluationResponse> result)
        {
            if (result.Success && result.Data is not null)
                return TechnicalAITaskStatus.Fulfilled;

            return result.ErrorCode switch
            {
                "TIMEOUT" => TechnicalAITaskStatus.Timeout,
                "MALFORMED_JSON" or "EMPTY_RESPONSE" => TechnicalAITaskStatus.InvalidOutput,
                _ => TechnicalAITaskStatus.Rejected
            };
        }

        private static TechnicalAITaskOutcome<TechnicalAIEvaluationResponse> Failed(
            DateTime startedAt,
            long latencyMs,
            TechnicalAITaskStatus status,
            string errorCode) => new(
                status,
                null,
                startedAt,
                DateTime.UtcNow,
                latencyMs,
                errorCode);
    }
}
