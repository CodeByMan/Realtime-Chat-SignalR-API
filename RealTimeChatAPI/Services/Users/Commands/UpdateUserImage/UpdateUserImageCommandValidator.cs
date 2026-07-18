using FluentValidation;
using RealTimeChatAPI.Helpers;

namespace RealTimeChatAPI.Services.Users.Commands.UpdateUserImage;

public class UpdateUserImageCommandValidator : AbstractValidator<UpdateUserImageCommand>
{
    public UpdateUserImageCommandValidator()
    {
        RuleFor(command => command.Image)
            .NotNull()
            .WithMessage("An image file is required.");

        RuleFor(command => command.Image.Length)
            .GreaterThan(0)
            .WithMessage("The image file cannot be empty.")
            .LessThanOrEqualTo(ImageFileValidator.MaximumFileSize)
            .WithMessage("Image size exceeds the 5 MB limit.")
            .When(command => command.Image is not null);
    }
}
