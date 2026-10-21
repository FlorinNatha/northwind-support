namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Payload sent to escalate a ticket (BR-7).
/// </summary>
public sealed record EscalateTicketRequest(string Reason, string EscalatedBy);
