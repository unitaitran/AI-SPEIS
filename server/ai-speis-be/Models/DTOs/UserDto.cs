namespace ai_speis_be.Models.DTOs
{
    public class UserResponseDto
    {
        public int UserId { get; set; }
       
        public int RoleId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public bool Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

    
    }
}
