using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// A single ticket escalation audit record (BR-7).
/// </summary>
public sealed class TicketEscalationDto
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    public TicketPriority FromPriority { get; set; }

    public TicketPriority ToPriority { get; set; }

    public AgentSummaryDto? FromAgent { get; set; }

    public AgentSummaryDto? ToAgent { get; set; }

    public DateTime? FromDueAtUtc { get; set; }

    public DateTime? ToDueAtUtc { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string EscalatedBy { get; set; } = string.Empty;

    public DateTime EscalatedAtUtc { get; set; }
}
