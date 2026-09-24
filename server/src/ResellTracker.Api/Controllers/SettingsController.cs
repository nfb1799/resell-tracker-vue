using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResellTracker.Api.Contracts;
using ResellTracker.Api.Services;
using ResellTracker.Domain;

namespace ResellTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settings")]
public class SettingsController(SettingsService settings) : ControllerBase
{
    [HttpGet]
    public Task<SettingsResponse> Get(CancellationToken ct) => settings.GetAsync(ct);

    [HttpPut]
    public Task<SettingsResponse> Save(SettingsRequest request, CancellationToken ct) => settings.SaveAsync(request, ct);

    /// <summary>A fee schedule for every platform, keyed by platform id, in registry order.</summary>
    [HttpGet("fees")]
    public Task<Dictionary<string, FeeScheduleDto>> GetFees(CancellationToken ct) => settings.GetFeesAsync(ct);

    /// <summary>Replaces the saved schedules. A platform left out goes back to its default.</summary>
    [HttpPut("fees")]
    public async Task<ActionResult<Dictionary<string, FeeScheduleDto>>> SaveFees(
        Dictionary<string, FeeScheduleDto> request, CancellationToken ct)
    {
        foreach (var id in request.Keys.Where(id => !Platforms.IsKnown(id)))
        {
            ModelState.AddModelError(id, $"\"{id}\" is not a known platform.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        return await settings.SaveFeesAsync(request, ct);
    }
}
