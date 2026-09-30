using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResellTracker.Api.Contracts;
using ResellTracker.Api.Services;
using ResellTracker.Domain;

namespace ResellTracker.Api.Controllers;

/// <summary>
/// Bulk import, CSV export and the JSON backup. Like the stats, anything dated
/// "today" (default acquired and listed dates, days listed) uses the date the
/// client sends, so it matches where the user is.
/// </summary>
[ApiController]
[Authorize]
public class ImportExportController(ImportService imports, ExportService exports, TimeProvider clock) : ControllerBase
{
    // The API's own JSON conventions, indented because a backup is a file people open.
    private static readonly JsonSerializerOptions BackupJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Takes a JSON array of rows (or a backup file's <c>{ "items": [...] }</c>). Photos
    /// are not sent here: the client fetches and resizes them, then sets each one on
    /// the item its row became.
    /// </summary>
    [HttpPost("api/items/import")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<ImportResponse>> Import([FromBody] JsonElement rows, [FromQuery] DateOnly? today, CancellationToken ct)
    {
        try
        {
            return await imports.ImportAsync(rows, today ?? UtcToday(), ct);
        }
        catch (ImportRejectedException ex)
        {
            return Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Nothing imported");
        }
    }

    /// <summary>Every item, or one status's (the Sales page exports sold items), as CSV.</summary>
    [HttpGet("api/export/csv")]
    public async Task<IActionResult> Csv([FromQuery] ItemStatus? status, [FromQuery] DateOnly? today, CancellationToken ct)
    {
        var date = today ?? UtcToday();
        var name = status == ItemStatus.Sold ? "sales" : "inventory";
        return File(await exports.CsvAsync(status, date, ct), "text/csv; charset=utf-8", $"{name}-{Stamp(date)}.csv");
    }

    /// <summary>The raw backup: settings and every item, photos excluded, in a shape the importer reads back.</summary>
    [HttpGet("api/export/json")]
    public async Task<IActionResult> Json([FromQuery] DateOnly? today, CancellationToken ct)
    {
        var backup = await exports.BackupAsync(ct);
        var json = JsonSerializer.SerializeToUtf8Bytes(backup, BackupJson);
        return File(json, "application/json", $"resell-backup-{Stamp(today ?? UtcToday())}.json");
    }

    private DateOnly UtcToday() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    private static string Stamp(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
