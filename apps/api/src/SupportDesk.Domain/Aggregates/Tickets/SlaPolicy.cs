using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Domain service encapsulating SLA and priority rules (BR-1, BR-2, BR-3, BR-6, BR-7).
/// Shared between initial ticket triage on creation and ticket escalation.
/// </summary>
public sealed class SlaPolicy(SlaOptions? options = null)
{
    public SlaOptions Options { get; } = options ?? new();

    /// <summary>
    /// Evaluates ticket priority per BR-1:
    /// Uses requested priority (or Medium if omitted); however, if the category forces
    /// Critical priority, Critical is always assigned.
    /// </summary>
    public (TicketPriority Priority, string Reason) DeterminePriority(Category category, TicketPriority? requestedPriority)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (category.ForcesCriticalPriority)
        {
            return (TicketPriority.Critical, $"Category '{category.Name}' forces Critical priority.");
        }

        if (requestedPriority.HasValue)
        {
            return (requestedPriority.Value, $"Requested priority {requestedPriority.Value} applied.");
        }

        return (TicketPriority.Medium, "Default Medium priority applied.");
    }

    /// <summary>
    /// Computes the SLA window and due date per BR-2, BR-3 and BR-6:
    /// Due = start time + window.
    /// Base window is configured per priority (Critical: 4h, High: 8h, Medium: 24h, Low: 72h).
    /// Premium customers receive half the window (or configured multiplier), with a minimum 1-hour floor.
    /// </summary>
    public (DateTime DueAtUtc, TimeSpan Window, string Reason) CalculateDueAt(
        TicketPriority priority,
        CustomerTier customerTier,
        DateTime startTimeUtc)
    {
        var baseHours = Options.GetBaseWindowHours(priority);
        double windowHours = baseHours;
        string reason;

        if (customerTier == CustomerTier.Premium)
        {
            windowHours = baseHours * Options.PremiumCustomerMultiplier;
            if (windowHours < Options.MinimumWindowHours)
            {
                windowHours = Options.MinimumWindowHours;
                reason = $"Base window of {baseHours}h reduced for Premium customer ({Options.PremiumCustomerMultiplier:P0}) and bounded by minimum floor of {Options.MinimumWindowHours}h: {windowHours}h window.";
            }
            else
            {
                reason = $"Base window of {baseHours}h reduced for Premium customer ({Options.PremiumCustomerMultiplier:P0}): {windowHours}h window.";
            }
        }
        else
        {
            reason = $"Standard SLA window for {priority} priority: {windowHours}h window.";
        }

        var window = TimeSpan.FromHours(windowHours);
        var dueAtUtc = startTimeUtc.Add(window);
        return (dueAtUtc, window, reason);
    }

    /// <summary>
    /// Returns the next priority level when escalating a ticket (BR-7).
    /// Raises priority one level: Low -> Medium, Medium -> High, High -> Critical.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">Ticket is already Critical.</exception>
    public TicketPriority GetNextPriority(TicketPriority current) => current switch
    {
        TicketPriority.Low => TicketPriority.Medium,
        TicketPriority.Medium => TicketPriority.High,
        TicketPriority.High => TicketPriority.Critical,
        TicketPriority.Critical => throw new BusinessRuleViolationException("Critical tickets cannot be escalated."),
        _ => throw new ArgumentOutOfRangeException(nameof(current), $"Unknown ticket priority '{current}'.")
    };

    /// <summary>
    /// Checks whether the given priority can be escalated further (BR-7).
    /// </summary>
    public bool CanEscalate(TicketPriority priority) => priority != TicketPriority.Critical;
}
