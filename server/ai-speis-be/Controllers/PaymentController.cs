using ai_speis_be.Models.DTOs.Payment;
using ai_speis_be.Services.PaymentService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ai_speis_be.Controllers
{
    [ApiController]
    [Route("api/payment")]
    [Route("payment")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("create")]
        [Authorize]
        public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequestDto request, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { Message = "Token khong hop le." });
            }

            var (success, errorMessage, payment) = await _paymentService.CreatePaymentAsync(userId, request.PriceId, request.UseRewardPoints, cancellationToken);
            if (!success)
            {
                var errorParts = errorMessage?.Split('|', 2) ?? Array.Empty<string>();
                if (errorParts.Length == 2 && errorParts[0] == "SUBSCRIPTION_DOWNGRADE_NOT_ALLOWED")
                    return Conflict(new { Code = errorParts[0], Message = errorParts[1] });
                if (errorParts.Length == 2)
                    return BadRequest(new { Code = errorParts[0], Message = errorParts[1] });
                return BadRequest(new { Message = errorMessage });
            }

            return Ok(payment);
        }

        [HttpGet("check/{orderCode}")]
        [Authorize]
        public async Task<IActionResult> CheckPayment(string orderCode, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { Message = "Token khong hop le." });
            }

            var (success, errorMessage, payment) = await _paymentService.CheckPaymentAsync(userId, orderCode, cancellationToken);
            if (!success)
            {
                return NotFound(new { Message = errorMessage });
            }

            return Ok(payment);
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook([FromBody] PaymentWebhookRequestDto request, CancellationToken cancellationToken)
        {
            var (success, errorMessage) = await _paymentService.HandleWebhookAsync(request, cancellationToken);
            if (!success)
            {
                // MoMo expects 204 No Content even on failure, to acknowledge receipt
                return NoContent();
            }

            return NoContent();
        }

        [HttpGet("callback")]
        [AllowAnonymous]
        public async Task<IActionResult> Callback([FromQuery] string orderId, [FromQuery] int? resultCode, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return BadRequest(new { Message = "Thiếu mã giao dịch." });
            }

            var (success, errorMessage) = await _paymentService.QueryTransactionStatusAsync(orderId, resultCode, cancellationToken);
            if (!success)
            {
                return BadRequest(new { Message = errorMessage });
            }

            return Ok(new { Success = true, Message = "Thanh toán thành công." });
        }

        private bool TryGetUserId(out int userId)
        {
            return int.TryParse(User.FindFirstValue("UserId"), out userId);
        }
    }
}
