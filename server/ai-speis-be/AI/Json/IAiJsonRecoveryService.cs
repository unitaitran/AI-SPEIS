using System.Text.Json;

namespace ai_speis_be.AI.Json
{
    public sealed class AiJsonRecoveryMetadata
    {
        public string RecoveryStatus { get; init; } = "NONE";
        public IReadOnlyList<string> RecoveryFlags { get; init; } = Array.Empty<string>();
        public string? ExceptionType { get; init; }
        public string? JsonErrorPath { get; init; }
        public long? JsonErrorOffset { get; init; }
    }

    public sealed class AiJsonRecoveryResult<T>
    {
        public bool Success { get; init; }
        public T? Data { get; init; }
        public AiJsonRecoveryMetadata Metadata { get; init; } = new();
    }

    public interface IAiJsonRecoveryService
    {
        AiJsonRecoveryResult<T> Deserialize<T>(string rawContent, JsonSerializerOptions strictOptions);
        string CreateSafeRawResponse(string? rawContent);
    }
}
