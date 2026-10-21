using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Client and server-side validation rules for ticket escalation (BR-7).
/// </summary>
public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Escalation reason is required.")
            .Must(r => r != null && r.Trim().Length >= 5 && r.Trim().Length <= 500)
            .WithMessage("Escalation reason must be between 5 and 500 characters.");

        RuleFor(x => x.EscalatedBy)
            .NotEmpty()
            .WithMessage("EscalatedBy actor is required.")
            .Must(e => e != null && e.Trim().Length > 0 && e.Trim().Length <= 100)
            .WithMessage("EscalatedBy must not exceed 100 characters.");
    }
}
