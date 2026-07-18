using FluentValidation;

namespace RealTimeChatAPI.Services.Users.Commands.UpdateUserPassword;

public class UpdateUserPasswordCommandValidator : AbstractValidator<UpdateUserPasswordCommand>
{
    public UpdateUserPasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .MinimumLength(6)
            .MaximumLength(255);
    }
}
