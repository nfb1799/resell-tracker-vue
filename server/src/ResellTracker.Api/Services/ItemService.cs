using Microsoft.EntityFrameworkCore;
using ResellTracker.Api.Contracts;
using ResellTracker.Api.Data;
using ResellTracker.Domain;

namespace ResellTracker.Api.Services;

/// <summary>What the item list can be narrowed by. All optional.</summary>
public sealed record ItemQuery(ItemStatus? Status, string? Platform, string? Q);

/// <summary>
/// Everything that reads or changes items. Every query starts from
/// <see cref="OwnedItems"/>, so nothing here can see another user's data, and every
/// change to an existing item checks the version the caller last saw.
/// </summary>
public sealed class ItemService(AppDbContext db, ICurrentUser user, SettingsService settings, TimeProvider clock)
{
    public const int MaxThumbnailBytes = 8000;
    public const int MaxPhotoBytes = 200 * 1024;

    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    private IQueryable<Item> OwnedItems() =>
        db.Items.Where(i => i.OwnerId == user.Id)
            .Include(i => i.Platforms)
            .Include(i => i.Sale)
            .Include(i => i.Donation)
            .AsSplitQuery();

    public async Task<List<ItemResponse>> ListAsync(ItemQuery query, CancellationToken ct)
    {
        var items = OwnedItems().AsNoTracking();

        if (query.Status is { } status)
        {
            items = items.Where(i => i.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Platform))
        {
            // A sold item belongs to the platform it actually sold on; anything else
            // to the platforms it is (or was, before being donated) listed on.
            var platform = query.Platform;
            items = items.Where(i => i.Status == ItemStatus.Sold
                ? i.Sale!.Platform == platform
                : i.Platforms.Any(p => p.PlatformId == platform));
        }

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var q = query.Q.Trim();
            items = items.Where(i =>
                i.Title.Contains(q) || i.Brand.Contains(q) || i.Category.Contains(q) || i.Size.Contains(q) ||
                i.Source.Contains(q) || i.Notes.Contains(q) || (i.Donation != null && i.Donation.Org.Contains(q)));
        }

