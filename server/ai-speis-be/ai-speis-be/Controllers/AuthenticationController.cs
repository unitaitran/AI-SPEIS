using ai_speis_be.Models.DTOs;
using ai_speis_be.Services.UserService;
using ai_speis_be.Services.TokenService;
using Microsoft.AspNetCore.Mvc;

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
                return Unauthorized(new { Message = "Invalid email or password" });
            }

            var isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash);
            if (!isPasswordValid)
            {
                return Unauthorized(new { Message = "Invalid email or password" });
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
                return Conflict(new { Message = "Email already in use" });
            }

            var newUser = await _userService.CreateUserAsync(registerDto);
            var createdUser = await _userService.GetUserByEmailAsync(newUser.Email);
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
    }
}
