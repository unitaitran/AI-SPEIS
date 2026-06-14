using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ai_speis_be.Models;

namespace ai_speis_be.Repositories.CVRepo
{
    public interface CVRepository
    {
        Task<IEnumerable<CVFile>> GetAllCVAsync();
        Task<CVFile?> GetCVByIdAsync(int id);
    }
}