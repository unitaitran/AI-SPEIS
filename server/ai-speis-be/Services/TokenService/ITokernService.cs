namespace ai_speis_be.Services.TokenService
{
    public interface ITokenService
    {
        string GenerateToken(int userId, string role, string fullName, string email);
    }
}