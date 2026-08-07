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
using ai_speis_be.Services.EmailService;
using ai_speis_be.Services.RewardService;
using ai_speis_be.Services.SubscriptionService;
using ai_speis_be.Services.NotificationService;

namespace ai_speis_be.Services.PaymentService
{
    public class PaymentService : IPaymentService
    {
        private static readonly TimeSpan ExpiryDuration = TimeSpan.FromMinutes(10);
        private static readonly HashSet<int> PendingMoMoResultCodes = new() { 1000, 7000, 7002 };
        private static readonly HashSet<int> CancelledMoMoResultCodes = new() { 1003, 1006, 1017 };

        private readonly IPaymentRepository _paymentRepository;
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IEmailSender _emailSender;
        private readonly IRewardService _rewardService;
        private readonly ISubscriptionService _subscriptionService;
        private readonly INotificationEventPublisher? _notificationPublisher;
        private readonly IAdminNotificationPublisher? _adminNotificationPublisher;

        public PaymentService(
            IPaymentRepository paymentRepository, 
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IEmailSender emailSender,
            IRewardService rewardService,
            ISubscriptionService subscriptionService,
            INotificationEventPublisher? notificationPublisher = null,
            IAdminNotificationPublisher? adminNotificationPublisher = null)
        {
            _paymentRepository = paymentRepository;
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _emailSender = emailSender;
            _rewardService = rewardService;
            _subscriptionService = subscriptionService;
            _notificationPublisher = notificationPublisher;
            _adminNotificationPublisher = adminNotificationPublisher;
        }

