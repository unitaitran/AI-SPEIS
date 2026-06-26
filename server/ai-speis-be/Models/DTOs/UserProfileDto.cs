using System.ComponentModel.DataAnnotations;

namespace ai_speis_be.Models.DTOs
{
    // ─── Response ────────────────────────────────────────────────────────────

    public sealed class UserMeResponseDto
    {
        public int UserId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? PhoneNumber { get; init; }
        public string Role { get; init; } = string.Empty;
        public bool Status { get; init; }
        public bool IsLocked { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public bool HasPassword { get; init; }
    }

    // ─── Requests ────────────────────────────────────────────────────────────

    public sealed class UpdateProfileRequestDto
    {
        [Required(ErrorMessage = "Họ và tên là bắt buộc.")]
        [MaxLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự.")]
        public string FullName { get; set; } = null!;

        [MaxLength(32, ErrorMessage = "Số điện thoại không được vượt quá 32 ký tự.")]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Số điện thoại phải có 10 chữ số và bắt đầu bằng 0.")]
        public string? PhoneNumber { get; set; }
    }

    public sealed class ChangePasswordRequestDto
    {
        public string? CurrentPassword { get; set; }

        [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
        [MinLength(6, ErrorMessage = "Mật khẩu mới phải ít nhất 6 ký tự.")]
        public string NewPassword { get; set; } = null!;

        [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc.")]
        [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmNewPassword { get; set; } = null!;
    }

    // ─── Result types ────────────────────────────────────────────────────────

    public enum UpdateProfileOutcome
    {
        Success,
        UserNotFound,
        PhoneNumberAlreadyExists
    }

    public sealed class UpdateProfileResult
    {
        public UpdateProfileOutcome Outcome { get; init; }
        public UserMeResponseDto? Profile { get; init; }

        public UpdateProfileResult(UpdateProfileOutcome outcome, UserMeResponseDto? profile = null)
        {
            Outcome = outcome;
            Profile = profile;
        }
    }

    public enum ChangePasswordOutcome
    {
        Success,
        UserNotFound,
        WrongCurrentPassword,
        GoogleAccount      // tài khoản đăng nhập bằng Google không có password
    }

    public sealed class ChangePasswordResult
    {
        public ChangePasswordOutcome Outcome { get; init; }
        public ChangePasswordResult(ChangePasswordOutcome outcome) => Outcome = outcome;
    }
}
