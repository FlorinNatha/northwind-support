using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static EscalateTicketRequest Request(string reason = "Issue escalated per customer request", string escalatedBy = "Support Lead") =>
        new(reason, escalatedBy);

    [Fact]
    public async Task HandleAsync_EscalatesPriority_RecomputesDueAt_AndRecordsAudit()
    {
        var context = new TicketCommandTestContext();
        var ticket = Ticket.Raise("TCK-0010", "System slow", "Database latency", customerId: 1, categoryId: 1, TicketPriority.Medium, context.Clock.UtcNow, context.Clock.UtcNow.AddHours(24));
        typeof(Ticket).GetProperty(nameof(Ticket.Id))?.SetValue(ticket, 10);
        context.ExistingTicket(ticket);

        var result = await context.EscalateTicket.HandleAsync(10, Request("Customer requested urgent escalation", "Alice Lead"), CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Single(ticket.Escalations);
        var esc = ticket.Escalations.First();
        Assert.Equal(TicketPriority.Medium, esc.FromPriority);
        Assert.Equal(TicketPriority.High, esc.ToPriority);
        Assert.Equal("Customer requested urgent escalation", esc.Reason);
        Assert.Equal("Alice Lead", esc.EscalatedBy);
        Assert.NotNull(result.Escalation);
        Assert.Equal(TicketPriority.High, result.Escalation.ToPriority);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsCritical_ThrowsBusinessRuleViolation()
    {
        var context = new TicketCommandTestContext();
        var ticket = Ticket.Raise("TCK-0010", "Major outage", "All down", customerId: 1, categoryId: 1, TicketPriority.Critical, context.Clock.UtcNow);
        typeof(Ticket).GetProperty(nameof(Ticket.Id))?.SetValue(ticket, 10);
        context.ExistingTicket(ticket);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(10, Request(), CancellationToken.None));

        Assert.Contains("Critical", ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketIsClosed_ThrowsBusinessRuleViolation()
    {
        var context = new TicketCommandTestContext();
        var ticket = Ticket.Raise("TCK-0010", "Old issue", "Done", customerId: 1, categoryId: 1, TicketPriority.Low, context.Clock.UtcNow);
        typeof(Ticket).GetProperty(nameof(Ticket.Id))?.SetValue(ticket, 10);
        ticket.ChangeStatus(TicketStatus.Closed, context.Clock.UtcNow);
        context.ExistingTicket(ticket);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => context.EscalateTicket.HandleAsync(10, Request(), CancellationToken.None));

        Assert.Contains("closed", ex.Message);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTicketNotFound_ThrowsNotFound()
    {
        var context = new TicketCommandTestContext();
        context.Tickets.Setup(t => t.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Ticket?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(999, Request(), CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_ReassignsWhenCurrentAgentNotEligible()
    {
        var context = new TicketCommandTestContext();
        // Category requiring specialist (categoryId 2)
        var category = new Category("Database", requiresSpecialist: true, forcesCriticalPriority: false);
        typeof(Category).GetProperty(nameof(Category.Id))?.SetValue(category, 2);
        context.Categories.Setup(c => c.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(category);

        // General agent (not specialized in cat 2)
        var generalAgent = new Agent("General Agent", "gen@test.com", 10, context.Clock.UtcNow);
        typeof(Agent).GetProperty(nameof(Agent.Id))?.SetValue(generalAgent, 1);
        context.Agents.Setup(a => a.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(generalAgent);

        // Specialist agent (specialized in cat 2)
        var specialistAgent = new Agent("Specialist Agent", "spec@test.com", 10, context.Clock.UtcNow);
        typeof(Agent).GetProperty(nameof(Agent.Id))?.SetValue(specialistAgent, 2);
        specialistAgent.AddSpecialization(2);

        context.Agents.Setup(a => a.GetWorkloadsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new AgentWorkload(generalAgent, 1),
                new AgentWorkload(specialistAgent, 0)
            ]);

        var ticket = Ticket.Raise("TCK-0010", "Db Lock", "Deadlocks occurring", customerId: 1, categoryId: 2, TicketPriority.Medium, context.Clock.UtcNow);
        typeof(Ticket).GetProperty(nameof(Ticket.Id))?.SetValue(ticket, 10);
        ticket.AssignTo(1, context.Clock.UtcNow);
        context.ExistingTicket(ticket);

        var result = await context.EscalateTicket.HandleAsync(10, Request(), CancellationToken.None);

        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.Equal(1, result.Escalation.FromAgent?.Id);
        Assert.Equal(2, result.Escalation.ToAgent?.Id);
    }
}
