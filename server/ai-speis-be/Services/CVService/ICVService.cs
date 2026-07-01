using ai_speis_be.DTOs.CvParsing;
using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Repositories.CVRepo;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ai_speis_be.Services.CVService
{
    public interface ICVService
    {
        Task<(bool Success, string? ErrorMessage, CVDto? CVDto)> UploadCVAsync(int userId, IFormFile file);
        Task<PagedResultDto<CVDto>> GetAllCVsAsync(CVQueryParameters query);
        Task<CVDto?> GetCVByIdAsync(int id);
        Task<PagedResultDto<CVDto>> GetCVByUserIdAsync(int userId, CVQueryParameters query);
        Task<(bool Success, string? ErrorMessage)> DeleteCVAsync(int id);
        Task<CVDto?> GetMyCVAsync(int userId);

        // CV Parsing methods (Step 7)
        Task<(bool Success, string? ErrorMessage)> TriggerParseAsync(int cvFileId, int userId);
        Task<CvParseStatusResponse?> GetParseStatusAsync(int cvFileId);
        Task<CvParsedDataResponse?> GetParsedDataAsync(int cvFileId);
        Task<(bool Success, string? ErrorMessage)> ConfirmParsedDataAsync(int cvFileId, int userId, CvConfirmRequest request);
    }
}
