using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealTimeChatAPI.Exceptions;

namespace RealTimeChatAPI.Middlewares;

public class ErrorHandlingMiddleware(ILogger<ErrorHandlingMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            await WriteProblem(context, StatusCodes.Status400BadRequest,
                "Validation failed", "One or more validation errors occurred.", ex);
        }
        catch (BadRequestException ex)
        {
            await WriteProblem(context, StatusCodes.Status400BadRequest,
                "Bad request", ex.Message, ex);
        }
        catch (InvalidLoginException ex)
        {
            await WriteProblem(context, StatusCodes.Status401Unauthorized,
                "Unauthorized", ex.Message, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteProblem(context, StatusCodes.Status401Unauthorized,
                "Unauthorized", "Authentication is required to access this resource.", ex);
        }
        catch (ForbiddenAccessException ex)
        {
            await WriteProblem(context, StatusCodes.Status403Forbidden,
                "Forbidden", ex.Message, ex);
        }
        catch (NotFoundException ex)
        {
            await WriteProblem(context, StatusCodes.Status404NotFound,
                "Not found", ex.Message, ex);
        }
        catch (UsernameAlreadyUsedException ex)
        {
            await WriteProblem(context, StatusCodes.Status409Conflict,
                "Conflict", ex.Message, ex);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await WriteProblem(context, StatusCodes.Status409Conflict,
                "Conflict", "The resource was modified by another request. Try again.", ex);
        }
        catch (Exception ex)
        {
            await WriteProblem(context, StatusCodes.Status500InternalServerError,
                "Internal server error", "An unexpected error occurred.", ex);
        }
    }

    private async Task WriteProblem(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        Exception exception)
    {
        if (context.Response.HasStarted)
            throw exception;

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception,
                "Unhandled request failure. Method: {Method}, Path: {Path}, TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);
        }
        else
        {
            logger.LogWarning(
                "Request rejected with status {StatusCode}. Method: {Method}, Path: {Path}, TraceId: {TraceId}",
                statusCode,
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path.Value
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsJsonAsync(problem);
    }
}
