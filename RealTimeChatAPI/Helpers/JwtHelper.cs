using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RealTimeChatAPI.Models;
using System.Security.Claims;
using System.Text;

namespace RealTimeChatAPI.Helpers;

public class JwtHelper(IConfiguration configuration)
{
    public string GenerateToken(User user)
    {
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Name, user.Name)
        ];

        var secret = GetRequiredValue("Jwt:Secret");
        var issuer = GetRequiredValue("Jwt:Issuer");
        var audience = GetRequiredValue("Jwt:Audience");
        var expirationInMinutes = configuration.GetValue<int>("Jwt:ExpirationInMinutes");

        if (Encoding.UTF8.GetByteCount(secret) < 32)
            throw new InvalidOperationException("Jwt:Secret must contain at least 32 bytes.");

        if (expirationInMinutes <= 0)
            throw new InvalidOperationException("Jwt:ExpirationInMinutes must be greater than zero.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expirationInMinutes),
            SigningCredentials = credentials,
            Issuer = issuer,
            Audience = audience
        };

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(tokenDescriptor);
    }

    private string GetRequiredValue(string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{key} is required.");

        return value;
    }
}
