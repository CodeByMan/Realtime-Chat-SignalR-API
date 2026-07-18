using Microsoft.IdentityModel.JsonWebTokens;

namespace RealTimeChatAPI.Services.Users;

public interface IUserContext
{
    CurrentUser CurrentUser();
}

public class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public CurrentUser CurrentUser()
    {
        var principal = httpContextAccessor.HttpContext?.User
            ?? throw new UnauthorizedAccessException("User context is not present.");

        if (principal.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("User is not authenticated.");

        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var username = principal.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value;
        var name = principal.FindFirst(JwtRegisteredClaimNames.Name)?.Value;

        if (!Guid.TryParse(subject, out var userId) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(name))
        {
            throw new UnauthorizedAccessException("Required user claims are missing or invalid.");
        }

        return new CurrentUser(userId, username, name);
    }
}
