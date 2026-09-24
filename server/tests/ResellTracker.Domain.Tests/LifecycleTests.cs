namespace ResellTracker.Domain.Tests;

public class LifecycleTests
{
    private static readonly DateOnly Today = new(2026, 9, 24);
    private static readonly DateOnly Earlier = new(2026, 9, 1);
    private static readonly string[] OnDepop = ["depop"];
    private static readonly string[] Nowhere = [];

    [Theory]
    [InlineData(ItemStatus.Inventory, true)]
    [InlineData(ItemStatus.Listed, true)]
    [InlineData(ItemStatus.Sold, false)]
    [InlineData(ItemStatus.Donated, false)]
    public void Only_inventory_and_listed_count_as_stock_on_hand(ItemStatus status, bool onHand) =>
        Assert.Equal(onHand, Lifecycle.IsOnHand(status));

    [Fact]
    public void Adding_the_first_platform_lists_the_item_as_of_today() =>
        Assert.Equal(new ListingState(ItemStatus.Listed, Today),
            Lifecycle.ApplyPlatforms(ItemStatus.Inventory, OnDepop, null, Today));

    [Fact]
    public void Listing_keeps_a_listed_date_already_set() =>
        Assert.Equal(new ListingState(ItemStatus.Listed, Earlier),
            Lifecycle.ApplyPlatforms(ItemStatus.Inventory, OnDepop, Earlier, Today));

    [Fact]
    public void Removing_every_platform_puts_a_listed_item_back_in_stock() =>
        Assert.Equal(new ListingState(ItemStatus.Inventory, Earlier),
            Lifecycle.ApplyPlatforms(ItemStatus.Listed, Nowhere, Earlier, Today));

    [Theory]
    [InlineData(ItemStatus.Sold)]
    [InlineData(ItemStatus.Donated)]
    public void Platforms_never_change_a_finished_item(ItemStatus status)
    {
        Assert.Equal(status, Lifecycle.ApplyPlatforms(status, Nowhere, Earlier, Today).Status);
        Assert.Equal(status, Lifecycle.ApplyPlatforms(status, OnDepop, Earlier, Today).Status);
    }

    [Theory]
    [InlineData(ItemStatus.Inventory)] // sold straight from stock, never listed
    [InlineData(ItemStatus.Listed)]
    [InlineData(ItemStatus.Sold)] // editing a logged sale
    public void Can_sell_anything_on_hand_or_edit_a_sale(ItemStatus status) =>
        Lifecycle.EnsureCanSell(status, price: 20);

    [Fact]
    public void Cannot_sell_a_donated_item() =>
        Assert.Throws<LifecycleException>(() => Lifecycle.EnsureCanSell(ItemStatus.Donated, price: 20));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_sale_needs_an_accepted_offer(decimal price) =>
        Assert.Throws<LifecycleException>(() => Lifecycle.EnsureCanSell(ItemStatus.Listed, price));

    [Theory]
    [InlineData(ItemStatus.Inventory)]
    [InlineData(ItemStatus.Listed)]
    [InlineData(ItemStatus.Donated)] // editing a logged donation
    public void Can_donate_anything_on_hand_or_edit_a_donation(ItemStatus status) =>
        Lifecycle.EnsureCanDonate(status);

    [Fact]
    public void Cannot_donate_a_sold_item() =>
        Assert.Throws<LifecycleException>(() => Lifecycle.EnsureCanDonate(ItemStatus.Sold));

    [Fact]
    public void Undoing_a_sale_goes_back_to_listed_while_still_on_a_platform() =>
        Assert.Equal(ItemStatus.Listed, Lifecycle.UndoSale(ItemStatus.Sold, OnDepop));

    [Fact]
    public void Undoing_a_sale_goes_back_to_stock_with_no_platform() =>
        Assert.Equal(ItemStatus.Inventory, Lifecycle.UndoSale(ItemStatus.Sold, Nowhere));

    [Fact]
    public void Undoing_a_donation_follows_the_same_rule()
    {
        Assert.Equal(ItemStatus.Listed, Lifecycle.UndoDonation(ItemStatus.Donated, OnDepop));
        Assert.Equal(ItemStatus.Inventory, Lifecycle.UndoDonation(ItemStatus.Donated, Nowhere));
    }

    [Theory]
    [InlineData(ItemStatus.Inventory)]
    [InlineData(ItemStatus.Listed)]
    [InlineData(ItemStatus.Donated)]
    public void Cannot_undo_a_sale_that_did_not_happen(ItemStatus status) =>
        Assert.Throws<LifecycleException>(() => Lifecycle.UndoSale(status, OnDepop));

    [Theory]
    [InlineData(ItemStatus.Inventory)]
    [InlineData(ItemStatus.Listed)]
    [InlineData(ItemStatus.Sold)]
    public void Cannot_undo_a_donation_that_did_not_happen(ItemStatus status) =>
        Assert.Throws<LifecycleException>(() => Lifecycle.UndoDonation(status, OnDepop));
}
