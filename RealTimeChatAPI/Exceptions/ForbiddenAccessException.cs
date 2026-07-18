namespace RealTimeChatAPI.Exceptions;

public class ForbiddenAccessException(string message = "You are not allowed to perform this action.")
    : Exception(message)
{
}
