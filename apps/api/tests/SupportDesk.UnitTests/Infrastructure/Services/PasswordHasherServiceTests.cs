using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public class PasswordHasherServiceTests
{
    private readonly PasswordHasherService _hasher = new();

    [Fact]
    public void HashPassword_ProducesNonEmptyHashedString()
    {
        const string password = "Password123!";
        var hash = _hasher.HashPassword(password);

        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.NotEqual(password, hash);
    }

    [Fact]
    public void HashPassword_SamePasswordProducesDifferentHashesDueToSalting()
    {
        const string password = "Password123!";
        var hash1 = _hasher.HashPassword(password);
        var hash2 = _hasher.HashPassword(password);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        const string password = "Password123!";
        var hash = _hasher.HashPassword(password);

        var isValid = _hasher.VerifyPassword(hash, password);

        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        const string password = "Password123!";
        var hash = _hasher.HashPassword(password);

        var isValid = _hasher.VerifyPassword(hash, "IncorrectPassword!");

        Assert.False(isValid);
    }

    [Theory]
    [InlineData(null, "Password123!")]
    [InlineData("", "Password123!")]
    [InlineData("hash", null)]
    [InlineData("hash", "")]
    public void VerifyPassword_NullOrEmptyInputs_ReturnsFalse(string? hash, string? password)
    {
        var isValid = _hasher.VerifyPassword(hash!, password!);
        Assert.False(isValid);
    }
}
