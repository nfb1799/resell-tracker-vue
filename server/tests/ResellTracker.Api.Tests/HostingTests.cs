using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellTracker.Api.Data;
using ResellTracker.Api.Services;

namespace ResellTracker.Api.Tests;

// How the app behaves once deployed: in a container that restarts and scales to
// zero, behind a proxy, sending real email.
public class HostingTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly WebApplicationFactoryClientOptions NoCookieJar =
        new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false };

    [Fact]
    public async Task A_session_survives_the_app_restarting()
    {
        using var before = factory.CreateClient(NoCookieJar);
        var signUp = await before.PostAsJsonAsync("/api/auth/register",
            new { email = $"{Guid.NewGuid():N}@example.test", password = ApiFactory.Password }, Ct);
        var cookie = signUp.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("rt_session=", StringComparison.Ordinal)).Split(';')[0];

        // A second host on the same database is what a restart or a scale-from-zero
        // looks like: a fresh process with no keys in memory, only what's stored.
        await using var restarted = factory.WithWebHostBuilder(_ => { });
        using var after = restarted.CreateClient(NoCookieJar);
        after.DefaultRequestHeaders.Add("Cookie", cookie);

        Assert.Equal(HttpStatusCode.OK, (await after.GetAsync("/api/auth/me", Ct)).StatusCode);

        // On a developer machine ASP.NET also keeps keys in the user profile, which
        // would make the above pass regardless; a container has no such folder, so
        // what matters is that the keys are in the database.
        Assert.True(await factory.WithDbAsync(db => db.DataProtectionKeys.AnyAsync(Ct)));
    }

    [Fact]
    public async Task Behind_the_proxy_each_client_address_gets_its_own_rate_limit()
    {
        await using var proxied = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Proxy:TrustForwardedHeaders", "true");
            b.UseSetting("RateLimits:DemosPerHour", "1");
        });
        using var browser = ApiFactory.CreateBrowser(proxied);

        async Task<HttpStatusCode> DemoFrom(string ip)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/demo");
            request.Headers.Add("X-Forwarded-For", ip);
            return (await browser.SendAsync(request, Ct)).StatusCode;
        }

        Assert.Equal(HttpStatusCode.OK, await DemoFrom("203.0.113.7"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await DemoFrom("203.0.113.7"));
        Assert.Equal(HttpStatusCode.OK, await DemoFrom("198.51.100.20"));
    }

    [Fact]
    public async Task Resend_gets_the_reset_link_from_the_configured_sender()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);

        await Sender(handler).SendPasswordResetLinkAsync(new AppUser(), "nik@example.test",
            "https://resell.test/reset-password?email=nik%40example.test&token=abc");

        Assert.Equal("https://api.resend.com/emails", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer re_test_key", handler.Request.Headers.Authorization!.ToString());
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("Resell Tracker <onboarding@resend.dev>", body.RootElement.GetProperty("from").GetString());
        Assert.Equal("nik@example.test", body.RootElement.GetProperty("to")[0].GetString());
        Assert.Contains("token=abc", body.RootElement.GetProperty("text").GetString(), StringComparison.Ordinal);
        Assert.Contains("token=abc", body.RootElement.GetProperty("html").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_email_is_logged_not_thrown()
    {
        // Without a domain of its own, Resend only delivers to the account owner and refuses anyone else.
        var handler = new RecordingHandler(HttpStatusCode.Forbidden);

        await Sender(handler).SendPasswordResetLinkAsync(new AppUser(), "someone.else@example.test", "https://resell.test/reset-password");

        Assert.NotNull(handler.Request);
    }

    [Theory]
    [InlineData("/assets/index-Cyt8IxV4.js", "public, max-age=31536000, immutable")]
    [InlineData("/index.html", "no-cache")]
    [InlineData("/sw.js", "no-cache")]
    [InlineData("/manifest.webmanifest", "no-cache")]
    public void Hashed_assets_cache_for_a_year_and_everything_else_is_rechecked(string path, string expected)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = path;

        StaticCaching.Apply(new StaticFileResponseContext(http, new NotFoundFileInfo(path)));

        Assert.Equal(expected, http.Response.Headers.CacheControl.ToString());
    }

    private static ResendEmailSender Sender(RecordingHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        http.DefaultRequestHeaders.Authorization = new("Bearer", "re_test_key");
        return new ResendEmailSender(http, Options.Create(new EmailOptions { ResendApiKey = "re_test_key" }), NullLogger<ResendEmailSender>.Instance);
    }

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = JsonContent.Create(new { message = "done" }) };
        }
    }
}
