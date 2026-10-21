namespace SupportDesk.Application.Contracts.Auth;

/// <summary>
/// Authenticated agent identity info returned on login and current user endpoints (AU-4, AU-5).
/// </summary>
public sealed record AuthAgentDto(int Id, string FullName, string Email);
