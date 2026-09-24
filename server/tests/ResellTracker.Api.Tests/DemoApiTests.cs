using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResellTracker.Api.Contracts;
using ResellTracker.Api.Services;
using ResellTracker.Domain;

namespace ResellTracker.Api.Tests;

public class DemoApiTests(ApiFactory factory)
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase) },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpClient Browser, MeResponse Me)> StartDemoAsync()
    {
        var browser = factory.CreateBrowser();
        var response = await browser.PostAsync("/api/auth/demo", null, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (browser, (await response.Content.ReadFromJsonAsync<MeResponse>(Json, Ct))!);
    }

    [Fact]
    public async Task Try_the_demo_signs_into_a_private_account_that_expires_in_a_day()
    {
        var (browser, me) = await StartDemoAsync();
        using var _ = browser;

        Assert.True(me.IsDemo);
        Assert.Equal("Demo", me.DisplayName);
        Assert.InRange(me.DemoExpiresAt!.Value, DateTimeOffset.UtcNow.AddHours(23), DateTimeOffset.UtcNow.AddHours(25));
        Assert.Equal(me, await browser.GetFromJsonAsync<MeResponse>("/api/auth/me", Json, Ct));
    }

    [Fact]
    public async Task The_demo_is_seeded_with_the_original_sample_inventory()
    {
        var (browser, _) = await StartDemoAsync();
        using var _b = browser;

        var items = (await browser.GetFromJsonAsync<List<ItemResponse>>("/api/items", Json, Ct))!;

        Assert.Equal(12, items.Count);
        Assert.Equal(6, items.Count(i => i.Status == ItemStatus.Sold));
        Assert.Equal(3, items.Count(i => i.Status == ItemStatus.Listed));
        Assert.Equal(2, items.Count(i => i.Status == ItemStatus.Inventory));
        Assert.Equal("Old Navy cargo shorts", Assert.Single(items, i => i.Status == ItemStatus.Donated).Title);

        // Newest first, as the sample was written: the Sambas came in 3 days ago.
        Assert.Equal("Adidas Sambas OG", items[0].Title);

        // One sale is still waiting on its payout, so its fees are estimated.
        var stussy = Assert.Single(items, i => i.Title == "Stussy 8-ball hoodie");
        Assert.True(stussy.Profit!.FeesEstimated);

        var jacket = Assert.Single(items, i => i.Title == "Carhartt Detroit jacket");
        Assert.Equal(144.60m - 22m - 11.4m - 1m, jacket.Profit!.Net);
        Assert.Equal(14, jacket.DaysListed); // listed 110 days ago, sold 96 days ago

        var settings = await browser.GetFromJsonAsync<SettingsResponse>("/api/settings", Json, Ct);
        Assert.Equal(400m, settings!.ProfitGoal);
    }

    [Fact]
    public async Task Two_visitors_never_see_each_others_changes()
    {
        var (first, _) = await StartDemoAsync();
        var (second, _) = await StartDemoAsync();
        using var _1 = first;
        using var _2 = second;

        var item = (await first.GetFromJsonAsync<List<ItemResponse>>("/api/items", Json, Ct))![0];
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/items/{item.Id}").WithVersion(item.Version);
        Assert.Equal(HttpStatusCode.NoContent, (await first.SendAsync(request, Ct)).StatusCode);

        Assert.Equal(11, (await first.GetFromJsonAsync<List<ItemResponse>>("/api/items", Json, Ct))!.Count);
        Assert.Equal(12, (await second.GetFromJsonAsync<List<ItemResponse>>("/api/items", Json, Ct))!.Count);
    }

    [Fact]
    public async Task A_demo_account_cannot_be_signed_into_with_a_password()
    {
        var (browser, me) = await StartDemoAsync();
        using var _ = browser;
        await browser.PostAsync("/api/auth/logout", null, Ct);

        var login = await browser.PostAsJsonAsync("/api/auth/login", new { email = me.Email, password = "" }, Ct);
        var reset = await browser.PostAsJsonAsync("/api/auth/forgot-password", new { email = me.Email }, Ct);

        Assert.NotEqual(HttpStatusCode.OK, login.StatusCode);
        Assert.Null(factory.Emails.ResetLinkFor(me.Email));
        Assert.Equal(HttpStatusCode.Accepted, reset.StatusCode);
    }

    [Fact]
    public async Task Cleanup_deletes_expired_demos_and_their_data_and_nothing_else()
    {
        var (expired, expiredMe) = await StartDemoAsync();
        var (fresh, freshMe) = await StartDemoAsync();
        var (real, realMe) = await factory.SignUpAsync();
        using var _1 = expired;
        using var _2 = fresh;
        using var _3 = real;

        await factory.WithDbAsync(db => db.Users.Where(u => u.Id == expiredMe.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.DemoExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)), Ct));

        int deleted;
        using (var scope = factory.Services.CreateScope())
        {
            deleted = await scope.ServiceProvider.GetRequiredService<DemoService>().DeleteExpiredAsync(Ct);
        }

        Assert.True(deleted >= 1);
        var (usersLeft, itemsLeft, freshItems) = await factory.WithDbAsync(async db => (
            await db.Users.CountAsync(u => u.Id == expiredMe.Id || u.Id == freshMe.Id || u.Id == realMe.Id, Ct),
            await db.Items.CountAsync(i => i.OwnerId == expiredMe.Id, Ct),
            await db.Items.CountAsync(i => i.OwnerId == freshMe.Id, Ct)));
        Assert.Equal(2, usersLeft);
        Assert.Equal(0, itemsLeft);
        Assert.Equal(12, freshItems);
    }
}
