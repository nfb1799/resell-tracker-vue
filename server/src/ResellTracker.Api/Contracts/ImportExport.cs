using ResellTracker.Domain;

namespace ResellTracker.Api.Contracts;

/// <summary>One row's outcome. An added row carries the item it became, so the client can attach its photo.</summary>
public sealed record ImportRowResult(int RowNumber, string Label, string Status, IReadOnlyList<string> Errors, ItemResponse? Item);

/// <summary>The run's summary, "11 added, 1 skipped", and every row's outcome.</summary>
public sealed record ImportResponse(int Added, int Skipped, IReadOnlyList<ImportRowResult> Rows);

// The backup file. It uses this app's own field names, so it imports straight
// back in (see Import.ReadRow); photos are left out, as in the original.

public sealed record BackupSale(
    string Platform,
    decimal ListedFor,
    decimal Price,
    decimal? Payout,
    decimal ShippingCharged,
    decimal ShippingCost,
    decimal OtherCosts,
    DateOnly Date);

public sealed record BackupDonation(DateOnly Date, string Org, decimal? ReceiptValue);

public sealed record BackupItem(
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
    ItemStatus Status,
    BackupSale? Sale,
    BackupDonation? Donation,
    DateTimeOffset CreatedAt);

public sealed record BackupSettings(
    string DisplayName,
    string Currency,
    string Theme,
    decimal ProfitGoal,
    IReadOnlyDictionary<string, FeeScheduleDto> Fees);

public sealed record Backup(DateTimeOffset ExportedAt, BackupSettings Settings, IReadOnlyList<BackupItem> Items);
