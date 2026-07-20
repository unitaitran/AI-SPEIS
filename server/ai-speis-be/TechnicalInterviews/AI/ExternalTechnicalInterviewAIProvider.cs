using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ai_speis_be.TechnicalInterviews.Configuration;

namespace ai_speis_be.TechnicalInterviews.AI
{
    public sealed class ExternalTechnicalInterviewAIProvider : ITechnicalInterviewAIProvider
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TechnicalInterviewOptions _options;
        private readonly ILogger<ExternalTechnicalInterviewAIProvider> _logger;

        public ExternalTechnicalInterviewAIProvider(
            IHttpClientFactory httpClientFactory,
            TechnicalInterviewOptions options,
            ILogger<ExternalTechnicalInterviewAIProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _logger = logger;
        }

        public string ProviderName => "external";

        public Task<AIProviderResult<TechnicalAISelectionResponse>> SelectQuestionAsync(
            TechnicalAISelectionRequest request,
            CancellationToken cancellationToken)
        {
            var prompt = TechnicalPromptFactory.Selection(request);
            return CallAsync<TechnicalAISelectionResponse>(prompt.System, prompt.User, cancellationToken);
        }

        public Task<AIProviderResult<TechnicalAIEvaluationResponse>> EvaluateAnswerAsync(
            TechnicalAIEvaluationRequest request,
            CancellationToken cancellationToken)
        {
            var prompt = TechnicalPromptFactory.Evaluation(request);
            return CallAsync<TechnicalAIEvaluationResponse>(prompt.System, prompt.User, cancellationToken);
        }

        public Task<AIProviderResult<TechnicalAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            TechnicalAIFinalSummaryRequest request,
            CancellationToken cancellationToken)
        {
            var prompt = TechnicalPromptFactory.Summary(request);
            return CallAsync<TechnicalAIFinalSummaryResponse>(prompt.System, prompt.User, cancellationToken);
        }

        private async Task<AIProviderResult<T>> CallAsync<T>(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                return Failure<T>(stopwatch, "CONFIGURATION_MISSING");
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

                    var client = _httpClientFactory.CreateClient("TechnicalInterviewAI");
                    using var response = await client.SendAsync(request, cancellationToken);
                    if (ShouldRetry(response.StatusCode) && attempt < _options.MaxRetries)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "Technical Interview AI returned HTTP {StatusCode}.",
                            (int)response.StatusCode);
                        return Failure<T>(stopwatch, $"HTTP_{(int)response.StatusCode}");
                    }

                    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    var envelope = JsonSerializer.Deserialize<ChatCompletionEnvelope>(responseJson, JsonOptions);
                    var content = envelope?.Choices.FirstOrDefault()?.Message.Content;
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        return Failure<T>(stopwatch, "EMPTY_RESPONSE");
                    }

                    var parsed = JsonSerializer.Deserialize<T>(StripMarkdownFence(content), JsonOptions);
                    if (parsed is null)
                    {
                        return Failure<T>(stopwatch, "MALFORMED_JSON");
                    }

                    stopwatch.Stop();
                    return new AIProviderResult<T>
                    {
                        Success = true,
                        Data = parsed,
                        Model = envelope?.Model ?? _options.Model,
                        LatencyMs = stopwatch.ElapsedMilliseconds,
                        InputTokens = envelope?.Usage?.PromptTokens,
                        OutputTokens = envelope?.Usage?.CompletionTokens
                    };
                }
                catch (JsonException)
                {
                    return Failure<T>(stopwatch, "MALFORMED_JSON");
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return Failure<T>(stopwatch, "TIMEOUT");
                }
                catch (HttpRequestException exception) when (attempt < _options.MaxRetries)
                {
                    _logger.LogWarning(exception, "Transient Technical Interview AI request failure.");
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
                }
                catch (HttpRequestException exception)
                {
                    _logger.LogWarning(exception, "Technical Interview AI request failed.");
                    return Failure<T>(stopwatch, "NETWORK_ERROR");
                }
            }

            return Failure<T>(stopwatch, "RETRY_EXHAUSTED");
        }

        private AIProviderResult<T> Failure<T>(Stopwatch stopwatch, string errorCode)
        {
            stopwatch.Stop();
            return new AIProviderResult<T>
            {
                Success = false,
                Model = _options.Model,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                ErrorCode = errorCode
            };
        }

        private static bool ShouldRetry(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
        }

        private static string StripMarkdownFence(string content)
        {
            var trimmed = content.Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                return trimmed;
            }

            var firstLineEnd = trimmed.IndexOf('\n');
            var withoutOpening = firstLineEnd >= 0 ? trimmed[(firstLineEnd + 1)..] : trimmed[3..];
            return withoutOpening.EndsWith("```", StringComparison.Ordinal)
                ? withoutOpening[..^3].Trim()
                : withoutOpening.Trim();
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
