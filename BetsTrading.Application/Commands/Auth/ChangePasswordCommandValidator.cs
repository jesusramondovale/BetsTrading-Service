using FluentValidation;

namespace BetsTrading.Application.Commands.Auth;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(3).WithMessage("New password must be at least 3 characters")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must be different from current password");
    }
}
