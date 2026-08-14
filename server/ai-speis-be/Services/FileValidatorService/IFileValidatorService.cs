using Microsoft.AspNetCore.Http;

namespace ai_speis_be.Services.FileValidatorService
{
    public interface IFileValidatorService
    {
        (bool IsValid, string? ErrorMessage) ValidatePdf(IFormFile file);
        (bool IsValid, string? ErrorMessage) ValidateImage(IFormFile file);
    }
}
