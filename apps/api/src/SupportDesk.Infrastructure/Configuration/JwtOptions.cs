namespace SupportDesk.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for JWT token generation and validation (AU-7).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Cryptographic secret key used to sign tokens with HMAC-SHA256 (minimum 256 bits / 32 characters).
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Valid token issuer.
    /// </summary>
    public string Issuer { get; set; } = "SupportDesk";

    /// <summary>
    /// Valid token audience.
    /// </summary>
    public string Audience { get; set; } = "SupportDeskClient";

    /// <summary>
    /// Token lifetime in minutes. Standard default is 480 minutes (8-hour shift).
    /// </summary>
    public int TokenLifetimeMinutes { get; set; } = 480;
}
