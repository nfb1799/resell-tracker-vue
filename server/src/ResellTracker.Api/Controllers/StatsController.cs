using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResellTracker.Api.Services;
using ResellTracker.Domain;

namespace ResellTracker.Api.Controllers;

/// <summary>
/// The dashboard tiles and trends, computed on the server, which is the source of
/// truth for money. "This month" and ages depend on the date where the user is, so
/// the client sends its local date as <c>today</c>; without one the server uses UTC.
/// </summary>
[ApiController]
[Authorize]
[Route("api/stats")]
public class StatsController(StatsService stats, TimeProvider clock) : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<DashboardStats> Dashboard([FromQuery] DateOnly? today, CancellationToken ct) =>
        stats.DashboardAsync(today ?? UtcToday(), ct);

    [HttpGet("trends")]
    public Task<TrendStats> Trends([FromQuery] DateOnly? today, CancellationToken ct) =>
        stats.TrendsAsync(today ?? UtcToday(), ct);

    private DateOnly UtcToday() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
