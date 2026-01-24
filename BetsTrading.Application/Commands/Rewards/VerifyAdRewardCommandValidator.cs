using FluentValidation;

namespace BetsTrading.Application.Commands.Rewards;

public class VerifyAdRewardCommandValidator : AbstractValidator<VerifyAdRewardCommand>
{
    public VerifyAdRewardCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.TransactionId)
            .NotEmpty().WithMessage("Transaction ID is required");

        RuleFor(x => x.Nonce)
            .NotEmpty().WithMessage("Nonce is required");
    }
}
