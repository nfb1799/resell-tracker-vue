using System.Text.Json;
using System.Text.Json.Serialization;
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
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Identity's user store and cookie scheme. Sign-in endpoints arrive in Phase 3;
// until then [Authorize] endpoints simply answer 401.
builder.Services.AddIdentityCore<AppUser>().AddEntityFrameworkStores<AppDbContext>();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(o =>
{
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
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<ItemService>();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

// Same-origin hosting: the built Vue app is copied into wwwroot and served from
// here, so the API and the SPA share one origin and auth cookies stay first-party.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Client-side routes (/inventory, /sales, ...) fall through to the SPA. Anything
// under /api that no controller matched stays a 404 instead of returning HTML.
app.MapFallbackToFile("index.html");
app.MapFallback("/api/{**path}", () => Results.NotFound());

await app.RunAsync();

// Exposed so the integration tests can boot the app with WebApplicationFactory.
public partial class Program;
