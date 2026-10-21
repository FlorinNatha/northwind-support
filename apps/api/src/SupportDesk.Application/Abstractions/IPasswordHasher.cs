namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Abstraction for secure password hashing and verification (AU-2).
/// </summary>
public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string hashedPassword, string providedPassword);
}