        var list = await items.OrderByDescending(i => i.CreatedAt).ToListAsync(ct);
        var fees = await settings.GetFeeSettingsAsync(ct);
        return [.. list.Select(i => ToResponse(i, fees))];
    }

    public async Task<ItemResponse> GetAsync(Guid id, CancellationToken ct) =>
        ToResponse(await FindAsync(id, ct), await settings.GetFeeSettingsAsync(ct));

    public async Task<ItemResponse> CreateAsync(ItemRequest request, CancellationToken ct)
    {
        var id = request.Id ?? Guid.CreateVersion7();
        if (await db.Items.AnyAsync(i => i.Id == id, ct))
        {
            throw new DuplicateItemException();
        }

        var now = clock.GetUtcNow();
        var item = new Item { Id = id, OwnerId = user.Id, Status = ItemStatus.Inventory, CreatedAt = now, UpdatedAt = now };
        ApplyFields(item, request);
        db.Items.Add(item);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public Task<ItemResponse> UpdateAsync(Guid id, byte[] version, ItemRequest request, CancellationToken ct) =>
        ChangeAsync(id, version, item => ApplyFields(item, request), ct);

    public async Task DeleteAsync(Guid id, byte[] version, CancellationToken ct)
    {
        var item = await FindAsync(id, ct);
        ExpectVersion(item, version);
        db.Items.Remove(item); // the database cascades to platforms, sale, donation and photo
        await SaveAsync(ct);
    }

    public Task<ItemResponse> SellAsync(Guid id, byte[] version, SaleRequest request, CancellationToken ct) =>
        ChangeAsync(id, version, item =>
        {
            var price = request.Price!.Value;
            Lifecycle.EnsureCanSell(item.Status, price);

            // Listed-for is snapshotted: an edit keeps the original snapshot unless a
            // new one is sent, so changing the item's price later can't rewrite history.
            var listedFor = request.ListedFor ?? item.Sale?.ListedFor ?? item.ListPrice ?? 0;
            item.Sale ??= new Sale { ItemId = item.Id };
            item.Sale.Platform = request.Platform;
            item.Sale.ListedFor = listedFor;
            item.Sale.Price = price;
            item.Sale.Payout = request.Payout;
            item.Sale.ShippingCharged = request.ShippingCharged ?? 0;
            item.Sale.ShippingCost = request.ShippingCost ?? 0;
            item.Sale.OtherCosts = request.OtherCosts ?? 0;
            item.Sale.Date = request.Date!.Value;
            item.Status = ItemStatus.Sold;
        }, ct);

    public Task<ItemResponse> UndoSaleAsync(Guid id, byte[] version, CancellationToken ct) =>
        ChangeAsync(id, version, item =>
        {
            item.Status = Lifecycle.UndoSale(item.Status, item.PlatformIds);
            item.Sale = null;
        }, ct);

    public Task<ItemResponse> DonateAsync(Guid id, byte[] version, DonationRequest request, CancellationToken ct) =>
        ChangeAsync(id, version, item =>
        {
            Lifecycle.EnsureCanDonate(item.Status);
            item.Donation ??= new Donation { ItemId = item.Id };
            item.Donation.Date = request.Date!.Value;
            item.Donation.Org = request.Org?.Trim() ?? "";
            item.Donation.ReceiptValue = request.ReceiptValue;
            item.Status = ItemStatus.Donated;
        }, ct);

    public Task<ItemResponse> UndoDonationAsync(Guid id, byte[] version, CancellationToken ct) =>
        ChangeAsync(id, version, item =>
        {
            item.Status = Lifecycle.UndoDonation(item.Status, item.PlatformIds);
            item.Donation = null;
        }, ct);

    public async Task<ItemResponse> SetPhotoAsync(Guid id, byte[] version, byte[] thumbnail, byte[] full, CancellationToken ct)
    {
        EnsureJpeg(thumbnail, MaxThumbnailBytes, "thumbnail");
        EnsureJpeg(full, MaxPhotoBytes, "full");

        var photo = await db.ItemPhotos.SingleOrDefaultAsync(p => p.ItemId == id, ct);
        return await ChangeAsync(id, version, item =>
        {
            item.ThumbnailJpeg = thumbnail;
            if (photo is null)
            {
                photo = new ItemPhoto { ItemId = item.Id };
                db.ItemPhotos.Add(photo);
            }

            photo.FullJpeg = full;
            photo.UpdatedAt = clock.GetUtcNow();
        }, ct);
    }

    public async Task<ItemResponse> DeletePhotoAsync(Guid id, byte[] version, CancellationToken ct)
    {
        var photo = await db.ItemPhotos.SingleOrDefaultAsync(p => p.ItemId == id, ct);
        return await ChangeAsync(id, version, item =>
        {
            item.ThumbnailJpeg = null;
            if (photo is not null)
            {
                db.ItemPhotos.Remove(photo);
            }
        }, ct);
    }

    /// <summary>The full-size photo, or null if the item has none.</summary>
    public async Task<ItemPhoto?> GetPhotoAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Items.AnyAsync(i => i.Id == id && i.OwnerId == user.Id, ct))
        {
            throw new ItemNotFoundException();
        }

        return await db.ItemPhotos.AsNoTracking().SingleOrDefaultAsync(p => p.ItemId == id, ct);
    }

    private async Task<Item> FindAsync(Guid id, CancellationToken ct) =>
        await OwnedItems().SingleOrDefaultAsync(i => i.Id == id, ct) ?? throw new ItemNotFoundException();

    /// <summary>
    /// Loads an owned item, checks the caller's version, applies a change and saves.
    /// Touching UpdatedAt means the item row itself is always written, so its
    /// RowVersion moves even when only a sale, donation or photo changed.
    /// </summary>
    private async Task<ItemResponse> ChangeAsync(Guid id, byte[] version, Action<Item> change, CancellationToken ct)
    {
        var item = await FindAsync(id, ct);
        ExpectVersion(item, version);
        change(item);
        item.UpdatedAt = clock.GetUtcNow();
        await SaveAsync(ct);
        return ToResponse(item, await settings.GetFeeSettingsAsync(ct));
    }

    private void ExpectVersion(Item item, byte[] version)
    {
        if (!item.RowVersion.AsSpan().SequenceEqual(version))
        {
            throw new StaleVersionException();
        }

        // Also make the UPDATE itself conditional, for a change that lands between
        // this read and the write.
        db.Entry(item).Property(i => i.RowVersion).OriginalValue = version;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new StaleVersionException();
        }
    }

    private void ApplyFields(Item item, ItemRequest request)
    {
        item.Title = request.Title.Trim();
        item.Brand = request.Brand?.Trim() ?? "";
        item.Category = request.Category?.Trim() ?? "";
        item.Size = request.Size?.Trim() ?? "";
        item.Condition = ItemFields.MatchCondition(request.Condition) ?? ItemFields.DefaultCondition;
        item.Notes = request.Notes?.Trim() ?? "";
        item.Cost = request.Cost ?? 0;
        item.Source = request.Source?.Trim() ?? "";
        item.AcquiredDate = request.AcquiredDate;
        item.ListPrice = request.ListPrice;

        var platforms = (request.Platforms ?? []).Distinct().ToList();
        SetPlatforms(item, platforms);

        var listing = Lifecycle.ApplyPlatforms(item.Status, platforms, request.ListedDate, Today);
        item.Status = listing.Status;
        item.ListedDate = listing.ListedDate;
    }

    /// <summary>Updates the join rows in place, keeping the order given.</summary>
    private void SetPlatforms(Item item, List<string> platforms)
    {
        foreach (var gone in item.Platforms.Where(p => !platforms.Contains(p.PlatformId)).ToList())
        {
            item.Platforms.Remove(gone);
            db.ItemPlatforms.Remove(gone);
        }

        for (var i = 0; i < platforms.Count; i++)
        {
            var row = item.Platforms.SingleOrDefault(p => p.PlatformId == platforms[i]);
            if (row is null)
            {
                row = new ItemPlatform { ItemId = item.Id, PlatformId = platforms[i] };
                item.Platforms.Add(row);
            }

            row.Position = (byte)i;
        }
    }

    private static void EnsureJpeg(byte[] bytes, int maxBytes, string name)
    {
        // Every JPEG starts with the SOI marker FF D8 FF.
        if (bytes.Length < 3 || bytes[0] != 0xFF || bytes[1] != 0xD8 || bytes[2] != 0xFF)
        {
            throw new InvalidPhotoException($"The {name} image must be a JPEG.");
        }

        if (bytes.Length > maxBytes)
        {
            throw new InvalidPhotoException($"The {name} image is {bytes.Length / 1024} KB; the limit is {maxBytes / 1024} KB.");
        }
    }

    private ItemResponse ToResponse(Item i, FeeSettings fees)
    {
        var platforms = i.PlatformIds;
        var sale = i.Sale is { } s
            ? new SaleFigures(s.Platform, s.Price, s.ShippingCharged, s.Payout, s.ShippingCost, s.OtherCosts)
            : null;
        var profit = i.Status == ItemStatus.Sold ? Profit.Compute(i.Cost, sale, fees) : null;

        return new ItemResponse(
            Id: i.Id,
            Status: i.Status,
            Title: i.Title,
            Brand: i.Brand,
            Category: i.Category,
            Size: i.Size,
            Condition: i.Condition,
            Notes: i.Notes,
            Cost: i.Cost,
            Source: i.Source,
            AcquiredDate: i.AcquiredDate,
            Platforms: platforms,
            ListPrice: i.ListPrice,
            ListedDate: i.ListedDate,
            Thumbnail: i.ThumbnailJpeg is { } t ? $"data:image/jpeg;base64,{Convert.ToBase64String(t)}" : null,
            Sale: i.Sale is { } sr
                ? new SaleResponse(sr.Platform, sr.ListedFor, sr.Price, sr.Payout, sr.ShippingCharged, sr.ShippingCost, sr.OtherCosts, sr.Date)
                : null,
            Donation: i.Donation is { } d ? new DonationResponse(d.Date, d.Org, d.ReceiptValue) : null,
            Profit: profit is null
                ? null
                : new ProfitResponse(profit.Gross, profit.Payout, profit.Fees, profit.Cogs, profit.ShippingCost,
                    profit.OtherCosts, profit.Costs, profit.Net, profit.Margin, profit.Roi, profit.FeesEstimated),
            ProjectedNet: Lifecycle.IsOnHand(i.Status) ? Profit.ProjectedNet(i.ListPrice, i.Cost, platforms, fees) : null,
            DaysListed: Aging.DaysListed(i.Status, i.AcquiredDate, i.ListedDate, i.Sale?.Date, Today),
            CreatedAt: i.CreatedAt,
            UpdatedAt: i.UpdatedAt,
            Version: Versions.Format(i.RowVersion));
    }
}
