using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ai_speis_be.AI.Json;
using ai_speis_be.TechnicalInterviews.Configuration;

namespace ai_speis_be.TechnicalInterviews.AI
{
    public sealed class ExternalTechnicalInterviewAIProvider : ITechnicalInterviewAIProvider
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new LenientDecimalJsonConverter() }
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITechnicalAIConcurrencyGate _concurrencyGate;
        private readonly TechnicalInterviewOptions _options;
        private readonly ILogger<ExternalTechnicalInterviewAIProvider> _logger;
        private readonly IAiJsonRecoveryService _jsonRecovery;

        public ExternalTechnicalInterviewAIProvider(
            IHttpClientFactory httpClientFactory,
            ITechnicalAIConcurrencyGate concurrencyGate,
            TechnicalInterviewOptions options,
            ILogger<ExternalTechnicalInterviewAIProvider> logger,
            IAiJsonRecoveryService? jsonRecovery = null)
        {
            _httpClientFactory = httpClientFactory;
            _concurrencyGate = concurrencyGate;
            _options = options;
            _logger = logger;
            _jsonRecovery = jsonRecovery ?? new AiJsonRecoveryService();
        }

        public string ProviderName => "external";

        public Task<AIProviderResult<TechnicalAISelectionResponse>> SelectQuestionsAsync(
            TechnicalAISelectionRequest request,
            CancellationToken cancellationToken)
        {
            var prompt = TechnicalPromptFactory.Selection(request);
            return CallAsync<TechnicalAISelectionResponse>(
                prompt.System,
                prompt.User,
                _options.TimeoutSeconds * 1_000,
                _options.MaxRetries,
                null,
                cancellationToken);
        }

        public Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAnswerProcessingContext context,
            CancellationToken cancellationToken)
        {
            var prompt = TechnicalPromptFactory.Evaluation(context);
            return CallAsync<TechnicalAIEvaluationResponse>(
                prompt.System,
                prompt.User,
                _options.EvaluationTimeoutMs,
                _options.EvaluationMaxRetries,
                _options.OllamaEvaluationModel,
                cancellationToken);
        }

        public Task<AIProviderResult<TechnicalAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            TechnicalAIFinalSummaryRequest request,
            CancellationToken cancellationToken)
        {
            var prompt = TechnicalPromptFactory.Summary(request);
            return CallAsync<TechnicalAIFinalSummaryResponse>(
                prompt.System,
                prompt.User,
                _options.TimeoutSeconds * 1_000,
                _options.MaxRetries,
                null,
                cancellationToken);
        }

        private async Task<AIProviderResult<T>> CallAsync<T>(
            string systemPrompt,
            string userPrompt,
            int timeoutMs,
            int maxRetries,
            string? ollamaModelOverride,
            CancellationToken cancellationToken)
        {
            var startedAt = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            var isOllama = string.Equals(_options.Provider, "ollama", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_options.Provider, "local", StringComparison.OrdinalIgnoreCase);
            var baseUrl = isOllama ? _options.OllamaBaseUrl : _options.BaseUrl;
            var model = isOllama
                ? !string.IsNullOrWhiteSpace(ollamaModelOverride)
                    ? ollamaModelOverride
                    : !string.IsNullOrWhiteSpace(_options.OllamaModel)
                        ? _options.OllamaModel
                        : _options.Model
                : _options.Model;

            if (!isOllama && string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                return Failure<T>(stopwatch, startedAt, "CONFIGURATION_MISSING", 0);
            }

            using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            operationCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
            var operationToken = operationCts.Token;

            var payload = isOllama
                ? (object)new
                {
                    model = model,
                    temperature = 0.1,
                    format = "json",
                    response_format = new { type = "json_object" },
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    }
                }
                : new
                {
                    model = model,
                    temperature = 0.1,
                    response_format = new { type = "json_object" },
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    }
                };

            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await using var lease = await _concurrencyGate.EnterAsync(operationToken);
                    using var request = new HttpRequestMessage(
                        HttpMethod.Post,
                        new Uri(new Uri(baseUrl), "chat/completions"));
                    if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                    }
                    request.Content = new StringContent(
                        JsonSerializer.Serialize(payload, JsonOptions),
                        Encoding.UTF8,
                        "application/json");

                    _logger.LogInformation(
                        "[AI-CALL] Sending Technical AI Request | Provider: {Provider} | Endpoint: {Endpoint} | Model: {Model}",
                        isOllama ? "ollama" : "gemini",
                        new Uri(new Uri(baseUrl), "chat/completions"),
                        model);

                    var client = _httpClientFactory.CreateClient("TechnicalInterviewAI");
                    using var response = await client.SendAsync(request, operationToken);
                    if (ShouldRetry(response.StatusCode) && attempt < maxRetries)
                    {
                        // Release the global quota slot while backing off so another
                        // session can make progress instead of waiting behind a retry.
                        await lease.DisposeAsync();
                        await BackoffAsync(attempt, operationToken);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "Technical Interview AI returned HTTP {StatusCode}.",
                            (int)response.StatusCode);
                        return Failure<T>(
                            stopwatch,
                            startedAt,
                            MapHttpError(response.StatusCode),
                            attempt);
                    }

                    var responseJson = await response.Content.ReadAsStringAsync(operationToken);
                    var envelope = JsonSerializer.Deserialize<ChatCompletionEnvelope>(responseJson, JsonOptions);
                    var content = envelope?.Choices.FirstOrDefault()?.Message.Content;
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        return Failure<T>(stopwatch, startedAt, "EMPTY_RESPONSE", attempt);
                    }

                    var recovery = _jsonRecovery.Deserialize<T>(content, JsonOptions);
                    if (!recovery.Success || recovery.Data is null)
                    {
                        return Failure<T>(
                            stopwatch,
                            startedAt,
                            "MALFORMED_JSON_UNRECOVERABLE",
                            attempt,
                            _jsonRecovery.CreateSafeRawResponse(content),
                            recovery.Metadata);
                    }

                    if (recovery.Metadata.RecoveryFlags.Count > 0)
                    {
                        _logger.LogWarning(
                            "Technical AI JSON recovered. Provider: {Provider}, Model: {Model}, Flags: {Flags}.",
                            isOllama ? "ollama" : "external",
                            model,
                            string.Join(',', recovery.Metadata.RecoveryFlags));
                    }

                    stopwatch.Stop();
                    return new AIProviderResult<T>
                    {
                        Success = true,
                        Data = recovery.Data,
                        Model = envelope?.Model ?? _options.Model,
                        LatencyMs = stopwatch.ElapsedMilliseconds,
                        InputTokens = envelope?.Usage?.PromptTokens,
                        OutputTokens = envelope?.Usage?.CompletionTokens,
                        RawResponse = _jsonRecovery.CreateSafeRawResponse(content),
                        JsonRecovery = recovery.Metadata,
                        RetryCount = attempt,
                        StartedAt = startedAt,
                        CompletedAt = DateTime.UtcNow
                    };
                }
                catch (JsonException)
                {
                    return Failure<T>(stopwatch, startedAt, "MALFORMED_JSON", attempt);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return Failure<T>(stopwatch, startedAt, "TIMEOUT", attempt);
                }
                catch (HttpRequestException exception) when (attempt < maxRetries)
                {
                    _logger.LogWarning(exception, "Transient Technical Interview AI request failure.");
                    await BackoffAsync(attempt, operationToken);
                }
                catch (HttpRequestException exception)
                {
                    _logger.LogWarning(exception, "Technical Interview AI request failed.");
                    return Failure<T>(stopwatch, startedAt, "NETWORK_ERROR", attempt);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Technical Interview AI provider failed while processing response.");
                    return Failure<T>(stopwatch, startedAt, "PROVIDER_EXCEPTION", attempt);
                }
            }

            return Failure<T>(stopwatch, startedAt, "RETRY_EXHAUSTED", maxRetries);
        }

        private AIProviderResult<T> Failure<T>(
            Stopwatch stopwatch,
            DateTime startedAt,
            string errorCode,
            int retryCount,
            string? rawResponse = null,
            AiJsonRecoveryMetadata? jsonRecovery = null)
        {
            stopwatch.Stop();
            return new AIProviderResult<T>
            {
                Success = false,
                Model = _options.Model,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                ErrorCode = errorCode,
                RawResponse = rawResponse,
                JsonRecovery = jsonRecovery,
                RetryCount = retryCount,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow
            };
        }

        private static Task BackoffAsync(int attempt, CancellationToken cancellationToken)
        {
            var delayMs = Math.Min(2_000, 250 * (1 << Math.Min(attempt, 3)));
            return Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);
        }

        private static string MapHttpError(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.TooManyRequests => "GEMINI_QUOTA_EXCEEDED",
                HttpStatusCode.Forbidden => "GEMINI_PERMISSION_DENIED",
                HttpStatusCode.ServiceUnavailable => "GEMINI_UNAVAILABLE",
                _ => $"HTTP_{(int)statusCode}"
            };
        }

        private static bool ShouldRetry(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
        }

        private sealed class ChatCompletionEnvelope
        {
            public string? Model { get; set; }
            public List<ChatChoice> Choices { get; set; } = new();
            public ChatUsage? Usage { get; set; }
        }

        private sealed class ChatChoice
        {
            public ChatMessage Message { get; set; } = new();
        }

        private sealed class ChatMessage
        {
            public string Content { get; set; } = string.Empty;
        }

        private sealed class ChatUsage
        {
            [JsonPropertyName("prompt_tokens")]
            public int? PromptTokens { get; set; }

            [JsonPropertyName("completion_tokens")]
            public int? CompletionTokens { get; set; }
        }
    }
}
