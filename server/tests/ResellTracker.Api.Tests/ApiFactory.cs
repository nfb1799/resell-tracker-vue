using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
/// Tests don't share data by resetting the database; each one signs in as a fresh
/// user, and since every query is scoped to its owner that isolates them.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DefaultServer = @"Server=.\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True";

    private readonly string _connectionString = new SqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("RESELLTRACKER_TEST_SQL") ?? DefaultServer)
    {
        InitialCatalog = $"ResellTracker_Test_{Guid.NewGuid():N}",
    }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
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

    /// <summary>A client signed in as a brand-new user who owns nothing yet.</summary>
    public async Task<HttpClient> CreateUserClientAsync()
    {
        var id = Guid.NewGuid();
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new AppUser { Id = id, UserName = $"{id}@test", Email = $"{id}@test" });
            await db.SaveChangesAsync();
        }

        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, id.ToString());
        return client;
    }

    /// <summary>Runs something against the database directly, for asserting on what was stored.</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}

/// <summary>
/// Signs a request in as the user named in the X-Test-User header. Exists only in
/// the test project; the app itself uses Identity's cookie.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var user))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.ToString())], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
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
