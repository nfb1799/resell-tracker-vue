using Microsoft.EntityFrameworkCore;
using ResellTracker.Api.Data;
using ResellTracker.Domain;

namespace ResellTracker.Api.Services;

/// <summary>
/// Loads the signed-in user's items into the shape the domain's Stats needs.
/// The money math stays in the domain; this only fetches.
/// </summary>
public sealed class StatsService(AppDbContext db, ICurrentUser user, SettingsService settings)
{
    public async Task<DashboardStats> DashboardAsync(DateOnly today, CancellationToken ct) =>
        Stats.Dashboard(await LoadAsync(today, ct), today);

    public async Task<TrendStats> TrendsAsync(DateOnly today, CancellationToken ct) =>
        Stats.Trends(await LoadAsync(today, ct), today);

    private async Task<List<StatItem>> LoadAsync(DateOnly today, CancellationToken ct)
    {
        var fees = await settings.GetFeeSettingsAsync(ct);
        var items = await db.Items.AsNoTracking()
            .Where(i => i.OwnerId == user.Id)
            .Select(i => new { i.Title, i.Status, i.Cost, i.ListPrice, i.Category, i.AcquiredDate, i.ListedDate, i.Sale })
            .ToListAsync(ct);

        return [.. items.Select(i =>
        {
            var sale = i.Sale is { } s
                ? new SaleFigures(s.Platform, s.Price, s.ShippingCharged, s.Payout, s.ShippingCost, s.OtherCosts)
                : null;
            return new StatItem(
                i.Title,
                i.Status,
                i.Cost,
                i.ListPrice,
                i.Category,
                i.Status == ItemStatus.Sold ? Profit.Compute(i.Cost, sale, fees) : null,
                i.Sale?.Platform,
                i.Sale?.Price,
                i.Sale?.Date,
                Aging.DaysListed(i.Status, i.AcquiredDate, i.ListedDate, i.Sale?.Date, today));
        })];
    }
}
