using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResellTracker.Api.Contracts;
using ResellTracker.Api.Data;

[assembly: AssemblyFixture(typeof(ResellTracker.Api.Tests.ApiFactory))]

namespace ResellTracker.Api.Tests;

/// <summary>
/// Boots the real app against a real SQL Server: one throwaway database per test
/// run, built by the actual migrations and dropped at the end.
///
/// The server comes from RESELLTRACKER_TEST_SQL (CI points it at a SQL Server
/// container) and defaults to the local .\SQLEXPRESS instance with Windows auth.
///
/// Tests sign in for real, through /api/auth and the session cookie, over HTTPS
/// because the cookie is Secure. Nothing is shared by resetting the database; each
/// test signs up as a fresh user, and every query being scoped to its owner is
/// what keeps tests apart.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "secret1";

    private const string DefaultServer = @"Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True";

    private readonly string _connectionString = new SqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("RESELLTRACKER_TEST_SQL") ?? DefaultServer)
    {
        InitialCatalog = $"ResellTracker_Test_{Guid.NewGuid():N}",
    }.ConnectionString;

    /// <summary>Every email the app "sent", instead of a real provider.</summary>
    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("App:BaseUrl", "https://resell.test");
        builder.UseSetting("Demo:CleanupEnabled", "false");
        // Every test signs up from the same (absent) client address; the limits
        // themselves are tested with their own host in RateLimitTests.
        builder.UseSetting("RateLimits:AuthPerMinute", "100000");
        builder.UseSetting("RateLimits:DemosPerHour", "100000");
        builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender<AppUser>>(Emails));
    }

    public async ValueTask InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }

    /// <summary>An HTTPS client that keeps cookies, signed in as nobody yet.</summary>
    public HttpClient CreateBrowser() => CreateBrowser(this);

    public static HttpClient CreateBrowser<T>(WebApplicationFactory<T> factory)
        where T : class =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    /// <summary>Signs up a brand-new user who owns nothing yet, and returns their signed-in client.</summary>
    public async Task<(HttpClient Client, MeResponse Me)> SignUpAsync()
    {
        var client = CreateBrowser();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = $"{Guid.NewGuid():N}@example.test", password = Password });
        response.EnsureSuccessStatusCode();
        return (client, (await response.Content.ReadFromJsonAsync<MeResponse>())!);
    }

    /// <summary>Runs something against the database directly, for asserting on what was stored.</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

public sealed class CapturingEmailSender : IEmailSender<AppUser>
{
    private readonly ConcurrentDictionary<string, string> _resetLinks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The last reset link mailed to an address, or null if none was.</summary>
    public string? ResetLinkFor(string email) => _resetLinks.GetValueOrDefault(email);

    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
    {
        _resetLinks[email] = resetLink;
        return Task.CompletedTask;
    }

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink) => throw new NotSupportedException();

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode) => throw new NotSupportedException();
}

internal static class HttpClientExtensions
{
    /// <summary>Sets If-Match for one request.</summary>
    public static HttpRequestMessage WithVersion(this HttpRequestMessage request, string version)
    {
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(version));
        return request;
    }
}
