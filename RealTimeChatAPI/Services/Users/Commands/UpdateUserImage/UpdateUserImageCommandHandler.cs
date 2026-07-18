using MediatR;
using RealTimeChatAPI.Data.Repositories;
using RealTimeChatAPI.Exceptions;
using RealTimeChatAPI.Helpers;
using RealTimeChatAPI.Models;

namespace RealTimeChatAPI.Services.Users.Commands.UpdateUserImage;

public class UpdateUserImageCommandHandler(
        ILogger<UpdateUserImageCommandHandler> logger,
        IWebHostEnvironment webHostEnvironment,
        IUsersRepository usersRepository,
        IUserContext userContext) : IRequestHandler<UpdateUserImageCommand>
{
    public async Task Handle(UpdateUserImageCommand request, CancellationToken cancellationToken)
    {
        var currentUser = userContext.CurrentUser();
        logger.LogInformation("User {UserId} is updating their profile image", currentUser.Id);

        var user = await usersRepository.GetByIdAsync(currentUser.Id)
            ?? throw new NotFoundException(nameof(User), currentUser.Id.ToString());

        var extension = await ImageFileValidator.ValidateAndGetExtension(
            request.Image,
            cancellationToken);
        var oldImage = user.Image;
        var (fileName, filePath) = ProfileImageStorage.CreateFilePath(
            webHostEnvironment,
            user.Id,
            extension);

        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                options: FileOptions.Asynchronous);
            await request.Image.CopyToAsync(stream, cancellationToken);
        }
        catch
        {
            TryDeleteNewFile(filePath, currentUser.Id);
            throw;
        }

        user.Image = $"images/{fileName}";

        try
        {
            await usersRepository.UpdateAsync(user);
        }
        catch
        {
            user.Image = oldImage;
            TryDeleteNewFile(filePath, currentUser.Id);
            throw;
        }

        try
        {
            ProfileImageStorage.DeleteIfSafe(webHostEnvironment, oldImage);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "The previous profile image for user {UserId} could not be removed",
                currentUser.Id);
        }
    }

    private void TryDeleteNewFile(string filePath, Guid userId)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "A newly uploaded profile image for user {UserId} could not be cleaned up",
                userId);
        }
    }
}
