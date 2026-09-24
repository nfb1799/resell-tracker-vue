using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResellTracker.Api.Contracts;
using ResellTracker.Api.Services;
using ResellTracker.Domain;

namespace ResellTracker.Api.Controllers;

/// <summary>
/// Items and everything that happens to them. Changes to an existing item need its
/// current version in If-Match (428 without one, 412 if it's stale), and every
/// response carries the new version as an ETag.
/// </summary>
[ApiController]
[Authorize]
[Route("api/items")]
public class ItemsController(ItemService items) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ItemResponse>>> List(
        [FromQuery] ItemStatus? status, [FromQuery] string? platform, [FromQuery] string? q, CancellationToken ct)
    {
        if (platform is not null && !Platforms.IsKnown(platform))
        {
            ModelState.AddModelError(nameof(platform), $"\"{platform}\" is not a known platform.");
            return ValidationProblem();
        }

        return await items.ListAsync(new ItemQuery(status, platform, q), ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ItemResponse>> Get(Guid id, CancellationToken ct) =>
        Versioned(await items.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ItemResponse>> Create(ItemRequest request, CancellationToken ct)
    {
        var item = await items.CreateAsync(request, ct);
        Response.Headers.ETag = item.Version;
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ItemResponse>> Update(Guid id, ItemRequest request, CancellationToken ct) =>
        Versioned(await items.UpdateAsync(id, RequiredVersion(), request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await items.DeleteAsync(id, RequiredVersion(), ct);
        return NoContent();
    }

    /// <summary>Logs a sale, or edits the one already logged.</summary>
    [HttpPut("{id:guid}/sale")]
    public async Task<ActionResult<ItemResponse>> Sell(Guid id, SaleRequest request, CancellationToken ct) =>
        Versioned(await items.SellAsync(id, RequiredVersion(), request, ct));

    /// <summary>Undoes a sale: back to listed if still on a platform, otherwise in stock.</summary>
    [HttpDelete("{id:guid}/sale")]
    public async Task<ActionResult<ItemResponse>> UndoSale(Guid id, CancellationToken ct) =>
        Versioned(await items.UndoSaleAsync(id, RequiredVersion(), ct));

    /// <summary>Marks an item donated, or edits the donation already logged.</summary>
    [HttpPut("{id:guid}/donation")]
    public async Task<ActionResult<ItemResponse>> Donate(Guid id, DonationRequest request, CancellationToken ct) =>
        Versioned(await items.DonateAsync(id, RequiredVersion(), request, ct));

    [HttpDelete("{id:guid}/donation")]
    public async Task<ActionResult<ItemResponse>> UndoDonation(Guid id, CancellationToken ct) =>
        Versioned(await items.UndoDonationAsync(id, RequiredVersion(), ct));

    /// <summary>
    /// Sets the item's photo. The browser has already resized it into two JPEGs: a
    /// ~96px thumbnail and a ~900px full image, sent as multipart fields.
    /// </summary>
    [HttpPut("{id:guid}/photo")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<ActionResult<ItemResponse>> SetPhoto(Guid id, IFormFile thumbnail, IFormFile full, CancellationToken ct) =>
        Versioned(await items.SetPhotoAsync(id, RequiredVersion(), await ReadAsync(thumbnail, ct), await ReadAsync(full, ct), ct));

    /// <summary>The full-size photo, fetched only when one item is opened.</summary>
    [HttpGet("{id:guid}/photo")]
    public async Task<IActionResult> GetPhoto(Guid id, CancellationToken ct)
    {
        var photo = await items.GetPhotoAsync(id, ct);
        if (photo is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, no-cache";
        return File(photo.FullJpeg, "image/jpeg", photo.UpdatedAt, new Microsoft.Net.Http.Headers.EntityTagHeaderValue(
            $"\"{photo.UpdatedAt.UtcTicks}\""));
    }

    [HttpDelete("{id:guid}/photo")]
    public async Task<ActionResult<ItemResponse>> DeletePhoto(Guid id, CancellationToken ct) =>
        Versioned(await items.DeletePhotoAsync(id, RequiredVersion(), ct));

    private ActionResult<ItemResponse> Versioned(ItemResponse item)
    {
        Response.Headers.ETag = item.Version;
        return item;
    }

    /// <summary>The If-Match version. A header that doesn't parse can't match anything, so it reads as stale.</summary>
    private byte[] RequiredVersion()
    {
        if (Request.Headers.IfMatch.Count == 0)
        {
            throw new VersionRequiredException();
        }

        return Versions.FromIfMatch(Request.Headers) ?? [];
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
