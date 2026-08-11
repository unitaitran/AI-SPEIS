using ai_speis_be.Models.DTOs.AdminPayment;
using ai_speis_be.Models.Enums;
using ai_speis_be.Services.AdminPaymentService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    /// <summary>
    /// Admin-only controller for monitoring MoMo payments, transaction history,
    /// statistics, verification, and reports export.
    /// </summary>
    [ApiController]
    [Route("api/admin/payments")]
    [Authorize(Roles = "admin,Admin")]
    public class AdminPaymentController : ControllerBase
    {
        private readonly IAdminPaymentService _adminPaymentService;
        private readonly ILogger<AdminPaymentController> _logger;

        public AdminPaymentController(
            IAdminPaymentService adminPaymentService,
            ILogger<AdminPaymentController> logger)
        {
            _adminPaymentService = adminPaymentService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves paginated list of payments with filtering, searching, and sorting.
        /// GET /api/admin/payments
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResultDto<PaymentListDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PaginatedResultDto<PaymentListDto>>> GetPayments(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] PaymentStatus? status = null,
            [FromQuery] int? planId = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = "newest",
            CancellationToken cancellationToken = default)
        {
            var result = await _adminPaymentService.GetPaymentsAsync(
                page, pageSize, status, planId, dateFrom, dateTo, search, sortBy, cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Retrieves overall payment statistics, revenue metrics, and charts data.
        /// GET /api/admin/payments/statistics
        /// </summary>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(PaymentStatisticsDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<PaymentStatisticsDto>> GetStatistics(CancellationToken cancellationToken)
        {
            var stats = await _adminPaymentService.GetStatisticsAsync(cancellationToken);
            return Ok(stats);
        }

        /// <summary>
        /// Retrieves top N recent payment transactions.
        /// GET /api/admin/payments/recent
        /// </summary>
        [HttpGet("recent")]
        [ProducesResponseType(typeof(List<PaymentListDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<List<PaymentListDto>>> GetRecent(
            [FromQuery] int count = 5,
            CancellationToken cancellationToken = default)
        {
            var recent = await _adminPaymentService.GetRecentPaymentsAsync(count, cancellationToken);
            return Ok(recent);
        }

        /// <summary>
        /// Exports payment transactions as a CSV/Excel report.
        /// GET /api/admin/payments/export
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> Export(
            [FromQuery] PaymentStatus? status = null,
            [FromQuery] int? planId = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = "newest",
            CancellationToken cancellationToken = default)
        {
            var bytes = await _adminPaymentService.ExportPaymentsExcelAsync(
                status, planId, dateFrom, dateTo, search, sortBy, cancellationToken);

            var fileName = $"MoMo_Payment_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }

        /// <summary>
        /// Retrieves detailed information for a single payment.
        /// GET /api/admin/payments/{id}
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(PaymentDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PaymentDetailDto>> GetPaymentDetail(
            int id, CancellationToken cancellationToken)
        {
            var detail = await _adminPaymentService.GetPaymentDetailAsync(id, cancellationToken);
            if (detail == null)
            {
                return NotFound(new { Message = "Không tìm thấy thông tin giao dịch thanh toán." });
            }
            return Ok(detail);
        }

        /// <summary>
        /// Re-verifies payment transaction status directly with MoMo API.
        /// POST /api/admin/payments/{id}/verify
        /// </summary>
        [HttpPost("{id:int}/verify")]
        [ProducesResponseType(typeof(PaymentDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> VerifyPayment(int id, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Admin initiated re-verification for payment ID: {PaymentId}", id);
            var (success, message, updatedDetail) = await _adminPaymentService.VerifyPaymentWithMoMoAsync(id, cancellationToken);

            if (!success)
            {
                return BadRequest(new
                {
                    Success = false,
                    Message = message,
                    Detail = updatedDetail
                });
            }

            return Ok(new
            {
                Success = true,
                Message = message,
                Detail = updatedDetail
            });
        }
    }
}
