using ai_speis_be.Models.DTOs;
using ai_speis_be.Services.UserService;
using ai_speis_be.Services.TokenService;
using ai_speis_be.Services.EmailService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using System.Net;

namespace ai_speis_be.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ITokenService _tokenService;
        private readonly IEmailSender _emailSender;


        public AuthenticationController(IUserService userService, ITokenService tokenService, IEmailSender emailSender)
        {
            _userService = userService;
            _tokenService = tokenService;
            _emailSender = emailSender;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userService.GetUserByEmailAsync(loginDto.Email);

            if (user == null)
            {
                return Unauthorized(new { Message = "Không tìm thấy tài khoản" });
            }

            if (user.IsLocked)
            {
                return Unauthorized(new { Message = "Tài khoản đã bị khóa." });
            }

            if (!user.Status)
            {
                return Unauthorized(new { Message = "Tài khoản chưa được kích hoạt. Vui lòng kiểm tra email xác nhận." });
            }

            if (user.PasswordHash == null)
            {
                return BadRequest(new { Message = "Tài khoản này được đăng ký qua Google. Vui lòng chọn đăng nhập bằng Google." });
            }

            var isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash);
            if (!isPasswordValid)
            {
                return Unauthorized(new { Message = "Sai tài khoản hoặc mật khẩu" });
            }

            var jwtToken = _tokenService.GenerateToken(user.UserId, user.Role.RoleName, user.FullName, user.Email);


            return Ok(new LoginResponseDto
            {
                JwtToken = jwtToken,
                Role = user.Role.RoleName,
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                ImageUrl = user.ImageUrl,
                IsPremium = user.IsPremium,
                RemainingInterviewQuota = user.RemainingInterviewQuota
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingUser = await _userService.GetUserByEmailAsync(registerDto.Email);
            if (existingUser != null)
            {
                return Conflict(new { Message = "Email đã được sử dụng" });
            }

            var newUser = await _userService.CreateUserAsync(registerDto);
            var confirmationLink = Url.Action(
                nameof(ConfirmEmail),
                "Authentication",
                new { token = newUser.EmailConfirmationToken },
                Request.Scheme);

            if (string.IsNullOrWhiteSpace(confirmationLink))
            {
                return StatusCode(500, new { Message = "Không tạo được link xác nhận email" });
            }

            var emailTemplate = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <title>Xác nhận tài khoản AI-SPEIS</title>
    <style>
        body {{ font-family: 'Inter', 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f7f6; margin: 0; padding: 0; }}
        .email-container {{ max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.05); }}
        .email-header {{ background-color: #EAF6FF; padding: 30px; text-align: center; border-bottom: 2px solid #6FB6E8; }}
        .email-header img {{ max-height: 50px; margin-bottom: 15px; }}
        .email-header h1 {{ margin: 0; color: #3F7FAE; font-size: 24px; font-weight: 700; }}
        .email-body {{ padding: 40px 30px; color: #4a5568; line-height: 1.6; }}
        .email-body p {{ margin-bottom: 20px; font-size: 16px; }}
        .email-body h2 {{ color: #1a202c; font-size: 20px; margin-bottom: 20px; }}
        .btn-container {{ text-align: center; margin: 35px 0; }}
        .btn {{ display: inline-block; background-color: #6FB6E8; color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px; box-shadow: 0 4px 6px rgba(111, 182, 232, 0.25); }}
        .email-footer {{ background-color: #f8fafc; padding: 20px; text-align: center; color: #a0aec0; font-size: 13px; border-top: 1px solid #e2e8f0; }}
        .highlight {{ color: #3F7FAE; font-weight: 600; }}
    </style>
</head>
<body>
    <div class=""email-container"">
        <div class=""email-header"">
            <h1>Chào mừng đến với AI-SPEIS</h1>
        </div>
        <div class=""email-body"">
            <h2>Xin chào <span class=""highlight"">{WebUtility.HtmlEncode(newUser.FullName)}</span>,</h2>
            <p>Cảm ơn bạn đã đăng ký tài khoản tại <strong>AI-SPEIS</strong> - Nền tảng luyện tập phỏng vấn thông minh dành cho người dùng.</p>
            <p>Để hoàn tất quá trình đăng ký và bắt đầu trải nghiệm mock interview cá nhân hóa, vui lòng kích hoạt tài khoản của bạn bằng cách bấm vào nút dưới đây:</p>
            
            <div class=""btn-container"">
                <a href=""{WebUtility.HtmlEncode(confirmationLink)}"" class=""btn"">Kích Hoạt Tài Khoản</a>
            </div>
            
            <p style=""font-size: 14px; color: #718096;"">Lưu ý: Link kích hoạt này chỉ có hiệu lực trong vòng <strong>24 giờ</strong>. Nếu bạn không tạo tài khoản này, vui lòng bỏ qua email này.</p>
        </div>
        <div class=""email-footer"">
            <p>© {DateTime.Now.Year} AI-SPEIS. Đã đăng ký bản quyền.</p>
            <p>Email này được gửi tự động, vui lòng không trả lời.</p>
        </div>
    </div>
</body>
</html>";

            await _emailSender.SendEmailAsync(
                newUser.Email,
                "Xác nhận tài khoản AI-SPEIS",
                emailTemplate);

            return Ok(new { Message = "Đăng ký thành công. Vui lòng kiểm tra email để kích hoạt tài khoản." });

        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromQuery] string token)
        {
            var confirmed = await _userService.ConfirmEmailAsync(token);
            var successMessage = "Kích hoạt tài khoản thành công. Bạn có thể đăng nhập.";
            var failureMessage = "Link xác nhận không hợp lệ hoặc đã hết hạn";
            if (!confirmed)
            {
                var url = $"http://localhost:3000/#login?status=error&message={Uri.EscapeDataString(failureMessage)}";
                return Redirect(url);
            }

            var redirectUrl = $"http://localhost:3000/#login?status=success&message={Uri.EscapeDataString(successMessage)}";
            return Redirect(redirectUrl);
 

        }

        [HttpGet("oauth/google")]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(GoogleCallbackComplete)) ?? "/api/Authentication/oauth/google/complete"
            };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }
        [HttpGet("oauth/google/complete")]
        public async Task<IActionResult> GoogleCallbackComplete()
        {
            var result = await HttpContext.AuthenticateAsync("External");
            if (!result.Succeeded)
            {
                return Unauthorized(new { Message = "Xác thực google thất bại" });
            }

            var email = result.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            var fullName = result.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized(new { Message = "Không tìm thấy email" });
            }

            var user = await _userService.GetUserByEmailAsync(email);
            if (user == null)
            {
                var newUser = await _userService.CreateGoogleUserAsync(email, fullName ?? "Người dùng Google");
                user = await _userService.GetUserByEmailAsync(newUser.Email);
                if (user == null)
                {
                    return StatusCode(500, new { Message = "Lỗi khi tạo tài khoản từ Google" });
                }
            }
            else
            {
                user = await _userService.ConfirmEmailFromGoogleAsync(email) ?? user;
            }

            if (user.IsLocked)
            {
                await HttpContext.SignOutAsync("External");
                return Unauthorized(new { Message = "Tài khoản đã bị khóa." });
            }

            if (!user.Status)
            {
                await HttpContext.SignOutAsync("External");
                return Unauthorized(new { Message = "Tài khoản chưa được kích hoạt hoặc đã bị khóa" });
            }

            var jwtToken = _tokenService.GenerateToken(user.UserId, user.Role.RoleName, user.FullName, user.Email);
            await HttpContext.SignOutAsync("External");

            var redirectUrl = $"http://localhost:3000/#dashboard?token={jwtToken}&userId={user.UserId}&role={user.Role.RoleName}&fullName={Uri.EscapeDataString(user.FullName)}&email={Uri.EscapeDataString(user.Email)}&imageUrl={Uri.EscapeDataString(user.ImageUrl ?? "")}&isPremium={user.IsPremium}&remainingInterviewQuota={user.RemainingInterviewQuota}";
            return Redirect(redirectUrl);
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userService.GetUserByEmailAsync(forgotPasswordDto.Email);
            if (user == null)
            {
                return NotFound(new { Message = "Không tìm thấy tài khoản với email này" });
            }

            if (user.PasswordHash == null)
            {
                return BadRequest(new { Message = "Tài khoản này được đăng ký qua Google. Vui lòng chọn đăng nhập bằng Google." });
            }

            var token = await _userService.InitiatePasswordResetAsync(forgotPasswordDto.Email);
            if (token == null)
            {
                return StatusCode(500, new { Message = "Không thể tạo token đặt lại mật khẩu" });
            }

            var resetLink = $"http://localhost:3000/#reset-password?token={token}";

            var emailTemplate = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <title>Đặt lại mật khẩu AI-SPEIS</title>
    <style>
        body {{ font-family: 'Inter', 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f7f6; margin: 0; padding: 0; }}
        .email-container {{ max-width: 600px; margin: 40px auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.05); }}
        .email-header {{ background-color: #EAF6FF; padding: 30px; text-align: center; border-bottom: 2px solid #6FB6E8; }}
        .email-header h1 {{ margin: 0; color: #3F7FAE; font-size: 24px; font-weight: 700; }}
        .email-body {{ padding: 40px 30px; color: #4a5568; line-height: 1.6; }}
        .email-body p {{ margin-bottom: 20px; font-size: 16px; }}
        .email-body h2 {{ color: #1a202c; font-size: 20px; margin-bottom: 20px; }}
        .btn-container {{ text-align: center; margin: 35px 0; }}
        .btn {{ display: inline-block; background-color: #6FB6E8; color: #ffffff; text-decoration: none; padding: 14px 32px; border-radius: 8px; font-weight: 600; font-size: 16px; box-shadow: 0 4px 6px rgba(111, 182, 232, 0.25); }}
        .email-footer {{ background-color: #f8fafc; padding: 20px; text-align: center; color: #a0aec0; font-size: 13px; border-top: 1px solid #e2e8f0; }}
        .highlight {{ color: #3F7FAE; font-weight: 600; }}
    </style>
</head>
<body>
    <div class=""email-container"">
        <div class=""email-header"">
            <h1>Yêu cầu đặt lại mật khẩu</h1>
        </div>
        <div class=""email-body"">
            <h2>Xin chào <span class=""highlight"">{WebUtility.HtmlEncode(user.FullName)}</span>,</h2>
            <p>Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản <strong>AI-SPEIS</strong> của bạn.</p>
            <p>Để tiến hành đặt mật khẩu mới, vui lòng click vào nút dưới đây:</p>
            
            <div class=""btn-container"">
                <a href=""{WebUtility.HtmlEncode(resetLink)}"" class=""btn"">Đặt Lại Mật Khẩu</a>
            </div>
            
            <p style=""font-size: 14px; color: #718096;"">Lưu ý: Yêu cầu đặt lại mật khẩu này chỉ có hiệu lực trong vòng <strong>1 giờ</strong>. Nếu bạn không yêu cầu việc này, bạn có thể an tâm bỏ qua email này.</p>
        </div>
        <div class=""email-footer"">
            <p>© {DateTime.Now.Year} AI-SPEIS. Đã đăng ký bản quyền.</p>
            <p>Email này được gửi tự động, vui lòng không trả lời.</p>
        </div>
    </div>
</body>
</html>";

            await _emailSender.SendEmailAsync(
                user.Email,
                "Đặt lại mật khẩu tài khoản AI-SPEIS",
                emailTemplate);

            return Ok(new { Message = "Yêu cầu đặt lại mật khẩu thành công. Vui lòng kiểm tra email của bạn." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var success = await _userService.ResetPasswordAsync(resetPasswordDto);
            if (!success)
            {
                return BadRequest(new { Message = "Đường dẫn đặt lại mật khẩu không hợp lệ hoặc đã hết hạn" });
            }

            return Ok(new { Message = "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập bằng mật khẩu mới." });
        }

        [HttpPost("refresh")]
        [Authorize]
        public async Task<IActionResult> RefreshToken()
        {
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized(new { Message = "Token không hợp lệ" });
            }

            var user = await _userService.GetUserByEmailAsync(email);
            if (user == null || user.IsLocked || !user.Status)
            {
                return Unauthorized(new { Message = "Tài khoản không tồn tại hoặc đã bị khóa" });
            }

            var newToken = _tokenService.GenerateToken(user.UserId, user.Role.RoleName, user.FullName, user.Email);
            return Ok(new LoginResponseDto
            {
                JwtToken = newToken,
                Role = user.Role.RoleName,
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                ImageUrl = user.ImageUrl,
                IsPremium = user.IsPremium,
                RemainingInterviewQuota = user.RemainingInterviewQuota
            });
        }
    }


}
