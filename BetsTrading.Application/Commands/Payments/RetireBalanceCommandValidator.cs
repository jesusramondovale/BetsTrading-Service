using FluentValidation;

namespace BetsTrading.Application.Commands.Payments;

public class RetireBalanceCommandValidator : AbstractValidator<RetireBalanceCommand>
{
    public RetireBalanceCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.Fcm)
            .NotEmpty().WithMessage("FCM token is required");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");

        RuleFor(x => x.Coins)
            .GreaterThan(0).WithMessage("Coins must be greater than zero");

        RuleFor(x => x.CurrencyAmount)
            .GreaterThan(0).WithMessage("Currency amount must be greater than zero");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required");

        RuleFor(x => x.Method)
            .NotEmpty().WithMessage("Payment method is required");
    }
}
