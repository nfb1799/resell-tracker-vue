using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ResellTracker.Api.Contracts;
using ResellTracker.Api.Data;
using ResellTracker.Domain;

namespace ResellTracker.Api.Services;

/// <summary>
/// The CSV (every item with its full profit breakdown, ready for bookkeeping) and
/// the raw JSON backup. Both computed here, from the same math as everything else.
/// </summary>
public sealed class ExportService(AppDbContext db, ICurrentUser user, SettingsService settings)
{
    // The original app's columns, in its order.
    private static readonly string[] Headers =
    [
        "Title", "Brand", "Category", "Size", "Condition", "Status",
        "Source", "Acquired", "Cost", "Listed on", "Listed date", "List price",
        "Sold platform", "Sold date", "Listed for at sale", "Offer accepted",
        "Shipping charged", "Payout", "Fees kept by platform", "Fees estimated",
        "Shipping cost", "Other costs", "Net profit", "Margin %",
        "Donated date", "Donated to", "Receipt value", "Written off",
        "Days listed", "Notes",
    ];

    /// <summary>Every item (or only one status's), newest first, as UTF-8 CSV with a BOM so Excel reads accents.</summary>
    public async Task<byte[]> CsvAsync(ItemStatus? status, DateOnly today, CancellationToken ct)
    {
        var fees = await settings.GetFeeSettingsAsync(ct);
        var items = await LoadAsync(status, ct);

        var csv = new StringBuilder();
        csv.AppendJoin(',', Headers).Append("\r\n");
        foreach (var item in items)
        {
            csv.AppendJoin(',', Row(item, fees, today)).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    public async Task<Backup> BackupAsync(CancellationToken ct)
    {
        var general = await settings.GetAsync(ct);
        var items = await LoadAsync(null, ct);
        return new Backup(
            DateTimeOffset.UtcNow,
            new BackupSettings(general.DisplayName, general.Currency, general.Theme, general.ProfitGoal, await settings.GetFeesAsync(ct)),
            [.. items.Select(i => new BackupItem(
                i.Title, i.Brand, i.Category, i.Size, i.Condition, i.Notes, i.Cost, i.Source, i.AcquiredDate,
                i.PlatformIds, i.ListPrice, i.ListedDate, i.Status,
                i.Sale is { } s ? new BackupSale(s.Platform, s.ListedFor, s.Price, s.Payout, s.ShippingCharged, s.ShippingCost, s.OtherCosts, s.Date) : null,
                i.Donation is { } d ? new BackupDonation(d.Date, d.Org, d.ReceiptValue) : null,
                i.CreatedAt))]);
    }

    private Task<List<Item>> LoadAsync(ItemStatus? status, CancellationToken ct) =>
        db.Items.AsNoTracking()
            .Where(i => i.OwnerId == user.Id && (status == null || i.Status == status))
            .Include(i => i.Platforms)
            .Include(i => i.Sale)
            .Include(i => i.Donation)
            .AsSplitQuery()
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

    private static IEnumerable<string> Row(Item item, FeeSettings fees, DateOnly today)
    {
        var sold = item.Status == ItemStatus.Sold && item.Sale is not null;
        var donated = item.Status == ItemStatus.Donated && item.Donation is not null;
        var s = item.Sale;
        var p = sold
            ? Profit.Compute(item.Cost, new SaleFigures(s!.Platform, s.Price, s.ShippingCharged, s.Payout, s.ShippingCost, s.OtherCosts), fees)
            : null;

        return
        [
            Text(item.Title), Text(item.Brand), Text(item.Category), Text(item.Size), Text(item.Condition),
            Text(item.Status.ToString().ToLowerInvariant()), Text(item.Source), Date(item.AcquiredDate), Money(item.Cost),
            Text(string.Join(" + ", item.PlatformIds)), Date(item.ListedDate), Money(item.ListPrice),
            sold ? Text(s!.Platform) : "", sold ? Date(s!.Date) : "",
            sold ? Money(s!.ListedFor) : "", sold ? Money(s!.Price) : "", sold ? Money(s!.ShippingCharged) : "",
            sold ? Money(p!.Payout) : "", sold ? Money(p!.Fees) : "", sold ? (p!.FeesEstimated ? "yes" : "no") : "",
            sold ? Money(p!.ShippingCost) : "", sold ? Money(p!.OtherCosts) : "", sold ? Money(p!.Net) : "",
            p?.Margin is { } margin ? (margin * 100).ToString("0.0", CultureInfo.InvariantCulture) : "",
            donated ? Date(item.Donation!.Date) : "", donated ? Text(item.Donation!.Org) : "",
            donated ? Money(item.Donation!.ReceiptValue) : "", donated ? Money(item.Cost) : "",
            Aging.DaysListed(item.Status, item.AcquiredDate, item.ListedDate, s?.Date, today)?.ToString(CultureInfo.InvariantCulture) ?? "",
            Text(item.Notes),
        ];
    }

    private static string Money(decimal? value) => value?.ToString("0.00", CultureInfo.InvariantCulture) ?? "";

    private static string Date(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

    /// <summary>
    /// A text cell, quoted when it holds a comma, quote or line break. A cell that
    /// would start with = + - or @ gets a leading apostrophe, so a title can't be
    /// run as a formula when the file is opened in a spreadsheet (CSV injection).
    /// </summary>
    private static string Text(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0], StringComparison.Ordinal))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : value;
    }
}
