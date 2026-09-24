using Microsoft.AspNetCore.Identity;
using ResellTracker.Domain;

namespace ResellTracker.Api.Data;

// Persistence shapes only. They never leave the API: controllers return DTOs
// (see Contracts), and the rules live in ResellTracker.Domain.

public class AppUser : IdentityUser<Guid>
{
    /// <summary>A throwaway account made by "Try the demo"; deleted once it expires.</summary>
    public bool IsDemo { get; set; }

    public DateTimeOffset? DemoExpiresAt { get; set; }
}

public class Item
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public ItemStatus Status { get; set; }

    public string Title { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Category { get; set; } = "";
    public string Size { get; set; } = "";
    public string Condition { get; set; } = ItemFields.DefaultCondition;
    public string Notes { get; set; } = "";
    public decimal Cost { get; set; }
    public string Source { get; set; } = "";
    public DateOnly? AcquiredDate { get; set; }
    public decimal? ListPrice { get; set; }
    public DateOnly? ListedDate { get; set; }

    /// <summary>~96px JPEG, small enough to ride along on every list query.</summary>
    public byte[]? ThumbnailJpeg { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    /// <summary>In listing order: the first one drives projected net and the Sold prefill.</summary>
    public List<ItemPlatform> Platforms { get; set; } = [];
    public Sale? Sale { get; set; }
    public Donation? Donation { get; set; }
    public ItemPhoto? Photo { get; set; }

    public List<string> PlatformIds => [.. Platforms.OrderBy(p => p.Position).Select(p => p.PlatformId)];
}

public class ItemPlatform
{
    public Guid ItemId { get; set; }
    public string PlatformId { get; set; } = "";
    public byte Position { get; set; }
}

public class Sale
{
    public Guid ItemId { get; set; }
    public string Platform { get; set; } = "";

    /// <summary>The asking price when it sold, snapshotted so later item edits can't rewrite history.</summary>
    public decimal ListedFor { get; set; }

    /// <summary>The offer accepted.</summary>
    public decimal Price { get; set; }

    /// <summary>What actually landed; null means not known yet, so fees are estimated.</summary>
    public decimal? Payout { get; set; }

    public decimal ShippingCharged { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal OtherCosts { get; set; }
    public DateOnly Date { get; set; }
}

public class Donation
{
    public Guid ItemId { get; set; }
    public DateOnly Date { get; set; }
    public string Org { get; set; } = "";

    /// <summary>Recorded as typed and used in no calculation.</summary>
    public decimal? ReceiptValue { get; set; }
}

/// <summary>The full-size photo, in its own table so list queries never load it.</summary>
public class ItemPhoto
{
    public Guid ItemId { get; set; }
    public byte[] FullJpeg { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; }
}

public class UserSettings
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Currency { get; set; } = "USD";
    public string Theme { get; set; } = "dark";
    public decimal ProfitGoal { get; set; }
}

/// <summary>
/// A fee schedule the user has saved for one platform. Only saved platforms have a
/// row; reads lay these over the registry defaults (see FeeSettings).
/// </summary>
public class PlatformFeeSetting
{
    public Guid OwnerId { get; set; }
    public string PlatformId { get; set; } = "";
    public decimal Percent { get; set; }
    public decimal Fixed { get; set; }
    public bool IncludesShipping { get; set; }
}
