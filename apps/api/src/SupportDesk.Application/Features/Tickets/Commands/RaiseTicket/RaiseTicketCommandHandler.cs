using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer with automatic priority triage, SLA due date calculation,
/// and intelligent agent assignment (BR-1 to BR-6).
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    SlaPolicy slaPolicy,
    AgentAssignmentPolicy assignmentPolicy,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var category = await categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var now = clock.UtcNow;

        // BR-1 Priority: requested priority or Medium default; category ForcesCriticalPriority overrides to Critical
        var (priority, priorityReason) = slaPolicy.DeterminePriority(category, request.RequestedPriority);

        // BR-2, BR-3, BR-6 SLA window: base window by priority, halved for Premium tier with 1h floor
        var (dueAtUtc, _, slaReason) = slaPolicy.CalculateDueAt(priority, customer.Tier, now);

        // BR-4, BR-5 Assignment: active agents, specialist matching, under capacity limit, fewest open tickets
        var workloads = await agents.GetWorkloadsAsync(ct);
        var (assignedAgent, assignmentReason) = assignmentPolicy.SelectAgent(category, workloads);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            priority,
            now,
            dueAtUtc);

        if (assignedAgent is not null)
        {
            ticket.AssignTo(assignedAgent.Id, now);
        }

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId} with priority {Priority} (Assigned: {AgentId}).",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId, ticket.Priority, ticket.AssignedAgentId);

        var detail = await getTicket.HandleAsync(ticket.Id, ct);
        detail.Triage = new TicketTriageDto(
            priorityReason,
            slaReason,
            assignmentReason,
            assignedAgent is not null);

        return detail;
    }
}
