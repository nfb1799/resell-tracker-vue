using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ResellTracker.Domain;

/// <summary>A sale carried by an imported row, as a backup export writes it.</summary>
public sealed record ImportedSale(
    string Platform,
    decimal ListedFor,
    decimal Price,
    decimal? Payout,
    decimal ShippingCharged,
    decimal ShippingCost,
    decimal OtherCosts,
    DateOnly Date);

public sealed record ImportedDonation(DateOnly Date, string Org, decimal? ReceiptValue);

/// <summary>An imported row, in the same shape the New item form produces.</summary>
public sealed record ImportedItem(
    string Title,
    string Brand,
    string Category,
    string Size,
    string Condition,
    string Notes,
    decimal Cost,
    string Source,
    DateOnly AcquiredDate,
    IReadOnlyList<string> Platforms,
    decimal? ListPrice,
    DateOnly? ListedDate,
    ItemStatus Status,
    ImportedSale? Sale,
    ImportedDonation? Donation);

public enum PhotoKind
{
    Url,
    DataUri,
}

public sealed record ImportedPhoto(string Src, PhotoKind Kind);

/// <summary>One row's verdict: an item to add, or the reasons it is skipped.</summary>
public sealed record ImportRow(int RowNumber, string Label, IReadOnlyList<string> Errors, ImportedItem? Item, ImportedPhoto? Photo)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Bulk import: turns one pasted JSON row into the item the New item form would
/// have made, or says exactly why it can't. The client runs the same rules (and
/// the same shared/import-cases.json) for instant feedback; this is the check
/// that decides.
///
/// Rows use the names a person would write (<c>sourcedFrom</c>, <c>askingPrice</c>,
/// a single <c>listingPlatform</c>) or this app's own (<c>source</c>, <c>listPrice</c>,
/// <c>platforms</c>), so a backup export imports cleanly, sale and donation included.
/// </summary>
public static partial class Import
{
    public const int MaxRows = 1000;

