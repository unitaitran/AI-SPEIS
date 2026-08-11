using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ai_speis_be.AI.Json
{
    public sealed class AiJsonRecoveryService : IAiJsonRecoveryService
    {
        private const int MaximumStoredResponseLength = 8_000;
        private static readonly Regex SensitiveFieldPattern = new(
            "(?i)(\\\"?(?:api[_-]?key|authorization|token|password)\\\"?\\s*[:=]\\s*\\\")[^\\\"]+(\\\")",
            RegexOptions.Compiled);

        public AiJsonRecoveryResult<T> Deserialize<T>(string rawContent, JsonSerializerOptions strictOptions)
        {
            var flags = new List<string>();
            var content = RemoveBom(rawContent ?? string.Empty, flags);
            content = RemoveMarkdownFence(content, flags);

            if (TryDeserialize(content, strictOptions, out T? parsed, out var strictError))
            {
                return Success(parsed, flags, "NONE", null);
            }

            if (!TryExtractBalancedObject(content, out var jsonObject, out var hasLeadingText, out var hasTrailingText))
            {
                return Failure<T>(flags, strictError);
            }

            if (hasLeadingText)
            {
                flags.Add("JSON_RECOVERED_LEADING_TEXT");
            }
            if (hasTrailingText)
            {
                flags.Add("JSON_RECOVERED_TRAILING_TEXT");
            }

            if (TryDeserialize(jsonObject, strictOptions, out parsed, out var extractedError))
            {
                return Success(parsed, flags, "RECOVERED", null);
            }

            var tolerantOptions = new JsonSerializerOptions(strictOptions)
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            if (TryDeserialize(jsonObject, tolerantOptions, out parsed, out var tolerantError))
            {
                flags.Add("JSON_RECOVERED_TOLERANT_PARSE");
                return Success(parsed, flags, "RECOVERED", null);
            }

            return Failure<T>(flags, tolerantError ?? extractedError ?? strictError);
        }

        public string CreateSafeRawResponse(string? rawContent)
        {
            if (string.IsNullOrEmpty(rawContent)) return string.Empty;

            var redacted = SensitiveFieldPattern.Replace(rawContent, "$1[REDACTED]$2");
            return redacted.Length <= MaximumStoredResponseLength
                ? redacted
                : redacted[..MaximumStoredResponseLength] + "[TRUNCATED]";
        }

        private static string RemoveBom(string content, ICollection<string> flags)
        {
            var result = content.TrimStart('\uFEFF');
            if (result.Length != content.Length)
            {
                flags.Add("JSON_RECOVERED_BOM");
            }
            return result.Trim();
        }

        private static string RemoveMarkdownFence(string content, ICollection<string> flags)
        {
            var opening = content.IndexOf("```");
            if (opening < 0) return content;

            var openingLineEnd = content.IndexOf('\n', opening);
            if (openingLineEnd < 0) return content;
            var closing = content.IndexOf("```", openingLineEnd + 1, StringComparison.Ordinal);
            if (closing < 0) return content;

            var before = content[..opening];
            var body = content[(openingLineEnd + 1)..closing];
            var after = content[(closing + 3)..];
            flags.Add("JSON_RECOVERED_FENCE");
            return string.Concat(before, "\n", body, "\n", after).Trim();
        }

        private static bool TryDeserialize<T>(
            string content,
            JsonSerializerOptions options,
            out T? parsed,
            out Exception? error)
        {
            try
            {
                parsed = JsonSerializer.Deserialize<T>(content, options);
                error = null;
                return parsed is not null;
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
            {
                parsed = default;
                error = exception;
                return false;
            }
        }

        private static bool TryExtractBalancedObject(
            string content,
            out string jsonObject,
            out bool hasLeadingText,
            out bool hasTrailingText)
        {
            jsonObject = string.Empty;
            hasLeadingText = false;
            hasTrailingText = false;
            var start = content.IndexOf('{');
            if (start < 0) return false;

            var depth = 0;
            var inString = false;
            var escaped = false;
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

                if (character == '"') inString = true;
                else if (character == '{') depth++;
                else if (character == '}')
                {
                    depth--;
                    if (depth < 0) return false;
                    if (depth != 0) continue;

                    jsonObject = content[start..(index + 1)];
                    hasLeadingText = !string.IsNullOrWhiteSpace(content[..start]);
                    hasTrailingText = !string.IsNullOrWhiteSpace(content[(index + 1)..]);
                    return true;
                }
            }

            return false;
        }

        private static AiJsonRecoveryResult<T> Success<T>(
            T? data,
            IReadOnlyList<string> flags,
            string status,
            Exception? error)
        {
            return new AiJsonRecoveryResult<T>
            {
                Success = data is not null,
                Data = data,
                Metadata = CreateMetadata(status, flags, error)
            };
        }

        private static AiJsonRecoveryResult<T> Failure<T>(IReadOnlyList<string> flags, Exception? error)
        {
            return new AiJsonRecoveryResult<T>
            {
                Success = false,
                Metadata = CreateMetadata("UNRECOVERABLE", flags, error)
            };
        }

        private static AiJsonRecoveryMetadata CreateMetadata(
            string status,
            IReadOnlyList<string> flags,
            Exception? error)
        {
            return new AiJsonRecoveryMetadata
            {
                RecoveryStatus = status,
                RecoveryFlags = flags.Distinct(StringComparer.Ordinal).ToArray(),
                ExceptionType = error?.GetType().Name,
                JsonErrorPath = (error as JsonException)?.Path,
                JsonErrorOffset = (error as JsonException)?.BytePositionInLine
            };
        }
    }
}
