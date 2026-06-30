using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ai_speis_be.Models;

namespace ai_speis_be.Repositories.CVRepo
{
    public interface ICVRepository
    {
        Task<IEnumerable<CVFile>> GetAllCVAsync();
        Task<CVFile?> GetCVByIdAsync(int id);
        Task<CVFile?> GetCVByUserIdAsync(int userId);    
        Task<CVFile> AddCVAsync(CVFile cvFile);
        Task<bool> DeleteCVAsync(int id);     
        Task<CVFile?> GetActiveCVByUserIdAsync(int userId);
        Task ArchiveAllActiveCVsByUserIdAsync(int userId);
        Task<CVFile> UpdateCVAsync(CVFile cvFile);
    }
}