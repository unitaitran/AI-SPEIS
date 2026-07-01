using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Repositories.JDRepo;  
namespace ai_speis_be.Services.JDService
{
    public interface IJDService
    {
        Task<(bool Success, string? ErrorMessage, JDDto? JDDto)> UploadJDAsync(int userId, IFormFile file);
        Task<(bool Success, string? ErrorMessage, JDDto? JDDto)> SubmitJDTextAsync(int userId, string rawText);
        Task<PagedResultDto<JDDto>> GetAllJDsAsync(JDQueryParameters query);
        Task<JDDto?> GetJDByIdAsync(int id);
        Task<PagedResultDto<JDDto>> GetJDByUserIdAsync(int userId, JDQueryParameters query);
        Task<(bool Success, string? ErrorMessage)> DeleteJDAsync(int id);
       
    }
}
