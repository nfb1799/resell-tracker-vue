namespace ResellTracker.Domain.Tests;

public class StatsTests
{
    private static readonly DateOnly Today = new(2026, 9, 24);

    private static StatItem Sold(decimal price, decimal cost, DateOnly date, string platform = "vinted", string category = "Tops", int days = 10, string title = "Sold") =>
        new(title, ItemStatus.Sold, cost, price, category,
            Profit.Compute(cost, new SaleFigures(platform, price, 0, price, 0, 0), FeeSettings.Defaults),
            platform, price, date, days);

    private static StatItem OnHand(ItemStatus status, decimal cost, decimal? listPrice = null, int? days = 5) =>
        new("Stock", status, cost, listPrice, "", null, null, null, null, days);

    private static StatItem Donated(decimal cost) =>
        new("Given away", ItemStatus.Donated, cost, 40, "", null, null, null, null, 200);

    [Fact]
    public void Dashboard_splits_this_month_from_all_time()
    {
        var stats = Stats.Dashboard(
            [Sold(30, 10, Today.AddDays(-3)), Sold(50, 20, new DateOnly(2026, 8, 31))], Today);

        Assert.Equal(20m, stats.Month.Net);
        Assert.Equal(1, stats.Month.Count);
        Assert.Equal(50m, stats.AllTime.Net);
        Assert.Equal(2, stats.AllTime.Count);
        Assert.Equal(20m / 30m, stats.MonthMargin);
    }

    [Fact]
    public void Only_inventory_and_listed_count_as_cash_tied_up()
    {
        var stats = Stats.Dashboard(
            [OnHand(ItemStatus.Inventory, 5), OnHand(ItemStatus.Listed, 12, listPrice: 40), Donated(18), Sold(30, 10, Today)], Today);

        Assert.Equal(17m, stats.TiedUp);
        Assert.Equal(2, stats.OnHandCount);
        Assert.Equal(1, stats.ListedCount);
        Assert.Equal(40m, stats.ListedValue);
    }

    [Fact]
    public void Write_offs_stay_out_of_sale_profit()
    {
        var stats = Stats.Dashboard([Donated(18), Sold(30, 10, Today)], Today);

        Assert.Equal(new WriteOff(18m, 1), stats.WriteOffs);
        Assert.Equal(20m, stats.AllTime.Net);
    }

    [Fact]
    public void Average_days_to_sell_rounds_half_up()
    {
        var stats = Stats.Dashboard([Sold(10, 1, Today, days: 10), Sold(10, 1, Today, days: 11)], Today);

        Assert.Equal(11, stats.AvgDaysToSell); // 10.5
    }

    [Fact]
    public void An_empty_account_has_no_averages_or_margins()
    {
        var stats = Stats.Dashboard([], Today);

        Assert.Null(stats.AvgDaysToSell);
        Assert.Null(stats.MonthMargin);
        Assert.Equal(0, stats.AllTime.Count);
    }

    [Fact]
    public void Months_run_from_the_first_sale_to_now_including_empty_ones()
    {
        var trends = Stats.Trends([Sold(30, 10, new DateOnly(2026, 6, 15)), Sold(50, 20, new DateOnly(2026, 9, 1))], Today);

        Assert.Equal(["2026-06", "2026-07", "2026-08", "2026-09"], trends.Months.Select(m => m.Month));
        Assert.Equal(0, trends.Months[1].Totals.Count);
        Assert.Equal(30m, trends.Months[3].Totals.Net);
    }

    [Fact]
    public void Months_keep_only_the_last_twelve()
    {
        var trends = Stats.Trends([Sold(30, 10, new DateOnly(2024, 1, 5))], Today);

        Assert.Equal(Stats.MonthsShown, trends.Months.Count);
        Assert.Equal("2026-09", trends.Months[^1].Month);
    }

    [Fact]
    public void Platforms_rank_by_net_and_skip_ones_with_no_sales()
    {
        var trends = Stats.Trends(
            [Sold(30, 10, Today, platform: "vinted"), Sold(100, 10, Today, platform: "ebay")], Today);

        Assert.Equal(["ebay", "vinted"], trends.ByPlatform.Select(p => p.Platform));
    }

    [Fact]
    public void Aging_buckets_only_count_stock_on_hand()
    {
        var trends = Stats.Trends(
            [OnHand(ItemStatus.Listed, 10, days: 5), OnHand(ItemStatus.Inventory, 4, days: 45), OnHand(ItemStatus.Listed, 7, days: 91), Donated(99)], Today);

        Assert.Equal([1, 1, 0, 1], trends.Aging.Select(b => b.Count));
        Assert.Equal([10m, 4m, 0m, 7m], trends.Aging.Select(b => b.Cost));
    }

    [Fact]
    public void Categories_group_blank_as_uncategorised_and_rank_by_net()
    {
        var trends = Stats.Trends(
            [Sold(30, 10, Today, category: "Tops"), Sold(90, 10, Today, category: " "), Sold(20, 10, Today, category: "Tops")], Today);

        Assert.Equal([Stats.Uncategorised, "Tops"], trends.ByCategory.Select(c => c.Category));
        Assert.Equal(2, trends.ByCategory[1].Totals.Count);
    }

    [Fact]
    public void The_best_flip_is_the_highest_net()
    {
        var trends = Stats.Trends([Sold(30, 10, Today, title: "Tee"), Sold(90, 10, Today, title: "Jacket")], Today);

        Assert.Equal("Jacket", trends.Best!.Title);
        Assert.Equal(80m, trends.Best.Net);
        Assert.Equal(8m, trends.Best.Roi);
    }
}
