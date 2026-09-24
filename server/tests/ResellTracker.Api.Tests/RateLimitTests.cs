using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;

namespace ResellTracker.Api.Tests;

// The shared test host lifts the limits so hundreds of sign-ups can run; these
// tests start their own host with the real kind of limits to prove they bite.
public class RateLimitTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sign_in_attempts_are_limited_per_client()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimits:AuthPerMinute", "3"));
        using var browser = ApiFactory.CreateBrowser(limited);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            var response = await browser.PostAsJsonAsync("/api/auth/login", new { email = "who@example.test", password = "guess" }, Ct);
            statuses.Add(response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task Demo_accounts_are_limited_per_client()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimits:DemosPerHour", "1"));
        using var browser = ApiFactory.CreateBrowser(limited);

        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsync("/api/auth/demo", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await browser.PostAsync("/api/auth/demo", null, Ct)).StatusCode);
    }
}
