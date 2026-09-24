using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ResellTracker.Domain;

namespace ResellTracker.Api.Tests;

public class ItemsApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Creates_an_item_with_the_form_defaults_and_reads_it_back()
    {
        var created = await CreateItemAsync(new { title = "  Nautica Polo  ", cost = 8, acquiredDate = "2026-08-28" });

        var item = await GetItemAsync(created.Id);
        Assert.Equal("Nautica Polo", item.Title);
        Assert.Equal(ItemStatus.Inventory, item.Status);
        Assert.Equal(ItemFields.DefaultCondition, item.Condition);
        Assert.Equal(8m, item.Cost);
        Assert.Equal(new DateOnly(2026, 8, 28), item.AcquiredDate);
        Assert.Empty(item.Platforms);
        Assert.Null(item.Sale);
        Assert.Null(item.Profit);
        Assert.Null(item.Thumbnail);
    }

    [Fact]
    public async Task Keeps_a_client_generated_id_so_offline_creates_can_be_replayed()
    {
        var id = Guid.NewGuid();

        var created = await CreateItemAsync(new { id, title = "Jacket" });

        Assert.Equal(id, created.Id);
    }

    [Fact]
    public async Task Refuses_a_second_create_with_the_same_id()
    {
        var id = Guid.NewGuid();
        await CreateItemAsync(new { id, title = "Jacket" });

        var again = await Client.PostAsJsonAsync("/api/items", new { id, title = "Jacket" }, Json, Ct);

        await ProblemAsync(again, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Naming_a_platform_lists_the_item_as_of_today()
    {
        var item = await CreateItemAsync(new { title = "Levi's 501", platforms = new[] { "ebay", "depop" }, listPrice = 40, cost = 8 });

        Assert.Equal(ItemStatus.Listed, item.Status);
        Assert.Equal(Today, item.ListedDate);
        Assert.Equal(["ebay", "depop"], item.Platforms); // order kept: the first one drives projected net
        Assert.Equal(26.30m, item.ProjectedNet); // 40 - (40 * 13.25% + 0.40) - 8
    }

    [Fact]
    public async Task Removing_every_platform_puts_it_back_in_stock()
    {
        var item = await CreateItemAsync(new { title = "Levi's 501", platforms = new[] { "ebay" }, listedDate = "2026-09-01" });

        var updated = await ChangeAsync(HttpMethod.Put, $"/api/items/{item.Id}", item.Version,
            new { title = "Levi's 501", platforms = Array.Empty<string>(), listedDate = "2026-09-01" });

        Assert.Equal(ItemStatus.Inventory, updated.Status);
        Assert.Equal(new DateOnly(2026, 9, 1), updated.ListedDate);
    }

    [Fact]
    public async Task Reordering_platforms_is_stored()
    {
        var item = await CreateItemAsync(new { title = "Tee", platforms = new[] { "ebay", "depop" } });

        await ChangeAsync(HttpMethod.Put, $"/api/items/{item.Id}", item.Version,
            new { title = "Tee", platforms = new[] { "vinted", "depop" }, listedDate = item.ListedDate });

        Assert.Equal(["vinted", "depop"], (await GetItemAsync(item.Id)).Platforms);
    }

    [Fact]
    public async Task Every_change_returns_a_new_version()
    {
        var item = await CreateItemAsync(new { title = "Tee" });

        var updated = await ChangeAsync(HttpMethod.Put, $"/api/items/{item.Id}", item.Version, new { title = "Tee, red" });

        Assert.NotEqual(item.Version, updated.Version);
    }

    [Fact]
    public async Task A_stale_version_is_412_and_changes_nothing()
    {
        var item = await CreateItemAsync(new { title = "Tee" });
        await ChangeAsync(HttpMethod.Put, $"/api/items/{item.Id}", item.Version, new { title = "Edited on the phone" });

        var fromLaptop = await SendAsync(HttpMethod.Put, $"/api/items/{item.Id}", item.Version, new { title = "Edited on the laptop" });

        await ProblemAsync(fromLaptop, HttpStatusCode.PreconditionFailed);
        Assert.Equal("Edited on the phone", (await GetItemAsync(item.Id)).Title);
    }

    [Fact]
    public async Task A_change_without_a_version_is_428()
    {
        var item = await CreateItemAsync(new { title = "Tee" });

        var response = await SendAsync(HttpMethod.Put, $"/api/items/{item.Id}", version: null, new { title = "Tee" });

        await ProblemAsync(response, HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task A_garbled_version_reads_as_stale()
    {
        var item = await CreateItemAsync(new { title = "Tee" });

        var response = await SendAsync(HttpMethod.Put, $"/api/items/{item.Id}", "\"not-a-version\"", new { title = "Tee" });

        await ProblemAsync(response, HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task Get_returns_the_version_as_an_etag()
    {
        var item = await CreateItemAsync(new { title = "Tee" });

        var response = await Client.GetAsync($"/api/items/{item.Id}", Ct);

        Assert.Equal(item.Version, response.Headers.ETag?.ToString());
    }

    [Theory]
    [InlineData("""{ "title": "" }""", "Title")]
    [InlineData("""{ "title": "   " }""", "Title")]
    [InlineData("""{ "title": "Tee", "condition": "Mint" }""", "Condition")]
    [InlineData("""{ "title": "Tee", "platforms": ["grailed"] }""", "Platforms")]
    [InlineData("""{ "title": "Tee", "cost": 8.505 }""", "Cost")]
    [InlineData("""{ "title": "Tee", "cost": -1 }""", "Cost")]
    [InlineData("""{ "title": "Tee", "listPrice": 100000000 }""", "ListPrice")]
    public async Task Rejects_invalid_fields_with_a_reason(string body, string field)
    {
        var response = await Client.PostAsync("/api/items", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Ct);

        var problem = await ValidationProblemAsync(response);
        Assert.Contains(problem.Errors.Keys, k => k.Equals(field, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("""{ "title": "Tee", "cost": "twenty" }""")]
    [InlineData("""{ "title": "Tee", "acquiredDate": "28/08/2026" }""")]
    public async Task Rejects_values_of_the_wrong_type(string body)
    {
        var response = await Client.PostAsync("/api/items", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Ct);

        await ValidationProblemAsync(response);
    }

    [Fact]
    public async Task Rejects_a_title_longer_than_the_shared_limit()
    {
        var response = await Client.PostAsJsonAsync("/api/items", new { title = new string('x', ItemFields.MaxTitle + 1) }, Json, Ct);

        await ValidationProblemAsync(response);
    }

    [Fact]
    public async Task Accepts_a_condition_in_any_case_and_stores_the_canonical_spelling()
    {
        var item = await CreateItemAsync(new { title = "Tee", condition = "new WITH tags" });

        Assert.Equal("New with tags", item.Condition);
    }

    [Fact]
    public async Task Unknown_ids_are_404()
    {
        var response = await Client.GetAsync($"/api/items/{Guid.NewGuid()}", Ct);

        await ProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deleting_an_item_takes_its_sale_and_photo_with_it()
    {
        var item = await CreateItemAsync(new { title = "Tee", platforms = new[] { "ebay" } });
        item = await ChangeAsync(HttpMethod.Put, $"/api/items/{item.Id}/sale", item.Version,
            new { platform = "ebay", price = 20, date = "2026-09-20" });

        var response = await SendAsync(HttpMethod.Delete, $"/api/items/{item.Id}", item.Version);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/items/{item.Id}", Ct)).StatusCode);
        var orphans = await Factory.WithDbAsync(async db =>
            await db.Sales.CountAsync(s => s.ItemId == item.Id) + await db.ItemPlatforms.CountAsync(p => p.ItemId == item.Id));
        Assert.Equal(0, orphans);
    }

    [Fact]
    public async Task Stores_money_as_exact_decimals()
    {
        var item = await CreateItemAsync(new { title = "Tee", cost = 0.29, listPrice = 1204.5 });

        var stored = await Factory.WithDbAsync(db => db.Items.SingleAsync(i => i.Id == item.Id));
        Assert.Equal(0.29m, stored.Cost);
        Assert.Equal(1204.50m, stored.ListPrice);
    }
}
