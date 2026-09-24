using Microsoft.EntityFrameworkCore;
using ResellTracker.Api.Contracts;
using ResellTracker.Api.Data;
using ResellTracker.Domain;

namespace ResellTracker.Api.Services;

public sealed class SettingsService(AppDbContext db, ICurrentUser user)
{
    public async Task<SettingsResponse> GetAsync(CancellationToken ct)
    {
        var settings = await db.UserSettings.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == user.Id, ct)
            ?? new UserSettings();
        return ToResponse(settings);
    }

    public async Task<SettingsResponse> SaveAsync(SettingsRequest request, CancellationToken ct)
    {
        var settings = await db.UserSettings.SingleOrDefaultAsync(s => s.UserId == user.Id, ct);
        if (settings is null)
        {
            settings = new UserSettings { UserId = user.Id };
            db.UserSettings.Add(settings);
        }

        settings.DisplayName = request.DisplayName?.Trim() ?? "";
        settings.Currency = request.Currency;
        settings.Theme = request.Theme;
        settings.ProfitGoal = request.ProfitGoal ?? 0;
        await db.SaveChangesAsync(ct);
        return ToResponse(settings);
    }

    /// <summary>The user's saved schedules laid over the registry defaults, for the profit math.</summary>
    public async Task<FeeSettings> GetFeeSettingsAsync(CancellationToken ct)
    {
        var saved = await db.PlatformFeeSettings.AsNoTracking()
            .Where(f => f.OwnerId == user.Id)
            .ToDictionaryAsync(f => f.PlatformId, f => new FeeSchedule(f.Percent, f.Fixed, f.IncludesShipping), ct);
        return new FeeSettings(saved);
    }

    /// <summary>A schedule for every platform in the registry, in registry order.</summary>
    public async Task<Dictionary<string, FeeScheduleDto>> GetFeesAsync(CancellationToken ct)
    {
        var fees = await GetFeeSettingsAsync(ct);
        return Platforms.All.ToDictionary(p => p.Id, p => ToDto(fees.For(p.Id)));
    }

    /// <summary>
    /// Replaces the saved schedules. A platform left out goes back to its default;
    /// one saved with exactly its default values stores no row either.
    /// </summary>
    public async Task<Dictionary<string, FeeScheduleDto>> SaveFeesAsync(
        IReadOnlyDictionary<string, FeeScheduleDto> request, CancellationToken ct)
    {
        var existing = await db.PlatformFeeSettings.Where(f => f.OwnerId == user.Id).ToListAsync(ct);

        foreach (var platform in Platforms.All)
        {
            var row = existing.SingleOrDefault(f => f.PlatformId == platform.Id);
            var schedule = request.TryGetValue(platform.Id, out var dto)
                ? new FeeSchedule(dto.Percent!.Value, dto.Fixed!.Value, dto.IncludesShipping)
                : platform.DefaultFees;

            if (schedule == platform.DefaultFees)
            {
                if (row is not null)
                {
                    db.PlatformFeeSettings.Remove(row);
                }

                continue;
            }

            if (row is null)
            {
                row = new PlatformFeeSetting { OwnerId = user.Id, PlatformId = platform.Id };
                db.PlatformFeeSettings.Add(row);
            }

            row.Percent = schedule.Percent;
            row.Fixed = schedule.Fixed;
            row.IncludesShipping = schedule.IncludesShipping;
        }

        await db.SaveChangesAsync(ct);
        return await GetFeesAsync(ct);
    }

    private static SettingsResponse ToResponse(UserSettings s) => new(s.DisplayName, s.Currency, s.Theme, s.ProfitGoal);

    private static FeeScheduleDto ToDto(FeeSchedule s) =>
        new() { Percent = s.Percent, Fixed = s.Fixed, IncludesShipping = s.IncludesShipping };
}
