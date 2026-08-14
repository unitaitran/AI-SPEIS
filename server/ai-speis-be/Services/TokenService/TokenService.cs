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
            var key = jwtSetting["Key"] ?? throw new InvalidOperationException("Jwt:Key is missing");
            var issuer = jwtSetting["Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is missing");
            var audience = jwtSetting["Audience"] ?? throw new InvalidOperationException("Jwt:Audience is missing");
            var expireMinutes = int.Parse(jwtSetting["ExpireMinutes"] ?? "10080");
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