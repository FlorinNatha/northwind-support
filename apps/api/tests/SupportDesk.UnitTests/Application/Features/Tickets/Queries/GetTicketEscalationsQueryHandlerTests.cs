using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Queries;

public class GetTicketEscalationsQueryHandlerTests
{
    private readonly Mock<ITicketQueries> _queries = new();
    private readonly GetTicketEscalationsQueryHandler _handler;

    public GetTicketEscalationsQueryHandlerTests()
    {
        _handler = new GetTicketEscalationsQueryHandler(_queries.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenTicketDoesNotExist_ThrowsNotFoundException()
    {
        _queries.Setup(q => q.GetDetailAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketDetailDto?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.HandleAsync(999, CancellationToken.None));

        Assert.Contains("Ticket", ex.Message);
        _queries.Verify(q => q.GetEscalationsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenTicketExists_ReturnsEscalationsFromQueries()
    {
        _queries.Setup(q => q.GetDetailAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDetailDto { Id = 42, Reference = "TCK-0042" });

        var expectedEscalations = new List<TicketEscalationDto>
        {
            new()
            {
                Id = 1,
                TicketId = 42,
                FromPriority = TicketPriority.Low,
                ToPriority = TicketPriority.Medium,
                Reason = "Customer requested escalation",
                EscalatedBy = "Alice Lead",
                EscalatedAtUtc = DateTime.UtcNow
            }
        };

        _queries.Setup(q => q.GetEscalationsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEscalations);

        var result = await _handler.HandleAsync(42, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(TicketPriority.Medium, result[0].ToPriority);
        Assert.Equal("Customer requested escalation", result[0].Reason);
    }
}
