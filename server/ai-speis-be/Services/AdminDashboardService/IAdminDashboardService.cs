using System.Threading;
using System.Threading.Tasks;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.AdminDashboardService
{
    public interface IAdminDashboardService
    {
        Task<AdminDashboardResponseDto> GetDashboardOverviewAsync(CancellationToken cancellationToken = default);
    }
}
