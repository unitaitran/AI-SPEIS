using Google.Apis.Auth.OAuth2;
using Google.Api.Gax.Grpc;
using Google.Cloud.Speech.V2;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace ai_speis_be.Services.SpeechToTextService
{
    public class SpeechToTextService : ISpeechToTextService
    {
        // Stay safely below Speech-to-Text V2's 15 KB per-message audio limit.
        private const int StreamingAudioChunkSize = 14 * 1024;
        private readonly ILogger<SpeechToTextService> _logger;

        public SpeechToTextService(ILogger<SpeechToTextService> logger)
        {
            _logger = logger;
        }

        public async Task<string> RecognizeSpeechAsync(
            IFormFile audioFile,
            string languageCode = "vi-VN",
            CancellationToken cancellationToken = default)
        {
            if (audioFile == null || audioFile.Length == 0)
                return string.Empty;

            using var memoryStream = new MemoryStream();
            await audioFile.CopyToAsync(memoryStream, cancellationToken);
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

                var recognitionConfig = new RecognitionConfig
                {
                    Model = "chirp_3",
                    LanguageCodes = { string.IsNullOrWhiteSpace(languageCode) ? "vi-VN" : languageCode.Trim() },
                    AutoDecodingConfig = new AutoDetectDecodingConfig(),
                    Features = new RecognitionFeatures
                    {
                        EnableAutomaticPunctuation = true,
                    },
                };

                // Technical answers may be up to two minutes. RecognizeAsync only accepts
                // audio shorter than 60 seconds, so send the recorded WebM through the
                // bidirectional streaming API even though transcription starts after stop.
                using var stream = client.StreamingRecognize(
                    callSettings: CallSettings.FromCancellationToken(cancellationToken));
                var finalSegments = new List<string>();
                string latestInterimTranscript = string.Empty;

                var responseTask = Task.Run(async () =>
                {
                    var responses = stream.GetResponseStream();
                    while (await responses.MoveNextAsync())
                    {
                        foreach (var result in responses.Current.Results)
                        {
                            if (result.Alternatives == null || result.Alternatives.Count == 0)
                                continue;

                            var transcript = result.Alternatives[0].Transcript?.Trim();
                            if (string.IsNullOrWhiteSpace(transcript))
                                continue;

                            if (result.IsFinal)
                                finalSegments.Add(transcript);
                            else
                                latestInterimTranscript = transcript;
                        }
                    }
                });

                await stream.WriteAsync(new StreamingRecognizeRequest
                {
                    Recognizer = recognizerName,
                    StreamingConfig = new StreamingRecognitionConfig
                    {
                        Config = recognitionConfig,
                        StreamingFeatures = new StreamingRecognitionFeatures
                        {
                            InterimResults = false,
                        },
                    },
                });

                for (var offset = 0; offset < audioBytes.Length; offset += StreamingAudioChunkSize)
                {
                    var length = Math.Min(StreamingAudioChunkSize, audioBytes.Length - offset);
                    await stream.WriteAsync(new StreamingRecognizeRequest
                    {
                        Audio = ByteString.CopyFrom(audioBytes, offset, length),
                    });
                }

                await stream.WriteCompleteAsync();
                await responseTask;

                return finalSegments.Count > 0
                    ? string.Join(" ", finalSegments)
                    : latestInterimTranscript;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
            {
                _logger.LogError(ex,
                    "Google Cloud STT authentication failed. " +
                    "Verify GOOGLE_APPLICATION_CREDENTIALS points to a valid service account key.");
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
            // GOOGLE_APPLICATION_CREDENTIALS is the standard Google ADC variable and
            // is already documented in .env.example. Keep the service-specific name as
            // a backwards-compatible fallback for existing deployments.
            var credentialsPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")
                ?? Environment.GetEnvironmentVariable("GOOGLE_SPEECH_TO_TEXT_CREDENTIALS");
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
