using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Repositories.UserRepo;
using System.Security.Cryptography;

namespace ai_speis_be.Services.UserService
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<IEnumerable<UserResponseDto>> GetUsersAsync()
        {
            var users = await _userRepository.GetUsersAsync();

            return users.Select(user => new UserResponseDto
            {
                UserId = user.UserId,
                RoleId = user.RoleId,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            });
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
                RoleId = 2, // Default to regular user role
                FullName = fullName,
                Email = email,
                Status = true,
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
    }

}
