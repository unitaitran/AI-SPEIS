using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ai_speis_be.Models.DTOs;

namespace ai_speis_be.Services.CVService
{
    public interface ICVService
    {
        Task<(bool Success, string? ErrorMessage, CVDto? CVDto)> UploadCVAsync(int userId, IFormFile file);
        Task<IEnumerable<CVDto>> GetAllCVsAsync();
        Task<CVDto?> GetCVByIdAsync(int id);
        Task<CVDto?> GetCVByUserIdAsync(int userId);
        Task<(bool Success, string? ErrorMessage)> DeleteCVAsync(int id);
        Task<CVDto?> GetMyCVAsync(int userId);
    }
}
