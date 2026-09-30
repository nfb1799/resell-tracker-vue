using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ResellTracker.Api.Contracts;
using ResellTracker.Domain;

namespace ResellTracker.Api.Services;

/// <summary>A whole import can't run: nothing to read, or too much.</summary>
public sealed class ImportRejectedException(string message) : Exception(message);

/// <summary>
/// Bulk import. Every row is checked by the domain's Import rules, and every valid
/// one goes through the same ItemService calls the New item form, the sale sheet
/// and the donation sheet use, so an imported item is indistinguishable from a
/// hand-entered one. A bad row is skipped with its reasons; the rest still go in.
/// </summary>
public sealed class ImportService(ItemService items)
{
    public async Task<ImportResponse> ImportAsync(JsonElement body, DateOnly today, CancellationToken ct)
    {
        var rows = Import.Rows(body) ?? throw new ImportRejectedException("Send a JSON array of items.");
        if (rows.Count == 0)
        {
            throw new ImportRejectedException("The array is empty — nothing to import.");
        }

        if (rows.Count > Import.MaxRows)
        {
            throw new ImportRejectedException($"That is {rows.Count} rows; import at most {Import.MaxRows} at a time.");
        }

        var results = new List<ImportRowResult>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = Import.ReadRow(rows[i], i, today);
            if (!row.IsValid)
            {
                results.Add(new ImportRowResult(row.RowNumber, row.Label, "skipped", row.Errors, null));
                continue;
            }

            try
            {
                results.Add(new ImportRowResult(row.RowNumber, row.Label, "added", [], await AddAsync(row.Item!, ct)));
            }
            catch (Exception ex) when (ex is LifecycleException or DbUpdateException)
            {
                results.Add(new ImportRowResult(row.RowNumber, row.Label, "skipped", [$"could not be saved: {ex.Message}"], null));
            }
        }

        return new ImportResponse(results.Count(r => r.Status == "added"), results.Count(r => r.Status == "skipped"), results);
    }

    private async Task<ItemResponse> AddAsync(ImportedItem item, CancellationToken ct)
    {
        var created = await items.CreateAsync(new ItemRequest
        {
            Title = item.Title,
            Brand = item.Brand,
            Category = item.Category,
            Size = item.Size,
            Condition = item.Condition,
            Notes = item.Notes,
            Cost = item.Cost,
            Source = item.Source,
            AcquiredDate = item.AcquiredDate,
            Platforms = [.. item.Platforms],
            ListPrice = item.ListPrice,
            ListedDate = item.ListedDate,
        }, ct);

        if (item.Sale is { } s)
        {
            return await items.SellAsync(created.Id, Versions.Parse(created.Version), new SaleRequest
            {
                Platform = s.Platform,
                ListedFor = s.ListedFor,
                Price = s.Price,
                Payout = s.Payout,
                ShippingCharged = s.ShippingCharged,
                ShippingCost = s.ShippingCost,
                OtherCosts = s.OtherCosts,
                Date = s.Date,
            }, ct);
        }

        if (item.Donation is { } d)
        {
            return await items.DonateAsync(created.Id, Versions.Parse(created.Version), new DonationRequest
            {
                Date = d.Date,
                Org = d.Org,
                ReceiptValue = d.ReceiptValue,
            }, ct);
        }

        return created;
    }
}
