using RealTimeChatAPI.Exceptions;

namespace RealTimeChatAPI.Helpers;

public static class ImageFileValidator
{
    public const long MaximumFileSize = 5 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, ImageType> AllowedTypes =
        new Dictionary<string, ImageType>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = new("image/jpeg", IsJpeg),
            [".jpeg"] = new("image/jpeg", IsJpeg),
            [".png"] = new("image/png", IsPng),
            [".gif"] = new("image/gif", IsGif),
            [".bmp"] = new("image/bmp", IsBmp),
            [".webp"] = new("image/webp", IsWebP)
        };

    public static async Task<string> ValidateAndGetExtension(
        IFormFile? image,
        CancellationToken cancellationToken = default)
    {
        if (image is null || image.Length == 0)
            throw new BadRequestException("No image file was uploaded.");

        if (image.Length > MaximumFileSize)
            throw new BadRequestException("Image size exceeds the 5 MB limit.");

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        if (!AllowedTypes.TryGetValue(extension, out var imageType))
            throw new BadRequestException("Invalid image extension.");

        var contentType = (image.ContentType ?? string.Empty)
            .Split(';', StringSplitOptions.TrimEntries)[0];
        if (!string.Equals(contentType, imageType.MimeType, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("The image MIME type does not match its extension.");

        var header = new byte[12];
        var totalRead = 0;
        await using (var stream = image.OpenReadStream())
        {
            while (totalRead < header.Length)
            {
                var bytesRead = await stream.ReadAsync(
                    header.AsMemory(totalRead, header.Length - totalRead),
                    cancellationToken);

                if (bytesRead == 0)
                    break;

                totalRead += bytesRead;
            }
        }

        if (!imageType.HasValidSignature(header.AsSpan(0, totalRead)))
            throw new BadRequestException("The uploaded file content is not a supported image.");

        return extension;
    }

    private static bool IsJpeg(ReadOnlySpan<byte> header) =>
        header.Length >= 3 &&
        header[0] == 0xFF &&
        header[1] == 0xD8 &&
        header[2] == 0xFF;

    private static bool IsPng(ReadOnlySpan<byte> header) =>
        header.Length >= 8 &&
        header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

    private static bool IsGif(ReadOnlySpan<byte> header) =>
        header.Length >= 6 &&
        (header[..6].SequenceEqual("GIF87a"u8) || header[..6].SequenceEqual("GIF89a"u8));

    private static bool IsBmp(ReadOnlySpan<byte> header) =>
        header.Length >= 2 && header[0] == (byte)'B' && header[1] == (byte)'M';

    private static bool IsWebP(ReadOnlySpan<byte> header) =>
        header.Length >= 12 &&
        header[..4].SequenceEqual("RIFF"u8) &&
        header.Slice(8, 4).SequenceEqual("WEBP"u8);

    private delegate bool SignatureValidator(ReadOnlySpan<byte> header);

    private sealed record ImageType(
        string MimeType,
        SignatureValidator HasValidSignature);
}
