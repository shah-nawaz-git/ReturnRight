using System.Security.Claims;
using System.Threading.RateLimiting;

namespace ReturnRight.Api.Security;

public static class RateLimitingSetup
{
    // Limits are read per request so that WebApplicationFactory config overrides
    // (which only land when the host is built) take effect in tests.
    private static int PermitLimit(HttpContext context, string key, int fallback)
    {
        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        return config.GetValue("RateLimiting:Enabled", true)
            ? config.GetValue(key, fallback)
            : int.MaxValue;
    }

    public static IServiceCollection AddReturnRightRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await ProblemResults.WriteAsync(
                    context.HttpContext.Response,
                    StatusCodes.Status429TooManyRequests,
                    "Too many attempts. Please wait a moment and try again.",
                    cancellationToken);
            };

            options.AddPolicy("auth", httpContext =>
            {
                var permitLimit = PermitLimit(httpContext, "RateLimiting:AuthPermitLimit", 10);
                return RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(1),
                    });
            });

            options.AddPolicy("uploads", httpContext =>
            {
                var permitLimit = PermitLimit(httpContext, "RateLimiting:UploadsPermitLimit", 20);
                var key = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    key,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(1),
                    });
            });
        });

        return services;
    }
}
