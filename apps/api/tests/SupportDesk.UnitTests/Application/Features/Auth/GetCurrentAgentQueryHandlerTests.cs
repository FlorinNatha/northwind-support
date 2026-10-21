using Moq;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Queries.GetCurrentAgent;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Common;
using SupportDesk.Domain.Repositories;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class GetCurrentAgentQueryHandlerTests
{
    private readonly Mock<IAgentRepository> _agentRepository = new();
    private readonly GetCurrentAgentQueryHandler _handler;

    public GetCurrentAgentQueryHandlerTests()
    {
        _handler = new GetCurrentAgentQueryHandler(_agentRepository.Object);
    }

    private static Agent CreateAgent(int id, string fullName, string email, bool isActive = true)
    {
        var agent = new Agent(fullName, email, 10, FixedClock.DefaultNow);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(agent, id);
        if (!isActive)
        {
            agent.Deactivate();
        }
        return agent;
    }

    [Fact]
    public async Task HandleAsync_ActiveAgentExists_ReturnsAgentDto()
    {
        // Arrange
        var agent = CreateAgent(1, "Alex Turner", "alex.turner@northwind-support.example");
        _agentRepository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        // Act
        var result = await _handler.HandleAsync(1, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Alex Turner", result.FullName);
        Assert.Equal("alex.turner@northwind-support.example", result.Email);
    }

    [Fact]
    public async Task HandleAsync_AgentNotFound_ThrowsAuthenticationFailedException()
    {
        // Arrange
        _agentRepository.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Agent?)null);

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _handler.HandleAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_InactiveAgent_ThrowsAuthenticationFailedException()
    {
        // Arrange
        var inactiveAgent = CreateAgent(6, "Yuki Tanaka", "yuki.tanaka@northwind-support.example", isActive: false);
        _agentRepository.Setup(r => r.GetByIdAsync(6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveAgent);

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            _handler.HandleAsync(6, CancellationToken.None));
    }
}