    // Error text echoes values the way JSON.stringify would, so the client's
    // copy of these rules produces identical messages.
    private static readonly JsonSerializerOptions Echo = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"^data:image/[a-z0-9.+-]+;base64,", RegexOptions.IgnoreCase)]
    private static partial Regex DataUriPattern();

    [GeneratedRegex(@"^https?://", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"[$£€,\s]")]
    private static partial Regex MoneyNoise();

    public static ImportRow ReadRow(JsonElement raw, int index, DateOnly today)
    {
        var rowNumber = index + 1;
        if (raw.ValueKind != JsonValueKind.Object)
        {
            return new ImportRow(rowNumber, $"Row {rowNumber}", ["not a JSON object"], null, null);
        }

        var errors = new List<string>();
        var title = Text(First(raw, "title", "name"));
        if (title.Length == 0)
        {
            errors.Add("title is required");
        }

        var condition = ReadCondition(First(raw, "condition"), errors);
        var platforms = ReadPlatforms(First(raw, "listingPlatform", "listingPlatforms", "platform", "platforms"), errors);
        var cost = ReadMoney(First(raw, "cost"), "cost", errors);
        var listPrice = ReadMoney(First(raw, "askingPrice", "listPrice"), "askingPrice", errors);
        var acquiredDate = ReadDate(First(raw, "acquiredDate"), "acquiredDate", errors);
        var listedDate = ReadDate(First(raw, "listedDate"), "listedDate", errors);
        var photo = ReadPhoto(First(raw, "photo", "photoUrl", "image", "imageUrl"), errors);
        var sale = ReadSale(First(raw, "sale"), listPrice, errors);
        var donation = ReadDonation(First(raw, "donation"), errors);
        if (sale is not null && donation is not null)
        {
            errors.Add("a row can't have both a sale and a donation");
        }

        var fields = new (string Name, string Value, int Max)[]
        {
            ("title", title, ItemFields.MaxTitle),
            ("brand", Text(First(raw, "brand")), ItemFields.MaxBrand),
            ("category", Text(First(raw, "category")), ItemFields.MaxCategory),
            ("size", Text(First(raw, "size")), ItemFields.MaxSize),
            ("sourcedFrom", Text(First(raw, "sourcedFrom", "source")), ItemFields.MaxSource),
            ("notes", Text(First(raw, "notes")), ItemFields.MaxNotes),
        };
        foreach (var (name, value, max) in fields.Where(f => f.Value.Length > f.Max))
        {
            errors.Add($"{name} can't be longer than {max} characters");
        }

        var label = title.Length > 0 ? title : $"Row {rowNumber}";
        if (errors.Count > 0)
        {
            return new ImportRow(rowNumber, label, errors, null, null);
        }

        // Naming a platform is what makes an item listed in the form, and the
        // listed date defaults to today there too; a sale or donation ends it.
        var listing = Lifecycle.ApplyPlatforms(ItemStatus.Inventory, platforms, listedDate, today);
        var status = sale is not null ? ItemStatus.Sold : donation is not null ? ItemStatus.Donated : listing.Status;

        var item = new ImportedItem(
            Title: title,
            Brand: fields[1].Value,
            Category: fields[2].Value,
            Size: fields[3].Value,
            Condition: condition ?? ItemFields.DefaultCondition,
            Notes: fields[5].Value,
            Cost: cost ?? 0,
            Source: fields[4].Value,
            AcquiredDate: acquiredDate ?? today,
            Platforms: platforms,
            ListPrice: listPrice,
            ListedDate: listing.ListedDate,
            Status: status,
            Sale: sale,
            Donation: donation);

        return new ImportRow(rowNumber, label, [], item, photo);
    }

    /// <summary>
    /// The rows of an import: a JSON array, a single object (an obvious enough
    /// intent), or a backup export's <c>{ "items": [...] }</c>.
    /// </summary>
    public static IReadOnlyList<JsonElement>? Rows(JsonElement root) => root.ValueKind switch
    {
        JsonValueKind.Array => [.. root.EnumerateArray()],
        JsonValueKind.Object when root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array =>
            [.. items.EnumerateArray()],
        JsonValueKind.Object => [root],
        _ => null,
    };

    private static JsonElement? First(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetProperty(name, out var value) && !IsBlank(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool IsBlank(JsonElement value) =>
        value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
        (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()));

    private static string Text(JsonElement? value) => value switch
    {
        null => "",
        { ValueKind: JsonValueKind.String } v => v.GetString()!.Trim(),
        { } v => v.GetRawText().Trim(),
    };

    private static string Quote(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? JsonSerializer.Serialize(value.GetString(), Echo) : value.GetRawText();

    /// <summary>
    /// Accepts 20, "20" and "$20.50"; refuses "twenty" rather than storing 0, since a
    /// cost that silently becomes zero corrupts every profit figure downstream.
    /// </summary>
    private static decimal? ReadMoney(JsonElement? value, string field, List<string> errors)
    {
        if (value is not { } v)
        {
            return null;
        }

        decimal amount;
        if (v.ValueKind == JsonValueKind.Number)
        {
            if (!v.TryGetDecimal(out amount))
            {
                errors.Add($"{field} is not a number");
                return null;
            }
        }
        else if (v.ValueKind == JsonValueKind.String)
        {
            var cleaned = MoneyNoise().Replace(v.GetString()!, "");
            if (!decimal.TryParse(cleaned, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount))
            {
                errors.Add($"{field} is not a number (got {Quote(v)})");
                return null;
            }
        }
        else
        {
            errors.Add($"{field} is not a number");
            return null;
        }

        if (amount < 0)
        {
            errors.Add($"{field} can't be negative");
        }
        else if (!Money.IsWholeCents(amount))
        {
            errors.Add($"{field} can't have more than 2 decimal places");
        }
        else if (amount > Money.MaxAmount)
        {
            errors.Add($"{field} is too large");
        }
        else
        {
            return amount;
        }

        return null;
    }

    private static DateOnly? ReadDate(JsonElement? value, string field, List<string> errors)
    {
        if (value is not { } v)
        {
            return null;
        }

        var text = v.ValueKind == JsonValueKind.String ? v.GetString()!.Trim() : null;
        if (text is null || !DatePattern().IsMatch(text))
        {
            errors.Add($"{field} must look like 2026-08-28 (got {Quote(v)})");
            return null;
        }

        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            errors.Add($"{field} is not a real date (got {Quote(v)})");
            return null;
        }

        return date;
    }

    private static string? ReadCondition(JsonElement? value, List<string> errors)
    {
        if (value is not { } v)
        {
            return null;
        }

        var match = ItemFields.MatchCondition(Text(v));
        if (match is null)
        {
            errors.Add($"condition {Quote(v)} is not one of {string.Join(", ", ItemFields.Conditions)}");
        }

        return match;
    }

    /// <summary>"Depop", "depop", "eBay" and "ebay" all land on the right id.</summary>
    private static string? MatchPlatform(string value) =>
        Platforms.All.FirstOrDefault(p =>
            string.Equals(p.Id, value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.Label, value, StringComparison.OrdinalIgnoreCase))?.Id;

    private static List<string> ReadPlatforms(JsonElement? value, List<string> errors)
    {
        var ids = new List<string>();
        if (value is not { } v)
        {
            return ids;
        }

        var entries = v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().ToList() : [v];
        foreach (var entry in entries.Where(e => !IsBlank(e)))
        {
            var id = MatchPlatform(Text(entry));
            if (id is null)
            {
                errors.Add($"listingPlatform {Quote(entry)} is not one of {string.Join(", ", Platforms.All.Select(p => p.Label))}");
            }
            else if (!ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>A photo is somewhere to fetch from or an inline data URI; anything else is a typo worth reporting.</summary>
    private static ImportedPhoto? ReadPhoto(JsonElement? value, List<string> errors)
    {
        if (value is not { } v)
        {
            return null;
        }

        var src = Text(v);
        if (DataUriPattern().IsMatch(src))
        {
            return new ImportedPhoto(src, PhotoKind.DataUri);
        }

        if (UrlPattern().IsMatch(src))
        {
            return new ImportedPhoto(src, PhotoKind.Url);
        }

        errors.Add("photo must be an http(s) URL or a data:image/…;base64 URI");
        return null;
    }

    private static ImportedSale? ReadSale(JsonElement? value, decimal? listPrice, List<string> errors)
    {
        if (value is not { } v)
        {
            return null;
        }

        if (v.ValueKind != JsonValueKind.Object)
        {
            errors.Add("sale must be an object");
            return null;
        }

        var before = errors.Count;
        var platformValue = First(v, "platform");
        var platform = platformValue is { } p ? MatchPlatform(Text(p)) : null;
        if (platform is null)
        {
            errors.Add(platformValue is { } given
                ? $"sale.platform {Quote(given)} is not one of {string.Join(", ", Platforms.All.Select(pl => pl.Label))}"
                : "sale.platform is required");
        }

        var priceValue = First(v, "price");
        var price = ReadMoney(priceValue, "sale.price", errors);
        if (priceValue is null)
        {
            errors.Add("sale.price is required");
        }
        else if (price == 0)
        {
            errors.Add("sale.price must be more than 0");
        }

        var listedFor = ReadMoney(First(v, "listedFor"), "sale.listedFor", errors);
        var payout = ReadMoney(First(v, "payout"), "sale.payout", errors);
        var shippingCharged = ReadMoney(First(v, "shippingCharged"), "sale.shippingCharged", errors);
        var shippingCost = ReadMoney(First(v, "shippingCost"), "sale.shippingCost", errors);
        var otherCosts = ReadMoney(First(v, "otherCosts"), "sale.otherCosts", errors);
        var date = ReadDate(First(v, "date"), "sale.date", errors);
        if (First(v, "date") is null)
        {
            errors.Add("sale.date is required");
        }

        return errors.Count > before
            ? null
            : new ImportedSale(platform!, listedFor ?? listPrice ?? 0, price!.Value, payout, shippingCharged ?? 0, shippingCost ?? 0, otherCosts ?? 0, date!.Value);
    }

    private static ImportedDonation? ReadDonation(JsonElement? value, List<string> errors)
    {
        if (value is not { } v)
        {
            return null;
        }

        if (v.ValueKind != JsonValueKind.Object)
        {
            errors.Add("donation must be an object");
            return null;
        }

        var before = errors.Count;
        var date = ReadDate(First(v, "date"), "donation.date", errors);
        if (First(v, "date") is null)
        {
            errors.Add("donation.date is required");
        }

        var org = Text(First(v, "org"));
        if (org.Length > ItemFields.MaxDonationOrg)
        {
            errors.Add($"donation.org can't be longer than {ItemFields.MaxDonationOrg} characters");
        }

        var receipt = ReadMoney(First(v, "receiptValue"), "donation.receiptValue", errors);
        return errors.Count > before ? null : new ImportedDonation(date!.Value, org, receipt);
    }
}
