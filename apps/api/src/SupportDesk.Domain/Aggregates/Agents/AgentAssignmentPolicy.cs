using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>
/// Pairs an agent with their current number of open (unresolved, unclosed) tickets.
/// </summary>
public sealed record AgentWorkload(Agent Agent, int OpenTicketCount);

/// <summary>
/// Domain service encapsulating the agent assignment rules (BR-4, BR-5, BR-7).
/// Used during initial ticket triage on creation and during owner re-evaluation upon escalation.
/// </summary>
public sealed class AgentAssignmentPolicy
{
    /// <summary>
    /// Checks if a specific agent meets all criteria to take a ticket in the given category:
    /// 1. Must be active (inactive agents take no new work).
    /// 2. If the category requires a specialist (BR-4), the agent must hold that specialization.
    /// 3. The agent's workload must be strictly below MaxOpenTickets (or at/below if already assigned).
    /// </summary>
    public bool IsEligible(
        Agent agent,
        Category category,
        int currentOpenTicketCount,
        bool isAlreadyAssigned = false)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(category);

        if (!agent.IsActive)
        {
            return false;
        }

        if (category.RequiresSpecialist && !agent.IsSpecializedIn(category.Id))
        {
            return false;
        }

        return isAlreadyAssigned
            ? currentOpenTicketCount <= agent.MaxOpenTickets
            : currentOpenTicketCount < agent.MaxOpenTickets;
    }

    /// <summary>
    /// Selects the best eligible agent per BR-5:
    /// Among active agents (and specialists where BR-4 applies) strictly below their capacity limit,
    /// selects the agent with the fewest open tickets.
    /// Deterministic tie-break by lowest agent Id.
    /// Returns null with a diagnostic reason if nobody is eligible (a normal outcome, not an error).
    /// </summary>
    public (Agent? Agent, string Reason) SelectAgent(
        Category category,
        IReadOnlyList<AgentWorkload> workloads)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(workloads);

        var active = workloads.Where(w => w.Agent.IsActive).ToList();
        if (active.Count == 0)
        {
            return (null, "No active agents are currently available.");
        }

        var specialized = category.RequiresSpecialist
            ? active.Where(w => w.Agent.IsSpecializedIn(category.Id)).ToList()
            : active;

        if (specialized.Count == 0)
        {
            return (null, $"No active agents are specialized in '{category.Name}'.");
        }

        var eligible = specialized
            .Where(w => w.OpenTicketCount < w.Agent.MaxOpenTickets)
            .OrderBy(w => w.OpenTicketCount)
            .ThenBy(w => w.Agent.Id)
            .ToList();

        if (eligible.Count == 0)
        {
            return (null, category.RequiresSpecialist
                ? $"All active agents specialized in '{category.Name}' have reached their open ticket capacity limit."
                : "All active agents have reached their open ticket capacity limit.");
        }

        var chosen = eligible[0];
        var reason = category.RequiresSpecialist
            ? $"Assigned to {chosen.Agent.FullName} (specialist in '{category.Name}') with lowest workload ({chosen.OpenTicketCount} open tickets)."
            : $"Assigned to {chosen.Agent.FullName} with lowest workload ({chosen.OpenTicketCount} open tickets).";

        return (chosen.Agent, reason);
    }

    /// <summary>
    /// Re-evaluates ticket ownership upon escalation (BR-7):
    /// Retains the current agent if they are still eligible; otherwise reassigns per BR-5.
    /// </summary>
    public (Agent? Agent, string Reason) ReevaluateAgentOnEscalation(
        Agent? currentAgent,
        int currentAgentOpenTickets,
        Category category,
        IReadOnlyList<AgentWorkload> workloads)
    {
        if (currentAgent is not null && IsEligible(currentAgent, category, currentAgentOpenTickets, isAlreadyAssigned: true))
        {
            return (currentAgent, $"Current agent {currentAgent.FullName} remains eligible and is retained.");
        }

        return SelectAgent(category, workloads);
    }
}
