using FluentValidation;

namespace BetsTrading.Application.Commands.Auth;

public class SendCodeCommandValidator : AbstractValidator<SendCodeCommand>
{
    public SendCodeCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required");
    }
}
