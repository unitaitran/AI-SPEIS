using Google.Cloud.Speech.V1;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ai_speis_be.Services.SpeechToTextService
{
    public class SpeechToTextService : ISpeechToTextService
    {
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

            var response = await client.RecognizeAsync(new RecognitionConfig
            {
                Encoding = RecognitionConfig.Types.AudioEncoding.WebmOpus,
                LanguageCode = languageCode,
            }, RecognitionAudio.FromBytes(audioBytes));

            if (response.Results.Count == 0) return string.Empty;

            var transcript = string.Join(" ", response.Results.Select(r => r.Alternatives.First().Transcript));
            return transcript;
        }
    }
}
