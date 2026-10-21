using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;

/// <summary>
/// Returns the audit history of every escalation on a ticket, newest first (BR-7).
/// </summary>
public sealed class GetTicketEscalationsQueryHandler(ITicketQueries tickets)
{
    /// <exception cref="NotFoundException">No ticket has this id.</exception>
    public async Task<IReadOnlyList<TicketEscalationDto>> HandleAsync(int ticketId, CancellationToken ct)
    {
        // Verify ticket exists (surfaces 404 if unknown)
        _ = await tickets.GetDetailAsync(ticketId, ct)
            ?? throw new NotFoundException("Ticket", ticketId);

        return await tickets.GetEscalationsAsync(ticketId, ct);
    }
}
