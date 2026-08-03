using ai_speis_be.DTOs.GoogleQuota;
using ai_speis_be.Services.GoogleQuotaService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    /// <summary>
    /// Admin-only endpoint for monitoring Google Cloud quota and usage.
    /// </summary>
    [ApiController]
    [Route("api/admin/google")]
    [Route("api/google")]
    [Authorize(Roles = "admin,Admin")]
    public sealed class GoogleQuotaController : ControllerBase
    {
        private readonly IGoogleQuotaService _quotaService;
        private readonly ICloudCostService _costService;
        private readonly ILogger<GoogleQuotaController> _logger;

        public GoogleQuotaController(
            IGoogleQuotaService quotaService,
            ICloudCostService costService,
            ILogger<GoogleQuotaController> logger)
        {
            _quotaService = quotaService;
            _costService = costService;
            _logger = logger;
        }

        /// <summary>
        /// Returns unified AI Usage & Google Resource Dashboard data (Usage + Billing Cost).
        /// GET /api/admin/google/dashboard
        /// </summary>
        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(GoogleDashboardResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<GoogleDashboardResponseDto>> GetDashboard(
            CancellationToken cancellationToken)
        {
            try
            {
                var usageTask = _quotaService.GetQuotaOverviewAsync(cancellationToken);
                var costTask = _costService.GetCloudCostAsync(cancellationToken);

                await Task.WhenAll(usageTask, costTask);

                var usage = await usageTask;
                var cost = await costTask;

                return Ok(new GoogleDashboardResponseDto
                {
                    ProjectId = usage.ProjectId,
                    QueriedAt = DateTime.UtcNow,
                    Usage = usage,
                    Cost = cost
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch Google Dashboard data");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Google Dashboard Fetch Failed",
                    Detail = "Unable to retrieve dashboard data from Google Cloud. " + ex.Message,
                    Status = 500
                });
            }
        }

        /// <summary>
        /// Returns billing cost data from BigQuery.
        /// GET /api/admin/google/cost
        /// </summary>
        [HttpGet("cost")]
        [ProducesResponseType(typeof(CloudCostDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<CloudCostDto>> GetCost(
            CancellationToken cancellationToken)
        {
            var result = await _costService.GetCloudCostAsync(cancellationToken);
            return Ok(result);
        }

        /// <summary>
        /// Returns quota usage overview for all enabled Google Cloud services.
        /// GET /api/google/quota
        /// </summary>
        [HttpGet("quota")]
        [ProducesResponseType(typeof(GoogleQuotaResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<GoogleQuotaResponseDto>> GetQuotaOverview(
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _quotaService.GetQuotaOverviewAsync(cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch Google quota overview");
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Google Quota Fetch Failed",
                    Detail = "Unable to retrieve quota data from Google Cloud. " +
                             "Please check service account permissions and try again.",
                    Status = 500
                });
            }
        }
    }
}
