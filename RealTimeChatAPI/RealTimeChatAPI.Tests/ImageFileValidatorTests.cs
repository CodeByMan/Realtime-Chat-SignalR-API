using Microsoft.AspNetCore.Http;
using RealTimeChatAPI.Exceptions;
using RealTimeChatAPI.Helpers;

namespace RealTimeChatAPI.Tests;

public class ImageFileValidatorTests
{
    private static readonly byte[] PngHeader =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00];

    [Fact]
    public async Task ValidPng_IsAccepted()
    {
        var image = CreateFile(PngHeader, "profile.png", "image/png");

        var extension = await ImageFileValidator.ValidateAndGetExtension(image);

        Assert.Equal(".png", extension);
    }

    [Fact]
    public async Task ForgedMimeType_IsRejected()
    {
        var image = CreateFile("not-an-image"u8.ToArray(), "profile.png", "image/png");

        await Assert.ThrowsAsync<BadRequestException>(() =>
            ImageFileValidator.ValidateAndGetExtension(image));
    }

    [Fact]
    public async Task UnsupportedExtension_IsRejected()
    {
        var image = CreateFile(PngHeader, "profile.svg", "image/svg+xml");

        await Assert.ThrowsAsync<BadRequestException>(() =>
            ImageFileValidator.ValidateAndGetExtension(image));
    }

    [Fact]
    public async Task MismatchedExtensionAndMimeType_IsRejected()
    {
        var image = CreateFile(PngHeader, "profile.jpg", "image/png");

        await Assert.ThrowsAsync<BadRequestException>(() =>
            ImageFileValidator.ValidateAndGetExtension(image));
    }

    [Fact]
    public async Task OversizedImage_IsRejectedBeforeReadingContent()
    {
        var image = new FormFile(
            new MemoryStream(PngHeader),
            0,
            ImageFileValidator.MaximumFileSize + 1,
            "Image",
            "profile.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        await Assert.ThrowsAsync<BadRequestException>(() =>
            ImageFileValidator.ValidateAndGetExtension(image));
    }

    private static IFormFile CreateFile(byte[] bytes, string fileName, string contentType) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Image", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
}
