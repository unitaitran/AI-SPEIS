using System;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace ai_speis_be.Services.FileValidatorService
{
    public class FileValidatorService : IFileValidatorService
    {
        private const long MinSizeInBytes = 100 * 1024; // 0.1 MB = 102,400 bytes
        private const long MaxSizeInBytes = 5 * 1024 * 1024; // 5 MB = 5,242,880 bytes
        private readonly string[] _allowedExtensions = { ".pdf" };
        private readonly string[] _allowedMimeTypes = { "application/pdf" };

        public (bool IsValid, string? ErrorMessage) ValidatePdf(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return (false, "File không được để trống.");
            }

            // Validate Size
            if (file.Length < MinSizeInBytes || file.Length > MaxSizeInBytes)
            {
                double currentSizeMb = file.Length / 1024.0 / 1024.0;
                return (false, $"Kích thước file phải nằm trong khoảng từ 0.1 MB đến 5 MB. (File hiện tại: {currentSizeMb:F2} MB)");
            }

            // Validate Extension
            var ext = Path.GetExtension(file.FileName).ToLower();
            if (Array.IndexOf(_allowedExtensions, ext) < 0)
            {
                return (false, "Chỉ chấp nhận file định dạng PDF (.pdf).");
            }

            // Validate MimeType / ContentType
            if (Array.IndexOf(_allowedMimeTypes, file.ContentType.ToLower()) < 0)
            {
                return (false, "Kiểu nội dung file không hợp lệ. Chỉ chấp nhận file PDF.");
            }

            // Validate File Signature (Magic Number: %PDF)
            try
            {
                using var stream = file.OpenReadStream();
                using var reader = new BinaryReader(stream);
                if (stream.Length >= 4)
                {
                    byte[] signature = reader.ReadBytes(4);
                    string signatureStr = System.Text.Encoding.ASCII.GetString(signature);
                    if (signatureStr != "%PDF")
                    {
                        return (false, "Nội dung file không đúng định dạng PDF hợp lệ.");
                    }
                }
                else
                {
                    return (false, "File có kích thước quá nhỏ để là PDF.");
                }
            }
            catch (Exception)
            {
                return (false, "Không thể đọc nội dung file để kiểm tra định dạng.");
            }

            return (true, null);
        }
    }
}
