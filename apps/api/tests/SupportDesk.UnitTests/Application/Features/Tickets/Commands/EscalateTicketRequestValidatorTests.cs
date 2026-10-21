using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketRequestValidatorTests
{
    private readonly EscalateTicketRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        var request = new EscalateTicketRequest("Customer reached out via phone requesting urgent attention", "Support Lead");
        var result = _validator.Validate(request);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234")] // 4 characters
    public void Validate_ShortReason_Fails(string reason)
    {
        var request = new EscalateTicketRequest(reason, "Support Lead");
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EscalateTicketRequest.Reason));
    }

    [Fact]
    public void Validate_ReasonExceeding500Characters_Fails()
    {
        var longReason = new string('a', 501);
        var request = new EscalateTicketRequest(longReason, "Support Lead");
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EscalateTicketRequest.Reason));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingEscalatedBy_Fails(string escalatedBy)
    {
        var request = new EscalateTicketRequest("Valid escalation reason here", escalatedBy);
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EscalateTicketRequest.EscalatedBy));
    }

    [Fact]
    public void Validate_EscalatedByExceeding100Characters_Fails()
    {
        var longBy = new string('a', 101);
        var request = new EscalateTicketRequest("Valid escalation reason here", longBy);
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EscalateTicketRequest.EscalatedBy));
    }
}
