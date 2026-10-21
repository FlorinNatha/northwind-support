using Microsoft.AspNetCore.Identity;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Infrastructure.Services;

/// <summary>
/// Password hashing using ASP.NET Core Identity PasswordHasher (PBKDF2 with salt) (AU-2).
/// </summary>
public sealed class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<Agent> _hasher = new();

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _hasher.HashPassword(null!, password);
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword) || string.IsNullOrWhiteSpace(providedPassword))
        {
            return false;
        }

        var result = _hasher.VerifyHashedPassword(null!, hashedPassword, providedPassword);
        return result != PasswordVerificationResult.Failed;
    }
}
