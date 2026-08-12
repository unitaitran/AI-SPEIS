using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        private static readonly JsonSerializerOptions V2JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
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
            => SelectQuestionsAsync(request, cancellationToken, null);

        public Task<AIProviderResult<TechnicalAISelectionResponse>> SelectQuestionsAsync(
            TechnicalAISelectionRequest request,
            CancellationToken cancellationToken,
            string? providerOverride)
        {
            var prompt = TechnicalPromptFactory.Selection(request);
            return CallAsync<TechnicalAISelectionResponse>(
                prompt.System,
                prompt.User,
                _options.TimeoutSeconds * 1_000,
                _options.MaxRetries,
                null,
                cancellationToken,
                null,
                providerOverride);
        }

        public Task<AIProviderResult<TechnicalV2EvaluationResponse>> EvaluateAnswerV2Async(
            TechnicalV2AnswerProcessingContext context,
            CancellationToken cancellationToken)
            => EvaluateAnswerV2Async(context, cancellationToken, null);

        public Task<AIProviderResult<TechnicalV2EvaluationResponse>> EvaluateAnswerV2Async(
            TechnicalV2AnswerProcessingContext context,
            CancellationToken cancellationToken,
            string? providerOverride)
        {
            var prompt = TechnicalPromptFactory.EvaluationV2(context, providerOverride);
            return CallAsync<TechnicalV2EvaluationResponse>(
                prompt.System,
                prompt.User,
                _options.EvaluationTimeoutMs,
                _options.EvaluationMaxRetries,
                _options.OllamaEvaluationModel,
                cancellationToken,
                V2JsonOptions,
                providerOverride);
        }

        public Task<AIProviderResult<TechnicalAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            TechnicalAIFinalSummaryRequest request,
            CancellationToken cancellationToken)
            => GenerateFinalSummaryAsync(request, cancellationToken, null);

        public Task<AIProviderResult<TechnicalAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            TechnicalAIFinalSummaryRequest request,
            CancellationToken cancellationToken,
            string? providerOverride)
        {
            var prompt = TechnicalPromptFactory.Summary(request);
            return CallAsync<TechnicalAIFinalSummaryResponse>(
                prompt.System,
                prompt.User,
                _options.TimeoutSeconds * 1_000,
                _options.MaxRetries,
                null,
                cancellationToken,
                null,
                providerOverride);
        }

        private async Task<AIProviderResult<T>> CallAsync<T>(
            string systemPrompt,
            string userPrompt,
            int timeoutMs,
            int maxRetries,
            string? ollamaModelOverride,
            CancellationToken cancellationToken,
            JsonSerializerOptions? responseOptions = null,
            string? providerOverride = null)
        {
            var startedAt = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            var effectiveProvider = string.IsNullOrWhiteSpace(providerOverride)
                ? _options.Provider
                : providerOverride;
            var isOllama = string.Equals(effectiveProvider, "ollama", StringComparison.OrdinalIgnoreCase)
                || string.Equals(effectiveProvider, "local", StringComparison.OrdinalIgnoreCase);
            var useNativeOllamaV2 = isOllama && typeof(T) == typeof(TechnicalV2EvaluationResponse);
            var useStructuredGeminiV2 = !isOllama && typeof(T) == typeof(TechnicalV2EvaluationResponse);
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
                return Failure<T>(stopwatch, startedAt, "CONFIGURATION_MISSING", 0, model: model);
            }

            using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            operationCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
            var operationToken = operationCts.Token;

            var payload = useNativeOllamaV2
                ? (object)new
                {
                    model = model,
                    stream = false,
                    // /api/chat enforces this schema at decode time. The OpenAI
                    // compatibility endpoint only requested JSON mode, which
                    // aispeis could satisfy with syntactically malformed arrays.
                    format = CreateTechnicalV2EvaluationSchema(),
                    options = new { temperature = 0.0 },
                    messages = new object[]
                    {
                        new { role = "system", content = AppendOllamaV2ContractHints(systemPrompt) },
                        new { role = "user", content = userPrompt }
                    }
                }
                : isOllama
                ? (object)new
                {
                    model = model,
                    // aispeis is more deterministic at zero temperature; the
                    // Ollama-only recovery below handles residual shape drift.
                    temperature = 0.0,
                    format = "json",
                    response_format = new { type = "json_object" },
                    messages = new object[]
                    {
                        new
                        {
                            role = "system",
                            content = isOllama && typeof(T) == typeof(TechnicalV2EvaluationResponse)
                                ? AppendOllamaV2ContractHints(systemPrompt)
                                : systemPrompt
                        },
                        new { role = "user", content = userPrompt }
                    }
                }
                : useStructuredGeminiV2
                ? (object)new
                {
                    model = model,
                    temperature = 0.1,
                    response_format = new
                    {
                        type = "json_schema",
                        json_schema = new
                        {
                            name = "technical_v2_evaluation",
                            strict = true,
                            schema = CreateTechnicalV2EvaluationSchema()
                        }
                    },
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
                    var endpoint = useNativeOllamaV2
                        ? new Uri(new Uri(baseUrl), "/api/chat")
                        : new Uri(new Uri(baseUrl), "chat/completions");
                    using var request = new HttpRequestMessage(
                        HttpMethod.Post,
                        endpoint);
                    if (!isOllama && !string.IsNullOrWhiteSpace(_options.ApiKey))
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
                        endpoint,
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
                        var errorBody = await response.Content.ReadAsStringAsync(operationToken);
                        if (isOllama)
                        {
                            _logger.LogWarning(
                                "Technical Interview AI returned HTTP {StatusCode}. Provider: ollama, ContentType: {ContentType}, ResponseLength: {ResponseLength}, Preview: {Preview}",
                                (int)response.StatusCode,
                                response.Content.Headers.ContentType?.ToString() ?? string.Empty,
                                errorBody.Length,
                                CreateSafeLogPreview(errorBody));
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Technical Interview AI returned HTTP {StatusCode}.",
                                (int)response.StatusCode);
                        }
                        return Failure<T>(
                            stopwatch,
                            startedAt,
                            MapHttpError(response.StatusCode),
                            attempt,
                            model: model);
                    }

                    var responseJson = await response.Content.ReadAsStringAsync(operationToken);
                    if (isOllama)
                    {
                        _logger.LogInformation(
                            "[AI-CALL] Technical AI Response | Provider: ollama | HTTP: {StatusCode} | ContentType: {ContentType} | ResponseLength: {ResponseLength} | Preview: {Preview}",
                            (int)response.StatusCode,
                            response.Content.Headers.ContentType?.ToString() ?? string.Empty,
                            responseJson.Length,
                            CreateSafeLogPreview(responseJson));
                    }
                    string? content;
                    string? responseModel;
                    int? inputTokens;
                    int? outputTokens;
                    try
                    {
                        if (useNativeOllamaV2)
                        {
                            var envelope = JsonSerializer.Deserialize<OllamaNativeChatEnvelope>(responseJson, JsonOptions);
                            content = envelope?.Message?.Content;
                            responseModel = envelope?.Model;
                            inputTokens = envelope?.PromptEvaluationTokens;
                            outputTokens = envelope?.EvaluationTokens;
                        }
                        else
                        {
                            var envelope = JsonSerializer.Deserialize<ChatCompletionEnvelope>(responseJson, JsonOptions);
                            content = envelope?.Choices.FirstOrDefault()?.Message.Content;
                            responseModel = envelope?.Model;
                            inputTokens = envelope?.Usage?.PromptTokens;
                            outputTokens = envelope?.Usage?.CompletionTokens;
                        }
                    }
                    catch (JsonException exception)
                    {
                        return Failure<T>(
                            stopwatch,
                            startedAt,
                            "MALFORMED_PROVIDER_ENVELOPE",
                            attempt,
                            _jsonRecovery.CreateSafeRawResponse(responseJson),
                            new AiJsonRecoveryMetadata
                            {
                                RecoveryStatus = "UNRECOVERABLE",
                                ExceptionType = exception.GetType().Name,
                                JsonErrorPath = exception.Path,
                                JsonErrorOffset = exception.BytePositionInLine
                            },
                            model);
                    }
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        return Failure<T>(stopwatch, startedAt, "EMPTY_RESPONSE", attempt, model: model);
                    }

                    var recovery = _jsonRecovery.Deserialize<T>(content, responseOptions ?? JsonOptions);
                    if (!recovery.Success || recovery.Data is null)
                    {
                        if (isOllama
                            && typeof(T) == typeof(TechnicalV2EvaluationResponse)
                            && TryRecoverOllamaV2(
                                content,
                                responseOptions ?? JsonOptions,
                                out var ollamaEvaluation,
                                out var ollamaRecovery))
                        {
                            stopwatch.Stop();
                            return new AIProviderResult<T>
                            {
                                Success = true,
                                Data = (T)(object)ollamaEvaluation,
                                Model = responseModel ?? model,
                                LatencyMs = stopwatch.ElapsedMilliseconds,
                                InputTokens = inputTokens,
                                OutputTokens = outputTokens,
                                RawResponse = _jsonRecovery.CreateSafeRawResponse(content),
                                JsonRecovery = ollamaRecovery,
                                RetryCount = attempt,
                                StartedAt = startedAt,
                                CompletedAt = DateTime.UtcNow
                            };
                        }

                        var errorCode = typeof(T) == typeof(TechnicalV2EvaluationResponse)
                            && IsCompleteJsonObject(content)
                            ? "INVALID_V2_CONTRACT"
                            : "MALFORMED_JSON_UNRECOVERABLE";
                        var metadata = errorCode == "INVALID_V2_CONTRACT"
                            ? CreateContractMismatchMetadata(recovery.Metadata)
                            : recovery.Metadata;

                        return Failure<T>(
                            stopwatch,
                            startedAt,
                            errorCode,
                            attempt,
                            _jsonRecovery.CreateSafeRawResponse(content),
                            metadata,
                            model);
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
                        Model = responseModel ?? model,
                        LatencyMs = stopwatch.ElapsedMilliseconds,
                        InputTokens = inputTokens,
                        OutputTokens = outputTokens,
                        RawResponse = _jsonRecovery.CreateSafeRawResponse(content),
                        JsonRecovery = recovery.Metadata,
                        RetryCount = attempt,
                        StartedAt = startedAt,
                        CompletedAt = DateTime.UtcNow
                    };
                }
                catch (JsonException)
                {
                    return Failure<T>(stopwatch, startedAt, "MALFORMED_JSON", attempt, model: model);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return Failure<T>(stopwatch, startedAt, "TIMEOUT", attempt, model: model);
                }
                catch (HttpRequestException exception) when (attempt < maxRetries)
                {
                    _logger.LogWarning(exception, "Transient Technical Interview AI request failure.");
                    await BackoffAsync(attempt, operationToken);
                }
                catch (HttpRequestException exception)
                {
                    _logger.LogWarning(exception, "Technical Interview AI request failed.");
                    return Failure<T>(stopwatch, startedAt, "NETWORK_ERROR", attempt, model: model);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Technical Interview AI provider failed while processing response.");
                    return Failure<T>(stopwatch, startedAt, "PROVIDER_EXCEPTION", attempt, model: model);
                }
            }

            return Failure<T>(stopwatch, startedAt, "RETRY_EXHAUSTED", maxRetries, model: model);
        }

        private AIProviderResult<T> Failure<T>(
            Stopwatch stopwatch,
            DateTime startedAt,
            string errorCode,
            int retryCount,
            string? rawResponse = null,
            AiJsonRecoveryMetadata? jsonRecovery = null,
            string? model = null)
        {
            stopwatch.Stop();
            return new AIProviderResult<T>
            {
                Success = false,
                Model = model ?? _options.Model,
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

        private static object CreateTechnicalV2EvaluationSchema()
        {
            var criterion = new
            {
                type = "object",
                properties = new
                {
                    rubricCode = new
                    {
                        type = "string",
                        @enum = new[]
                        {
                            "ACCURACY",
                            "TECHNICAL_DEPTH",
                            "REASONING",
                            "APPLICATION",
                            "COMMUNICATION"
                        }
                    },
                    suggestedScore = new { type = "number", minimum = 0, maximum = 10 },
                    evidence = new { type = "array", items = new { type = "string" } },
                    missingEvidence = new { type = "array", items = new { type = "string" } }
                },
                required = new[] { "rubricCode", "suggestedScore", "evidence", "missingEvidence" },
                additionalProperties = false
            };

            return new
            {
                type = "object",
                properties = new
                {
                    evaluation = new
                    {
                        type = "object",
                        properties = new
                        {
                            dimensionEvaluations = new
                            {
                                type = "array",
                                minItems = 5,
                                maxItems = 5,
                                items = criterion
                            }
                        },
                        required = new[] { "dimensionEvaluations" },
                        additionalProperties = false
                    }
                },
                required = new[] { "evaluation" },
                additionalProperties = false
            };
        }

        private static string AppendOllamaV2ContractHints(string systemPrompt)
        {
            return systemPrompt + "\n"
                + "Ollama output rules: dimensionEvaluations must contain exactly five JSON objects, one for each exact rubricCode. "
                + "The evidence and missingEvidence values must always be JSON arrays of strings (use [] when empty), never scalar strings or objects. "
                + "Close every JSON object and array before returning the final character. Do not quote an object as a string.";
        }

        private string CreateSafeLogPreview(string? content)
        {
            var safe = _jsonRecovery.CreateSafeRawResponse(content);
            return safe.Length <= 600 ? safe : safe[..600] + "[TRUNCATED]";
        }

        private static bool TryRecoverOllamaV2(
            string rawContent,
            JsonSerializerOptions options,
            out TechnicalV2EvaluationResponse evaluation,
            out AiJsonRecoveryMetadata metadata)
        {
            evaluation = new TechnicalV2EvaluationResponse();
            var flags = new List<string>();
            var candidate = rawContent.Trim().TrimStart('\uFEFF');
            candidate = candidate.Replace("\"dimensionsEvaluations\"", "\"dimensionEvaluations\"", StringComparison.Ordinal);
            if (!string.Equals(candidate, rawContent.Trim().TrimStart('\uFEFF'), StringComparison.Ordinal))
            {
                flags.Add("OLLAMA_DIMENSION_KEY_NORMALIZED");
            }

            if (!TryExtractAndBalanceJsonObject(candidate, out candidate, out var braceRepaired))
            {
                metadata = FailureRecoveryMetadata(flags, null);
                return false;
            }

            if (braceRepaired) flags.Add("OLLAMA_JSON_BRACES_REPAIRED");

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(candidate);
            }
            catch (JsonException exception)
            {
                metadata = FailureRecoveryMetadata(flags, exception);
                return false;
            }

            if (root is not JsonObject rootObject
                || rootObject["evaluation"] is not JsonObject evaluationObject
                || evaluationObject["dimensionEvaluations"] is not JsonArray dimensions)
            {
                metadata = FailureRecoveryMetadata(flags, null);
                return false;
            }

            for (var index = 0; index < dimensions.Count; index++)
            {
                if (dimensions[index] is JsonValue stringValue
                    && stringValue.TryGetValue<string>(out var serializedDimension)
                    && TryParseDimensionObject(serializedDimension, out var parsedDimension))
                {
                    dimensions[index] = parsedDimension;
                    flags.Add("OLLAMA_STRINGIFIED_DIMENSION_UNWRAPPED");
                }
            }

            foreach (var dimension in dimensions.OfType<JsonObject>())
            {
                if (NormalizeStringArray(dimension, "evidence", flags, invalidShapeSetsScore: true))
                {
                    dimension["suggestedScore"] = null;
                }

                if (NormalizeStringArray(dimension, "missingEvidence", flags, invalidShapeSetsScore: true))
                {
                    dimension["suggestedScore"] = null;
                }
            }

            try
            {
                var normalizedJson = root.ToJsonString();
                evaluation = JsonSerializer.Deserialize<TechnicalV2EvaluationResponse>(normalizedJson, options)
                    ?? new TechnicalV2EvaluationResponse();
                if (evaluation.Evaluation?.DimensionEvaluations is null)
                {
                    metadata = FailureRecoveryMetadata(flags, null);
                    return false;
                }

                metadata = new AiJsonRecoveryMetadata
                {
                    RecoveryStatus = "RECOVERED",
                    RecoveryFlags = flags.Distinct(StringComparer.Ordinal).ToArray()
                };
                return true;
            }
            catch (JsonException exception)
            {
                metadata = FailureRecoveryMetadata(flags, exception);
                return false;
            }
        }

        private static bool TryParseDimensionObject(string content, out JsonObject? dimension)
        {
            dimension = null;
            try
            {
                dimension = JsonNode.Parse(content) as JsonObject;
                return dimension is not null;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool NormalizeStringArray(
            JsonObject dimension,
            string propertyName,
            ICollection<string> flags,
            bool invalidShapeSetsScore)
        {
            var node = dimension[propertyName];
            if (node is JsonArray array)
            {
                var normalized = new JsonArray();
                var changed = false;
                var invalidItem = false;
                foreach (var item in array)
                {
                    if (item is JsonValue stringValue
                        && stringValue.TryGetValue<string>(out var text))
                    {
                        normalized.Add(text);
                    }
                    else if (item is JsonObject objectValue
                        && objectValue["text"] is JsonValue textValue
                        && textValue.TryGetValue<string>(out var objectText))
                    {
                        // aispeis returns this exact observed form for a single
                        // missingEvidence item: { "text": "..." }.
                        normalized.Add(objectText);
                        changed = true;
                        flags.Add("OLLAMA_TEXT_OBJECT_ARRAY_ITEM_NORMALIZED");
                    }
                    else
                    {
                        changed = true;
                        invalidItem = true;
                    }
                }

                if (changed)
                {
                    dimension[propertyName] = normalized;
                }

                if (invalidItem && invalidShapeSetsScore)
                {
                    flags.Add("OLLAMA_INVALID_CRITERION_SHAPE");
                }

                return invalidItem && invalidShapeSetsScore;
            }

            if (node is JsonValue scalar && scalar.TryGetValue<string>(out var scalarText))
            {
                dimension[propertyName] = new JsonArray(JsonValue.Create(scalarText));
                flags.Add("OLLAMA_SCALAR_ARRAY_NORMALIZED");
                return false;
            }

            dimension[propertyName] = new JsonArray();
            if (invalidShapeSetsScore) flags.Add("OLLAMA_INVALID_CRITERION_SHAPE");
            return invalidShapeSetsScore;
        }

        private static bool IsCompleteJsonObject(string content)
        {
            var candidate = content.Trim().TrimStart('\uFEFF');
            try
            {
                return JsonNode.Parse(candidate) is JsonObject;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static AiJsonRecoveryMetadata CreateContractMismatchMetadata(AiJsonRecoveryMetadata source)
        {
            return new AiJsonRecoveryMetadata
            {
                RecoveryStatus = "CONTRACT_MISMATCH",
                RecoveryFlags = source.RecoveryFlags
                    .Append("V2_DTO_CONTRACT_MISMATCH")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                ExceptionType = source.ExceptionType,
                JsonErrorPath = source.JsonErrorPath,
                JsonErrorOffset = source.JsonErrorOffset
            };
        }

        private static bool TryExtractAndBalanceJsonObject(
            string content,
            out string candidate,
            out bool repaired)
        {
            candidate = string.Empty;
            repaired = false;
            var start = content.IndexOf('{');
            if (start < 0) return false;

            var closers = new Stack<char>();
            var inString = false;
            var escaped = false;
            var end = -1;
            for (var index = start; index < content.Length; index++)
            {
                var character = content[index];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (character == '\\') escaped = true;
                    else if (character == '"') inString = false;
                    continue;
                }

                if (character == '"')
                {
                    inString = true;
                    continue;
                }

                if (character == '{') closers.Push('}');
                else if (character == '[') closers.Push(']');
                else if (character is '}' or ']')
                {
                    if (closers.Count == 0 || closers.Pop() != character) return false;
                    if (closers.Count == 0)
                    {
                        end = index + 1;
                        break;
                    }
                }
            }

            if (inString) return false;
            if (end < 0)
            {
                if (closers.Count == 0) return false;
                var builder = new StringBuilder(content[start..]);
                foreach (var closer in closers) builder.Append(closer);
                candidate = builder.ToString();
                repaired = true;
                return true;
            }

            candidate = content[start..end];
            return true;
        }

        private static AiJsonRecoveryMetadata FailureRecoveryMetadata(
            IReadOnlyCollection<string> flags,
            JsonException? exception)
        {
            return new AiJsonRecoveryMetadata
            {
                RecoveryStatus = "UNRECOVERABLE",
                RecoveryFlags = flags.Distinct(StringComparer.Ordinal).ToArray(),
                ExceptionType = exception?.GetType().Name,
                JsonErrorPath = exception?.Path,
                JsonErrorOffset = exception?.BytePositionInLine
            };
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

        private sealed class OllamaNativeChatEnvelope
        {
            public string? Model { get; set; }
            public ChatMessage? Message { get; set; }

            [JsonPropertyName("prompt_eval_count")]
            public int? PromptEvaluationTokens { get; set; }

            [JsonPropertyName("eval_count")]
            public int? EvaluationTokens { get; set; }
        }
    }
}
