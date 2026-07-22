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

            var batchRequest = new Judge0BatchRequest { submissions = submissions };
            var jsonContent = JsonSerializer.Serialize(batchRequest, JsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation(
                "Gửi batch submission đến Judge0: {Count} test cases",
                submissions.Count);

            // Step 1: POST batch submission -> Nhận danh sách token [{ "token": "..." }]
            var response = await client.PostAsync("/submissions/batch", content, cancellationToken);

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
            var getUrl = $"/submissions/batch?tokens={tokenList}&fields=stdout,stderr,compile_output,message,time,memory,token,status";

            // Step 2: Poll GET batch endpoint cho đến khi tất cả các submission hoàn thành (status.id > 2)
            const int maxAttempts = 30; // max 9 seconds (30 * 300ms)
            const int delayMs = 300;

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
                            .Select(t => dict.TryGetValue(t.token, out var sub) ? sub : new Judge0SubmissionResponse())
                            .ToList();
                    }
                }

                await Task.Delay(delayMs, cancellationToken);
            }

            throw new TimeoutException("Thời gian chờ phản hồi từ Judge0 quá lâu.");
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
