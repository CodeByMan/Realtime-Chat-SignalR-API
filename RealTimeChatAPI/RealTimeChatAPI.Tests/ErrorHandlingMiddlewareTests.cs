using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using RealTimeChatAPI.Exceptions;
using RealTimeChatAPI.Middlewares;

namespace RealTimeChatAPI.Tests;

public class ErrorHandlingMiddlewareTests
{
    [Fact]
    public async Task BadRequest_IsMappedTo400()
    {
        var context = await Invoke(new BadRequestException("Invalid request."));
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task Validation_IsMappedTo400()
    {
        var context = await Invoke(new ValidationException("Invalid input."));
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvalidLogin_IsMappedTo401()
    {
        var context = await Invoke(new InvalidLoginException());
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Forbidden_IsMappedTo403()
    {
        var context = await Invoke(new ForbiddenAccessException());
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task NotFound_IsMappedTo404()
    {
        var context = await Invoke(new NotFoundException("User", Guid.NewGuid().ToString()));
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task DuplicateUsername_IsMappedTo409()
    {
        var context = await Invoke(new UsernameAlreadyUsedException("alice"));
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
    }

    [Fact]
    public async Task UnexpectedException_IsMappedTo500WithoutRawMessage()
    {
        var context = await Invoke(new Exception("database-password=secret"));
        context.Response.Body.Position = 0;
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.DoesNotContain("database-password=secret", responseBody);
        Assert.Contains("An unexpected error occurred", responseBody);
    }

    private static async Task<DefaultHttpContext> Invoke(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/test";
        context.Response.Body = new MemoryStream();
        var middleware = new ErrorHandlingMiddleware(
            NullLogger<ErrorHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context, _ => Task.FromException(exception));
        return context;
    }
}
