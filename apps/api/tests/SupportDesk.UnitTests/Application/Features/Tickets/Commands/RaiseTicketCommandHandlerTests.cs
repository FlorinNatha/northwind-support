using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null, int categoryId = 3, int customerId = 1) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", customerId, categoryId, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenCategoryForcesCritical_OverridesToCritical()
    {
        var context = new TicketCommandTestContext();
        var criticalCategory = new Category("Security Incident", requiresSpecialist: true, forcesCriticalPriority: true);

        context.Categories
            .Setup(c => c.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(criticalCategory);

        var detail = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Critical, context.AddedTicket.Priority);
        Assert.NotNull(detail.Triage);
        Assert.Contains("Security Incident", detail.Triage.PriorityReason);
    }

    [Fact]
    public async Task HandleAsync_AssignsAgentAndCalculatesDueAt()
    {
        var context = new TicketCommandTestContext();
        var agent = new Agent("Sarah Connor", "sarah@northwind.example", maxOpenTickets: 5, context.Clock.UtcNow);
        typeof(Agent).GetProperty(nameof(Agent.Id))?.SetValue(agent, 10);

        context.Agents
            .Setup(a => a.GetWorkloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentWorkload(agent, 2)]);

        var detail = await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(10, context.AddedTicket.AssignedAgentId);
        Assert.NotNull(context.AddedTicket.DueAtUtc);
        Assert.NotNull(detail.Triage);
        Assert.True(detail.Triage.IsAssigned);
    }

    [Fact]
    public async Task HandleAsync_WhenNoAgentAvailable_LeavesTicketUnassignedWithReason()
    {
        var context = new TicketCommandTestContext();
        var specialistCategory = new Category("Database", requiresSpecialist: true, forcesCriticalPriority: false);
        context.Categories
            .Setup(c => c.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(specialistCategory);

        var generalAgent = new Agent("Bob General", "bob@example.com", 10, context.Clock.UtcNow);
        context.Agents
            .Setup(a => a.GetWorkloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentWorkload(generalAgent, 1)]);

        var detail = await context.RaiseTicket.HandleAsync(Request(TicketPriority.Medium), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Null(context.AddedTicket.AssignedAgentId);
        Assert.NotNull(detail.Triage);
        Assert.False(detail.Triage.IsAssigned);
        Assert.Contains("No active agents are specialized in 'Database'.", detail.Triage.AssignmentReason);
    }
}