        public async Task<(bool Success, string? ErrorMessage, PaymentResponseDto? Payment)> CreatePaymentAsync(
            int userId,
            int priceId,
            bool useRewardPoints,
            CancellationToken cancellationToken = default)
        {
            var price = await _context.SubscriptionPrices
                .Include(item => item.Plan)
                .FirstOrDefaultAsync(item => item.PriceId == priceId, cancellationToken);
            if (price == null) return (false, "Gói hoặc mức giá không hợp lệ.", null);

            var eligibility = await _subscriptionService.CanPurchaseAsync(userId, priceId, cancellationToken);
            if (!eligibility.Allowed)
                return (false, $"{eligibility.ErrorCode}|{eligibility.ErrorMessage}", null);

            var availableRewardPoints = useRewardPoints
                ? await _rewardService.GetAvailablePointsAsync(userId, cancellationToken)
                : 0;
            var rewardPointsToUse = Math.Min(
                availableRewardPoints,
                decimal.ToInt32(decimal.Floor(price.Amount)));

            var orderCode = await GenerateUniqueOrderCodeAsync(cancellationToken);
            var now = DateTime.UtcNow;

            var payment = new Payment
            {
                UserId = userId,
                PackageId = priceId,
                PriceId = priceId,
                OriginalAmount = price.Amount,
                DiscountAmount = rewardPointsToUse,
                RewardPointsUsed = rewardPointsToUse,
                Amount = price.Amount - rewardPointsToUse,
                Currency = price.Currency,
                OrderCode = orderCode,
                Status = PaymentStatus.Pending,
                CreatedAt = now,
                ExpiredAt = now.Add(ExpiryDuration),
                PaidAt = null,
            };

            await using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
            {
                var reservation = await _rewardService.ReserveForPaymentAsync(
                    userId, rewardPointsToUse, orderCode, price.Amount, cancellationToken);
                if (!reservation.Success) return (false, reservation.ErrorMessage, null);
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            if (payment.Amount == 0)
            {
                await CompletePaymentAsync(payment, PaymentStatus.PaidByReward, null, cancellationToken);
                return (true, null, MapToPaymentResponse(payment));
            }

            var payUrl = await CreateMoMoPaymentRequestAsync(payment, cancellationToken);
            if (string.IsNullOrEmpty(payUrl))
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = "Could not create MoMo payment request.";
                await _rewardService.ReleasePaymentReservationAsync(userId, rewardPointsToUse, orderCode, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
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
                await _rewardService.ReleasePaymentReservationAsync(payment.UserId, payment.RewardPointsUsed, payment.OrderCode, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
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

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(signature),
                    Encoding.UTF8.GetBytes(webhook.Signature ?? string.Empty)))
            {
                return (false, "Chữ ký không hợp lệ.");
            }

            if (TryExpirePayment(payment))
            {
                await _rewardService.ReleasePaymentReservationAsync(payment.UserId, payment.RewardPointsUsed, payment.OrderCode, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return (false, "Giao dịch đã hết hạn.");
            }

            if (payment.Status is PaymentStatus.Paid or PaymentStatus.PaidByReward)
            {
                return (true, null);
            }

            if (payment.Status is PaymentStatus.Failed or PaymentStatus.Cancelled or PaymentStatus.Expired)
                return (false, "Giao dịch không còn ở trạng thái chờ thanh toán.");

            if (PendingMoMoResultCodes.Contains(webhook.ResultCode))
            {
                return (true, null);
            }

            if (webhook.ResultCode != 0)
            {
                var failedStatus = GetUnsuccessfulStatus(webhook.ResultCode);
                await MarkPaymentUnsuccessfulAsync(
                    payment,
                    failedStatus,
                    BuildMoMoFailureReason(webhook.ResultCode, webhook.Message),
                    cancellationToken);
                return (false, $"Thanh toán không thành công: {webhook.Message}");
            }

            if (webhook.Amount != payment.Amount)
                return (false, "Số tiền webhook không khớp với đơn hàng.");

            await CompletePaymentAsync(payment, PaymentStatus.Paid, webhook.TransId.ToString(), cancellationToken);
            return (true, null);
        }

        public async Task<(bool Success, string? ErrorMessage)> QueryTransactionStatusAsync(
            string orderCode,
            int? resultCode = null,
            CancellationToken cancellationToken = default)
        {
            var payment = await _paymentRepository.GetByOrderCodeAsync(orderCode, cancellationToken);
            if (payment is null)
            {
                return (false, "Không tìm thấy giao dịch.");
            }

            if (payment.Status is PaymentStatus.Paid or PaymentStatus.PaidByReward)
            {
                return (true, null);
            }

            if (payment.Status is PaymentStatus.Failed or PaymentStatus.Cancelled or PaymentStatus.Expired)
                return (false, "Giao dịch không còn ở trạng thái chờ thanh toán.");

            if (TryExpirePayment(payment))
            {
                await _rewardService.ReleasePaymentReservationAsync(payment.UserId, payment.RewardPointsUsed, payment.OrderCode, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return (false, "Giao dịch đã hết hạn.");
            }

            try
            {
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
                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    var momoResponse = JsonSerializer.Deserialize<MoMoQueryResponse>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (momoResponse?.ResultCode == 0)
                    {
                        if (momoResponse.Amount.HasValue && momoResponse.Amount.Value != payment.Amount)
                            return (false, "Số tiền xác nhận từ MoMo không khớp với đơn hàng.");
                        await CompletePaymentAsync(payment, PaymentStatus.Paid, momoResponse.TransId?.ToString(), cancellationToken);
                        return (true, null);
                    }

                    if (momoResponse is not null && !PendingMoMoResultCodes.Contains(momoResponse.ResultCode))
                    {
                        var failedStatus = GetUnsuccessfulStatus(momoResponse.ResultCode);
                        var failureReason = BuildMoMoFailureReason(momoResponse.ResultCode, momoResponse.Message);
                        await MarkPaymentUnsuccessfulAsync(payment, failedStatus, failureReason, cancellationToken);
                        return (false, failureReason);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore query exception
            }

            return (false, "Thanh toán chưa thành công hoặc thất bại.");
        }

        private async Task MarkPaymentUnsuccessfulAsync(
            Payment payment,
            PaymentStatus status,
            string failureReason,
            CancellationToken cancellationToken)
        {
            if (payment.Status != PaymentStatus.Pending)
            {
                return;
            }

            payment.Status = status;
            payment.FailureReason = failureReason.Length <= 500
                ? failureReason
                : failureReason[..500];
            await _rewardService.ReleasePaymentReservationAsync(
                payment.UserId,
                payment.RewardPointsUsed,
                payment.OrderCode,
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await PublishPaymentFailureNotificationAsync(payment, cancellationToken);
        }

        private static PaymentStatus GetUnsuccessfulStatus(int resultCode) =>
            CancelledMoMoResultCodes.Contains(resultCode)
                ? PaymentStatus.Cancelled
                : PaymentStatus.Failed;

        private static string BuildMoMoFailureReason(int resultCode, string? message) =>
            $"MoMo resultCode {resultCode}: {message ?? "No message returned."}";

        private async Task CompletePaymentAsync(
            Payment payment,
            PaymentStatus successfulStatus,
            string? providerTransactionId,
            CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var transactionCommitted = false;
            try
            {
                if (payment.Status is PaymentStatus.Paid or PaymentStatus.PaidByReward)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }

                payment.Status = successfulStatus;
                payment.PaidAt = DateTime.UtcNow;
                payment.ProviderTransactionId = providerTransactionId;
                await _rewardService.RedeemPaymentReservationAsync(
                    payment.UserId, payment.RewardPointsUsed, payment.OrderCode, cancellationToken);
                await _subscriptionService.ActivateFromPaymentAsync(payment, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == payment.UserId, cancellationToken);
                if (user is not null)
                {
                    // Send email
                    var billingCycle = await _context.SubscriptionPrices
                        .Where(price => price.PriceId == payment.PriceId)
                        .Select(price => price.BillingCycle)
                        .FirstOrDefaultAsync(cancellationToken);
                    var packageDuration = billingCycle == BillingCycle.Yearly ? "1 năm" : "1 tháng";
                    var subject = "👑 Kích Hoạt Gói Premium Thành Công - AI-SPEIS";
                    var emailBody = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; color: #333; line-height: 1.6;'>
                            <div style='text-align: center; padding: 20px; background: linear-gradient(135deg, #FFD700 0%, #FFA500 100%); border-radius: 10px 10px 0 0;'>
                                <h1 style='color: #fff; margin: 0; font-size: 24px;'>Chúc mừng bạn đã nâng cấp Premium!</h1>
                            </div>
                            <div style='padding: 30px; background-color: #f9f9f9; border-left: 1px solid #ddd; border-right: 1px solid #ddd; border-bottom: 1px solid #ddd; border-radius: 0 0 10px 10px;'>
                                <p style='font-size: 16px;'>Xin chào <strong>{user.FullName}</strong>,</p>
                                <p>Cảm ơn bạn đã tin tưởng và nâng cấp gói dịch vụ <strong>Premium ({packageDuration})</strong> tại AI-SPEIS.</p>
                                <p>Thanh toán đã được xác nhận. Thời hạn Premium hiện tại của bạn đến ngày <strong>{user.PremiumExpireAt:dd/MM/yyyy}</strong>. Quota 15 lượt chỉ được cấp khi kỳ Premium tương ứng bắt đầu và làm mới sau mỗi 30 ngày.</p>
                                
                                <div style='background-color: #fff; padding: 15px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #FFA500;'>
                                    <h3 style='margin-top: 0; color: #FFA500;'>Đặc quyền của bạn:</h3>
                                    <ul style='margin-bottom: 0; padding-left: 20px;'>
                                        <li>15 lượt phỏng vấn AI toàn diện mỗi tháng</li>
                                        <li>Đánh giá & phân tích kỹ năng chuyên sâu</li>
                                        <li>Làm mới 15 lượt ưu tiên mỗi tháng</li>
                                    </ul>
                                </div>
                                
                                <p>Hãy truy cập nền tảng và trải nghiệm những buổi phỏng vấn cùng AI ngay hôm nay!</p>
                                
                                <div style='text-align: center; margin-top: 30px;'>
                                    <a href='{_configuration["Frontend:LoginUrl"]?.Replace("#login", "")}' style='background-color: #4A90E2; color: #fff; text-decoration: none; padding: 12px 25px; border-radius: 5px; font-weight: bold; display: inline-block;'>Bắt đầu phỏng vấn</a>
                                </div>
                            </div>
                            <div style='text-align: center; padding: 15px; color: #888; font-size: 12px;'>
                                <p>&copy; {DateTime.UtcNow.Year} AI-SPEIS. Mọi thắc mắc xin vui lòng liên hệ bộ phận hỗ trợ.</p>
                            </div>
                        </div>";

                    // Fire and forget email sending to not block the transaction commit
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailSender.SendEmailAsync(user.Email, subject, emailBody);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[EmailError] Failed to send Premium Activation Email to {user.Email}: {ex.Message}");
                        }
                    });
                }

                await transaction.CommitAsync(cancellationToken);
                transactionCommitted = true;
                await PublishSubscriptionActivatedNotificationAsync(payment, cancellationToken);
                await PublishPaymentSucceededForAdminsAsync(payment, cancellationToken);
            }
            catch
            {
                if (!transactionCommitted)
                    await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                await PublishSubscriptionActivationFailedForAdminAsync(payment, cancellationToken);
                throw;
            }
        }

        private async Task PublishSubscriptionActivationFailedForAdminAsync(Payment payment, CancellationToken cancellationToken)
        {
            if (_adminNotificationPublisher is null) return;
            try
            {
                await _adminNotificationPublisher.PublishAsync(new AdminNotificationEvent(
                    payment.UserId, NotificationType.SUBSCRIPTION_ACTIVATION_FAILED,
                    NotificationCategory.SUBSCRIPTION, NotificationSeverity.CRITICAL,
                    "Subscription activation failed", "The subscription could not be activated after payment confirmation.",
                    NotificationEntityType.PAYMENT, payment.PaymentId.ToString(), "/admin/payments",
                    $"SUBSCRIPTION_ACTIVATION_FAILED:{payment.PaymentId}",
                    new Dictionary<string, object?> { ["transactionReference"] = payment.PaymentId }), cancellationToken);
            }
            catch (Exception notificationException)
            {
                Console.WriteLine($"[NotificationError] Failed to publish subscription activation failure: {notificationException.Message}");
            }
        }

        private async Task PublishPaymentSucceededForAdminsAsync(Payment payment, CancellationToken cancellationToken)
        {
            if (_adminNotificationPublisher is null) return;
            try
            {
                await _adminNotificationPublisher.PublishAsync(new AdminNotificationEvent(
                    payment.UserId, NotificationType.SUBSCRIPTION_PAYMENT_SUCCEEDED,
                    NotificationCategory.SUBSCRIPTION, NotificationSeverity.SUCCESS,
                    "Subscription payment received", "A subscription payment was completed successfully.",
                    NotificationEntityType.PAYMENT, payment.PaymentId.ToString(), "/admin/payments",
                    $"SUBSCRIPTION_PAYMENT_SUCCEEDED:{payment.PaymentId}",
                    new Dictionary<string, object?> { ["transactionReference"] = payment.PaymentId }), cancellationToken);
            }
            catch (Exception notificationException)
            {
                Console.WriteLine($"[NotificationError] Failed to publish payment success: {notificationException.Message}");
            }
        }

        private async Task PublishSubscriptionActivatedNotificationAsync(Payment payment, CancellationToken cancellationToken)
        {
            if (_notificationPublisher is null) return;
            try
            {
                var subscription = await _context.UserSubscriptions.Include(item => item.Plan)
                    .FirstOrDefaultAsync(item => item.UserId == payment.UserId, cancellationToken);
                if (subscription is null) return;
                await _notificationPublisher.PublishAsync(new NotificationEvent(
                    payment.UserId, NotificationRecipientRole.USER, NotificationType.SUBSCRIPTION_ACTIVATED,
                    NotificationCategory.SUBSCRIPTION, NotificationSeverity.SUCCESS, "Subscription activated",
                    $"Your {subscription.Plan.Name} subscription is now active.", NotificationEntityType.SUBSCRIPTION,
                    subscription.UserSubscriptionId.ToString(), "/user/packages",
                    $"SUBSCRIPTION_ACTIVATED:{payment.PaymentId}:{payment.UserId}", new { planName = subscription.Plan.Name }), cancellationToken);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[NotificationError] Failed to publish subscription activation: {exception.Message}");
            }
        }

        private async Task PublishPaymentFailureNotificationAsync(Payment payment, CancellationToken cancellationToken)
        {
            if (_notificationPublisher is null || payment.Status != PaymentStatus.Failed) return;
            try
            {
                await _notificationPublisher.PublishAsync(new NotificationEvent(
                    payment.UserId, NotificationRecipientRole.USER, NotificationType.SUBSCRIPTION_PAYMENT_FAILED,
                    NotificationCategory.SUBSCRIPTION, NotificationSeverity.ERROR, "Subscription payment failed",
                    "We could not renew your subscription. Please review your payment information.",
                    NotificationEntityType.PAYMENT, payment.PaymentId.ToString(), "/user/packages",
                    $"SUBSCRIPTION_PAYMENT_FAILED:{payment.PaymentId}:{payment.UserId}"), cancellationToken);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[NotificationError] Failed to publish payment failure: {exception.Message}");
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

            var expired = DateTime.UtcNow >= (payment.ExpiredAt ?? payment.CreatedAt.Add(ExpiryDuration));
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
                PriceId = payment.PriceId,
                OriginalAmount = payment.OriginalAmount,
                DiscountAmount = payment.DiscountAmount,
                RewardPointsUsed = payment.RewardPointsUsed,
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
                PriceId = payment.PriceId,
                OriginalAmount = payment.OriginalAmount,
                DiscountAmount = payment.DiscountAmount,
                RewardPointsUsed = payment.RewardPointsUsed,
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
        public decimal? Amount { get; set; }
        public long? TransId { get; set; }
    }
}
