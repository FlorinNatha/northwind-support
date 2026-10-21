namespace SupportDesk.Application.Contracts.Auth;

/// <summary>
/// Successful authentication response (AU-4).
/// </summary>
public sealed record LoginResponse(
    string Token,
    DateTime ExpiresAtUtc,
    AuthAgentDto Agent);
