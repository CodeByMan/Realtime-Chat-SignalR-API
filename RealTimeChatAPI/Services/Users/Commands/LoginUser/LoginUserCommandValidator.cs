using FluentValidation;

namespace RealTimeChatAPI.Services.Users.Commands.LoginUser;

public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserCommandValidator()
    {
        RuleFor(command => command.Username)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MaximumLength(255);
    }
}
