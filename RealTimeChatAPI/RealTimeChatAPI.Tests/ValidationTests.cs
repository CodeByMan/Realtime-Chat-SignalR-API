using RealTimeChatAPI.Services.Users.Commands.LoginUser;
using RealTimeChatAPI.Services.Users.Commands.UpdateUserPassword;

namespace RealTimeChatAPI.Tests;

public class ValidationTests
{
    [Fact]
    public void LoginValidator_RejectsEmptyCredentials()
    {
        var result = new LoginUserCommandValidator().Validate(new LoginUserCommand());
        Assert.False(result.IsValid);
    }

    [Fact]
    public void PasswordValidator_RequiresCurrentAndValidNewPassword()
    {
        var result = new UpdateUserPasswordCommandValidator().Validate(
            new UpdateUserPasswordCommand
            {
                CurrentPassword = "",
                NewPassword = "123"
            });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "CurrentPassword");
        Assert.Contains(result.Errors, error => error.PropertyName == "NewPassword");
    }
}
