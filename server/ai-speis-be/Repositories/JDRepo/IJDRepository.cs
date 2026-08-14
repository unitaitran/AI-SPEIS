using ai_speis_be.Models;

namespace ai_speis_be.Repositories.JDRepo
{
    public interface IJDRepository
    {
        Task<PagedResult<JDFile>> GetAllJDAsync(JDQueryParameters query, CancellationToken cancellationToken = default);
        Task<JDFile?> GetJDByIdAsync(int id);
        Task<PagedResult<JDFile>> GetJDByUserIdAsync(int userId, JDQueryParameters query, CancellationToken cancellationToken = default);
        Task<JDFile> AddJDAsync(JDFile jdFile);
        Task<JDFile?> DeleteJDAsync(int id);
        Task<JDFile> UpdateJDAsync(JDFile jdFile);
    }
}
