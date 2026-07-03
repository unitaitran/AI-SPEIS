using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Repositories.JDRepo;
using ai_speis_be.DTOs.JdParsing;

namespace ai_speis_be.Services.JDService
{
    public interface IJDService
    {
        Task<(bool Success, string? ErrorMessage, JDDto? JDDto)> UploadJDAsync(int userId, IFormFile file);
        Task<(bool Success, string? ErrorMessage, JDDto? JDDto)> SubmitJDTextAsync(int userId, string fileName, string rawText);
        Task<PagedResultDto<JDDto>> GetAllJDsAsync(JDQueryParameters query);
        Task<JDDto?> GetJDByIdAsync(int id);
        Task<PagedResultDto<JDDto>> GetJDByUserIdAsync(int userId, JDQueryParameters query);
        Task<(bool Success, string? ErrorMessage)> DeleteJDAsync(int id);
        
        Task<bool> TriggerParseAsync(int userId, int jdId);
        Task<object?> GetParseStatusAsync(int userId, int jdId);
        Task<JdParsedDataResponse?> GetParsedDataAsync(int userId, int jdId);
        Task<bool> ConfirmParsedDataAsync(int userId, int jdId, JdConfirmRequest request);
        Task<CvJdMatchResultResponse?> MatchCvToJdAsync(int userId, int jdId, int cvId);
    }
}
