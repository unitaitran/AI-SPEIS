using ai_speis_be.Models.DTOs;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.TextToSpeech.V1;

namespace ai_speis_be.Services.TextToSpeechService
{
    public sealed class TextToSpeechService : ITextToSpeechService
    {
        private readonly IWebHostEnvironment _environment;

        public TextToSpeechService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<byte[]> SynthesizeSpeechAsync(
            TextToSpeechRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var text = request.Text.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<byte>();
            }

            var client = await CreateClientAsync(cancellationToken);
            var response = await client.SynthesizeSpeechAsync(
                new SynthesizeSpeechRequest
                {
                    Input = new SynthesisInput
                    {
                        Text = text
                    },
                    Voice = new VoiceSelectionParams
                    {
                        LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode)
                            ? "vi-VN"
                            : request.LanguageCode.Trim(),
                        Name = string.IsNullOrWhiteSpace(request.VoiceName)
                            ? ""
                            : request.VoiceName.Trim(),
                        SsmlGender = SsmlVoiceGender.Neutral
                    },
                    AudioConfig = new AudioConfig
                    {
                        AudioEncoding = AudioEncoding.Mp3,
                        SpeakingRate = request.SpeakingRate,
                        Pitch = request.Pitch
                    }
                },
                cancellationToken);

            return response.AudioContent.ToByteArray();
        }

        private async Task<TextToSpeechClient> CreateClientAsync(CancellationToken cancellationToken)
        {
            var credentialsPath = ResolveCredentialsPath();
            var builder = new TextToSpeechClientBuilder();

            if (!string.IsNullOrWhiteSpace(credentialsPath))
            {
                var credential = await CredentialFactory
                    .FromFileAsync<ServiceAccountCredential>(credentialsPath, cancellationToken);
                builder.GoogleCredential = credential.ToGoogleCredential();
            }

            return await builder.BuildAsync(cancellationToken);
        }

        private string? ResolveCredentialsPath()
        {
            var configuredPath = Environment.GetEnvironmentVariable("GOOGLE_TEXT_TO_SPEECH_CREDENTIALS");
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                configuredPath = Path.Combine("keys", "google-text-to-speech.json");
            }

            if (Path.IsPathRooted(configuredPath))
            {
                return File.Exists(configuredPath) ? configuredPath : null;
            }

            var contentRootPath = Path.Combine(_environment.ContentRootPath, configuredPath);
            if (File.Exists(contentRootPath))
            {
                return contentRootPath;
            }

            var currentDirectoryPath = Path.GetFullPath(configuredPath);
            return File.Exists(currentDirectoryPath) ? currentDirectoryPath : null;
        }
    }
}
