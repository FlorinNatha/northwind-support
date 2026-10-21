namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date (BR-8).
/// SLA status is always derived from the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        DateTime? windowStartUtc = null,
        double atRiskThresholdPercent = 25.0)
    {
        if (dueAtUtc is null)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is not null)
        {
            return resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > dueAtUtc)
        {
            return SlaStatus.Breached;
        }

        if (windowStartUtc is not null)
        {
            var totalWindow = dueAtUtc.Value - windowStartUtc.Value;
            if (totalWindow <= TimeSpan.Zero)
            {
                return SlaStatus.AtRisk;
            }

            var remaining = dueAtUtc.Value - nowUtc;
            var remainingPercent = (remaining.TotalSeconds / totalWindow.TotalSeconds) * 100.0;
            if (remainingPercent <= atRiskThresholdPercent)
            {
                return SlaStatus.AtRisk;
            }
        }

        return SlaStatus.WithinSla;
    }

    /// <summary>
    /// Evaluates SLA status for a <see cref="Ticket"/> instance using its dates and the current time.
    /// </summary>
    public static SlaStatus Evaluate(Ticket ticket, DateTime nowUtc, double atRiskThresholdPercent = 25.0)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return Evaluate(ticket.DueAtUtc, ticket.ResolvedAtUtc, nowUtc, ticket.CreatedAtUtc, atRiskThresholdPercent);
    }
}

