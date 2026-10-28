using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SGrowing.Api.Common;

/// <summary>
/// RFC 7807 compliant problem details generator and standardized API responses.
/// Ensures consistent machine-readable error shapes across all minimal API endpoints.
/// </summary>
public static class SGrowingProblemDetails
{
    public static IResult Create(
        int statusCode,
        string title,
        string detail,
        string instance,
        string errorType = "https://sgrowing.io/errors/api-error",
        IDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails
        {
            Type = errorType,
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = instance
        };

        if (extensions != null)
        {
            foreach (var kvp in extensions)
            {
                problem.Extensions[kvp.Key] = kvp.Value;
            }
        }

        return Results.Problem(problem);
    }

    public static IResult Conflict(string detail, string instance, string title = "Resource Conflict") =>
        Create(
            StatusCodes.Status409Conflict,
            title,
            detail,
            instance,
            "https://sgrowing.io/errors/concurrency-conflict");

    public static IResult NotFound(string detail, string instance, string title = "Resource Not Found") =>
        Create(
            StatusCodes.Status404NotFound,
            title,
            detail,
            instance,
            "https://sgrowing.io/errors/not-found");

    public static IResult BadRequest(string detail, string instance, string title = "Invalid Request Parameters") =>
        Create(
            StatusCodes.Status400BadRequest,
            title,
            detail,
            instance,
            "https://sgrowing.io/errors/bad-request");

    public static IResult UnprocessableEntity(string detail, string instance, string title = "Unprocessable Entity") =>
        Create(
            StatusCodes.Status422UnprocessableEntity,
            title,
            detail,
            instance,
            "https://sgrowing.io/errors/validation-error");
}
