using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using RealTimeChatAPI.Data.Repositories;
using RealTimeChatAPI.Models;
using RealTimeChatAPI.Services.Users;
using RealTimeChatAPI.Services.Users.Commands.DeleteUserImage;
using RealTimeChatAPI.Services.Users.Commands.UpdateUserImage;

namespace RealTimeChatAPI.Tests;

public class ImageCleanupTests
{
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00];

    [Fact]
    public async Task FailedDatabaseUpdate_RemovesNewlyWrittenImage()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var environment = new TestWebHostEnvironment(root);
            var user = CreateUser();
            var repository = new FakeUsersRepository(user) { ThrowOnUpdate = true };
            var handler = new UpdateUserImageCommandHandler(
                NullLogger<UpdateUserImageCommandHandler>.Instance,
                environment,
                repository,
                new FakeUserContext(user));

            await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
                new UpdateUserImageCommand { Image = CreatePngFile() },
                CancellationToken.None));

            var imageDirectory = Path.Combine(root, "wwwroot", "images");
            Assert.False(Directory.Exists(imageDirectory) && Directory.EnumerateFiles(imageDirectory).Any());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteProfileImage_RemovesDatabaseReferenceAndSafeFile()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var environment = new TestWebHostEnvironment(root);
            var imageDirectory = Path.Combine(root, "wwwroot", "images");
            Directory.CreateDirectory(imageDirectory);
            var imagePath = Path.Combine(imageDirectory, "existing.png");
            await File.WriteAllBytesAsync(imagePath, PngBytes);
            var user = CreateUser();
            user.Image = "images/existing.png";
            var repository = new FakeUsersRepository(user);
            var handler = new DeleteUserImageCommandHandler(
                NullLogger<DeleteUserImageCommandHandler>.Instance,
                environment,
                repository,
                new FakeUserContext(user));

            await handler.Handle(new DeleteUserImageCommand(), CancellationToken.None);

            Assert.Null(user.Image);
            Assert.False(File.Exists(imagePath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DeleteProfileImage_RejectsPathTraversal()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var environment = new TestWebHostEnvironment(root);
            var outsideFile = Path.Combine(root, "outside.png");
            File.WriteAllBytes(outsideFile, PngBytes);

            var deleted = RealTimeChatAPI.Helpers.ProfileImageStorage.DeleteIfSafe(
                environment,
                "../outside.png");

            Assert.False(deleted);
            Assert.True(File.Exists(outsideFile));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static IFormFile CreatePngFile() =>
        new FormFile(new MemoryStream(PngBytes), 0, PngBytes.Length, "Image", "profile.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

    private static User CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Alice",
        Username = "alice",
        HashedPassword = "not-used"
    };

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"RealTimeChatAPI-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakeUsersRepository(User user) : IUsersRepository
    {
        public bool ThrowOnUpdate { get; init; }

        public Task Add(User newUser) => Task.CompletedTask;

        public Task<User?> GetByUsernameAsync(string username) =>
            Task.FromResult<User?>(string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase)
                ? user
                : null);

        public Task<User?> GetByIdAsync(Guid id) =>
            Task.FromResult<User?>(id == user.Id ? user : null);

        public Task UpdateAsync(User updatedUser)
        {
            if (ThrowOnUpdate)
                throw new InvalidOperationException("Simulated database failure.");

            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserContext(User user) : IUserContext
    {
        public CurrentUser CurrentUser() => new(user.Id, user.Username, user.Name);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            WebRootPath = Path.Combine(contentRootPath, "wwwroot");
        }

        public string ApplicationName { get; set; } = "RealTimeChatAPI.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
