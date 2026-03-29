using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace PedalAcrossCanada.Server.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteErrorResponseAsync(context, ex);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.ContentType = "application/problem+json";

        var (statusCode, title, detail) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            KeyNotFoundException        => (StatusCodes.Status404NotFound, "Not Found", exception.Message),
            InvalidOperationException   => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            ArgumentException           => (StatusCodes.Status400BadRequest, "Bad Request", exception.Message),
            _                           => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "Please try again later.")
        };

        context.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Title  = title,
            Status = statusCode,
            Detail = detail
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
