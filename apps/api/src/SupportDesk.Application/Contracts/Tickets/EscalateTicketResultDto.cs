namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Result returned when a ticket has been escalated (BR-7).
/// </summary>
public sealed record EscalateTicketResultDto(
    TicketDetailDto Ticket,
    TicketEscalationDto Escalation);
