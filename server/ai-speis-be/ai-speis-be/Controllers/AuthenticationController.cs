using ai_speis_be.Models.DTOs;
using ai_speis_be.Services.UserService;
using ai_speis_be.Services.TokenService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;

namespace ai_speis_be.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ITokenService _tokenService;

        public AuthenticationController(IUserService userService, ITokenService tokenService)
        {
            _userService = userService;
            _tokenService = tokenService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userService.GetUserByEmailAsync(loginDto.Email);

            if (user == null || !user.Status)
            {
                return Unauthorized(new { Message = "Không tìm thấy tài khoản" });
            }

            if(user.PasswordHash == null)
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
                Email = user.Email
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto)
        {
            if(!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingUser = await  _userService.GetUserByEmailAsync(registerDto.Email);
            if(existingUser != null)
            {
                return Conflict(new { Message = "Email đã được sử dụng" });
            }

            var newUser = await _userService.CreateUserAsync(registerDto);
            var createdUser = await _userService.GetUserByEmailAsync(newUser.Email);
            if (createdUser == null)
            {
                return StatusCode(500, new { Message = "Đăng ký thành công nhưng không tìm thấy tài khoản sau đó" });
            }
            var jwtToken = _tokenService.GenerateToken(createdUser.UserId, createdUser.Role.RoleName, createdUser.FullName, createdUser.Email);
            return Ok(new RegisterResponseDto
            {
                JwtToken = jwtToken,
                Role = createdUser.Role.RoleName,
                UserId = createdUser.UserId,
                FullName = createdUser.FullName,
                Email = createdUser.Email
            });

        }
        [HttpGet("oauth/google")]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties {
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
                var newUser = await _userService.CreateGoogleUserAsync(email, fullName ?? "Google User");
                user = await _userService.GetUserByEmailAsync(newUser.Email);
                if (user == null)
                {
                    return StatusCode(500, new { Message = "Lỗi khi tạo tài khoản từ Google" });
                }
            }

            var jwtToken = _tokenService.GenerateToken(user.UserId, user.Role.RoleName, user.FullName, user.Email);
            await HttpContext.SignOutAsync("External");

            return Ok(new LoginResponseDto
            {
                JwtToken = jwtToken,
                Role = user.Role.RoleName,
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email
            });
        }
    }
}
