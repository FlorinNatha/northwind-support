using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Common;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Agents;

public class AgentAssignmentPolicyTests
{
    private readonly AgentAssignmentPolicy _policy = new();

    private static Agent CreateAgent(int id, string fullName, int maxOpenTickets = 5, bool isActive = true)
    {
        var agent = new Agent(fullName, $"{fullName.ToLowerInvariant().Replace(" ", ".")}@example.com", maxOpenTickets, FixedClock.DefaultNow);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(agent, id);
        if (!isActive)
        {
            agent.Deactivate();
        }
        return agent;
    }

    private static Category CreateCategory(int id, string name, bool requiresSpecialist = false, bool forcesCriticalPriority = false)
    {
        var category = new Category(name, requiresSpecialist, forcesCriticalPriority);
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(category, id);
        return category;
    }

    #region BR-4 Specialists Tests

    [Fact]
    public void SelectAgent_CategoryRequiresSpecialist_OnlySelectsSpecialistAgent()
    {
        var outageCategory = CreateCategory(1, "Outage", requiresSpecialist: true);

        var generalist = CreateAgent(1, "Generalist Bob", maxOpenTickets: 5);
        var specialist = CreateAgent(2, "Specialist Alice", maxOpenTickets: 5);
        specialist.AddSpecialization(outageCategory.Id);

        var workloads = new List<AgentWorkload>
        {
            new(generalist, OpenTicketCount: 0),
            new(specialist, OpenTicketCount: 2)
        };

        var (chosenAgent, reason) = _policy.SelectAgent(outageCategory, workloads);

        Assert.NotNull(chosenAgent);
        Assert.Equal(specialist.Id, chosenAgent.Id);
        Assert.Contains("specialist in 'Outage'", reason);
    }

    [Fact]
    public void SelectAgent_CategoryDoesNotRequireSpecialist_SelectsAnyEligibleAgentWithFewestTickets()
    {
        var generalCategory = CreateCategory(2, "General", requiresSpecialist: false);

        var agent1 = CreateAgent(1, "Agent 1", maxOpenTickets: 5);
        var agent2 = CreateAgent(2, "Agent 2", maxOpenTickets: 5);

        var workloads = new List<AgentWorkload>
        {
            new(agent1, OpenTicketCount: 3),
            new(agent2, OpenTicketCount: 1)
        };

        var (chosenAgent, _) = _policy.SelectAgent(generalCategory, workloads);

        Assert.NotNull(chosenAgent);
        Assert.Equal(agent2.Id, chosenAgent.Id);
    }

    #endregion

    #region BR-5 Assignment Rules & Edge Cases

    [Fact]
    public void SelectAgent_ChoosesAgentWithFewestOpenTickets()
    {
        var category = CreateCategory(1, "Support", requiresSpecialist: false);

        var busyAgent = CreateAgent(1, "Busy Bob", maxOpenTickets: 10);
        var quietAgent = CreateAgent(2, "Quiet Quinn", maxOpenTickets: 10);

        var workloads = new List<AgentWorkload>
        {
            new(busyAgent, OpenTicketCount: 4),
            new(quietAgent, OpenTicketCount: 1)
        };

        var (chosenAgent, reason) = _policy.SelectAgent(category, workloads);

        Assert.NotNull(chosenAgent);
        Assert.Equal(quietAgent.Id, chosenAgent.Id);
        Assert.Contains("1 open tickets", reason);
    }

    [Fact]
    public void SelectAgent_TiesBrokenDeterministicallyByLowestAgentId()
    {
        var category = CreateCategory(1, "Support", requiresSpecialist: false);

        var agent10 = CreateAgent(10, "Agent 10", maxOpenTickets: 5);
        var agent3 = CreateAgent(3, "Agent 3", maxOpenTickets: 5);
        var agent7 = CreateAgent(7, "Agent 7", maxOpenTickets: 5);

        // All have identical workload of 2 open tickets
        var workloads = new List<AgentWorkload>
        {
            new(agent10, OpenTicketCount: 2),
            new(agent3, OpenTicketCount: 2),
            new(agent7, OpenTicketCount: 2)
        };

        var (chosenAgent, _) = _policy.SelectAgent(category, workloads);

        Assert.NotNull(chosenAgent);
        Assert.Equal(agent3.Id, chosenAgent.Id);
    }

    [Fact]
    public void SelectAgent_AgentAtMaxCapacity_IsExcluded()
    {
        var category = CreateCategory(1, "Support", requiresSpecialist: false);

        var atCapacityAgent = CreateAgent(1, "Full Agent", maxOpenTickets: 3);
        var availableAgent = CreateAgent(2, "Available Agent", maxOpenTickets: 5);

        var workloads = new List<AgentWorkload>
        {
            new(atCapacityAgent, OpenTicketCount: 3), // 3 of 3 (strictly below limit fails)
            new(availableAgent, OpenTicketCount: 4)  // 4 of 5 (strictly below limit passes)
        };

        var (chosenAgent, _) = _policy.SelectAgent(category, workloads);

        Assert.NotNull(chosenAgent);
        Assert.Equal(availableAgent.Id, chosenAgent.Id);
    }

