using Google.Cloud.Speech.V1;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ai_speis_be.Services.SpeechToTextService
{
    public class SpeechToTextService : ISpeechToTextService
    {
        private static readonly Dictionary<string, RecognitionConfig.Types.AudioEncoding> ContentTypeToEncoding = new(StringComparer.OrdinalIgnoreCase)
        {
            ["audio/webm"]          = RecognitionConfig.Types.AudioEncoding.WebmOpus,
            ["audio/webm;codecs=opus"] = RecognitionConfig.Types.AudioEncoding.WebmOpus,
            ["audio/ogg"]           = RecognitionConfig.Types.AudioEncoding.OggOpus,
            ["audio/ogg;codecs=opus"] = RecognitionConfig.Types.AudioEncoding.OggOpus,
            ["audio/mpeg"]          = RecognitionConfig.Types.AudioEncoding.Mp3,
            ["audio/mp3"]           = RecognitionConfig.Types.AudioEncoding.Mp3,
            ["audio/wav"]           = RecognitionConfig.Types.AudioEncoding.Linear16,
            ["audio/wave"]          = RecognitionConfig.Types.AudioEncoding.Linear16,
            ["audio/x-wav"]         = RecognitionConfig.Types.AudioEncoding.Linear16,
            ["audio/flac"]          = RecognitionConfig.Types.AudioEncoding.Flac,
            ["audio/x-flac"]        = RecognitionConfig.Types.AudioEncoding.Flac,
        };

        public async Task<string> RecognizeSpeechAsync(IFormFile audioFile, string languageCode = "vi-VN")
        {
            if (audioFile == null || audioFile.Length == 0)
                return string.Empty;

            using var memoryStream = new MemoryStream();
            await audioFile.CopyToAsync(memoryStream);
            var audioBytes = memoryStream.ToArray();

            var credentialsPath = System.Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
            if (!string.IsNullOrEmpty(credentialsPath) && !Path.IsPathRooted(credentialsPath))
            {
                credentialsPath = Path.GetFullPath(credentialsPath);
            }

            var builder = new SpeechClientBuilder();
            if (!string.IsNullOrEmpty(credentialsPath))
            {
                builder.CredentialsPath = credentialsPath;
            }
            var client = await builder.BuildAsync();

            var encoding = DetectEncoding(audioFile.ContentType);

            var response = await client.RecognizeAsync(new RecognitionConfig
            {
                Encoding = encoding,
                LanguageCode = languageCode,
                // MP3 không cần khai báo sample rate (Google tự detect)
                SampleRateHertz = encoding == RecognitionConfig.Types.AudioEncoding.Mp3 ? 0 : 0,
                EnableAutomaticPunctuation = true,
            }, RecognitionAudio.FromBytes(audioBytes));

            if (response.Results.Count == 0) return string.Empty;

            return string.Join(" ", response.Results.Select(r => r.Alternatives.First().Transcript));
        }

        private static RecognitionConfig.Types.AudioEncoding DetectEncoding(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return RecognitionConfig.Types.AudioEncoding.WebmOpus;

            // Normalize: "audio/webm; codecs=opus" → "audio/webm;codecs=opus"
            var normalized = contentType.Replace(" ", "").ToLowerInvariant();

            // Check exact match first
            if (ContentTypeToEncoding.TryGetValue(normalized, out var encoding))
                return encoding;

            // Check prefix match (e.g. "audio/webm; codecs=vp9,opus")
            foreach (var kvp in ContentTypeToEncoding)
            {
                if (normalized.StartsWith(kvp.Key))
                    return kvp.Value;
            }

            // Fallback
            return RecognitionConfig.Types.AudioEncoding.WebmOpus;
        }
    }
}
