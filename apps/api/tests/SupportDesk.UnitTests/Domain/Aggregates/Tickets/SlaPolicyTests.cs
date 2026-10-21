using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaPolicyTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;
    private readonly SlaPolicy _policy = new();

    #region BR-1 Priority Determination Tests

    [Fact]
    public void DeterminePriority_ForcesCriticalCategory_AlwaysReturnsCritical()
    {
        // Category forces critical priority (e.g. Outage or Security)
        var outageCategory = new Category("Outage", requiresSpecialist: true, forcesCriticalPriority: true);

        // Even when user explicitly requests Low priority
        var (priority, reason) = _policy.DeterminePriority(outageCategory, TicketPriority.Low);

        Assert.Equal(TicketPriority.Critical, priority);
        Assert.Contains("forces Critical priority", reason);
        Assert.Contains("Outage", reason);
    }

    [Fact]
    public void DeterminePriority_ForcesCriticalCategory_OverridesNullPriority()
    {
        var securityCategory = new Category("Security", requiresSpecialist: true, forcesCriticalPriority: true);

        var (priority, reason) = _policy.DeterminePriority(securityCategory, null);

        Assert.Equal(TicketPriority.Critical, priority);
        Assert.Contains("forces Critical priority", reason);
        Assert.Contains("Security", reason);
    }

    [Fact]
    public void DeterminePriority_StandardCategory_UsesRequestedPriority()
    {
        var generalCategory = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);

        var (priority, reason) = _policy.DeterminePriority(generalCategory, TicketPriority.High);

        Assert.Equal(TicketPriority.High, priority);
        Assert.Contains("Requested priority High applied", reason);
    }

    [Fact]
    public void DeterminePriority_StandardCategory_DefaultsToMediumWhenPriorityOmitted()
    {
        var generalCategory = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);

        var (priority, reason) = _policy.DeterminePriority(generalCategory, null);

        Assert.Equal(TicketPriority.Medium, priority);
        Assert.Contains("Default Medium priority applied", reason);
    }

    #endregion

    #region BR-2 & BR-3 SLA Window and Due Date Calculation Tests

    [Theory]
    [InlineData(TicketPriority.Critical, 4)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.Low, 72)]
    public void CalculateDueAt_StandardCustomer_UsesBaseWindow(TicketPriority priority, int expectedHours)
    {
        var (dueAtUtc, window, reason) = _policy.CalculateDueAt(priority, CustomerTier.Standard, Now);

        Assert.Equal(TimeSpan.FromHours(expectedHours), window);
        Assert.Equal(Now.AddHours(expectedHours), dueAtUtc);
        Assert.Contains("Standard SLA window", reason);
        Assert.Contains($"{expectedHours}h", reason);
    }

    [Theory]
    [InlineData(TicketPriority.Critical, 2)]     // 4h * 0.5 = 2h
    [InlineData(TicketPriority.High, 4)]         // 8h * 0.5 = 4h
    [InlineData(TicketPriority.Medium, 12)]      // 24h * 0.5 = 12h
    [InlineData(TicketPriority.Low, 36)]         // 72h * 0.5 = 36h
    public void CalculateDueAt_PremiumCustomer_HalvesWindow(TicketPriority priority, int expectedHours)
    {
        var (dueAtUtc, window, reason) = _policy.CalculateDueAt(priority, CustomerTier.Premium, Now);

        Assert.Equal(TimeSpan.FromHours(expectedHours), window);
        Assert.Equal(Now.AddHours(expectedHours), dueAtUtc);
        Assert.Contains("reduced for Premium customer", reason);
    }

    [Fact]
    public void CalculateDueAt_PremiumCustomer_EnforcesOneHourFloor()
    {
        // Configure an options instance where Critical base window is 1h, so 1h * 0.5 = 0.5h (< 1h floor)
        var customOptions = new SlaOptions
        {
            BaseWindowHours = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Critical"] = 1
            },
            PremiumCustomerMultiplier = 0.5,
            MinimumWindowHours = 1.0
        };
        var policy = new SlaPolicy(customOptions);

        var (dueAtUtc, window, reason) = policy.CalculateDueAt(TicketPriority.Critical, CustomerTier.Premium, Now);

        // Must not drop below 1 hour
        Assert.Equal(TimeSpan.FromHours(1), window);
        Assert.Equal(Now.AddHours(1), dueAtUtc);
        Assert.Contains("bounded by minimum floor", reason);
    }

    [Fact]
    public void CalculateDueAt_CustomOptions_AreRespected()
    {
        var customOptions = new SlaOptions
        {
            BaseWindowHours = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Critical"] = 6
            },
            PremiumCustomerMultiplier = 0.25,
            MinimumWindowHours = 2.0
        };
        var policy = new SlaPolicy(customOptions);

        // 6h * 0.25 = 1.5h, which is below 2.0h floor -> 2.0h
        var (dueAtUtc, window, _) = policy.CalculateDueAt(TicketPriority.Critical, CustomerTier.Premium, Now);

        Assert.Equal(TimeSpan.FromHours(2), window);
        Assert.Equal(Now.AddHours(2), dueAtUtc);
    }

    #endregion

    #region BR-7 Escalation Priority Progression Tests

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void GetNextPriority_RaisesOneLevel(TicketPriority current, TicketPriority expected)
    {
        var next = _policy.GetNextPriority(current);
        Assert.Equal(expected, next);
    }

    [Fact]
    public void GetNextPriority_Critical_ThrowsBusinessRuleViolationException()
    {
        var ex = Assert.Throws<BusinessRuleViolationException>(() =>
            _policy.GetNextPriority(TicketPriority.Critical));

        Assert.Contains("Critical tickets cannot be escalated", ex.Message);
    }

    [Theory]
    [InlineData(TicketPriority.Low, true)]
    [InlineData(TicketPriority.Medium, true)]
    [InlineData(TicketPriority.High, true)]
    [InlineData(TicketPriority.Critical, false)]
    public void CanEscalate_ReturnsExpectedResult(TicketPriority priority, bool expected)
    {
        Assert.Equal(expected, _policy.CanEscalate(priority));
    }

    #endregion
}
