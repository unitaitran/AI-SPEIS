using ai_speis_be.Models.DTOs;
using ai_speis_be.Services.UserService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ai_speis_be.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public sealed class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Lấy thông tin tài khoản của user hiện tại.
        /// </summary>
        [HttpGet("me")]
        [ProducesResponseType(typeof(UserMeResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserMeResponseDto>> GetMe(CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return InvalidTokenProblem();

            var profile = await _userService.GetMyProfileAsync(userId, cancellationToken);
            if (profile is null)
                return NotFound(new ProblemDetails
                {
                    Title = "User not found",
                    Detail = "The authenticated user account could not be found.",
                    Status = StatusCodes.Status404NotFound
                });

            return Ok(profile);
        }

        /// <summary>
        /// Cập nhật ảnh đại diện (avatar).
        /// </summary>
        [HttpPost("me/avatar")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdateAvatar(IFormFile file, CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return InvalidTokenProblem();

            var (success, errorMessage, newImageUrl) = await _userService.UpdateAvatarAsync(userId, file, cancellationToken);
            
            if (!success)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Lỗi tải ảnh",
                    Detail = errorMessage ?? "Đã xảy ra lỗi khi tải ảnh.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            return Ok(new { ImageUrl = newImageUrl });
        }

        /// <summary>
        /// Cập nhật thông tin profile (FullName, PhoneNumber).
        /// </summary>
        [HttpPut("me/profile")]
        [ProducesResponseType(typeof(UserMeResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserMeResponseDto>> UpdateMyProfile(
            [FromBody] UpdateProfileRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return InvalidTokenProblem();

            var result = await _userService.UpdateMyProfileAsync(userId, request, cancellationToken);

            return result.Outcome switch
            {
                UpdateProfileOutcome.Success => Ok(result.Profile),
                UpdateProfileOutcome.UserNotFound => NotFound(new ProblemDetails
                {
                    Title = "User not found",
                    Detail = "The authenticated user account could not be found.",
                    Status = StatusCodes.Status404NotFound
                }),
                UpdateProfileOutcome.PhoneNumberAlreadyExists => BadRequest(new ProblemDetails
                {
                    Title = "Phone number already exists",
                    Detail = "Số điện thoại này đã được sử dụng bởi tài khoản khác.",
                    Status = StatusCodes.Status400BadRequest
                }),
                _ => throw new InvalidOperationException($"Unsupported outcome: {result.Outcome}")
            };
        }

        /// <summary>
        /// Đổi mật khẩu. Yêu cầu xác minh mật khẩu hiện tại.
        /// Tài khoản đăng nhập qua Google không hỗ trợ chức năng này.
        /// </summary>
        [HttpPut("me/security")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordRequestDto request,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
                return InvalidTokenProblem();

            var result = await _userService.ChangePasswordAsync(userId, request, cancellationToken);

            return result.Outcome switch
            {
                ChangePasswordOutcome.Success => Ok(new { Message = "Mật khẩu đã được cập nhật thành công." }),
                ChangePasswordOutcome.WrongCurrentPassword => BadRequest(new ProblemDetails
                {
                    Title = "Incorrect current password",
                    Detail = "Current password is incorrect.",
                    Status = StatusCodes.Status400BadRequest
                }),
                ChangePasswordOutcome.GoogleAccount => BadRequest(new ProblemDetails
                {
                    Title = "Password change not supported",
                    Detail = "This account uses Google Sign-In and does not have a password to change.",
                    Status = StatusCodes.Status400BadRequest
                }),
                ChangePasswordOutcome.UserNotFound => NotFound(new ProblemDetails
                {
                    Title = "User not found",
                    Detail = "The authenticated user account could not be found.",
                    Status = StatusCodes.Status404NotFound
                }),
                _ => throw new InvalidOperationException($"Unsupported outcome: {result.Outcome}")
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private bool TryGetUserId(out int userId)
            => int.TryParse(User.FindFirstValue("UserId"), out userId);

        private UnauthorizedObjectResult InvalidTokenProblem()
            => Unauthorized(new ProblemDetails
            {
                Title = "Invalid authentication token",
                Detail = "The authenticated user identifier is missing or invalid.",
                Status = StatusCodes.Status401Unauthorized
            });
    }
}
