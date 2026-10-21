using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SupportDesk.Infrastructure.Configuration;
using SupportDesk.Infrastructure.Services;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public class JwtTokenServiceTests
{
    private readonly JwtOptions _options = new()
    {
        SecretKey = "NorthwindSupportDeskDevJwtSecretKey2026!",
        Issuer = "SupportDesk",
        Audience = "SupportDeskClient",
        TokenLifetimeMinutes = 480
    };

    [Fact]
    public void GenerateToken_ValidAgent_ReturnsTokenWithCorrectClaimsAndExpiration()
    {
        // Arrange
        var clock = new FixedClock(new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc));
        var service = new JwtTokenService(_options, clock);

        const int agentId = 1;
        const string email = "alex.turner@northwind-support.example";
        const string fullName = "Alex Turner";

        // Act
        var (tokenString, expiresAtUtc) = service.GenerateToken(agentId, email, fullName);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(tokenString));
        Assert.Equal(clock.UtcNow.AddMinutes(480), expiresAtUtc);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        Assert.Equal(_options.Issuer, jwt.Issuer);
        Assert.Contains(_options.Audience, jwt.Audiences);

        var subClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == ClaimTypes.NameIdentifier);
        Assert.NotNull(subClaim);
        Assert.Equal(agentId.ToString(), subClaim.Value);

        var emailClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email || c.Type == ClaimTypes.Email);
        Assert.NotNull(emailClaim);
        Assert.Equal(email, emailClaim.Value);

        var nameClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Name || c.Type == ClaimTypes.Name);
        Assert.NotNull(nameClaim);
        Assert.Equal(fullName, nameClaim.Value);

        var jtiClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        Assert.NotNull(jtiClaim);
        Assert.NotEmpty(jtiClaim.Value);
    }

    [Theory]
    [InlineData(0, "test@example.com", "Test User")]
    [InlineData(-1, "test@example.com", "Test User")]
    [InlineData(1, "", "Test User")]
    [InlineData(1, "test@example.com", "")]
    public void GenerateToken_InvalidArguments_ThrowsException(int agentId, string email, string fullName)
    {
        var clock = new FixedClock(new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc));
        var service = new JwtTokenService(_options, clock);

        Assert.ThrowsAny<ArgumentException>(() => service.GenerateToken(agentId, email, fullName));
    }
}
