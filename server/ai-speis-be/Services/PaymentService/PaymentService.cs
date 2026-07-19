using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ai_speis_be.Models;
using ai_speis_be.Models.DTOs.Payment;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.PaymentRepo;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Services.PaymentService
{
    public class PaymentService : IPaymentService
    {
        private static readonly TimeSpan ExpiryDuration = TimeSpan.FromMinutes(10);
        private const int PremiumQuotaThreshold = 15;
        private static readonly Regex OrderCodeRegex = new(@"ASP\d{18}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IPaymentRepository _paymentRepository;
        private readonly ApplicationDbContext _context;

        private static readonly IReadOnlyDictionary<int, decimal> PackageAmountMap = new Dictionary<int, decimal>
        {
            [1] = 29000m,
        };

        public PaymentService(IPaymentRepository paymentRepository, ApplicationDbContext context)
        {
            _paymentRepository = paymentRepository;
            _context = context;
        }

        public async Task<(bool Success, string? ErrorMessage, PaymentResponseDto? Payment)> CreatePaymentAsync(
            int userId,
            int packageId,
            CancellationToken cancellationToken = default)
        {
            if (!PackageAmountMap.TryGetValue(packageId, out var amount))
            {
                return (false, "Goi dich vu khong hop le.", null);
            }

            var userExists = await _context.Users.AnyAsync(user => user.UserId == userId, cancellationToken);
            if (!userExists)
            {
                return (false, "Nguoi dung khong ton tai.", null);
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
            return (true, null, MapToPaymentResponse(payment));
        }

        public async Task<(bool Success, string? ErrorMessage, PaymentCheckResponseDto? Payment)> CheckPaymentAsync(
            int userId,
            string orderCode,
            CancellationToken cancellationToken = default)
        {
            var payment = await _paymentRepository.GetByOrderCodeAsync(orderCode, cancellationToken);
            if (payment is null || payment.UserId != userId)
            {
                return (false, "Khong tim thay giao dich.", null);
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
            var orderCode = ResolveOrderCode(webhook);
            if (string.IsNullOrWhiteSpace(orderCode))
            {
                return (false, "Khong the xac dinh orderCode.");
            }

            var payment = await _paymentRepository.GetByOrderCodeAsync(orderCode, cancellationToken);
            if (payment is null)
            {
                return (false, "Khong tim thay giao dich.");
            }

            if (TryExpirePayment(payment))
            {
                await _paymentRepository.UpdateAsync(payment, cancellationToken);
                return (false, "Giao dich da het han.");
            }

            if (payment.Status == PaymentStatus.Paid)
            {
                return (true, null);
            }

            var description = webhook.Description ?? string.Empty;
            if (webhook.Amount != payment.Amount)
            {
                return (false, "So tien khong khop.");
            }

            if (!description.Contains(payment.OrderCode, StringComparison.OrdinalIgnoreCase))
            {
                return (false, "Noi dung thanh toan khong hop le.");
            }

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
                return (true, null);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
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

        private static string? ResolveOrderCode(PaymentWebhookRequestDto webhook)
        {
            if (!string.IsNullOrWhiteSpace(webhook.OrderCode))
            {
                return webhook.OrderCode.Trim();
            }

            if (string.IsNullOrWhiteSpace(webhook.Description))
            {
                return null;
            }

            var match = OrderCodeRegex.Match(webhook.Description);
            return match.Success ? match.Value : null;
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
                QrUrl = BuildVietQrUrl(payment.Amount, payment.OrderCode),
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

        private static string BuildVietQrUrl(decimal amount, string orderCode)
        {
            var encodedDescription = Uri.EscapeDataString(orderCode);
            var amountText = Convert.ToInt64(decimal.Round(amount, 0, MidpointRounding.AwayFromZero)).ToString();
            return $"https://vietqr.app/img?acc=4270767262&bank=BIDV&amount={amountText}&des={encodedDescription}&template=compact";
        }
    }
}
