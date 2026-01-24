using FluentValidation;

namespace BetsTrading.Application.Commands.Rewards;

public class RequestAdNonceCommandValidator : AbstractValidator<RequestAdNonceCommand>
{
    public RequestAdNonceCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.AdUnitId)
            .NotEmpty().WithMessage("Ad Unit ID is required");
    }
}
