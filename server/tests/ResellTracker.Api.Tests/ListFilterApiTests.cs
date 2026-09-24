using System.Net;
using ResellTracker.Domain;

namespace ResellTracker.Api.Tests;

public class ListFilterApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Lists_newest_first()
    {
        var first = await CreateItemAsync(new { title = "First" });
        var second = await CreateItemAsync(new { title = "Second" });

        Assert.Equal([second.Id, first.Id], (await ListAsync()).Select(i => i.Id));
    }

    [Fact]
    public async Task Filters_by_status()
    {
        await CreateItemAsync(new { title = "In stock" });
        await CreateItemAsync(new { title = "Listed", platforms = new[] { "depop" } });

        var listed = await ListAsync("?status=listed");

        Assert.Equal("Listed", Assert.Single(listed).Title);
        Assert.Equal(ItemStatus.Listed, listed[0].Status);
    }

    [Fact]
    public async Task A_sold_item_matches_the_platform_it_sold_on_not_where_it_was_listed()
    {
        var item = await CreateItemAsync(new { title = "Cross-listed", platforms = new[] { "depop", "ebay" } });
        await ChangeAsync(HttpMethod.Put, $"/api/items/{item.Id}/sale", item.Version, new { platform = "ebay", price = 30, date = "2026-09-20" });
        await CreateItemAsync(new { title = "Still on Depop", platforms = new[] { "depop" } });

        Assert.Equal(["Still on Depop"], (await ListAsync("?platform=depop")).Select(i => i.Title));
        Assert.Equal(["Cross-listed"], (await ListAsync("?platform=ebay")).Select(i => i.Title));
    }

    [Fact]
    public async Task Searches_text_fields_and_the_donation_org_case_insensitively()
    {
        await CreateItemAsync(new { title = "Polo", brand = "Nautica" });
        await CreateItemAsync(new { title = "Tee", notes = "small NAUTICA logo" });
        var donated = await CreateItemAsync(new { title = "Scarf" });
        await ChangeAsync(HttpMethod.Put, $"/api/items/{donated.Id}/donation", donated.Version, new { date = "2026-09-21", org = "Goodwill" });
        await CreateItemAsync(new { title = "Jeans" });

        Assert.Equal(["Tee", "Polo"], (await ListAsync("?q=nautica")).Select(i => i.Title));
        Assert.Equal(["Scarf"], (await ListAsync("?q=goodwill")).Select(i => i.Title));
    }

    [Fact]
    public async Task Search_treats_wildcard_characters_literally()
    {
        await CreateItemAsync(new { title = "100% cotton tee" });
        await CreateItemAsync(new { title = "Wool jumper" });

        Assert.Equal(["100% cotton tee"], (await ListAsync("?q=%25")).Select(i => i.Title));
    }

    [Fact]
    public async Task Rejects_an_unknown_platform_or_status()
    {
        await ValidationProblemAsync(await Client.GetAsync("/api/items?platform=grailed", Ct));
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/items?status=lost", Ct)).StatusCode);
    }
}
