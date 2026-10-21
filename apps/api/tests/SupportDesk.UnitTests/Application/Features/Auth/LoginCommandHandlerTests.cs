using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Commands.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Common;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginCommandHandlerTests
{
    private readonly Mock<IAgentRepository> _agentRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenService> _jwtTokenService = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(
            _agentRepository.Object,
            _passwordHasher.Object,
            _jwtTokenService.Object,
            NullLogger<LoginCommandHandler>.Instance);
    }

    private static Agent CreateAgent(int id, string fullName, string email, bool isActive = true, string? passwordHash = "hashed_pw")
    {
        var agent = new Agent(fullName, email, 10, FixedClock.DefaultNow);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(agent, id);
        if (passwordHash is not null)
        {
            agent.SetPasswordHash(passwordHash);
        }
        if (!isActive)
        {
            agent.Deactivate();
        }
        return agent;
    }

    [Fact]
    public async Task HandleAsync_ValidCredentials_ReturnsLoginResponseWithTokenAndAgentInfo()
    {
        // Arrange
        const string email = "alex.turner@northwind-support.example";
        const string password = "Password123!";
        const string expectedToken = "mock.jwt.token";
        var expiresAt = DateTime.UtcNow.AddHours(8);

        var agent = CreateAgent(1, "Alex Turner", email);

        _agentRepository.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _passwordHasher.Setup(p => p.VerifyPassword("hashed_pw", password))
            .Returns(true);

        _jwtTokenService.Setup(j => j.GenerateToken(1, email, "Alex Turner"))
            .Returns((expectedToken, expiresAt));

        var request = new LoginRequest(email, password);

        // Act
        var result = await _handler.HandleAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedToken, result.Token);
        Assert.Equal(expiresAt, result.ExpiresAtUtc);
        Assert.Equal(1, result.Agent.Id);
        Assert.Equal("Alex Turner", result.Agent.FullName);
        Assert.Equal(email, result.Agent.Email);
    }

    [Fact]
    public async Task HandleAsync_UnrecognisedEmail_ThrowsAuthenticationFailedException()
    {
        // Arrange
        _agentRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        var request = new LoginRequest("unknown@northwind-support.example", "Password123!");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _handler.HandleAsync(request, CancellationToken.None));

        Assert.Equal(AuthenticationFailedException.DefaultMessage, ex.Message);
    }

    [Fact]
    public async Task HandleAsync_WrongPassword_ThrowsAuthenticationFailedException()
    {
        // Arrange
        const string email = "alex.turner@northwind-support.example";
        var agent = CreateAgent(1, "Alex Turner", email);

        _agentRepository.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        _passwordHasher.Setup(p => p.VerifyPassword("hashed_pw", "WrongPassword!"))
            .Returns(false);

        var request = new LoginRequest(email, "WrongPassword!");

        // Act & Assert (AU-4: must be identical error message to non-existent email)
        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _handler.HandleAsync(request, CancellationToken.None));

        Assert.Equal(AuthenticationFailedException.DefaultMessage, ex.Message);
    }

    [Fact]
    public async Task HandleAsync_InactiveAgent_ThrowsAuthenticationFailedException()
    {
        // Arrange
        const string email = "yuki.tanaka@northwind-support.example";
        var inactiveAgent = CreateAgent(6, "Yuki Tanaka", email, isActive: false);

        _agentRepository.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveAgent);

        var request = new LoginRequest(email, "Password123!");

        // Act & Assert (AU-4: inactive agent cannot log in and gets identical error)
        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _handler.HandleAsync(request, CancellationToken.None));

        Assert.Equal(AuthenticationFailedException.DefaultMessage, ex.Message);
    }

    [Fact]
    public async Task HandleAsync_AgentWithoutPasswordHash_ThrowsAuthenticationFailedException()
    {
        // Arrange
        const string email = "alex.turner@northwind-support.example";
        var agent = CreateAgent(1, "Alex Turner", email, passwordHash: null);

        _agentRepository.Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        var request = new LoginRequest(email, "Password123!");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _handler.HandleAsync(request, CancellationToken.None));

        Assert.Equal(AuthenticationFailedException.DefaultMessage, ex.Message);
    }
}
