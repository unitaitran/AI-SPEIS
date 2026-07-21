using Google.Apis.Auth.OAuth2;
using Google.Cloud.Speech.V2;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ai_speis_be.Services.SpeechToTextService
{
    public class SpeechToTextService : ISpeechToTextService
    {
        private readonly ILogger<SpeechToTextService> _logger;

        public SpeechToTextService(ILogger<SpeechToTextService> logger)
        {
            _logger = logger;
        }

        public async Task<string> RecognizeSpeechAsync(IFormFile audioFile, string languageCode = "vi-VN")
        {
            if (audioFile == null || audioFile.Length == 0)
                return string.Empty;

            using var memoryStream = new MemoryStream();
            await audioFile.CopyToAsync(memoryStream);
            var audioBytes = memoryStream.ToArray();

            try
            {
                var client = await CreateClientAsync();

                var projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")
                    ?? throw new InvalidOperationException(
                        "GOOGLE_CLOUD_PROJECT environment variable is not set. " +
                        "Speech-to-Text V2 requires an explicit project ID.");

                var region = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_SPEECH_REGION")
                    ?? "us";

                // Use the implicit default recognizer ("_") — no need to pre-create a Recognizer resource.
                // Format: projects/{project}/locations/{location}/recognizers/_
                var recognizerName = $"projects/{projectId}/locations/{region}/recognizers/_";

                var request = new RecognizeRequest
                {
                    Recognizer = recognizerName,
                    Config = new RecognitionConfig
                    {
                        // Chirp 3 — Google's latest foundation model for speech recognition.
                        // Supports 100+ languages including Vietnamese with high accuracy.
                        Model = "chirp_3",

                        // Language(s) for recognition. V2 uses repeated LanguageCodes (not single LanguageCode).
                        LanguageCodes = { string.IsNullOrWhiteSpace(languageCode) ? "vi-VN" : languageCode.Trim() },

                        // Let Google auto-detect the audio encoding, sample rate, and channel count.
                        // This replaces the manual ContentType-to-AudioEncoding mapping from V1.
                        // Supports: WebM/Opus, OGG/Opus, MP3, WAV/LINEAR16, FLAC, and more.
                        AutoDecodingConfig = new AutoDetectDecodingConfig(),

                        // Recognition features — replaces top-level config booleans from V1.
                        Features = new RecognitionFeatures
                        {
                            EnableAutomaticPunctuation = true,
                            EnableWordTimeOffsets = true,
                        },
                    },
                    // Audio content sent inline (same as V1's RecognitionAudio.FromBytes).
                    Content = ByteString.CopyFrom(audioBytes),
                };

                var response = await client.RecognizeAsync(request);

                if (response.Results == null || response.Results.Count == 0)
                    return string.Empty;

                return string.Join(" ", response.Results
                    .Where(r => r.Alternatives != null && r.Alternatives.Count > 0)
                    .Select(r => r.Alternatives[0].Transcript));
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
            {
                _logger.LogError(ex,
                    "Google Cloud STT authentication failed. " +
                    "Verify GOOGLE_SPEECH_TO_TEXT_CREDENTIALS points to a valid service account key.");
                throw new InvalidOperationException(
                    "Speech-to-Text authentication failed. Check service account credentials.", ex);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogError(ex,
                    "Recognizer not found. Verify GOOGLE_CLOUD_PROJECT and GOOGLE_CLOUD_SPEECH_REGION are correct.");
                throw new InvalidOperationException(
                    "Speech-to-Text recognizer not found. Check project ID and region configuration.", ex);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.InvalidArgument)
            {
                _logger.LogError(ex,
                    "Invalid argument sent to Google Cloud STT. Detail={Detail}. " +
                    "This may indicate an unsupported audio format, invalid region, or misconfigured request.",
                    ex.Status.Detail);
                throw new InvalidOperationException(
                    $"Speech-to-Text request was invalid: {ex.Status.Detail}", ex);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.ResourceExhausted)
            {
                _logger.LogError(ex,
                    "Google Cloud STT quota exceeded. " +
                    "Check your project's quota and billing at https://console.cloud.google.com/iam-admin/quotas");
                throw new InvalidOperationException(
                    "Speech-to-Text quota exceeded. Please try again later.", ex);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable
                                          || ex.StatusCode == StatusCode.Cancelled)
            {
                _logger.LogError(ex,
                    "Google Cloud STT service unavailable or request cancelled. StatusCode={StatusCode}",
                    ex.StatusCode);
                throw new InvalidOperationException(
                    "Speech-to-Text service is temporarily unavailable. Please try again.", ex);
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex,
                    "Unexpected gRPC error from Google Cloud STT. StatusCode={StatusCode}, Detail={Detail}",
                    ex.StatusCode, ex.Status.Detail);
                throw new InvalidOperationException(
                    $"Speech-to-Text error: {ex.Status.Detail}", ex);
            }
        }

        /// <summary>
        /// Creates a SpeechClient configured with the regional endpoint required by Chirp 3.
        /// Chirp 3 is not available on the global endpoint — a regional endpoint must be used.
        /// </summary>
        private async Task<SpeechClient> CreateClientAsync()
        {
            var credentialsPath = Environment.GetEnvironmentVariable("GOOGLE_SPEECH_TO_TEXT_CREDENTIALS");
            if (!string.IsNullOrEmpty(credentialsPath) && !Path.IsPathRooted(credentialsPath))
            {
                credentialsPath = Path.GetFullPath(credentialsPath);
            }

            var region = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_SPEECH_REGION")
                ?? "us";

            var builder = new SpeechClientBuilder
            {
                // Regional endpoint required for Chirp 3.
                // Format: {region}-speech.googleapis.com
                Endpoint = $"{region}-speech.googleapis.com",
            };

            // Load credentials from file. GoogleCredential.FromFile is the recommended
            // non-deprecated method for loading service account credentials from a JSON key file.
            if (!string.IsNullOrEmpty(credentialsPath) && File.Exists(credentialsPath))
            {
#pragma warning disable CS0618 // GoogleCredential.FromFile — suppress if future deprecation
                var credential = GoogleCredential.FromFile(credentialsPath)
                    .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
#pragma warning restore CS0618
                builder.GoogleCredential = credential;
            }

            return await builder.BuildAsync();
        }
    }
}
