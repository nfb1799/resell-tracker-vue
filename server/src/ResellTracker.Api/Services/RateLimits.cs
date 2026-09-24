using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ResellTracker.Api.Services;

/// <summary>
/// Per-IP limits on the endpoints worth abusing: guessing passwords, mailing reset
/// links, and minting demo accounts. Identity's lockout separately stops guessing
/// at any one account.
/// </summary>
public static class RateLimits
{
    public const string Auth = "auth";
    public const string Demo = "demo";

    public static IServiceCollection AddAppRateLimits(this IServiceCollection services, IConfiguration config)
    {
        var authPerMinute = config.GetValue("RateLimits:AuthPerMinute", 10);
        var demosPerHour = config.GetValue("RateLimits:DemosPerHour", 5);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "Too many attempts. Wait a minute and try again.",
                    },
                });
            };

            options.AddPolicy(Auth, http => FixedWindow(http, authPerMinute, TimeSpan.FromMinutes(1)));
            options.AddPolicy(Demo, http => FixedWindow(http, demosPerHour, TimeSpan.FromHours(1)));
        });
    }

    // Behind a proxy the client address comes from forwarded headers, configured with hosting.
    private static RateLimitPartition<string> FixedWindow(HttpContext http, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, QueueLimit = 0 });
}
