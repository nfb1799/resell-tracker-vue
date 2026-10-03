using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResellTracker.Api.Data;
using ResellTracker.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default"), sql =>
        // A serverless database pauses when idle and refuses connections for up to
        // a minute while it resumes; retrying rides that out instead of failing
        // the first request after a quiet spell.
        sql.EnableRetryOnFailure(maxRetryCount: 8, maxRetryDelay: TimeSpan.FromSeconds(15), errorNumbersToAdd: null)));

// The keys that encrypt sign-in cookies live in the database, not the container:
// otherwise every restart or scale-from-zero would mint new keys and sign
// everyone out, and two instances couldn't read each other's cookies.
builder.Services.AddDataProtection().PersistKeysToDbContext<AppDbContext>().SetApplicationName("ResellTracker");

// Hosted behind a TLS-terminating proxy, the client's address and scheme arrive
// in X-Forwarded-* headers. Trusted only when configured, since anything can send them.
if (builder.Configuration.GetValue<bool>("Proxy:TrustForwardedHeaders"))
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // The proxy's address isn't fixed; the app is reachable only through it.
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    });
}

builder.Services
    .AddIdentityCore<AppUser>(o =>
    {
        // The original app's rule: 6 characters, nothing else forced.
        o.Password.RequiredLength = 6;
        o.Password.RequireDigit = false;
        o.Password.RequireLowercase = false;
        o.Password.RequireUppercase = false;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequiredUniqueChars = 1;
        o.User.RequireUniqueEmail = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "rt_session";
    o.Cookie.HttpOnly = true;
    // Local development runs on plain http://localhost; everywhere else, tests
    // included, the cookie is only ever sent over HTTPS.
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    // Strict keeps the cookie off every cross-site request, which is the CSRF defence:
    // the SPA is same-origin, so it never needs the cookie on one.
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.ExpireTimeSpan = TimeSpan.FromDays(14);
    o.SlidingExpiration = true;

    // An API answers 401/403; it doesn't redirect to a login page.
    o.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
// A deleted account (an expired demo) or a changed password stops a session within minutes.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));
builder.Services.AddAuthorization();
builder.Services.AddAppRateLimits(builder.Configuration);
builder.Services.AddAppEmail(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<ItemService>();
builder.Services.AddScoped<DemoService>();
builder.Services.AddScoped<StatsService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<ExportService>();
if (builder.Configuration.GetValue("Demo:CleanupEnabled", true))
{
    builder.Services.AddHostedService<DemoCleanupService>();
}

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

if (app.Configuration.GetValue<bool>("Proxy:TrustForwardedHeaders"))
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Same-origin hosting: the built Vue app is copied into wwwroot and served from
// here, so the API and the SPA share one origin and auth cookies stay first-party.
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = StaticCaching.Apply });

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// Client-side routes (/inventory, /sales, ...) fall through to the SPA. Anything
// under /api that no controller matched stays a 404 instead of returning HTML.
app.MapFallbackToFile("index.html", new StaticFileOptions { OnPrepareResponse = StaticCaching.Apply });
app.MapFallback("/api/{**path}", () => Results.NotFound());

await app.RunAsync();

// Exposed so the integration tests can boot the app with WebApplicationFactory.
public partial class Program;
