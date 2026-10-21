using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketTests
{
    private static readonly DateTime Later = FixedClock.DefaultNow.AddHours(3);

    [Fact]
    public void Raise_StartsNewUnassignedAndWithoutADueDate()
    {
        var ticket = Ticket.Raise(
            "TCK-0041", "  Cannot log in  ", "  Happens for every user.  ", 1, 3,
            TicketPriority.High, FixedClock.DefaultNow);

        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal("Cannot log in", ticket.Title);
        Assert.Equal("Happens for every user.", ticket.Description);
        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(ticket.DueAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenResolving_StampsResolutionAndUpdateTimes()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.InProgress).Build();

        ticket.ChangeStatus(TicketStatus.Resolved, Later);

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(Later, ticket.ResolvedAtUtc);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
        Assert.False(ticket.IsOpen);
    }

    [Fact]
    public void ChangeStatus_WhenReopening_ClearsTheResolutionTime()
    {
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build();

        ticket.ChangeStatus(TicketStatus.InProgress, Later);

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Null(ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenClosingAnUnresolvedTicket_RecordsAResolutionTime()
    {
        var ticket = new TicketBuilder().WithStatus(TicketStatus.Open).Build();

        ticket.ChangeStatus(TicketStatus.Closed, Later);

        Assert.Equal(Later, ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenClosingAResolvedTicket_KeepsTheOriginalResolutionTime()
    {
        var ticket = new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build();

        ticket.ChangeStatus(TicketStatus.Closed, Later);

        Assert.Equal(FixedClock.DefaultNow, ticket.ResolvedAtUtc);
    }

    [Fact]
    public void ChangeStatus_WhenTicketIsAlreadyClosed_Throws()
    {
        var ticket = new TicketBuilder()
            .WithReference("TCK-0026")
            .WithStatus(TicketStatus.Closed)
            .Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => ticket.ChangeStatus(TicketStatus.Open, Later));

        Assert.Contains("TCK-0026", exception.Message);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }

    [Fact]
    public void AssignTo_WithoutAnAgent_TakesTheTicketBack()
    {
        var ticket = new TicketBuilder().AssignedTo(2).Build();

        ticket.AssignTo(null, Later);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal(Later, ticket.UpdatedAtUtc);
    }

    #region BR-7 Escalation Tests

    [Fact]
    public void Escalate_UpdatesPriorityDueAtAndAssignee_AndRecordsHistory()
    {
        var dueOriginal = FixedClock.DefaultNow.AddHours(24);
        var dueNew = Later.AddHours(8);

        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Medium)
            .AssignedTo(1)
            .WithDueAt(dueOriginal)
            .Build();

        var escalation = ticket.Escalate(
            newPriority: TicketPriority.High,
            newDueAtUtc: dueNew,
            newAgentId: 2,
            reason: "Customer called three times in the last hour.",
            escalatedBy: "TeamLead",
            nowUtc: Later);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(dueNew, ticket.DueAtUtc);
        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.Equal(Later, ticket.UpdatedAtUtc);

        Assert.Single(ticket.Escalations);
        Assert.Same(escalation, ticket.Escalations.First());

        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(1, escalation.FromAgentId);
        Assert.Equal(2, escalation.ToAgentId);
        Assert.Equal(dueOriginal, escalation.FromDueAtUtc);
        Assert.Equal(dueNew, escalation.ToDueAtUtc);
        Assert.Equal("Customer called three times in the last hour.", escalation.Reason);
        Assert.Equal("TeamLead", escalation.EscalatedBy);
        Assert.Equal(Later, escalation.EscalatedAtUtc);
    }

    [Fact]
    public void Escalate_WhenAlreadyCritical_ThrowsBusinessRuleViolationException()
    {
        var ticket = new TicketBuilder()
            .WithReference("TCK-0009")
            .WithPriority(TicketPriority.Critical)
            .Build();

        var ex = Assert.Throws<BusinessRuleViolationException>(() =>
            ticket.Escalate(TicketPriority.Critical, Later.AddHours(4), 1, "Urgent issue", "Lead", Later));

        Assert.Contains("TCK-0009", ex.Message);
        Assert.Contains("Critical priority and cannot be escalated", ex.Message);
    }

    [Fact]
    public void Escalate_WhenResolved_ThrowsBusinessRuleViolationException()
    {
        var ticket = new TicketBuilder()
            .WithReference("TCK-0025")
            .WithPriority(TicketPriority.High)
            .WithStatus(TicketStatus.Resolved)
            .Build();

        var ex = Assert.Throws<BusinessRuleViolationException>(() =>
            ticket.Escalate(TicketPriority.Critical, Later.AddHours(4), 1, "Needs reopen", "Lead", Later));

        Assert.Contains("TCK-0025", ex.Message);
        Assert.Contains("resolved and cannot be escalated", ex.Message);
    }

    [Fact]
    public void Escalate_WhenClosed_ThrowsBusinessRuleViolationException()
    {
        var ticket = new TicketBuilder()
            .WithReference("TCK-0026")
            .WithPriority(TicketPriority.Medium)
            .WithStatus(TicketStatus.Closed)
            .Build();

        var ex = Assert.Throws<BusinessRuleViolationException>(() =>
            ticket.Escalate(TicketPriority.High, Later.AddHours(8), 1, "Reopen request", "Lead", Later));

        Assert.Contains("TCK-0026", ex.Message);
        Assert.Contains("closed and cannot be escalated", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("1234")] // 4 characters (< 5)
    public void Escalate_InvalidReason_ThrowsArgumentException(string invalidReason)
    {
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Low)
            .Build();

        Assert.ThrowsAny<ArgumentException>(() =>
            ticket.Escalate(TicketPriority.Medium, Later.AddHours(24), 1, invalidReason, "Lead", Later));
    }

    [Fact]
    public void Escalate_ReasonTooLong_ThrowsArgumentException()
    {
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Low)
            .Build();

        var longReason = new string('A', 501);

        Assert.Throws<ArgumentException>(() =>
            ticket.Escalate(TicketPriority.Medium, Later.AddHours(24), 1, longReason, "Lead", Later));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Escalate_EmptyEscalatedBy_ThrowsArgumentException(string invalidActor)
    {
        var ticket = new TicketBuilder()
            .WithPriority(TicketPriority.Low)
            .Build();

        Assert.ThrowsAny<ArgumentException>(() =>
            ticket.Escalate(TicketPriority.Medium, Later.AddHours(24), 1, "Valid reason here", invalidActor, Later));
    }

    #endregion
}
