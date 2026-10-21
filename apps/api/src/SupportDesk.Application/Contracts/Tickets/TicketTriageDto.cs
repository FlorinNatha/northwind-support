namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Explains how priority, SLA window and agent assignment were determined during automatic triage (BR-1 to BR-6).
/// </summary>
public sealed record TicketTriageDto(
    string PriorityReason,
    string SlaReason,
    string AssignmentReason,
    bool IsAssigned);
