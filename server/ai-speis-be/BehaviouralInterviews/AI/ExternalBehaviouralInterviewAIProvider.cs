using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ai_speis_be.AI.Json;
using ai_speis_be.BehaviouralInterviews.Configuration;

namespace ai_speis_be.BehaviouralInterviews.AI
{
    public sealed class ExternalBehaviouralInterviewAIProvider : IBehaviouralInterviewAIProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly BehaviouralInterviewOptions _options;
        private readonly ILogger<ExternalBehaviouralInterviewAIProvider> _logger;
        private readonly IAiJsonRecoveryService _jsonRecovery;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new LenientDecimalJsonConverter() }
        };

        public ExternalBehaviouralInterviewAIProvider(
            IHttpClientFactory httpClientFactory,
            BehaviouralInterviewOptions options,
            ILogger<ExternalBehaviouralInterviewAIProvider> logger,
            IAiJsonRecoveryService? jsonRecovery = null)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _logger = logger;
            _jsonRecovery = jsonRecovery ?? new AiJsonRecoveryService();
        }

        public string ProviderName => "external";

        public Task<BehaviouralAIProviderResult<BehaviouralAISelectionResponse>> SelectQuestionsAsync(
            BehaviouralAISelectionRequest request,
            CancellationToken cancellationToken)
        {
            var prompt = BehaviouralPromptFactory.Selection(request);
            return CallAsync<BehaviouralAISelectionResponse>(prompt.System, prompt.User, cancellationToken);
        }

        public Task<BehaviouralAIProviderResult<BehaviouralAIEvaluationResponse>> EvaluateAnswerAsync(
            BehaviouralAIEvaluationRequest request,
            CancellationToken cancellationToken)
        {
            var prompt = BehaviouralPromptFactory.Evaluation(request);
            return CallAsync<BehaviouralAIEvaluationResponse>(prompt.System, prompt.User, cancellationToken);
        }

        public Task<BehaviouralAIProviderResult<BehaviouralAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            BehaviouralAIFinalSummaryRequest request,
            CancellationToken cancellationToken)
        {
            var prompt = BehaviouralPromptFactory.Summary(request);
            return CallAsync<BehaviouralAIFinalSummaryResponse>(prompt.System, prompt.User, cancellationToken);
        }

        private async Task<BehaviouralAIProviderResult<T>> CallAsync<T>(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken)
        {
            var startedAt = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            var isOllama = string.Equals(_options.Provider, "ollama", StringComparison.OrdinalIgnoreCase)
                || string.Equals(_options.Provider, "local", StringComparison.OrdinalIgnoreCase);
            var baseUrl = isOllama ? _options.OllamaBaseUrl : _options.BaseUrl;
            var model = isOllama
                ? typeof(T) == typeof(BehaviouralAIEvaluationResponse)
                    && !string.IsNullOrWhiteSpace(_options.OllamaEvaluationModel)
                    ? _options.OllamaEvaluationModel
                    : !string.IsNullOrWhiteSpace(_options.OllamaModel) ? _options.OllamaModel : _options.Model
                : _options.Model;

            if (!isOllama && string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                return Failure<T>(stopwatch, startedAt, "CONFIGURATION_MISSING", 0);
            }

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

            for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
            {
                try
                {
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
                        "[AI-CALL] Sending Behavioural AI Request | Provider: {Provider} | Endpoint: {Endpoint} | Model: {Model}",
                        isOllama ? "ollama" : "gemini",
                        new Uri(new Uri(baseUrl), "chat/completions"),
                        model);

                    var client = _httpClientFactory.CreateClient("BehaviouralInterviewAI");
                    using var response = await client.SendAsync(request, cancellationToken);
                    if (ShouldRetry(response.StatusCode) && attempt < _options.MaxRetries)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                        _logger.LogWarning(
                            "Behavioural Interview AI returned HTTP {StatusCode}. Body: {Body}",
                            (int)response.StatusCode,
                            errorBody);
                        return Failure<T>(stopwatch, startedAt, MapHttpError(response.StatusCode), attempt);
                    }

                    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    var envelope = JsonSerializer.Deserialize<ChatCompletionEnvelope>(responseJson, JsonOptions);
                    var content = envelope?.Choices.FirstOrDefault()?.Message.Content;
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        return Failure<T>(stopwatch, startedAt, "EMPTY_RESPONSE", attempt);
                    }

                    var recovery = _jsonRecovery.Deserialize<T>(content, JsonOptions);
                    if (!recovery.Success || recovery.Data is null)
                    {
                        return Failure<T>(stopwatch, startedAt, "MALFORMED_JSON_UNRECOVERABLE", attempt,
                            _jsonRecovery.CreateSafeRawResponse(content), recovery.Metadata);
                    }

                    var parsed = recovery.Data;

                    if (parsed is BehaviouralAIEvaluationResponse evalResponse)
                    {
                        NormalizeBehaviouralRubricCodes(evalResponse);
                    }

                    stopwatch.Stop();
                    return new BehaviouralAIProviderResult<T>
                    {
                        Success = true,
                        Data = parsed,
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
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Behavioural AI JSON Deserialization failed: {Error}", ex.Message);
                    return Failure<T>(stopwatch, startedAt, "MALFORMED_JSON_UNRECOVERABLE", attempt);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return Failure<T>(stopwatch, startedAt, "TIMEOUT", attempt);
                }
                catch (HttpRequestException exception) when (attempt < _options.MaxRetries)
                {
                    _logger.LogWarning(exception, "Transient Behavioural Interview AI request failure.");
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
                }
                catch (HttpRequestException exception)
                {
                    _logger.LogWarning(exception, "Behavioural Interview AI request failed.");
                    return Failure<T>(stopwatch, startedAt, "NETWORK_ERROR", attempt);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Behavioural Interview AI provider failed while processing response.");
                    return Failure<T>(stopwatch, startedAt, "PROVIDER_EXCEPTION", attempt);
                }
            }

            return Failure<T>(stopwatch, startedAt, "RETRY_EXHAUSTED", _options.MaxRetries);
        }

        private BehaviouralAIProviderResult<T> Failure<T>(
            Stopwatch stopwatch,
            DateTime startedAt,
            string errorCode,
            int retryCount,
            string? rawResponse = null,
            AiJsonRecoveryMetadata? jsonRecovery = null)
        {
            stopwatch.Stop();
            return new BehaviouralAIProviderResult<T>
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

        private static string MapHttpError(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.TooManyRequests => "GEMINI_QUOTA_EXCEEDED",
                HttpStatusCode.Forbidden => "GEMINI_PERMISSION_DENIED",
                HttpStatusCode.Unauthorized => "GEMINI_UNAUTHORIZED",
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
        private static void NormalizeBehaviouralRubricCodes(BehaviouralAIEvaluationResponse evalResponse)
        {
            if (evalResponse.DimensionEvaluations is null) return;
            foreach (var dim in evalResponse.DimensionEvaluations)
            {
                if (string.IsNullOrWhiteSpace(dim.RubricCode)) continue;
                var code = dim.RubricCode.Trim().ToUpperInvariant();
                if (code == "SITUATION_TASK" || code == "ACTION" || code == "RESULT" || code == "COMPETENCY" || code == "COMMUNICATION")
                {
                    dim.RubricCode = code;
                    continue;
                }

                if (code.Contains("SITUATION") || code.Contains("CONTEXT") || code.Contains("TASK"))
                {
                    dim.RubricCode = "SITUATION_TASK";
                }
                else if (code.Contains("ACTION") || code.Contains("OWNERSHIP"))
                {
                    dim.RubricCode = "ACTION";
                }
                else if (code.Contains("RESULT") || code.Contains("REFLECTION"))
                {
                    dim.RubricCode = "RESULT";
                }
                else if (code.Contains("COMPETENCY") || code.Contains("FIT"))
                {
                    dim.RubricCode = "COMPETENCY";
                }
                else if (code.Contains("COMMUNICATION"))
                {
                    dim.RubricCode = "COMMUNICATION";
                }
            }
        }
    }
}
