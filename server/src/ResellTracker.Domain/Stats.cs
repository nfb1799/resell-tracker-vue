namespace ResellTracker.Domain;

/// <summary>What the dashboard and trends need to know about one item.</summary>
public sealed record StatItem(
    string Title,
    ItemStatus Status,
    decimal Cost,
    decimal? ListPrice,
    string Category,
    // The profit breakdown, for sold items.
    ProfitBreakdown? Profit,
    string? SalePlatform,
    decimal? SalePrice,
    DateOnly? SaleDate,
    int? DaysListed);

public sealed record DashboardStats(
    // Sales dated in the current calendar month.
    ProfitTotals Month,
    ProfitTotals AllTime,
    WriteOff WriteOffs,
    // What everything still on hand cost: cash tied up in stock.
    decimal TiedUp,
    int OnHandCount,
    int ListedCount,
    // Asking prices of everything listed.
    decimal ListedValue,
    // Average days from listing to sale, whole days; null with no sales.
    int? AvgDaysToSell,
    // Net over gross for this month; null with no revenue.
    decimal? MonthMargin);

public sealed record MonthTotals(string Month, ProfitTotals Totals);

public sealed record PlatformTotals(string Platform, ProfitTotals Totals);

public sealed record AgeBucket(string Label, int MinDays, int? MaxDays, int Count, decimal Cost);

public sealed record CategoryTotals(string Category, ProfitTotals Totals, decimal? Margin, int? AvgDaysToSell);

public sealed record BestFlip(string Title, decimal Net, decimal Cogs, decimal Price, string Platform, decimal? Roi);

public sealed record TrendStats(
    IReadOnlyList<MonthTotals> Months,
    IReadOnlyList<PlatformTotals> ByPlatform,
    IReadOnlyList<AgeBucket> Aging,
    IReadOnlyList<CategoryTotals> ByCategory,
    BestFlip? Best);

/// <summary>
/// The dashboard tiles and the trends page, computed from a user's items. The
/// same rules as the original app: sold items make profit, donated items are a
/// separate write-off, and only inventory and listed items count as stock.
/// </summary>
public static class Stats
{
    public const int MonthsShown = 12;
    public const int CategoriesShown = 8;
    public const string Uncategorised = "Uncategorised";

    private static readonly (string Label, int Min, int? Max)[] AgeBuckets =
    [
        ("0–30 days", 0, 30),
        ("31–60", 31, 60),
        ("61–90", 61, 90),
        ("90+", 91, null),
    ];

    public static DashboardStats Dashboard(IReadOnlyCollection<StatItem> items, DateOnly today)
    {
        var sold = items.Where(i => i.Status == ItemStatus.Sold).ToList();
        var onHand = items.Where(i => Lifecycle.IsOnHand(i.Status)).ToList();
        var listed = onHand.Where(i => i.Status == ItemStatus.Listed).ToList();
        var month = Totals(sold.Where(i => SameMonth(i.SaleDate, today)));

        return new DashboardStats(
            Month: month,
            AllTime: Totals(sold),
            WriteOffs: Profit.WriteOffTotal(items.Where(i => i.Status == ItemStatus.Donated).Select(i => i.Cost)),
            TiedUp: onHand.Sum(i => i.Cost),
            OnHandCount: onHand.Count,
            ListedCount: listed.Count,
            ListedValue: listed.Sum(i => i.ListPrice ?? 0),
            AvgDaysToSell: AverageDays(sold),
            MonthMargin: month.Gross > 0 ? month.Net / month.Gross : null);
    }

    public static TrendStats Trends(IReadOnlyCollection<StatItem> items, DateOnly today)
    {
        var sold = items.Where(i => i.Status == ItemStatus.Sold).ToList();

        return new TrendStats(
            Months: MonthSeries(sold, today),
            ByPlatform: [.. Platforms.All
                .Select(p => new PlatformTotals(p.Id, Totals(sold.Where(i => i.SalePlatform == p.Id))))
                .Where(p => p.Totals.Count > 0)
                .OrderByDescending(p => p.Totals.Net)],
            Aging: [.. AgeBuckets.Select(b =>
            {
                var group = items.Where(i => Lifecycle.IsOnHand(i.Status) && i.DaysListed is { } d && d >= b.Min && (b.Max is null || d <= b.Max)).ToList();
                return new AgeBucket(b.Label, b.Min, b.Max, group.Count, group.Sum(i => i.Cost));
            })],
            ByCategory: [.. sold
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Category) ? Uncategorised : i.Category.Trim())
                .Select(g =>
                {
                    var totals = Totals(g);
                    return new CategoryTotals(g.Key, totals, totals.Gross > 0 ? totals.Net / totals.Gross : null, AverageDays(g));
                })
                .OrderByDescending(c => c.Totals.Net)
                .Take(CategoriesShown)],
            Best: sold
                .Where(i => i.Profit is not null)
                .OrderByDescending(i => i.Profit!.Net)
                .Select(i => new BestFlip(i.Title, i.Profit!.Net, i.Profit.Cogs, i.SalePrice ?? 0, i.SalePlatform ?? "", i.Profit.Roi))
                .FirstOrDefault());
    }

    /// <summary>
    /// Every calendar month from the first sale to this one, gaps included so a
    /// quiet month reads as a gap, trimmed to the last <see cref="MonthsShown"/>.
    /// </summary>
    private static List<MonthTotals> MonthSeries(List<StatItem> sold, DateOnly today)
    {
        var dated = sold.Where(i => i.SaleDate is not null).ToList();
        if (dated.Count == 0)
        {
            return [];
        }

        var first = dated.Min(i => i.SaleDate!.Value);
        var cursor = new DateOnly(first.Year, first.Month, 1);
        var last = new DateOnly(today.Year, today.Month, 1);
        var series = new List<MonthTotals>();
        for (; cursor <= last; cursor = cursor.AddMonths(1))
        {
            var month = cursor;
            series.Add(new MonthTotals(month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), Totals(dated.Where(i => SameMonth(i.SaleDate, month)))));
        }

        return series.TakeLast(MonthsShown).ToList();
    }

    private static ProfitTotals Totals(IEnumerable<StatItem> sold) =>
        Profit.Total(sold.Where(i => i.Profit is not null).Select(i => i.Profit!));

    private static bool SameMonth(DateOnly? date, DateOnly month) =>
        date is { } d && d.Year == month.Year && d.Month == month.Month;

    /// <summary>The average, rounded half up to a whole day, as the original's Math.round did.</summary>
    private static int? AverageDays(IEnumerable<StatItem> items)
    {
        var days = items.Where(i => i.DaysListed is not null).Select(i => i.DaysListed!.Value).ToList();
        return days.Count == 0 ? null : (int)Math.Floor((decimal)days.Sum() / days.Count + 0.5m);
    }
}
