using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Repositories.UserRepo;

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
                Status = true,
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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };
            return await _userRepository.CreateUserAsync(user);
        }
    }

}