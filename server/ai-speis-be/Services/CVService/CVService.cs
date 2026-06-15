using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using ai_speis_be.Repositories.CVRepo;
using ai_speis_be.Services.FileValidatorService;

namespace ai_speis_be.Services.CVService
{
    public class CVService : ICVService
    {
        private readonly ICVRepository _cvRepository;
        private readonly IFileValidatorService _fileValidatorService;

        public CVService(ICVRepository cvRepository, IFileValidatorService fileValidatorService)
        {
            _cvRepository = cvRepository;
            _fileValidatorService = fileValidatorService;
        }

        public async Task<(bool Success, string? ErrorMessage, CVDto? CVDto)> UploadCVAsync(int userId, IFormFile file)
        {
            // 1. Validate file using FileValidatorService
            var (isValid, validationError) = _fileValidatorService.ValidatePdf(file);
            if (!isValid)
            {
                return (false, validationError, null);
            }

            try
            {
                // 2. Prepare upload folder inside wwwroot
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "cvs");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // 3. Generate a unique file name to avoid collisions
                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName).ToLower()}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // 4. Save file to disk
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                // 4.5. Soft delete previous active CV
                var activeCV = await _cvRepository.GetActiveCVByUserIdAsync(userId);
                if (activeCV != null)
                {
                    activeCV.Status = CVFileStatus.Archived;
                    activeCV.UpdatedAt = DateTime.Now;
                    await _cvRepository.UpdateCVAsync(activeCV);
                }

                // 5. Save metadata to database
                var cvFile = new CVFile
                {
                    UserId = userId,
                    FileName = file.FileName, // Keep original file name for display
                    FilePath = $"/uploads/cvs/{uniqueFileName}", // Web-accessible relative path
                    FileSize = file.Length,
                    FileType = file.ContentType,
                    Status = CVFileStatus.Pending,
                    UploadedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                var savedCV = await _cvRepository.AddCVAsync(cvFile);

                return (true, null, MapToDto(savedCV));
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi hệ thống khi tải file: {ex.Message}", null);
            }
        }

        public async Task<IEnumerable<CVDto>> GetAllCVsAsync()
        {
            var cvs = await _cvRepository.GetAllCVAsync();
            return cvs.Select(MapToDto);
        }

        public async Task<CVDto?> GetCVByIdAsync(int id)
        {
            var cv = await _cvRepository.GetCVByIdAsync(id);
            return cv != null ? MapToDto(cv) : null;
        }

        public async Task<CVDto?> GetCVByUserIdAsync(int userId)
        {
            var cv = await _cvRepository.GetCVByUserIdAsync(userId);
            return cv != null ? MapToDto(cv) : null;
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteCVAsync(int id)
        {
            var cv = await _cvRepository.GetCVByIdAsync(id);
            if (cv == null)
            {
                return (false, "Không tìm thấy file CV.");
            }

            var cvDeleted = await _cvRepository.DeleteCVAsync(cv.CVFileId);
            return (true, null); 
        }

        public async Task<CVDto?> GetMyCVAsync(int userId)
        { 
            var cv = await _cvRepository.GetActiveCVByUserIdAsync(userId);
            return cv != null ? MapToDto(cv) : null;
        }
        private CVDto MapToDto(CVFile cv)
        {
            return new CVDto
            {
                CVFileId = cv.CVFileId,
                UserId = cv.UserId,
                FileName = cv.FileName,
                FilePath = cv.FilePath,
                FileSize = cv.FileSize,
                FileType = cv.FileType,
                Status = cv.Status,
                UploadedAt = cv.UploadedAt,
                UpdatedAt = cv.UpdatedAt
            };
        }
    }
}
