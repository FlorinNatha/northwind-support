namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Configurable SLA response windows and thresholds, loaded from configuration (e.g. appsettings.json).
/// </summary>
public sealed class SlaOptions
{
    public const string SectionName = "Sla";

    /// <summary>
    /// Base SLA response window in hours for each ticket priority.
    /// Default: Critical 4h, High 8h, Medium 24h, Low 72h.
    /// </summary>
    public Dictionary<string, int> BaseWindowHours { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Low"] = 72,
        ["Medium"] = 24,
        ["High"] = 8,
        ["Critical"] = 4
    };

    /// <summary>
    /// Multiplier applied to the base SLA window for Premium customer tier tickets.
    /// Default: 0.5 (half the window).
    /// </summary>
    public double PremiumCustomerMultiplier { get; set; } = 0.5;

    /// <summary>
    /// The minimum SLA response window floor in hours for any ticket, regardless of multipliers.
    /// Default: 1 hour.
    /// </summary>
    public double MinimumWindowHours { get; set; } = 1.0;

    /// <summary>
    /// The percentage of total window remaining at or below which an unresolved ticket becomes AtRisk.
    /// Default: 25%.
    /// </summary>
    public double AtRiskThresholdPercent { get; set; } = 25.0;

    /// <summary>
    /// Gets the base response window in hours for a given priority.
    /// </summary>
    public double GetBaseWindowHours(TicketPriority priority)
    {
        if (BaseWindowHours != null && BaseWindowHours.TryGetValue(priority.ToString(), out var hours))
        {
            return hours;
        }

        return priority switch
        {
            TicketPriority.Critical => 4,
            TicketPriority.High => 8,
            TicketPriority.Medium => 24,
            TicketPriority.Low => 72,
            _ => 24
        };
    }
}
