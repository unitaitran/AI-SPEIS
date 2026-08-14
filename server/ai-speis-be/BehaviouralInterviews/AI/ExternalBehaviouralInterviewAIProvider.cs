using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
            => SelectQuestionsAsync(request, cancellationToken, null);

        public Task<BehaviouralAIProviderResult<BehaviouralAISelectionResponse>> SelectQuestionsAsync(
            BehaviouralAISelectionRequest request,
            CancellationToken cancellationToken,
            string? providerOverride)
        {
            var prompt = BehaviouralPromptFactory.Selection(request);
            return CallAsync<BehaviouralAISelectionResponse>(prompt.System, prompt.User, cancellationToken, providerOverride);
        }

        public Task<BehaviouralAIProviderResult<BehaviouralAIEvaluationResponse>> EvaluateAnswerAsync(
            BehaviouralAIEvaluationRequest request,
            CancellationToken cancellationToken)
            => EvaluateAnswerAsync(request, cancellationToken, null);

        public Task<BehaviouralAIProviderResult<BehaviouralAIEvaluationResponse>> EvaluateAnswerAsync(
            BehaviouralAIEvaluationRequest request,
            CancellationToken cancellationToken,
            string? providerOverride)
        {
            var targetProvider = providerOverride ?? _options.Provider;
            var prompt = BehaviouralPromptFactory.Evaluation(request, targetProvider);
            return CallAsync<BehaviouralAIEvaluationResponse>(prompt.System, prompt.User, cancellationToken, providerOverride);
        }

        public Task<BehaviouralAIProviderResult<BehaviouralAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            BehaviouralAIFinalSummaryRequest request,
            CancellationToken cancellationToken)
            => GenerateFinalSummaryAsync(request, cancellationToken, null);

        public Task<BehaviouralAIProviderResult<BehaviouralAIFinalSummaryResponse>> GenerateFinalSummaryAsync(
            BehaviouralAIFinalSummaryRequest request,
            CancellationToken cancellationToken,
            string? providerOverride)
        {
            var prompt = BehaviouralPromptFactory.Summary(request);
            return CallAsync<BehaviouralAIFinalSummaryResponse>(prompt.System, prompt.User, cancellationToken, providerOverride);
        }

        private async Task<BehaviouralAIProviderResult<T>> CallAsync<T>(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken,
            string? providerOverride)
        {
            var startedAt = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            var effectiveProvider = string.IsNullOrWhiteSpace(providerOverride)
                ? _options.Provider
                : providerOverride;
            var isOllama = string.Equals(effectiveProvider, "ollama", StringComparison.OrdinalIgnoreCase)
                || string.Equals(effectiveProvider, "local", StringComparison.OrdinalIgnoreCase);
            var providerName = isOllama ? "ollama" : "gemini";
            var useStructuredGeminiEvaluation = !isOllama && typeof(T) == typeof(BehaviouralAIEvaluationResponse);
            var baseUrl = isOllama ? _options.OllamaBaseUrl : _options.BaseUrl;
            var model = isOllama
                ? typeof(T) == typeof(BehaviouralAIEvaluationResponse)
                    && !string.IsNullOrWhiteSpace(_options.OllamaEvaluationModel)
                    ? _options.OllamaEvaluationModel
                    : !string.IsNullOrWhiteSpace(_options.OllamaModel) ? _options.OllamaModel : _options.Model
                : _options.Model;

            if (!isOllama && string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                return Failure<T>(stopwatch, startedAt, "CONFIGURATION_MISSING", 0, providerName: providerName);
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
                : useStructuredGeminiEvaluation
                ? (object)new
                {
                    model = model,
                    temperature = 0.1,
                    response_format = new
                    {
                        type = "json_schema",
                        json_schema = new
                        {
                            name = "behavioural_evaluation",
                            strict = true,
                            schema = CreateBehaviouralEvaluationSchema()
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
                        return Failure<T>(stopwatch, startedAt, MapHttpError(response.StatusCode), attempt, providerName: providerName, model: model);
                    }

                    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    var envelope = JsonSerializer.Deserialize<ChatCompletionEnvelope>(responseJson, JsonOptions);
                    var content = envelope?.Choices.FirstOrDefault()?.Message.Content;
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        return Failure<T>(stopwatch, startedAt, "EMPTY_RESPONSE", attempt, providerName: providerName, model: model);
                    }

                    var recovery = _jsonRecovery.Deserialize<T>(content, JsonOptions);
                    T? parsed = recovery.Success ? recovery.Data : default;
                    AiJsonRecoveryMetadata? finalMetadata = recovery.Metadata;
                    var requiresBehaviouralRecovery = parsed is BehaviouralAIEvaluationResponse behaviouralEvaluation
                        && (behaviouralEvaluation.DimensionEvaluations.Count == 0
                            || behaviouralEvaluation.DimensionEvaluations.Any(dimension => dimension.SuggestedScore is null));

                    if ((!recovery.Success || parsed is null || requiresBehaviouralRecovery)
                        && typeof(T) == typeof(BehaviouralAIEvaluationResponse))
                    {
                        if (TryRecoverBehaviouralEvaluation(content, JsonOptions, out var recoveredEval, out var recoveredMetadata))
                        {
                            parsed = (T)(object)recoveredEval;
                            finalMetadata = recoveredMetadata;
                        }
                    }

                    if (parsed is null
                        || parsed is BehaviouralAIEvaluationResponse { DimensionEvaluations.Count: 0 })
                    {
                        var errorCode = typeof(T) == typeof(BehaviouralAIEvaluationResponse)
                            && IsCompleteJsonObject(content)
                            ? "INVALID_BEHAVIOURAL_CONTRACT"
                            : "MALFORMED_JSON_UNRECOVERABLE";
                        var metadata = errorCode == "INVALID_BEHAVIOURAL_CONTRACT"
                            ? CreateContractMismatchMetadata(finalMetadata ?? recovery.Metadata)
                            : finalMetadata;
                        return Failure<T>(stopwatch, startedAt, errorCode, attempt,
                            _jsonRecovery.CreateSafeRawResponse(content), metadata, providerName: providerName, model: model);
                    }

                    if (parsed is BehaviouralAIEvaluationResponse evalResponse)
                    {
                        NormalizeBehaviouralRubricCodes(evalResponse);
                    }

                    stopwatch.Stop();
                    return new BehaviouralAIProviderResult<T>
                    {
                        Success = true,
                        Data = parsed,
                        ProviderName = providerName,
                        Model = envelope?.Model ?? model,
                        LatencyMs = stopwatch.ElapsedMilliseconds,
                        InputTokens = envelope?.Usage?.PromptTokens,
                        OutputTokens = envelope?.Usage?.CompletionTokens,
                        RawResponse = _jsonRecovery.CreateSafeRawResponse(content),
                        JsonRecovery = finalMetadata,
                        RetryCount = attempt,
                        StartedAt = startedAt,
                        CompletedAt = DateTime.UtcNow
                    };
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Behavioural AI JSON Deserialization failed: {Error}", ex.Message);
                    return Failure<T>(stopwatch, startedAt, "MALFORMED_JSON_UNRECOVERABLE", attempt, providerName: providerName);
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return Failure<T>(stopwatch, startedAt, "TIMEOUT", attempt, providerName: providerName);
                }
                catch (HttpRequestException exception) when (attempt < _options.MaxRetries)
                {
                    _logger.LogWarning(exception, "Transient Behavioural Interview AI request failure.");
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
                }
                catch (HttpRequestException exception)
                {
                    _logger.LogWarning(exception, "Behavioural Interview AI request failed.");
                    return Failure<T>(stopwatch, startedAt, "NETWORK_ERROR", attempt, providerName: providerName);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Behavioural Interview AI provider failed while processing response.");
                    return Failure<T>(stopwatch, startedAt, "PROVIDER_EXCEPTION", attempt, providerName: providerName);
                }
            }

            return Failure<T>(stopwatch, startedAt, "RETRY_EXHAUSTED", _options.MaxRetries, providerName: providerName);
        }

        private static bool TryRecoverBehaviouralEvaluation(
            string rawContent,
            JsonSerializerOptions options,
            out BehaviouralAIEvaluationResponse evaluation,
            out AiJsonRecoveryMetadata metadata)
        {
            evaluation = new BehaviouralAIEvaluationResponse();
            var flags = new List<string>();
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                metadata = FailureRecoveryMetadata(flags, null);
                return false;
            }

            var candidate = rawContent.Trim().TrimStart('\uFEFF');

            // Strip Markdown code fences if present (e.g. ```json ... ```)
            if (candidate.Contains("```"))
            {
                var fenceStart = candidate.IndexOf("```", StringComparison.Ordinal);
                var lineEnd = candidate.IndexOf('\n', fenceStart);
                var fenceEnd = candidate.LastIndexOf("```", StringComparison.Ordinal);
                if (fenceStart >= 0 && fenceEnd > fenceStart)
                {
                    if (lineEnd > fenceStart && lineEnd < fenceEnd)
                        candidate = candidate[(lineEnd + 1)..fenceEnd].Trim();
                    else
                        candidate = candidate[(fenceStart + 3)..fenceEnd].Trim();
                    flags.Add("BEHAVIOURAL_JSON_FENCE_STRIPPED");
                }
            }

            var originalCandidate = candidate;
            candidate = candidate
                .Replace("\"dimensionsEvaluations\"", "\"dimensionEvaluations\"", StringComparison.Ordinal)
                .Replace("\"dimension_evaluations\"", "\"dimensionEvaluations\"", StringComparison.Ordinal)
                .Replace("\"dimensions\"", "\"dimensionEvaluations\"", StringComparison.Ordinal)
                .Replace("\"evaluations\"", "\"dimensionEvaluations\"", StringComparison.Ordinal);

            if (!string.Equals(candidate, originalCandidate, StringComparison.Ordinal))
            {
                flags.Add("BEHAVIOURAL_DIMENSION_KEY_NORMALIZED");
            }

            if (!TryExtractAndBalanceJsonObject(candidate, out candidate, out var braceRepaired))
            {
                metadata = FailureRecoveryMetadata(flags, null);
                return false;
            }

            if (braceRepaired) flags.Add("BEHAVIOURAL_JSON_BRACES_REPAIRED");

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

            // If top-level JSON array [ { "rubricCode": ... }, ... ], wrap inside { "dimensionEvaluations": [ ... ] }
            if (root is JsonArray rootArray)
            {
                root = new JsonObject
                {
                    ["dimensionEvaluations"] = rootArray.DeepClone()
                };
                flags.Add("BEHAVIOURAL_TOP_LEVEL_ARRAY_WRAPPED");
            }

            if (root is JsonObject rootObject)
            {
                JsonArray? dimensions = null;
                if (rootObject["dimensionEvaluations"] is JsonArray directDims)
                {
                    dimensions = directDims;
                }
                else if (rootObject["evaluation"] is JsonObject evalObj && evalObj["dimensionEvaluations"] is JsonArray subArr)
                {
                    dimensions = subArr;
                    flags.Add("BEHAVIOURAL_EVALUATION_OBJECT_UNWRAPPED");
                }
                else if (rootObject["evaluation"] is JsonArray evalArr)
                {
                    dimensions = evalArr;
                    flags.Add("BEHAVIOURAL_EVALUATION_ARRAY_WRAPPED");
                }

                if (dimensions != null)
                {
                    for (var index = 0; index < dimensions.Count; index++)
                    {
                        if (dimensions[index] is JsonValue stringValue
                            && stringValue.TryGetValue<string>(out var serializedDimension)
                            && TryParseDimensionObject(serializedDimension, out var parsedDimension))
                        {
                            dimensions[index] = parsedDimension;
                            flags.Add("BEHAVIOURAL_STRINGIFIED_DIMENSION_UNWRAPPED");
                        }
                    }

                    foreach (var dimension in dimensions.OfType<JsonObject>())
                    {
                        NormalizeStringArray(dimension, "evidence", flags, invalidShapeSetsScore: false);
                        NormalizeStringArray(dimension, "missingEvidence", flags, invalidShapeSetsScore: false);

                        // Ensure suggestedScore is numeric decimal if model returned it as a string ("8.5")
                        if (dimension["suggestedScore"] is JsonValue scoreVal
                            && scoreVal.TryGetValue<string>(out var scoreStr)
                            && decimal.TryParse(scoreStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedScore))
                        {
                            dimension["suggestedScore"] = parsedScore;
                        }
                    }

                    var normalizedRoot = new JsonObject
                    {
                        ["dimensionEvaluations"] = dimensions.DeepClone()
                    };

                    try
                    {
                        var normalizedJson = normalizedRoot.ToJsonString();
                        evaluation = JsonSerializer.Deserialize<BehaviouralAIEvaluationResponse>(normalizedJson, options)
                            ?? new BehaviouralAIEvaluationResponse();
                        if (evaluation.DimensionEvaluations is not null && evaluation.DimensionEvaluations.Count > 0)
                        {
                            NormalizeBehaviouralRubricCodes(evaluation);
                            metadata = new AiJsonRecoveryMetadata
                            {
                                RecoveryStatus = "RECOVERED",
                                RecoveryFlags = flags.Distinct(StringComparer.Ordinal).ToArray()
                            };
                            return true;
                        }
                    }
                    catch (JsonException exception)
                    {
                        metadata = FailureRecoveryMetadata(flags, exception);
                        return false;
                    }
                }
            }

            metadata = FailureRecoveryMetadata(flags, null);
            return false;
        }

        private static bool TryExtractAndBalanceJsonObject(
            string content,
            out string candidate,
            out bool repaired)
        {
            candidate = string.Empty;
            repaired = false;
            if (string.IsNullOrWhiteSpace(content)) return false;

            var braceStart = content.IndexOf('{');
            var bracketStart = content.IndexOf('[');

            int start;
            if (braceStart < 0 && bracketStart < 0) return false;
            if (braceStart >= 0 && bracketStart >= 0)
                start = Math.Min(braceStart, bracketStart);
            else if (braceStart >= 0)
                start = braceStart;
            else
                start = bracketStart;

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

        private static void NormalizeDimension(JsonObject dimension, ICollection<string> flags)
        {
            NormalizePropertyName(dimension, "rubricCode", new[] { "code", "criterionCode", "criterion" }, flags);
            NormalizePropertyName(dimension, "suggestedScore", new[] { "score", "suggested_score" }, flags);
            NormalizeStringArray(dimension, "evidence", flags, invalidShapeSetsScore: false);
            NormalizeStringArray(dimension, "missingEvidence", flags, invalidShapeSetsScore: false);
        }

        private static void NormalizePropertyName(
            JsonObject dimension,
            string targetProperty,
            IReadOnlyList<string> aliases,
            ICollection<string> flags)
        {
            if (dimension[targetProperty] is not null)
            {
                return;
            }

            foreach (var alias in aliases)
            {
                if (dimension[alias] is null)
                {
                    continue;
                }

                dimension[targetProperty] = dimension[alias]!.DeepClone();
                flags.Add($"{targetProperty.ToUpperInvariant()}_NORMALIZED");
                return;
            }
        }

        private static bool NormalizeStringArray(
            JsonObject dimension,
            string propertyName,
            ICollection<string> flags,
            bool invalidShapeSetsScore = false)
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
                        normalized.Add(objectText);
                        changed = true;
                        flags.Add("BEHAVIOURAL_TEXT_OBJECT_ARRAY_ITEM_NORMALIZED");
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
                    flags.Add("BEHAVIOURAL_INVALID_CRITERION_SHAPE");
                }

                return invalidItem && invalidShapeSetsScore;
            }

            if (node is JsonValue scalar && scalar.TryGetValue<string>(out var scalarText))
            {
                dimension[propertyName] = new JsonArray(JsonValue.Create(scalarText));
                flags.Add("BEHAVIOURAL_SCALAR_ARRAY_NORMALIZED");
                return false;
            }

            dimension[propertyName] = new JsonArray();
            if (invalidShapeSetsScore) flags.Add("BEHAVIOURAL_INVALID_CRITERION_SHAPE");
            return invalidShapeSetsScore;
        }

        private static bool IsCompleteJsonObject(string content)
        {
            try
            {
                return JsonNode.Parse(content.Trim().TrimStart('\uFEFF')) is JsonObject;
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
                    .Append("BEHAVIOURAL_DTO_CONTRACT_MISMATCH")
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                ExceptionType = source.ExceptionType,
                JsonErrorPath = source.JsonErrorPath,
                JsonErrorOffset = source.JsonErrorOffset
            };
        }

        private static object CreateBehaviouralEvaluationSchema()
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
                            "SITUATION_TASK",
                            "ACTION",
                            "RESULT",
                            "COMPETENCY",
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
            };
        }

        private BehaviouralAIProviderResult<T> Failure<T>(
            Stopwatch stopwatch,
            DateTime startedAt,
            string errorCode,
            int retryCount,
            string? rawResponse = null,
            AiJsonRecoveryMetadata? jsonRecovery = null,
            string? providerName = null,
            string? model = null)
        {
            stopwatch.Stop();
            var isOllama = string.Equals(providerName, "ollama", StringComparison.OrdinalIgnoreCase)
                || string.Equals(providerName, "local", StringComparison.OrdinalIgnoreCase)
                || string.Equals(providerName, "aispeis", StringComparison.OrdinalIgnoreCase);
            var effectiveModel = model ?? (isOllama
                ? (!string.IsNullOrWhiteSpace(_options.OllamaModel) ? _options.OllamaModel : "aispeis")
                : _options.Model);

            return new BehaviouralAIProviderResult<T>
            {
                Success = false,
                ProviderName = providerName ?? _options.Provider,
                Model = effectiveModel,
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
