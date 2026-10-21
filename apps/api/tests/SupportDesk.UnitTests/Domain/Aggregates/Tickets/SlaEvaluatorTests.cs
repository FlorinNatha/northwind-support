using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class SlaEvaluatorTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    [Fact]
    public void Evaluate_WithoutADueDate_IsNotApplicable() =>
        Assert.Equal(SlaStatus.NotApplicable, SlaEvaluator.Evaluate(null, null, Now));

    [Fact]
    public void Evaluate_ResolvedOnTime_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now.AddMinutes(-5), Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedExactlyAtDueDate_IsMet() =>
        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(Now, Now, Now.AddDays(1)));

    [Fact]
    public void Evaluate_ResolvedLate_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now, Now.AddMinutes(1), Now));

    [Fact]
    public void Evaluate_OpenAndPastDue_IsBreached() =>
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(Now.AddMinutes(-1), null, Now));

    [Fact]
    public void Evaluate_OpenAndBeforeDue_WithoutWindowStart_IsWithinSla() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddHours(1), null, Now));

    [Fact]
    public void Evaluate_OpenAndRemainingGreaterThan25Percent_IsWithinSla()
    {
        // 4 hour window: created at Now, due at Now + 4h.
        // At Now + 2h, 50% remaining > 25% threshold.
        var created = Now;
        var due = Now.AddHours(4);
        var current = Now.AddHours(2);

        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(due, null, current, created));
    }

    [Fact]
    public void Evaluate_OpenAndRemainingJustAbove25Percent_IsWithinSla()
    {
        // 240 minute window. 25% is 60 minutes remaining.
        // At 179 minutes elapsed (61 minutes remaining), ratio is 61/240 = 25.42% > 25%.
        var created = Now;
        var due = Now.AddMinutes(240);
        var current = Now.AddMinutes(179);

        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(due, null, current, created));
    }

    [Fact]
    public void Evaluate_OpenAndRemainingExactly25Percent_IsAtRisk()
    {
        // At 180 minutes elapsed (60 minutes remaining), ratio is 60/240 = 25.0%.
        var created = Now;
        var due = Now.AddMinutes(240);
        var current = Now.AddMinutes(180);

        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(due, null, current, created));
    }

    [Fact]
    public void Evaluate_OpenAndRemainingLessThan25Percent_IsAtRisk()
    {
        // At 181 minutes elapsed (59 minutes remaining), ratio is 59/240 = 24.58% <= 25%.
        var created = Now;
        var due = Now.AddMinutes(240);
        var current = Now.AddMinutes(181);

        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(due, null, current, created));
    }

    [Fact]
    public void Evaluate_OpenAndDueRightNow_IsAtRisk()
    {
        // Right at the deadline (0s remaining, but not strictly past due).
        var created = Now;
        var due = Now.AddHours(4);
        var current = due;

        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(due, null, current, created));
    }

    [Fact]
    public void Evaluate_OpenAndOneSecondPastDue_IsBreached()
    {
        var created = Now;
        var due = Now.AddHours(4);
        var current = due.AddSeconds(1);

        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(due, null, current, created));
    }

    [Fact]
    public void Evaluate_ResolvedOnTimeDuringAtRiskWindow_IsMet()
    {
        // Even if resolved at 90% of the window elapsed, resolving on or before due is Met.
        var created = Now;
        var due = Now.AddHours(4);
        var resolved = Now.AddHours(3.5);
        var current = Now.AddHours(5);

        Assert.Equal(SlaStatus.Met, SlaEvaluator.Evaluate(due, resolved, current, created));
    }

    [Fact]
    public void Evaluate_CustomAtRiskThreshold_IsRespected()
    {
        // Total window 100 minutes. 40 minutes remaining (40%).
        var created = Now;
        var due = Now.AddMinutes(100);
        var current = Now.AddMinutes(60);

        // Under 25% threshold: 40% > 25% -> WithinSla
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(due, null, current, created, atRiskThresholdPercent: 25));

        // Under 50% threshold: 40% <= 50% -> AtRisk
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(due, null, current, created, atRiskThresholdPercent: 50));
    }

    [Fact]
    public void Evaluate_WithTicketAggregate_DerivesCorrectSlaStatus()
    {
        var ticket = new TicketBuilder()
            .CreatedAt(Now)
            .WithDueAt(Now.AddHours(4))
            .Build();

        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(ticket, Now.AddHours(1)));
        Assert.Equal(SlaStatus.AtRisk, SlaEvaluator.Evaluate(ticket, Now.AddHours(3)));
        Assert.Equal(SlaStatus.Breached, SlaEvaluator.Evaluate(ticket, Now.AddHours(4).AddMinutes(1)));
    }
}
