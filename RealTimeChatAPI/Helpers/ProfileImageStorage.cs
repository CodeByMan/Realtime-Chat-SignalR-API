namespace RealTimeChatAPI.Helpers;

public static class ProfileImageStorage
{
    public static (string FileName, string FullPath) CreateFilePath(
        IWebHostEnvironment environment,
        Guid userId,
        string extension)
    {
        var imagesDirectory = GetImagesDirectory(environment);
        Directory.CreateDirectory(imagesDirectory);

        var fileName = $"user-{userId}-{Guid.NewGuid():N}{extension}";
        var fullPath = Path.GetFullPath(Path.Combine(imagesDirectory, fileName));

        if (!IsInsideDirectory(fullPath, imagesDirectory))
            throw new InvalidOperationException("The generated image path is outside the image directory.");

        return (fileName, fullPath);
    }

    public static bool DeleteIfSafe(IWebHostEnvironment environment, string? relativeImagePath)
    {
        if (string.IsNullOrWhiteSpace(relativeImagePath))
            return false;

        var webRoot = GetWebRoot(environment);
        var imagesDirectory = GetImagesDirectory(environment);
        var normalizedRelativePath = relativeImagePath
            .TrimStart('/', '\\')
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(webRoot, normalizedRelativePath));

        if (!IsInsideDirectory(fullPath, imagesDirectory) || !File.Exists(fullPath))
            return false;

        File.Delete(fullPath);
        return true;
    }

    private static string GetImagesDirectory(IWebHostEnvironment environment) =>
        Path.GetFullPath(Path.Combine(GetWebRoot(environment), "images"));

    private static string GetWebRoot(IWebHostEnvironment environment) =>
        Path.GetFullPath(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));

    private static bool IsInsideDirectory(string path, string directory)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var directoryWithSeparator = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(directoryWithSeparator, comparison);
    }
}
