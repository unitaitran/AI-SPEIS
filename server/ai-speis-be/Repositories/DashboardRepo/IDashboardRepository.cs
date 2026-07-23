using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Repositories.DashboardRepo
{
    public interface IDashboardRepository
    {
        Task<AdminDashboardDto> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
    }
}
