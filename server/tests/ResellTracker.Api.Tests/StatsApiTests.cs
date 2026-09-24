using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResellTracker.Domain;

namespace ResellTracker.Api.Tests;

// Checked against the demo seed (shared/demo-items.json), whose figures were
// worked out by hand:
//
//   net per sale   Carhartt 110.20, Nike 92.21, Ralph Lauren 35.20, Levi's 40.50,
//                  Patagonia 56.15, Stussy 70.16 (fees estimated: 115 x 13.25% + 0.40)
//   days to sell   14, 16, 29, 27, 22, 22  -> average 21.67, shown as 22
public class StatsApiTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Today => DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<HttpClient> DemoAsync()
    {
        var browser = factory.CreateBrowser();
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsync("/api/auth/demo", null, Ct)).StatusCode);
        return browser;
    }

    [Fact]
    public async Task Dashboard_totals_the_demo_inventory()
    {
        using var browser = await DemoAsync();

        var stats = (await browser.GetFromJsonAsync<DashboardStats>($"/api/stats/dashboard?today={Today}", Json, Ct))!;

        Assert.Equal(6, stats.AllTime.Count);
        Assert.Equal(1, stats.AllTime.Estimated);
        Assert.Equal(404.42m, stats.AllTime.Net);
        Assert.Equal(new WriteOff(5m, 1), stats.WriteOffs);
        Assert.Equal(123m, stats.TiedUp); // 8 + 55 + 30 listed, 4 + 26 in stock
        Assert.Equal(5, stats.OnHandCount);
        Assert.Equal(3, stats.ListedCount);
        Assert.Equal(398m, stats.ListedValue);
        Assert.Equal(22, stats.AvgDaysToSell);
    }

    [Fact]
    public async Task Trends_break_the_demo_down_by_platform_age_and_best_flip()
    {
        using var browser = await DemoAsync();

        var trends = (await browser.GetFromJsonAsync<TrendStats>($"/api/stats/trends?today={Today}", Json, Ct))!;

        Assert.Equal(["ebay", "depop", "vinted"], trends.ByPlatform.Select(p => p.Platform));
        Assert.Equal(218.52m, trends.ByPlatform[0].Totals.Net);
        Assert.Equal([3, 1, 1, 0], trends.Aging.Select(b => b.Count));
        Assert.Equal([60m, 55m, 8m, 0m], trends.Aging.Select(b => b.Cost));
        Assert.Equal("Carhartt Detroit jacket", trends.Best!.Title);
        Assert.Equal(110.20m, trends.Best.Net);
        Assert.Equal(DateTime.UtcNow.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), trends.Months[^1].Month);
        Assert.Equal(404.42m, trends.Months.Sum(m => m.Totals.Net));
    }

    [Fact]
    public async Task A_new_account_has_empty_stats()
    {
        var (client, _) = await factory.SignUpAsync();
        using var _ = client;

        var stats = (await client.GetFromJsonAsync<DashboardStats>("/api/stats/dashboard", Json, Ct))!;
        var trends = (await client.GetFromJsonAsync<TrendStats>("/api/stats/trends", Json, Ct))!;

        Assert.Equal(0, stats.AllTime.Count);
        Assert.Null(stats.AvgDaysToSell);
        Assert.Empty(trends.Months);
        Assert.Null(trends.Best);
    }

    [Fact]
    public async Task Stats_need_a_signed_in_user()
    {
        using var browser = factory.CreateBrowser();

        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/stats/dashboard", Ct)).StatusCode);
    }
}
