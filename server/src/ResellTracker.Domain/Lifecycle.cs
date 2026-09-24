namespace ResellTracker.Domain;

/// <summary>
/// An item's lifecycle. Two statuses are endings:
/// <code>
///   inventory → listed → sold      the money came back
///                      → donated   it did not, and the cost is written off
/// </code>
/// Inventory and Listed are never chosen directly: they follow the item's
/// platforms. Anything in either is stock on hand, which is what "cash tied up"
/// and the aging buckets count; sold and donated items have left the shelf.
/// </summary>
public enum ItemStatus
{
    Inventory,
    Listed,
    Sold,
    Donated,
}

/// <summary>A change the lifecycle doesn't allow, with a reason fit to show the user.</summary>
public sealed class LifecycleException(string message) : Exception(message);

/// <summary>Where an on-hand item stands after its platforms change.</summary>
public sealed record ListingState(ItemStatus Status, DateOnly? ListedDate);

public static class Lifecycle
{
    public static bool IsOnHand(ItemStatus status) => status is ItemStatus.Inventory or ItemStatus.Listed;

    /// <summary>
    /// Adding the first platform makes a stocked item listed (listed date defaulting
    /// to today); removing the last one puts it back in stock. Sold and donated
    /// items keep their status whatever their platforms say.
    /// </summary>
    public static ListingState ApplyPlatforms(
        ItemStatus status, IReadOnlyCollection<string> platforms, DateOnly? listedDate, DateOnly today)
    {
        var onPlatform = platforms.Count > 0;
        return status switch
        {
            ItemStatus.Inventory when onPlatform => new ListingState(ItemStatus.Listed, listedDate ?? today),
            ItemStatus.Listed when !onPlatform => new ListingState(ItemStatus.Inventory, listedDate),
            _ => new ListingState(status, listedDate),
        };
    }

    /// <summary>
    /// Selling is open to anything on hand, listed or not, and to a sold item whose
    /// sale is being edited. A donated item has to have its donation undone first.
    /// </summary>
    public static void EnsureCanSell(ItemStatus status, decimal price)
    {
        if (status is ItemStatus.Donated)
        {
            throw new LifecycleException("A donated item can't be sold. Undo the donation first.");
        }

        if (price <= 0)
        {
            throw new LifecycleException("Enter the offer you accepted.");
        }
    }

    /// <summary>Donating is open to anything on hand, and to a donated item being edited.</summary>
    public static void EnsureCanDonate(ItemStatus status)
    {
        if (status is ItemStatus.Sold)
        {
            throw new LifecycleException("A sold item can't be donated. Undo the sale first.");
        }
    }

    public static ItemStatus UndoSale(ItemStatus status, IReadOnlyCollection<string> platforms) =>
        status is ItemStatus.Sold
            ? StatusAfterUndo(platforms)
            : throw new LifecycleException("This item hasn't been sold.");

    public static ItemStatus UndoDonation(ItemStatus status, IReadOnlyCollection<string> platforms) =>
        status is ItemStatus.Donated
            ? StatusAfterUndo(platforms)
            : throw new LifecycleException("This item hasn't been donated.");

    /// <summary>Back to listed if it is still on a platform, otherwise into plain stock.</summary>
    private static ItemStatus StatusAfterUndo(IReadOnlyCollection<string> platforms) =>
        platforms.Count > 0 ? ItemStatus.Listed : ItemStatus.Inventory;
}
