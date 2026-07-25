using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ai_speis_be.BehaviouralInterviews.Configuration;

namespace ai_speis_be.BehaviouralInterviews.AI
{
    public sealed class ExternalBehaviouralInterviewAIProvider : IBehaviouralInterviewAIProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly BehaviouralInterviewOptions _options;
        private readonly ILogger<ExternalBehaviouralInterviewAIProvider> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public ExternalBehaviouralInterviewAIProvider(
            IHttpClientFactory httpClientFactory,
            BehaviouralInterviewOptions options,
            ILogger<ExternalBehaviouralInterviewAIProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _logger = logger;
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
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                return Failure<T>(stopwatch, startedAt, "CONFIGURATION_MISSING", 0);
            }

            var payload = new
            {
                model = _options.Model,
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
                        new Uri(new Uri(_options.BaseUrl), "chat/completions"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                    request.Content = new StringContent(
                        JsonSerializer.Serialize(payload, JsonOptions),
                        Encoding.UTF8,
                        "application/json");

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

                    var parsed = JsonSerializer.Deserialize<T>(StripMarkdownFence(content), JsonOptions);
                    if (parsed is null)
                    {
                        return Failure<T>(stopwatch, startedAt, "MALFORMED_JSON", attempt);
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
                        RetryCount = attempt,
                        StartedAt = startedAt,
                        CompletedAt = DateTime.UtcNow
                    };
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Behavioural AI JSON Deserialization failed: {Error}", ex.Message);
                    return Failure<T>(stopwatch, startedAt, "MALFORMED_JSON", attempt);
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
            }

            return Failure<T>(stopwatch, startedAt, "RETRY_EXHAUSTED", _options.MaxRetries);
        }

        private BehaviouralAIProviderResult<T> Failure<T>(
            Stopwatch stopwatch,
            DateTime startedAt,
            string errorCode,
            int retryCount)
        {
            stopwatch.Stop();
            return new BehaviouralAIProviderResult<T>
            {
                Success = false,
                Model = _options.Model,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                ErrorCode = errorCode,
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

        private static string StripMarkdownFence(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;
            var trimmed = content.Trim();

            var firstBrace = trimmed.IndexOf('{');
            var lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return trimmed.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
            }

            return trimmed;
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
