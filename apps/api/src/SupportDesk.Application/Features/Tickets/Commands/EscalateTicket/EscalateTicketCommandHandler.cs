using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates a ticket (BR-7): increases priority by one level, recalculates due date from now,
/// re-evaluates assignee eligibility, and records an immutable audit history record.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    SlaPolicy slaPolicy,
    AgentAssignmentPolicy assignmentPolicy,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<EscalateTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">Ticket, Customer, or Category not found (404).</exception>
    /// <exception cref="SupportDesk.Domain.Exceptions.BusinessRuleViolationException">Ticket is Critical or Resolved/Closed (409).</exception>
    public async Task<EscalateTicketResultDto> HandleAsync(
        int id,
        EscalateTicketRequest request,
        CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Ticket", id);

        var nextPriority = slaPolicy.GetNextPriority(ticket.Priority);

        var customer = await customers.GetByIdAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        var category = await categories.GetByIdAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var now = clock.UtcNow;

        // Recompute due date from the moment of escalation (BR-2, BR-3)
        var (newDueAtUtc, _, _) = slaPolicy.CalculateDueAt(nextPriority, customer.Tier, now);

        // Re-evaluate owner: keep current agent if still eligible, else reassign per BR-5
        Agent? currentAgent = null;
        if (ticket.AssignedAgentId.HasValue)
        {
            currentAgent = await agents.GetByIdAsync(ticket.AssignedAgentId.Value, ct);
        }

        var workloads = await agents.GetWorkloadsAsync(ct);
        var currentAgentOpenTickets = workloads.FirstOrDefault(w => w.Agent.Id == ticket.AssignedAgentId)?.OpenTicketCount ?? 0;

        var (newAgent, _) = assignmentPolicy.ReevaluateAgentOnEscalation(
            currentAgent,
            currentAgentOpenTickets,
            category,
            workloads);

        var escalation = ticket.Escalate(
            nextPriority,
            newDueAtUtc,
            newAgent?.Id,
            request.Reason,
            request.EscalatedBy,
            now);

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated to {Priority} by {EscalatedBy}. Due: {DueAtUtc}, Agent: {AgentId}",
            ticket.Reference, ticket.Priority, request.EscalatedBy, ticket.DueAtUtc, ticket.AssignedAgentId);

        var updatedTicket = await getTicket.HandleAsync(ticket.Id, ct);

        var escalationDto = new TicketEscalationDto
        {
            Id = escalation.Id,
            TicketId = escalation.TicketId,
            FromPriority = escalation.FromPriority,
            ToPriority = escalation.ToPriority,
            FromAgent = currentAgent == null ? null : new AgentSummaryDto(currentAgent.Id, currentAgent.FullName),
            ToAgent = newAgent == null ? null : new AgentSummaryDto(newAgent.Id, newAgent.FullName),
            FromDueAtUtc = escalation.FromDueAtUtc,
            ToDueAtUtc = escalation.ToDueAtUtc,
            Reason = escalation.Reason,
            EscalatedBy = escalation.EscalatedBy,
            EscalatedAtUtc = escalation.EscalatedAtUtc
        };

        return new EscalateTicketResultDto(updatedTicket, escalationDto);
    }
}
