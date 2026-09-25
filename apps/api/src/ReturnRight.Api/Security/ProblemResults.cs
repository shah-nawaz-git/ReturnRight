using Microsoft.AspNetCore.Mvc;

namespace ReturnRight.Api.Security;

/// <summary>Helpers for RFC 7807 problem responses with consumer-friendly titles.</summary>
public static class ProblemResults
{
    public static IResult Create(int status, string title, IDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title };
        if (extensions is not null)
        {
            foreach (var pair in extensions)
            {
                problem.Extensions[pair.Key] = pair.Value;
            }
        }
        return Results.Problem(problem);
    }

    public static IResult Validation(string title, IDictionary<string, string[]> errors)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
        };
        problem.Extensions["errors"] = errors;
        return Results.Problem(problem);
    }

    public static IResult ValidationField(string field, string message, string? title = null) =>
        Validation(
            title ?? "Some fields need attention. Please review and try again.",
            new Dictionary<string, string[]> { [field] = [message] });

    /// <summary>Writes a problem+json response — used by middleware outside endpoint results.</summary>
    public static Task WriteAsync(
        HttpResponse response, int status, string title, CancellationToken cancellationToken = default)
    {
        response.StatusCode = status;
        response.ContentType = "application/problem+json";
        var problem = new ProblemDetails { Status = status, Title = title };
        return response.WriteAsJsonAsync(problem, cancellationToken);
    }
}
