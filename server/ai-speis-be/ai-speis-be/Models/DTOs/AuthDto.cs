using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.Models.DTOs
{
    public class LoginDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;
        [Required]
        public string Password { get; set; } = null!;
    }
    public class LoginResponseDto
    {
        public string JwtToken { get; set; } = null!;
        public string Role { get; set; } = null!;
        public int UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
    }

    public class RegisterDto
    {
        [Required]
        public string FullName { get; set; } = null!;
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;
        [Required]
        [Phone]
        public string? PhoneNumber { get; set; }
        [Required]
        public string Password { get; set; } = null!;
    }

    public class RegisterResponseDto
    {
        public string JwtToken { get; set; } = null!;
        public string Role { get; set; } = null!;
        public int UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
    }
}
