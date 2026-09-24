using System.Net;
using ResellTracker.Api.Contracts;
using ResellTracker.Domain;

namespace ResellTracker.Api.Tests;

public class SaleAndDonationApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private Task<ItemResponse> ListedAsync(decimal listPrice = 40, decimal cost = 8) =>
        CreateItemAsync(new { title = "Levi's 501", platforms = new[] { "ebay" }, listPrice, cost, listedDate = "2026-09-01" });

    private Task<ItemResponse> SellAsync(ItemResponse item, object sale) =>
        ChangeAsync(HttpMethod.Put, $"/api/items/{item.Id}/sale", item.Version, sale);

    [Fact]
    public async Task A_sale_with_its_payout_has_exact_fees()
    {
        var item = await ListedAsync();

        var sold = await SellAsync(item, new
        {
            platform = "ebay",
            price = 40,
            shippingCharged = 5,
            payout = 38.64,
            shippingCost = 4.5,
            otherCosts = 0.5,
            date = "2026-09-20",
        });

        Assert.Equal(ItemStatus.Sold, sold.Status);
        Assert.NotNull(sold.Profit);
        Assert.False(sold.Profit.FeesEstimated);
        Assert.Equal(45m, sold.Profit.Gross);
        Assert.Equal(6.36m, sold.Profit.Fees);
        Assert.Equal(25.64m, sold.Profit.Net);
        Assert.Equal(19, sold.DaysListed); // listed Sept 1, sold Sept 20
        Assert.Null(sold.ProjectedNet);
    }

    [Fact]
    public async Task A_sale_without_a_payout_is_estimated_from_the_rate_table()
    {
        var sold = await SellAsync(await ListedAsync(), new { platform = "ebay", price = 150, date = "2026-09-20" });

        Assert.True(sold.Profit!.FeesEstimated);
        Assert.Equal(20.28m, sold.Profit.Fees); // the halfway case, rounded up
        Assert.Null(sold.Sale!.Payout);
    }

    [Fact]
    public async Task Listed_for_is_snapshotted_from_the_asking_price()
    {
        var item = await ListedAsync(listPrice: 40);
        var sold = await SellAsync(item, new { platform = "ebay", price = 32, date = "2026-09-20" });

        // Editing the sale later (to add the payout) keeps the snapshot.
        var edited = await SellAsync(sold, new { platform = "ebay", price = 32, payout = 27.5, date = "2026-09-20" });

        Assert.Equal(40m, sold.Sale!.ListedFor);
        Assert.Equal(40m, edited.Sale!.ListedFor);
        Assert.False(edited.Profit!.FeesEstimated);
    }

    [Fact]
    public async Task Stock_that_was_never_listed_can_be_sold()
    {
        var item = await CreateItemAsync(new { title = "Tee", cost = 2 });

        var sold = await SellAsync(item, new { platform = "vinted", price = 10, payout = 10, date = "2026-09-20" });

        Assert.Equal(ItemStatus.Sold, sold.Status);
        Assert.Equal(8m, sold.Profit!.Net);
    }

    [Fact]
    public async Task A_sale_needs_a_positive_accepted_offer()
    {
        var item = await ListedAsync();

        var response = await SendAsync(HttpMethod.Put, $"/api/items/{item.Id}/sale", item.Version,
            new { platform = "ebay", price = 0, date = "2026-09-20" });

        var problem = await ValidationProblemAsync(response);
        Assert.Contains("Price", problem.Errors.Keys);
    }

    [Fact]
    public async Task Undoing_a_sale_returns_it_to_listed_and_drops_the_sale()
    {
        var sold = await SellAsync(await ListedAsync(), new { platform = "ebay", price = 30, date = "2026-09-20" });

        var undone = await ChangeAsync(HttpMethod.Delete, $"/api/items/{sold.Id}/sale", sold.Version);

        Assert.Equal(ItemStatus.Listed, undone.Status);
        Assert.Null(undone.Sale);
        Assert.Null(undone.Profit);
    }

    [Fact]
    public async Task Undoing_a_sale_of_unlisted_stock_returns_it_to_stock()
    {
        var item = await CreateItemAsync(new { title = "Tee" });
        var sold = await SellAsync(item, new { platform = "vinted", price = 10, date = "2026-09-20" });

        var undone = await ChangeAsync(HttpMethod.Delete, $"/api/items/{sold.Id}/sale", sold.Version);

        Assert.Equal(ItemStatus.Inventory, undone.Status);
    }

    [Fact]
    public async Task Donating_records_the_details_and_leaves_profit_alone()
    {
        var item = await ListedAsync(cost: 18);

        var donated = await ChangeAsync(HttpMethod.Put, $"/api/items/{item.Id}/donation", item.Version,
            new { date = "2026-09-21", org = " Goodwill ", receiptValue = 25 });

        Assert.Equal(ItemStatus.Donated, donated.Status);
        Assert.Equal(new DonationResponse(new DateOnly(2026, 9, 21), "Goodwill", 25m), donated.Donation);
        Assert.Null(donated.Profit);
        Assert.Null(donated.ProjectedNet); // no longer on hand
    }

    [Fact]
    public async Task A_donated_item_cannot_be_sold_until_the_donation_is_undone()
    {
        var item = await ListedAsync();
        var donated = await ChangeAsync(HttpMethod.Put, $"/api/items/{item.Id}/donation", item.Version, new { date = "2026-09-21" });

        var sell = await SendAsync(HttpMethod.Put, $"/api/items/{item.Id}/sale", donated.Version,
            new { platform = "ebay", price = 30, date = "2026-09-22" });

        var problem = await ProblemAsync(sell, HttpStatusCode.Conflict);
        Assert.Contains("Undo the donation", problem.Detail, StringComparison.Ordinal);

        var undone = await ChangeAsync(HttpMethod.Delete, $"/api/items/{item.Id}/donation", donated.Version);
        Assert.Equal(ItemStatus.Listed, undone.Status);
        Assert.Equal(ItemStatus.Sold, (await SellAsync(undone, new { platform = "ebay", price = 30, date = "2026-09-22" })).Status);
    }

    [Fact]
    public async Task A_sold_item_cannot_be_donated()
    {
        var sold = await SellAsync(await ListedAsync(), new { platform = "ebay", price = 30, date = "2026-09-20" });

        var response = await SendAsync(HttpMethod.Put, $"/api/items/{sold.Id}/donation", sold.Version, new { date = "2026-09-21" });

        await ProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Undoing_something_that_did_not_happen_is_a_conflict()
    {
        var item = await ListedAsync();

        await ProblemAsync(await SendAsync(HttpMethod.Delete, $"/api/items/{item.Id}/sale", item.Version), HttpStatusCode.Conflict);
        await ProblemAsync(await SendAsync(HttpMethod.Delete, $"/api/items/{item.Id}/donation", item.Version), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_sale_on_a_stale_version_is_refused()
    {
        var item = await ListedAsync();
        await ChangeAsync(HttpMethod.Put, $"/api/items/{item.Id}", item.Version, new { title = "Renamed", platforms = new[] { "ebay" } });

        var response = await SendAsync(HttpMethod.Put, $"/api/items/{item.Id}/sale", item.Version,
            new { platform = "ebay", price = 30, date = "2026-09-20" });

        await ProblemAsync(response, HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task Editing_the_item_after_a_sale_keeps_it_sold()
    {
        var sold = await SellAsync(await ListedAsync(), new { platform = "ebay", price = 30, date = "2026-09-20" });

        var edited = await ChangeAsync(HttpMethod.Put, $"/api/items/{sold.Id}", sold.Version,
            new { title = "Levi's 501, corrected", platforms = Array.Empty<string>(), listPrice = 99, cost = 8 });

        Assert.Equal(ItemStatus.Sold, edited.Status);
        Assert.Equal(40m, edited.Sale!.ListedFor);
    }
}