    [Fact]
    public void SelectAgent_InactiveAgent_IsExcludedEvenWithZeroWorkload()
    {
        var category = CreateCategory(1, "Support", requiresSpecialist: false);

        var inactiveAgent = CreateAgent(1, "Inactive Bob", maxOpenTickets: 5, isActive: false);
        var activeAgent = CreateAgent(2, "Active Alice", maxOpenTickets: 5, isActive: true);

        var workloads = new List<AgentWorkload>
        {
            new(inactiveAgent, OpenTicketCount: 0),
            new(activeAgent, OpenTicketCount: 3)
        };

        var (chosenAgent, _) = _policy.SelectAgent(category, workloads);

        Assert.NotNull(chosenAgent);
        Assert.Equal(activeAgent.Id, chosenAgent.Id);
    }

    [Fact]
    public void SelectAgent_NobodyEligibleDueToCapacity_ReturnsNullWithReason()
    {
        var category = CreateCategory(1, "Support", requiresSpecialist: false);

        var agent1 = CreateAgent(1, "Agent 1", maxOpenTickets: 2);
        var agent2 = CreateAgent(2, "Agent 2", maxOpenTickets: 3);

        var workloads = new List<AgentWorkload>
        {
            new(agent1, OpenTicketCount: 2),
            new(agent2, OpenTicketCount: 3)
        };

        var (chosenAgent, reason) = _policy.SelectAgent(category, workloads);

        Assert.Null(chosenAgent);
        Assert.Contains("reached their open ticket capacity limit", reason);
    }

    [Fact]
    public void SelectAgent_NobodyEligibleDueToMissingSpecialist_ReturnsNullWithReason()
    {
        var securityCategory = CreateCategory(1, "Security", requiresSpecialist: true);

        var generalist1 = CreateAgent(1, "Generalist 1", maxOpenTickets: 10);
        var generalist2 = CreateAgent(2, "Generalist 2", maxOpenTickets: 10);

        var workloads = new List<AgentWorkload>
        {
            new(generalist1, OpenTicketCount: 0),
            new(generalist2, OpenTicketCount: 1)
        };

        var (chosenAgent, reason) = _policy.SelectAgent(securityCategory, workloads);

        Assert.Null(chosenAgent);
        Assert.Contains("No active agents are specialized in 'Security'", reason);
    }

    [Fact]
    public void SelectAgent_NoActiveAgents_ReturnsNullWithReason()
    {
        var category = CreateCategory(1, "Support", requiresSpecialist: false);

        var inactiveAgent = CreateAgent(1, "Inactive", maxOpenTickets: 5, isActive: false);

        var workloads = new List<AgentWorkload>
        {
            new(inactiveAgent, OpenTicketCount: 0)
        };

        var (chosenAgent, reason) = _policy.SelectAgent(category, workloads);

        Assert.Null(chosenAgent);
        Assert.Contains("No active agents are currently available", reason);
    }

    #endregion

    #region BR-7 Escalation Re-evaluation Tests

    [Fact]
    public void ReevaluateAgentOnEscalation_CurrentAgentStillEligible_IsRetained()
    {
        var category = CreateCategory(1, "General", requiresSpecialist: false);
        var currentAgent = CreateAgent(1, "Current Agent", maxOpenTickets: 5);
        var otherAgent = CreateAgent(2, "Other Agent", maxOpenTickets: 5);

        var workloads = new List<AgentWorkload>
        {
            new(currentAgent, OpenTicketCount: 5), // Already assigned, so count == maxOpenTickets is allowed
            new(otherAgent, OpenTicketCount: 1)
        };

        var (chosenAgent, reason) = _policy.ReevaluateAgentOnEscalation(
            currentAgent,
            currentAgentOpenTickets: 5,
            category,
            workloads);

        Assert.NotNull(chosenAgent);
        Assert.Equal(currentAgent.Id, chosenAgent.Id);
        Assert.Contains("remains eligible and is retained", reason);
    }

    [Fact]
    public void ReevaluateAgentOnEscalation_CurrentAgentNowInactive_ReassignsPerBR5()
    {
        var category = CreateCategory(1, "General", requiresSpecialist: false);
        var currentAgent = CreateAgent(1, "Deactivated Agent", maxOpenTickets: 5, isActive: false);
        var otherAgent = CreateAgent(2, "Eligible Other", maxOpenTickets: 5, isActive: true);

        var workloads = new List<AgentWorkload>
        {
            new(currentAgent, OpenTicketCount: 1),
            new(otherAgent, OpenTicketCount: 2)
        };

        var (chosenAgent, reason) = _policy.ReevaluateAgentOnEscalation(
            currentAgent,
            currentAgentOpenTickets: 1,
            category,
            workloads);

        Assert.NotNull(chosenAgent);
        Assert.Equal(otherAgent.Id, chosenAgent.Id);
        Assert.Contains("Assigned to Eligible Other", reason);
    }

    [Fact]
    public void ReevaluateAgentOnEscalation_CurrentAgentLacksSpecialistCategory_ReassignsPerBR5()
    {
        var billingCategory = CreateCategory(1, "Billing", requiresSpecialist: true);

        var currentAgent = CreateAgent(1, "Not Specialized", maxOpenTickets: 5);
        var billingSpecialist = CreateAgent(2, "Billing Specialist", maxOpenTickets: 5);
        billingSpecialist.AddSpecialization(billingCategory.Id);

        var workloads = new List<AgentWorkload>
        {
            new(currentAgent, OpenTicketCount: 1),
            new(billingSpecialist, OpenTicketCount: 2)
        };

        var (chosenAgent, _) = _policy.ReevaluateAgentOnEscalation(
            currentAgent,
            currentAgentOpenTickets: 1,
            billingCategory,
            workloads);

        Assert.NotNull(chosenAgent);
        Assert.Equal(billingSpecialist.Id, chosenAgent.Id);
    }

    #endregion
}
