using System.ComponentModel.DataAnnotations;
using ResellTracker.Domain;

namespace ResellTracker.Api.Contracts;

/// <summary>
/// Creates or replaces an item's own fields. Status is never sent: it follows the
/// platforms (see Lifecycle.ApplyPlatforms), and selling or donating has its own
/// endpoints.
/// </summary>
public sealed class ItemRequest : IValidatableObject
{
    /// <summary>Client-generated on create, so a replayed offline create is recognisable.</summary>
    public Guid? Id { get; init; }

    [Required]
    public string Title { get; init; } = "";

    public string? Brand { get; init; }
    public string? Category { get; init; }
    public string? Size { get; init; }
    public string? Condition { get; init; }
    public string? Notes { get; init; }

    [Money]
    public decimal? Cost { get; init; }

    public string? Source { get; init; }
    public DateOnly? AcquiredDate { get; init; }

    /// <summary>In listing order; the first is the one projected net and the Sold prefill use.</summary>
    public List<string>? Platforms { get; init; }

    [Money]
    public decimal? ListPrice { get; init; }

    public DateOnly? ListedDate { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var errors = Rules.MaxLength(Title, ItemFields.MaxTitle, nameof(Title))
            .Concat(Rules.MaxLength(Brand, ItemFields.MaxBrand, nameof(Brand)))
            .Concat(Rules.MaxLength(Category, ItemFields.MaxCategory, nameof(Category)))
            .Concat(Rules.MaxLength(Size, ItemFields.MaxSize, nameof(Size)))
            .Concat(Rules.MaxLength(Source, ItemFields.MaxSource, nameof(Source)))
            .Concat(Rules.MaxLength(Notes, ItemFields.MaxNotes, nameof(Notes)))
            .Concat((Platforms ?? []).SelectMany(p => Rules.KnownPlatform(p, nameof(Platforms))));

        foreach (var error in errors)
        {
            yield return error;
        }

        if (!string.IsNullOrWhiteSpace(Condition) && ItemFields.MatchCondition(Condition) is null)
        {
            yield return new ValidationResult(
                $"\"{Condition}\" is not one of {string.Join(", ", ItemFields.Conditions)}.", [nameof(Condition)]);
        }
    }
}

/// <summary>Logs or edits a sale. The item's status becomes sold.</summary>
public sealed class SaleRequest : IValidatableObject
{
    [Required]
    public string Platform { get; init; } = "";

    /// <summary>The asking price at the time; defaults to the item's list price.</summary>
    [Money]
    public decimal? ListedFor { get; init; }

    /// <summary>The offer accepted.</summary>
    [Required, Money(Positive = true)]
    public decimal? Price { get; init; }

    /// <summary>What actually landed. Leave null until it clears; fees are estimated until then.</summary>
    [Money]
    public decimal? Payout { get; init; }

    [Money]
    public decimal? ShippingCharged { get; init; }

    [Money]
    public decimal? ShippingCost { get; init; }

    [Money]
    public decimal? OtherCosts { get; init; }

    [Required]
    public DateOnly? Date { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        Rules.KnownPlatform(Platform, nameof(Platform));
}

/// <summary>Logs or edits a donation. The item's status becomes donated.</summary>
public sealed class DonationRequest : IValidatableObject
{
    [Required]
    public DateOnly? Date { get; init; }

    public string? Org { get; init; }

    /// <summary>Stored as typed; used in no calculation.</summary>
    [Money]
    public decimal? ReceiptValue { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) =>
        Rules.MaxLength(Org, ItemFields.MaxDonationOrg, nameof(Org));
}

public sealed record SaleResponse(
    string Platform,
    decimal ListedFor,
    decimal Price,
    decimal? Payout,
    decimal ShippingCharged,
    decimal ShippingCost,
    decimal OtherCosts,
    DateOnly Date);

public sealed record DonationResponse(DateOnly Date, string Org, decimal? ReceiptValue);

public sealed record ProfitResponse(
    decimal Gross,
    decimal Payout,
    decimal Fees,
    decimal Cogs,
    decimal ShippingCost,
    decimal OtherCosts,
    decimal Costs,
    decimal Net,
    decimal? Margin,
    decimal? Roi,
    bool FeesEstimated);

public sealed record ItemResponse(
    Guid Id,
    ItemStatus Status,
    string Title,
    string Brand,
    string Category,
    string Size,
    string Condition,
    string Notes,
    decimal Cost,
    string Source,
    DateOnly? AcquiredDate,
    IReadOnlyList<string> Platforms,
    decimal? ListPrice,
    DateOnly? ListedDate,
    // The ~96px thumbnail as a data: URL, ready for an img src; null without a photo.
    string? Thumbnail,
    SaleResponse? Sale,
    DonationResponse? Donation,
    // The full breakdown, for sold items only.
    ProfitResponse? Profit,
    // Estimated net at the asking price, for items still on hand with one.
    decimal? ProjectedNet,
    int? DaysListed,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    // The value to send back in If-Match; also returned as the ETag header.
    string Version);
