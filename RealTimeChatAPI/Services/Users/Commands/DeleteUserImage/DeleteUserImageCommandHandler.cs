using MediatR;
using RealTimeChatAPI.Data.Repositories;
using RealTimeChatAPI.Exceptions;
using RealTimeChatAPI.Helpers;
using RealTimeChatAPI.Models;

namespace RealTimeChatAPI.Services.Users.Commands.DeleteUserImage;

public class DeleteUserImageCommandHandler(
        ILogger<DeleteUserImageCommandHandler> logger,
        IWebHostEnvironment webHostEnvironment,
        IUsersRepository usersRepository,
        IUserContext userContext) : IRequestHandler<DeleteUserImageCommand>
{
    public async Task Handle(DeleteUserImageCommand request, CancellationToken cancellationToken)
    {
        var currentUser = userContext.CurrentUser();
        logger.LogInformation("User {UserId} is deleting their profile image", currentUser.Id);

        var user = await usersRepository.GetByIdAsync(currentUser.Id)
            ?? throw new NotFoundException(nameof(User), currentUser.Id.ToString());

        var imageToDelete = user.Image;
        user.Image = null;
        await usersRepository.UpdateAsync(user);

        try
        {
            ProfileImageStorage.DeleteIfSafe(webHostEnvironment, imageToDelete);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "The profile image file for user {UserId} could not be removed",
                currentUser.Id);
        }
    }
}
