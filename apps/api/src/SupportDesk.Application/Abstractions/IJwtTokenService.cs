namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Abstraction for issuing signed JWT tokens (AU-4, AU-7).
/// </summary>
public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(int agentId, string email, string fullName);
}
