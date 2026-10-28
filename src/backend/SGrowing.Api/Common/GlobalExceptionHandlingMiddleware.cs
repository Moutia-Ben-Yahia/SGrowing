using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SGrowing.Api.Common;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred during request {Path}. TraceId: {TraceId}",
                context.Request.Path, context.TraceIdentifier);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/problem+json";

        var (statusCode, title, errorType) = ex switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request", "https://sgrowing.io/errors/bad-request"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource Not Found", "https://sgrowing.io/errors/not-found"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", "https://sgrowing.io/errors/unauthorized"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Operation Conflict", "https://sgrowing.io/errors/conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "https://sgrowing.io/errors/internal-error")
        };

        context.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Type = errorType,
            Title = title,
            Status = statusCode,
            Detail = ex.Message,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        var json = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(json);
    }
}
