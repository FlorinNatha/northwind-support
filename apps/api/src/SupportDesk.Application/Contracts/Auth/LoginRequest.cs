namespace SupportDesk.Application.Contracts.Auth;

/// <summary>
/// Credentials payload sent to sign in (AU-4).
/// </summary>
public sealed record LoginRequest(string Email, string Password);
