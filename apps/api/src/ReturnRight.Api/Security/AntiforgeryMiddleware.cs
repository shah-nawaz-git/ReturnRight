using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace ReturnRight.Api.Security;

/// <summary>
/// Validates the antiforgery token on every mutating /api/* request.
/// The token is bound to the authenticated user, so the client must fetch a
/// fresh token from GET /api/auth/csrf after login or logout.
/// </summary>
public class AntiforgeryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        var path = context.Request.Path;
        var method = context.Request.Method;

        if (path.StartsWithSegments("/api")
            && !HttpMethods.IsGet(method)
            && !HttpMethods.IsHead(method)
            && !HttpMethods.IsOptions(method))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Your session needs to be refreshed. Please try again.",
                };
                problem.Extensions["code"] = "csrf_invalid";
                context.Response.StatusCode = problem.Status.Value;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(problem);
                return;
            }
        }

        await next(context);
    }
}
