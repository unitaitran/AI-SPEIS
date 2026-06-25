using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Repositories.UserRepo;
using ai_speis_be.Services.FileValidatorService;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using System.IO;

namespace ai_speis_be.Services.UserService
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IFileValidatorService _fileValidatorService;

        public UserService(IUserRepository userRepository, IFileValidatorService fileValidatorService)
        {
            _userRepository = userRepository;
            _fileValidatorService = fileValidatorService;
        }

        public Task<PagedResultDto<AdminUserListItemDto>> GetUsersAsync(
            AdminUserQueryDto query,
            CancellationToken cancellationToken = default)
        {
            return _userRepository.GetUsersAsync(query, cancellationToken);
        }

        public Task<AdminUserDetailDto?> GetAdminUserDetailAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            return _userRepository.GetAdminUserDetailAsync(
                userId,
                cancellationToken);
        }

        public async Task<LockUserResult> LockUserAsync(
            int userId,
            int actingUserId,
            string? reason,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetUserByIdAsync(
                userId,
                cancellationToken);

            if (user is null)
            {
                return new LockUserResult(LockUserOutcome.UserNotFound);
            }

            if (user.UserId == actingUserId)
            {
                return new LockUserResult(
                    LockUserOutcome.CannotLockSelf,
                    user);
            }

            if (HasProtectedAdministrativeRole(user.Role.RoleName))
            {
                return new LockUserResult(
                    LockUserOutcome.ProtectedRole,
                    user);
            }

            if (user.IsLocked)
            {
                return new LockUserResult(
                    LockUserOutcome.AlreadyLocked,
                    user);
            }

            user.IsLocked = true;
            user.Status = false;
            user.LockReason = NormalizeReason(reason);
            user.LockedAt = DateTime.UtcNow;
            user.LockedByUserId = actingUserId;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateUserAsync(user, cancellationToken);

            return new LockUserResult(LockUserOutcome.Locked, user);
        }

        public async Task<UnlockUserResult> UnlockUserAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetUserByIdAsync(
                userId,
                cancellationToken);

            if (user is null)
            {
                return new UnlockUserResult(UnlockUserOutcome.UserNotFound);
            }

            if (!user.IsLocked)
            {
                return new UnlockUserResult(
                    UnlockUserOutcome.NotLocked,
                    user);
            }

            user.IsLocked = false;
            user.Status = true;
            user.LockReason = null;
            user.LockedAt = null;
            user.LockedByUserId = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateUserAsync(user, cancellationToken);

            return new UnlockUserResult(UnlockUserOutcome.Unlocked, user);
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _userRepository.GetUserByEmailAsync(email);
        }
        public async Task<User> CreateUserAsync(RegisterDto registerDto)
        {
            var user = new User
            {
                RoleId = 2, // Default to regular user role
                FullName = registerDto.FullName,
                Email = registerDto.Email,
                PhoneNumber = registerDto.PhoneNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                Status = false,
                ImageUrl = "/AvatarImage/avt_default.jpg",
                EmailConfirmationToken = GenerateEmailConfirmationToken(),
                EmailConfirmationTokenExpiresAt = DateTime.UtcNow.AddHours(24),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };

            return await _userRepository.CreateUserAsync(user);
        }
        public async Task<User> CreateGoogleUserAsync(string email, string fullName)
        {
            var user = new User{
                RoleId = 5, // Default to regular user role
                FullName = fullName,
                Email = email,
                Status = true,
                ImageUrl = "/AvatarImage/avt_default.jpg",      
                PasswordHash = null,
                PhoneNumber = null,
                EmailConfirmedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };
            return await _userRepository.CreateUserAsync(user);
        }

        public async Task<bool> ConfirmEmailAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var user = await _userRepository.GetUserByEmailConfirmationTokenAsync(token);
            if (user == null ||
                user.IsLocked ||
                user.EmailConfirmationTokenExpiresAt is null ||
                user.EmailConfirmationTokenExpiresAt <= DateTime.UtcNow)
            {
                return false;
            }

            user.Status = true;
            user.EmailConfirmedAt = DateTime.UtcNow;
            user.EmailConfirmationToken = null;
            user.EmailConfirmationTokenExpiresAt = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateUserAsync(user);
            return true;
        }

        public async Task<User?> ConfirmEmailFromGoogleAsync(string email)
        {
            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null)
            {
                return null;
            }

            if (user.IsLocked)
            {
                return user;
            }
            //nếu user đã tồn tại và xác nhận email => trả về user, không cần cập nhật gì

            if (user.EmailConfirmedAt !=  null &&
                string.IsNullOrWhiteSpace(user.EmailConfirmationToken))
            {
                return user;
            }

            user.Status = true;
            user.EmailConfirmedAt = DateTime.UtcNow;
            user.EmailConfirmationToken = null;
            user.EmailConfirmationTokenExpiresAt = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateUserAsync(user);
            return user;
        }

        private static string GenerateEmailConfirmationToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        }

        private static string? NormalizeReason(string? reason)
        {
            return string.IsNullOrWhiteSpace(reason)
                ? null
                : reason.Trim();
        }

        private static bool HasProtectedAdministrativeRole(string roleName)
        {
            var normalizedRoleName = new string(
                roleName
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToUpperInvariant)
                    .ToArray());

            return normalizedRoleName is "ADMIN" or "SYSTEMADMIN";
        }
    
        private static string GeneratePasswordResetToken(int userId)
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        }

        public async Task<string?> InitiatePasswordResetAsync(string email)
        {
            var user = await _userRepository.GetUserByEmailAsync(email);
            if (user == null)
            {
                return null;
            }

            var token = GeneratePasswordResetToken(user.UserId);
            user.PasswordResetToken = token;
            user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateUserAsync(user);
            return token;
        }

        public async Task<bool> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
        {
            var user = await _userRepository.GetUserByPasswordResetTokenAsync(resetPasswordDto.Token);
            if (user == null || 
                user.PasswordResetTokenExpiresAt == null || 
                user.PasswordResetTokenExpiresAt <= DateTime.UtcNow)
            {
                return false;
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(resetPasswordDto.NewPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiresAt = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateUserAsync(user);
            return true;
        }

        // ── Profile & Security ─────────────────────────────────────────────────

        public async Task<UserMeResponseDto?> GetMyProfileAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetUserByIdAsync(userId, cancellationToken);
            return user is null ? null : MapToUserMeResponseDto(user);
        }

        public async Task<UpdateProfileResult> UpdateMyProfileAsync(
            int userId,
            UpdateProfileRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetUserByIdAsync(userId, cancellationToken);
            if (user is null)
                return new UpdateProfileResult(UpdateProfileOutcome.UserNotFound);

            if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
            {
                var cleanPhone = dto.PhoneNumber.Trim();
                var phoneUser = await _userRepository.GetUserByPhoneNumberAsync(cleanPhone);
                if (phoneUser != null && phoneUser.UserId != userId)
                {
                    return new UpdateProfileResult(UpdateProfileOutcome.PhoneNumberAlreadyExists);
                }
            }

            user.FullName = dto.FullName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber)
                ? null
                : dto.PhoneNumber.Trim();
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateUserAsync(user, cancellationToken);
            return new UpdateProfileResult(UpdateProfileOutcome.Success, MapToUserMeResponseDto(user));
        }
        public async Task<(bool Success, string? ErrorMessage, string? NewImageUrl)> UpdateAvatarAsync(
            int userId,
            IFormFile file,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetUserByIdAsync(userId, cancellationToken);
            if (user is null)
                return (false, "Không tìm thấy người dùng.", null);

            var (isValid, errorMessage) = _fileValidatorService.ValidateImage(file);
            if (!isValid)
                return (false, errorMessage, null);

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "AvatarImage");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"user_{userId}_{Guid.NewGuid().ToString("N").Substring(0, 8)}{Path.GetExtension(file.FileName).ToLower()}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            // Xoá ảnh cũ nếu không phải ảnh mặc định
            if (!string.IsNullOrEmpty(user.ImageUrl) && !user.ImageUrl.Contains("avt_default.jpg"))
            {
                var oldFileName = Path.GetFileName(user.ImageUrl);
                var oldFilePath = Path.Combine(uploadsFolder, oldFileName);
                if (File.Exists(oldFilePath))
                {
                    try { File.Delete(oldFilePath); } catch { /* Bỏ qua lỗi xoá file */ }
                }
            }

            user.ImageUrl = $"/AvatarImage/{uniqueFileName}";
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateUserAsync(user, cancellationToken);

            return (true, null, user.ImageUrl);
        }

        public async Task<ChangePasswordResult> ChangePasswordAsync(
            int userId,
            ChangePasswordRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            var user = await _userRepository.GetUserByIdAsync(userId, cancellationToken);
            if (user is null)
                return new ChangePasswordResult(ChangePasswordOutcome.UserNotFound);

            // Nếu tài khoản đã có mật khẩu => bắt buộc kiểm tra mật khẩu hiện tại
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                if (string.IsNullOrEmpty(dto.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
                    return new ChangePasswordResult(ChangePasswordOutcome.WrongCurrentPassword);
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateUserAsync(user, cancellationToken);
            return new ChangePasswordResult(ChangePasswordOutcome.Success);
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private static UserMeResponseDto MapToUserMeResponseDto(User user) => new()
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.RoleName,
            Status = user.Status,
            IsLocked = user.IsLocked,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            HasPassword = !string.IsNullOrEmpty(user.PasswordHash)
        };
    }
}
