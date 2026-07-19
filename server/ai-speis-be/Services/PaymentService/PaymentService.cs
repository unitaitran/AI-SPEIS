using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ai_speis_be.Models;
using ai_speis_be.Models.DTOs.Payment;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.PaymentRepo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ai_speis_be.Services.PaymentService
{
    public class PaymentService : IPaymentService
    {
        private static readonly TimeSpan ExpiryDuration = TimeSpan.FromMinutes(10);
        private const int PremiumQuotaThreshold = 15;

        private readonly IPaymentRepository _paymentRepository;
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        private static readonly IReadOnlyDictionary<int, decimal> PackageAmountMap = new Dictionary<int, decimal>
        {
            [1] = 59000m,   // 1 month
            [2] = 599000m,  // 1 year
        };

        public PaymentService(
            IPaymentRepository paymentRepository, 
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _paymentRepository = paymentRepository;
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<(bool Success, string? ErrorMessage, PaymentResponseDto? Payment)> CreatePaymentAsync(
            int userId,
            int packageId,
            CancellationToken cancellationToken = default)
        {
            if (!PackageAmountMap.TryGetValue(packageId, out var amount))
            {
                return (false, "Gói dịch vụ không hợp lệ.", null);
            }

            var userExists = await _context.Users.AnyAsync(user => user.UserId == userId, cancellationToken);
            if (!userExists)
            {
                return (false, "Người dùng không tồn tại.", null);
            }

            var orderCode = await GenerateUniqueOrderCodeAsync(cancellationToken);
            var now = DateTime.UtcNow;

            var payment = new Payment
            {
                UserId = userId,
                PackageId = packageId,
                Amount = amount,
                OrderCode = orderCode,
                Status = PaymentStatus.Pending,
                CreatedAt = now,
                PaidAt = null,
            };

            await _paymentRepository.CreateAsync(payment, cancellationToken);

            var payUrl = await CreateMoMoPaymentRequestAsync(payment, cancellationToken);
            if (string.IsNullOrEmpty(payUrl))
            {
                return (false, "Lỗi khi kết nối với MoMo.", null);
            }

            var response = MapToPaymentResponse(payment);
            response.PayUrl = payUrl;

            return (true, null, response);
        }

        public async Task<(bool Success, string? ErrorMessage, PaymentCheckResponseDto? Payment)> CheckPaymentAsync(
            int userId,
            string orderCode,
            CancellationToken cancellationToken = default)
        {
            var payment = await _paymentRepository.GetByOrderCodeAsync(orderCode, cancellationToken);
            if (payment is null || payment.UserId != userId)
            {
                return (false, "Không tìm thấy giao dịch.", null);
            }

            if (TryExpirePayment(payment))
            {
                await _paymentRepository.UpdateAsync(payment, cancellationToken);
            }

            return (true, null, MapToCheckResponse(payment));
        }

        public async Task<(bool Success, string? ErrorMessage)> HandleWebhookAsync(
            PaymentWebhookRequestDto webhook,
            CancellationToken cancellationToken = default)
        {
            var orderCode = webhook.OrderId;
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return (false, "Không thể xác định orderCode.");
            }

            var payment = await _paymentRepository.GetByOrderCodeAsync(orderCode, cancellationToken);
            if (payment is null)
            {
                return (false, "Không tìm thấy giao dịch.");
            }

            // Verify signature
            var accessKey = _configuration["MoMo:AccessKey"] ?? "";
            var secretKey = _configuration["MoMo:SecretKey"] ?? "";
            
            var rawHash = $"accessKey={accessKey}&amount={webhook.Amount}&extraData={webhook.ExtraData}&message={webhook.Message}&orderId={webhook.OrderId}&orderInfo={webhook.OrderInfo}&orderType={webhook.OrderType}&partnerCode={webhook.PartnerCode}&payType={webhook.PayType}&requestId={webhook.RequestId}&responseTime={webhook.ResponseTime}&resultCode={webhook.ResultCode}&transId={webhook.TransId}";
            var signature = ComputeHmacSha256(rawHash, secretKey);

            if (signature != webhook.Signature)
            {
                return (false, "Chữ ký không hợp lệ.");
            }

            if (TryExpirePayment(payment))
            {
                await _paymentRepository.UpdateAsync(payment, cancellationToken);
                return (false, "Giao dịch đã hết hạn.");
            }

            if (payment.Status == PaymentStatus.Paid)
            {
                return (true, null);
            }

            if (webhook.ResultCode != 0)
            {
                // Payment failed on MoMo side
                return (false, $"Thanh toán thất bại: {webhook.Message}");
            }

            await CompletePaymentAsync(payment, cancellationToken);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> QueryTransactionStatusAsync(
            string orderCode,
            CancellationToken cancellationToken = default)
        {
            var payment = await _paymentRepository.GetByOrderCodeAsync(orderCode, cancellationToken);
            if (payment is null)
            {
                return (false, "Không tìm thấy giao dịch.");
            }

            if (payment.Status == PaymentStatus.Paid)
            {
                return (true, null);
            }

            if (TryExpirePayment(payment))
            {
                await _paymentRepository.UpdateAsync(payment, cancellationToken);
                return (false, "Giao dịch đã hết hạn.");
            }

            var partnerCode = _configuration["MoMo:PartnerCode"] ?? "";
            var accessKey = _configuration["MoMo:AccessKey"] ?? "";
            var secretKey = _configuration["MoMo:SecretKey"] ?? "";
            var apiEndpoint = _configuration["MoMo:ApiEndpoint"] ?? "";
            var requestId = Guid.NewGuid().ToString();

            var rawHash = $"accessKey={accessKey}&orderId={orderCode}&partnerCode={partnerCode}&requestId={requestId}";
            var signature = ComputeHmacSha256(rawHash, secretKey);

            var requestData = new
            {
                partnerCode,
                requestId,
                orderId = orderCode,
                signature,
                lang = "vi"
            };

            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            
            var response = await client.PostAsync($"{apiEndpoint}/v2/gateway/api/query", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (false, "Không thể truy vấn trạng thái từ MoMo.");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var momoResponse = JsonSerializer.Deserialize<MoMoQueryResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (momoResponse?.ResultCode == 0)
            {
                await CompletePaymentAsync(payment, cancellationToken);
                return (true, null);
            }

            return (false, momoResponse?.Message ?? "Thanh toán chưa thành công hoặc thất bại.");
        }

        private async Task CompletePaymentAsync(Payment payment, CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                payment.Status = PaymentStatus.Paid;
                payment.PaidAt = DateTime.UtcNow;
                await _paymentRepository.UpdateAsync(payment, cancellationToken);

                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == payment.UserId, cancellationToken);
                if (user is not null && user.RemainingInterviewQuota != PremiumQuotaThreshold)
                {
                    user.RemainingInterviewQuota = PremiumQuotaThreshold;
                    user.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        private async Task<string?> CreateMoMoPaymentRequestAsync(Payment payment, CancellationToken cancellationToken)
        {
            var partnerCode = _configuration["MoMo:PartnerCode"] ?? "";
            var accessKey = _configuration["MoMo:AccessKey"] ?? "";
            var secretKey = _configuration["MoMo:SecretKey"] ?? "";
            var apiEndpoint = _configuration["MoMo:ApiEndpoint"] ?? "";
            var redirectUrl = _configuration["MoMo:RedirectUrl"] ?? "";
            var ipnUrl = _configuration["MoMo:IpnUrl"] ?? "";

            var orderInfo = $"Thanh toan goi Premium AI-SPEIS: {payment.OrderCode}";
            var amount = Convert.ToInt64(decimal.Round(payment.Amount, 0, MidpointRounding.AwayFromZero)).ToString();
            var requestId = Guid.NewGuid().ToString();
            var extraData = "";
            var requestType = "captureWallet";

            var rawHash = $"accessKey={accessKey}&amount={amount}&extraData={extraData}&ipnUrl={ipnUrl}&orderId={payment.OrderCode}&orderInfo={orderInfo}&partnerCode={partnerCode}&redirectUrl={redirectUrl}&requestId={requestId}&requestType={requestType}";
            var signature = ComputeHmacSha256(rawHash, secretKey);

            var requestData = new
            {
                partnerCode,
                partnerName = "AI-SPEIS",
                storeId = "MomoTestStore",
                requestId,
                amount,
                orderId = payment.OrderCode,
                orderInfo,
                redirectUrl,
                ipnUrl,
                lang = "vi",
                extraData,
                requestType,
                signature
            };

            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{apiEndpoint}/v2/gateway/api/create", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var momoResponse = JsonSerializer.Deserialize<MoMoCreateResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return momoResponse?.PayUrl;
        }

        private static string ComputeHmacSha256(string message, string secretKey)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var messageBytes = Encoding.UTF8.GetBytes(message);

            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(messageBytes);

            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private static bool TryExpirePayment(Payment payment)
        {
            if (payment.Status != PaymentStatus.Pending)
            {
                return false;
            }

            var expired = DateTime.UtcNow >= payment.CreatedAt.Add(ExpiryDuration);
            if (!expired)
            {
                return false;
            }

            payment.Status = PaymentStatus.Expired;
            return true;
        }

        private async Task<string> GenerateUniqueOrderCodeAsync(CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var randomSuffix = RandomNumberGenerator.GetInt32(1000, 9999);
                var code = $"ASP{DateTime.UtcNow:yyyyMMddHHmmss}{randomSuffix}";
                if (!await _paymentRepository.ExistsByOrderCodeAsync(code, cancellationToken))
                {
                    return code;
                }
            }

            return $"ASP{DateTime.UtcNow:yyyyMMddHHmmssfff}{RandomNumberGenerator.GetInt32(10000, 99999)}";
        }

        private static PaymentResponseDto MapToPaymentResponse(Payment payment)
        {
            var createdAtUtc = AsUtc(payment.CreatedAt);
            var paidAtUtc = AsUtc(payment.PaidAt);
            var expiresAtUtc = AsUtc(payment.CreatedAt.Add(ExpiryDuration));

            return new PaymentResponseDto
            {
                PaymentId = payment.PaymentId,
                UserId = payment.UserId,
                PackageId = payment.PackageId,
                Amount = payment.Amount,
                OrderCode = payment.OrderCode,
                Status = payment.Status.ToString(),
                CreatedAt = createdAtUtc,
                PaidAt = paidAtUtc,
                ExpiresAt = expiresAtUtc,
                PayUrl = ""
            };
        }

        private static PaymentCheckResponseDto MapToCheckResponse(Payment payment)
        {
            var createdAtUtc = AsUtc(payment.CreatedAt);
            var paidAtUtc = AsUtc(payment.PaidAt);
            var expiresAt = AsUtc(payment.CreatedAt.Add(ExpiryDuration));
            return new PaymentCheckResponseDto
            {
                OrderCode = payment.OrderCode,
                Status = payment.Status.ToString(),
                Amount = payment.Amount,
                PackageId = payment.PackageId,
                CreatedAt = createdAtUtc,
                ExpiresAt = expiresAt,
                PaidAt = paidAtUtc,
                IsExpired = DateTime.UtcNow >= expiresAt || payment.Status == PaymentStatus.Expired,
            };
        }

        private static DateTime AsUtc(DateTime value) =>
            value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static DateTime? AsUtc(DateTime? value) =>
            value.HasValue ? AsUtc(value.Value) : null;
    }

    public class MoMoCreateResponse
    {
        public string? PayUrl { get; set; }
        public string? Message { get; set; }
        public int ResultCode { get; set; }
    }

    public class MoMoQueryResponse
    {
        public string? OrderId { get; set; }
        public int ResultCode { get; set; }
        public string? Message { get; set; }
    }
}
