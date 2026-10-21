using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Infrastructure.Configuration;

namespace SupportDesk.Infrastructure.Services;

/// <summary>
/// Generates HMAC-SHA256 signed JSON Web Tokens for authenticated agents (AU-4, AU-7).
/// </summary>
public sealed class JwtTokenService(JwtOptions options, IClock clock) : IJwtTokenService
{
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public (string Token, DateTime ExpiresAtUtc) GenerateToken(int agentId, string email, string fullName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(agentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var now = clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(options.TokenLifetimeMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, agentId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, agentId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Name, fullName),
            new Claim(ClaimTypes.Name, fullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = options.Issuer,
            Audience = options.Audience,
            NotBefore = now,
            IssuedAt = now,
            Expires = expiresAtUtc,
            SigningCredentials = credentials
        };

        var securityToken = _tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = _tokenHandler.WriteToken(securityToken);

        return (tokenString, expiresAtUtc);
    }
}
