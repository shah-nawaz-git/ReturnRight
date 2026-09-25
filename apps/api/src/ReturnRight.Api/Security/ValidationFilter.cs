using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ReturnRight.Api.Security;

/// <summary>
/// Runs a FluentValidation validator for the first argument of type T and
/// returns an RFC 7807 problem with an `errors` dictionary on failure.
/// </summary>
public sealed class ValidationFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().FirstOrDefault();
        if (request is null)
        {
            return await next(context);
        }

        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(request);
        if (result.IsValid)
        {
            return await next(context);
        }

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Some fields need attention. Please review and try again.",
        };
        problem.Extensions["errors"] = errors;
        return Results.Problem(problem);
    }
}
