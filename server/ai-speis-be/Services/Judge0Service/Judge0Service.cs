using System.Text;
using System.Text.Json;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.Judge0Service
{
    public class Judge0Service : IJudge0Service
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<Judge0Service> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public Judge0Service(
            IHttpClientFactory httpClientFactory,
            ILogger<Judge0Service> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<List<Judge0SubmissionResponse>> SubmitBatchAsync(
            List<Judge0SubmissionRequest> submissions,
            CancellationToken cancellationToken = default)
        {
            if (submissions.Count == 0)
            {
                return new List<Judge0SubmissionResponse>();
            }

            var client = _httpClientFactory.CreateClient("Judge0");

            // Encode source_code and stdin into Base64 for safety against special characters and multiline breaks
            var encodedSubmissions = submissions.Select(s => new Judge0SubmissionRequest
            {
                source_code = EncodeBase64(s.source_code),
                language_id = s.language_id,
                stdin = !string.IsNullOrEmpty(s.stdin) ? EncodeBase64(s.stdin) : s.stdin,
                cpu_time_limit = s.cpu_time_limit,
                memory_limit = s.memory_limit,
                command_line_arguments = s.command_line_arguments,
                compiler_options = s.compiler_options
            }).ToList();

            var batchRequest = new Judge0BatchRequest { submissions = encodedSubmissions };
            var jsonContent = JsonSerializer.Serialize(batchRequest, JsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation(
                "Gửi batch submission (Base64 encoded) đến Judge0: {Count} test cases",
                submissions.Count);

            // Step 1: POST batch submission -> Nhận danh sách token [{ "token": "..." }]
            var response = await client.PostAsync("/submissions/batch?base64_encoded=true", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Judge0 batch submission POST thất bại. Status: {Status}, Body: {Body}",
                    response.StatusCode, errorBody);
                throw new HttpRequestException(
                    $"Judge0 trả về lỗi {response.StatusCode}: {errorBody}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponses = JsonSerializer.Deserialize<List<Judge0TokenResponse>>(
                responseBody, JsonOptions);

            if (tokenResponses == null || tokenResponses.Count == 0)
            {
                _logger.LogError("Judge0 không trả về token nào từ batch submission.");
                throw new InvalidOperationException("Không thể đọc danh sách token từ Judge0.");
            }

            var tokenList = string.Join(",", tokenResponses.Select(t => t.token));
            var getUrl = $"/submissions/batch?tokens={tokenList}&base64_encoded=true&fields=stdout,stderr,compile_output,message,time,memory,token,status";

            // Step 2: Poll GET batch endpoint cho đến khi tất cả các submission hoàn thành (status.id > 2)
            // Budget: 120 * 500ms = 60s (enough for 10s cpu_time + compilation + overhead)
            const int maxAttempts = 120;
            const int delayMs = 500;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var getResponse = await client.GetAsync(getUrl, cancellationToken);
                if (!getResponse.IsSuccessStatusCode)
                {
                    var errorBody = await getResponse.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError(
                        "Judge0 batch GET thất bại. Status: {Status}, Body: {Body}",
                        getResponse.StatusCode, errorBody);
                    throw new HttpRequestException(
                        $"Judge0 trả về lỗi {getResponse.StatusCode}: {errorBody}");
                }

                var getResponseBody = await getResponse.Content.ReadAsStringAsync(cancellationToken);
                var batchResponse = JsonSerializer.Deserialize<Judge0BatchResponse>(getResponseBody, JsonOptions);

                if (batchResponse?.submissions != null && batchResponse.submissions.Count > 0)
                {
                    bool allFinished = batchResponse.submissions.All(s => s.status != null && s.status.id > 2);
                    if (allFinished || attempt == maxAttempts - 1)
                    {
                        _logger.LogInformation(
                            "Judge0 batch submission hoàn tất sau {Attempt} lượt poll: {Count} kết quả",
                            attempt + 1, batchResponse.submissions.Count);

                        var dict = batchResponse.submissions
                            .Where(s => !string.IsNullOrEmpty(s.token))
                            .ToDictionary(s => s.token!);

                        return tokenResponses
                            .Select(t =>
                            {
                                if (dict.TryGetValue(t.token, out var sub))
                                {
                                    sub.stdout = DecodeBase64(sub.stdout);
                                    sub.stderr = DecodeBase64(sub.stderr);
                                    sub.compile_output = DecodeBase64(sub.compile_output);
                                    sub.message = DecodeBase64(sub.message);
                                    return sub;
                                }
                                return new Judge0SubmissionResponse();
                            })
                            .ToList();
                    }
                }

                await Task.Delay(delayMs, cancellationToken);
            }

            throw new TimeoutException("Thời gian chờ phản hồi từ Judge0 quá lâu.");
        }

        private static string EncodeBase64(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        private static string? DecodeBase64(string? base64EncodedData)
        {
            if (string.IsNullOrWhiteSpace(base64EncodedData)) return base64EncodedData;
            try
            {
                byte[] base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
                return Encoding.UTF8.GetString(base64EncodedBytes);
            }
            catch
            {
                return base64EncodedData;
            }
        }

        public async Task<List<Judge0LanguageDto>> GetLanguagesAsync(
            CancellationToken cancellationToken = default)
        {
            var client = _httpClientFactory.CreateClient("Judge0");

            var response = await client.GetAsync("/languages", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Judge0 get languages thất bại. Status: {Status}, Body: {Body}",
                    response.StatusCode, errorBody);
                throw new HttpRequestException(
                    $"Judge0 trả về lỗi {response.StatusCode}: {errorBody}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var languages = JsonSerializer.Deserialize<List<Judge0LanguageDto>>(
                responseBody, JsonOptions);

            return languages ?? new List<Judge0LanguageDto>();
        }
    }
}
