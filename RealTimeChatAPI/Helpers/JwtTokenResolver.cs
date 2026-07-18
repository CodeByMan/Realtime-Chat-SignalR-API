namespace RealTimeChatAPI.Helpers;

public static class JwtTokenResolver
{
    private static readonly PathString ChatHubPath = new("/ChatHub");

    public static string? GetSignalRAccessToken(HttpRequest request)
    {
        if (!request.Path.StartsWithSegments(ChatHubPath, StringComparison.OrdinalIgnoreCase))
            return null;

        var token = request.Query["access_token"].ToString();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}
