using System;
using System.Threading;
using System.Threading.Tasks;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Services.AdminDashboardService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ai_speis_be.Controllers
{
    /// <summary>
    /// Unified Admin Dashboard Controller for AI-SPEIS.
    /// Aggregates system metrics, revenue, subscriptions, interviews, AI usage, recent activities and quick actions.
    /// </summary>
    [ApiController]
    [Route("api/admin/dashboard")]
    [Authorize(Roles = "admin,Admin")]
    public sealed class AdminDashboardController : ControllerBase
    {
        private readonly IAdminDashboardService _dashboardService;

        public AdminDashboardController(IAdminDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        /// <summary>
        /// Retrieves complete aggregated Admin Dashboard data.
        /// GET /api/admin/dashboard
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(AdminDashboardResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<AdminDashboardResponseDto>> GetDashboard(CancellationToken cancellationToken)
        {
            var data = await _dashboardService.GetDashboardOverviewAsync(cancellationToken);
            return Ok(data);
        }
    }
}
