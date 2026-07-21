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

            var response = await client.PostAsync(
                "/submissions/batch?wait=true&base64_encoded=false&fields=stdout,stderr,compile_output,message,time,memory,token,status",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Judge0 batch submission thất bại. Status: {Status}, Body: {Body}",
                    response.StatusCode, errorBody);
                throw new HttpRequestException(
                    $"Judge0 trả về lỗi {response.StatusCode}: {errorBody}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var results = JsonSerializer.Deserialize<List<Judge0SubmissionResponse>>(
                responseBody, JsonOptions);

            if (results == null)
            {
                _logger.LogError("Judge0 trả về response null hoặc không parse được.");
                throw new InvalidOperationException("Không thể đọc kết quả từ Judge0.");
            }

            _logger.LogInformation(
                "Judge0 batch submission thành công: {Count} kết quả",
                results.Count);

            return results;
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
