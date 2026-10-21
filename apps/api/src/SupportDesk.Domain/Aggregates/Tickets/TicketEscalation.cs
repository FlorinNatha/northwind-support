using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// An immutable audit record of a ticket escalation (BR-7).
/// Records priority, assignee and due-date changes along with who escalated the ticket and why.
/// </summary>
public sealed class TicketEscalation : Entity
{
    private TicketEscalation()
    {
        // For EF Core.
    }

    internal TicketEscalation(
        int ticketId,
        TicketPriority fromPriority,
        TicketPriority toPriority,
        int? fromAgentId,
        int? toAgentId,
        DateTime? fromDueAtUtc,
        DateTime? toDueAtUtc,
        string reason,
        string escalatedBy,
        DateTime escalatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(escalatedBy);

        var trimmedReason = reason.Trim();
        if (trimmedReason.Length < 5 || trimmedReason.Length > 500)
        {
            throw new ArgumentException("Escalation reason must be between 5 and 500 characters.", nameof(reason));
        }

        var trimmedBy = escalatedBy.Trim();
        if (trimmedBy.Length > 100)
        {
            throw new ArgumentException("EscalatedBy cannot exceed 100 characters.", nameof(escalatedBy));
        }

        TicketId = ticketId;
        FromPriority = fromPriority;
        ToPriority = toPriority;
        FromAgentId = fromAgentId;
        ToAgentId = toAgentId;
        FromDueAtUtc = fromDueAtUtc;
        ToDueAtUtc = toDueAtUtc;
        Reason = trimmedReason;
        EscalatedBy = trimmedBy;
        EscalatedAtUtc = escalatedAtUtc;
    }

    public int TicketId { get; private set; }

    public TicketPriority FromPriority { get; private set; }

    public TicketPriority ToPriority { get; private set; }

    public int? FromAgentId { get; private set; }

    public int? ToAgentId { get; private set; }

    public DateTime? FromDueAtUtc { get; private set; }

    public DateTime? ToDueAtUtc { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string EscalatedBy { get; private set; } = string.Empty;

    public DateTime EscalatedAtUtc { get; private set; }
}
