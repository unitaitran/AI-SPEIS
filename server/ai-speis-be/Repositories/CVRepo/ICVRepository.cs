using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ai_speis_be.Models;

namespace ai_speis_be.Repositories.CVRepo
{
    public interface ICVRepository
    {
        Task<PagedResult<CVFile>> GetAllCVAsync(JDQueryParameters query, CancellationToken cancellationToken = default);
        Task<CVFile?> GetCVByIdAsync(int id);
        Task<PagedResult<CVFile>> GetCVByUserIdAsync(int userId, JDQueryParameters query, CancellationToken cancellationToken = default);    
        Task<CVFile> AddCVAsync(CVFile cvFile);
        Task<bool> DeleteCVAsync(int id);     
        Task<CVFile?> GetActiveCVByUserIdAsync(int userId);
        Task ArchiveAllActiveCVsByUserIdAsync(int userId);
        Task<CVFile> UpdateCVAsync(CVFile cvFile);
    }
}