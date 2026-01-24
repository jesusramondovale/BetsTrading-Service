using FluentValidation;

namespace BetsTrading.Application.Commands.Bets;

public class CreateBetCommandValidator : AbstractValidator<CreateBetCommand>
{
    public CreateBetCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.Ticker)
            .NotEmpty().WithMessage("Ticker is required");

        RuleFor(x => x.BetAmount)
            .GreaterThan(0).WithMessage("Bet amount must be greater than zero");

        RuleFor(x => x.OriginValue)
            .GreaterThan(0).WithMessage("Origin value must be greater than zero");

        RuleFor(x => x.BetZoneId)
            .GreaterThan(0).WithMessage("Bet zone ID is required");
    }
}
