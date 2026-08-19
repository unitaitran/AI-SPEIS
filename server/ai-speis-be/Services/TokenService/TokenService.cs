using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
namespace ai_speis_be.Services.TokenService
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public string GenerateToken(int userId, string role, string fullName, string email)
        {
            var jwtSetting = _configuration.GetSection("Jwt");
            var key = jwtSetting["Key"]
                ?? _configuration["JWT_KEY"]
                ?? _configuration["Jwt__Key"]
                ?? throw new InvalidOperationException("FATAL: Jwt:Key is missing. Add Jwt:Key or JWT_KEY to environment variables.");
            var issuer = jwtSetting["Issuer"] ?? _configuration["Jwt__Issuer"] ?? "ai-speis-be";
            var audience = jwtSetting["Audience"] ?? _configuration["Jwt__Audience"] ?? "ai-speis-fe";
            var expireMinutes = int.Parse(jwtSetting["ExpireMinutes"] ?? _configuration["Jwt__ExpireMinutes"] ?? "10080");
            var claim = new List<Claim>
            {
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role),
                new Claim(ClaimTypes.Name, fullName),
                new Claim("UserId", userId.ToString())
            };
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claim),
                Expires = DateTime.UtcNow.AddMinutes(expireMinutes),
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256Signature)
            }; 
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
            
        }
    }
}