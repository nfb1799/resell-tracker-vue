using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace ResellTracker.Api.Services;

/// <summary>
/// Cache headers for the built SPA. Vite puts a content hash in every file under
/// /assets, so those can be cached for a year; everything else (index.html, the
/// service worker, the manifest) must be checked on every load, or a deploy would
/// never reach people who already have the app.
/// </summary>
public static class StaticCaching
{
    public static void Apply(StaticFileResponseContext context)
    {
        var path = context.Context.Request.Path;
        context.Context.Response.Headers[HeaderNames.CacheControl] =
            path.StartsWithSegments("/assets", StringComparison.Ordinal)
                ? "public, max-age=31536000, immutable"
                : "no-cache";
    }
}
