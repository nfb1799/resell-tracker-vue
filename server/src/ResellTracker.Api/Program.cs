var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Same-origin hosting: the built Vue app is copied into wwwroot and served from
// here, so the API and the SPA share one origin and auth cookies stay first-party.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// Client-side routes (/inventory, /sales, ...) fall through to the SPA. Anything
// under /api that no controller matched stays a 404 instead of returning HTML.
app.MapFallbackToFile("index.html");
app.MapFallback("/api/{**path}", () => Results.NotFound());

app.Run();

// Exposed so the integration tests can boot the app with WebApplicationFactory.
public partial class Program;
