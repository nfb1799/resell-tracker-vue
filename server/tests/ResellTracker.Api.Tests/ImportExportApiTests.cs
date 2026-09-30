using System.Net;
using System.Net.Http.Json;
using System.Text;
using ResellTracker.Api.Contracts;
using ResellTracker.Domain;

namespace ResellTracker.Api.Tests;

public class ImportExportApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private const string Today = "2026-09-30";

    private async Task<ImportResponse> ImportAsync(object rows, HttpClient? client = null)
    {
        var response = await (client ?? Client).PostAsJsonAsync($"/api/items/import?today={Today}", rows, Json, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ImportResponse>(Json, Ct))!;
    }

    private async Task<string[]> CsvLinesAsync(string query = "")
    {
        var bytes = await Client.GetByteArrayAsync($"/api/export/csv?today={Today}{query}", Ct);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]); // BOM, so Excel reads accents
        return Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact]
    public async Task Imports_valid_rows_and_skips_bad_ones_with_reasons()
    {
        var result = await ImportAsync(new object[]
        {
            new { title = "Nautica Polo", cost = "$4", listingPlatform = "Depop", askingPrice = 20, sourcedFrom = "Dad" },
            new { brand = "No title" },
            new { title = "Tee", condition = "good" },
        });

        Assert.Equal(2, result.Added);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(["added", "skipped", "added"], result.Rows.Select(r => r.Status));
        Assert.Equal(["title is required"], result.Rows[1].Errors);

        var polo = result.Rows[0].Item!;
        Assert.Equal(ItemStatus.Listed, polo.Status);
        Assert.Equal(["depop"], polo.Platforms);
        Assert.Equal(4m, polo.Cost);
        Assert.Equal("Dad", polo.Source);
        Assert.Equal(new DateOnly(2026, 9, 30), polo.ListedDate); // today, as the client sent it
        Assert.Equal(2, (await ListAsync()).Count);
    }

    [Fact]
    public async Task An_imported_sale_goes_through_the_same_profit_math()
    {
        var result = await ImportAsync(new[]
        {
            new { title = "Hoodie", cost = 20, listPrice = 120, sale = new { platform = "ebay", price = 115, shippingCost = 9.2, date = "2026-09-28" } },
        });

        var hoodie = result.Rows[0].Item!;
        Assert.Equal(ItemStatus.Sold, hoodie.Status);
        Assert.True(hoodie.Profit!.FeesEstimated);
        Assert.Equal(70.16m, hoodie.Profit.Net); // 115 - (15.24 + 0.40) - 20 - 9.2
        Assert.Equal(120m, hoodie.Sale!.ListedFor);
    }

    [Theory]
    [InlineData("[]", "empty")]
    [InlineData("\"not rows\"", "array")]
    public async Task Refuses_an_import_with_nothing_to_read(string body, string reason)
    {
        var response = await Client.PostAsync("/api/items/import", new StringContent(body, Encoding.UTF8, "application/json"), Ct);

        var problem = await ProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Contains(reason, problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refuses_more_than_the_row_limit()
    {
        var rows = Enumerable.Range(0, Import.MaxRows + 1).Select(i => new { title = $"Item {i}" });

        var response = await Client.PostAsJsonAsync("/api/items/import", rows, Json, Ct);

        await ProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty(await ListAsync());
    }

    [Fact]
    public async Task Csv_has_the_original_columns_and_a_full_breakdown_per_sale()
    {
        await ImportAsync(new object[]
        {
            new
            {
                title = "Levi's 501", brand = "Levi's", cost = 8, platforms = new[] { "ebay" }, listPrice = 40,
                listedDate = "2026-09-01", acquiredDate = "2026-08-30",
                sale = new { platform = "ebay", listedFor = 40, price = 40, shippingCharged = 5, payout = 38.64, shippingCost = 4.5, otherCosts = 0.5, date = "2026-09-20" },
            },
        });

        var lines = await CsvLinesAsync();

        Assert.Equal(
            "Title,Brand,Category,Size,Condition,Status,Source,Acquired,Cost,Listed on,Listed date,List price," +
            "Sold platform,Sold date,Listed for at sale,Offer accepted,Shipping charged,Payout,Fees kept by platform,Fees estimated," +
            "Shipping cost,Other costs,Net profit,Margin %,Donated date,Donated to,Receipt value,Written off,Days listed,Notes",
            lines[0]);
        Assert.Equal(30, lines[0].Split(',').Length);
        Assert.Equal(
            "Levi's 501,Levi's,,,Excellent,sold,,2026-08-30,8.00,ebay,2026-09-01,40.00," +
            "ebay,2026-09-20,40.00,40.00,5.00,38.64,6.36,no,4.50,0.50,25.64,57.0,,,,,19,",
            lines[1]);
    }

    [Fact]
    public async Task Csv_marks_estimated_fees_and_writes_off_donations()
    {
        await ImportAsync(new object[]
        {
            new { title = "Hoodie", cost = 20, sale = new { platform = "ebay", price = 115, date = "2026-09-28" } },
            new { title = "Shorts", cost = 5, donation = new { date = "2026-09-18", org = "Goodwill", receiptValue = 8 } },
        });

        var lines = await CsvLinesAsync();

        Assert.Contains(lines, l => l.StartsWith("Hoodie,", StringComparison.Ordinal) && l.Contains(",15.64,yes,", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("Shorts,", StringComparison.Ordinal) && l.Contains(",2026-09-18,Goodwill,8.00,5.00,", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Csv_quotes_awkward_text_and_defuses_spreadsheet_formulas()
    {
        await ImportAsync(new object[]
        {
            new { title = "=HYPERLINK(\"http://evil.test\",\"click\")", notes = "Pit to pit, 22in\nsmall \"stain\"" },
        });

        var csv = (await CsvLinesAsync())[1..];
        var row = string.Join("\r\n", csv);

        Assert.StartsWith("\"'=HYPERLINK(\"\"http://evil.test\"\",\"\"click\"\")\"", row, StringComparison.Ordinal);
        Assert.EndsWith("\"Pit to pit, 22in\nsmall \"\"stain\"\"\"", row, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_sales_export_holds_only_sold_items()
    {
        await ImportAsync(new object[]
        {
            new { title = "Sold one", sale = new { platform = "vinted", price = 10, date = "2026-09-01" } },
            new { title = "Still here" },
        });

        var response = await Client.GetAsync($"/api/export/csv?status=sold&today={Today}", Ct);
        var lines = await CsvLinesAsync("&status=sold");

        Assert.Equal($"sales-{Today}.csv", response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Sold one,", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_backup_imported_into_a_new_account_recreates_every_item()
    {
        // The demo inventory has every status: sold (one still waiting on its
        // payout), listed on two platforms, in stock, and donated.
        using var demo = Factory.CreateBrowser();
        Assert.Equal(HttpStatusCode.OK, (await demo.PostAsync("/api/auth/demo", null, Ct)).StatusCode);
        var original = (await demo.GetFromJsonAsync<List<ItemResponse>>("/api/items", Json, Ct))!;
        var backup = await demo.GetStringAsync("/api/export/json", Ct);

        var result = await ImportAsync(System.Text.Json.JsonDocument.Parse(backup).RootElement);

        Assert.Equal(original.Count, result.Added);
        Assert.Equal(0, result.Skipped);
        var restored = await ListAsync();
        static object Comparable(ItemResponse i) => new
        {
            i.Title,
            i.Brand,
            i.Category,
            i.Size,
            i.Condition,
            i.Notes,
            i.Cost,
            i.Source,
            i.AcquiredDate,
            Platforms = string.Join('+', i.Platforms),
            i.ListPrice,
            i.ListedDate,
            i.Status,
            i.Sale,
            i.Donation,
            i.Profit,
            i.ProjectedNet,
        };
        Assert.Equal(
            original.Select(Comparable).OrderBy(o => o.ToString(), StringComparer.Ordinal),
            restored.Select(Comparable).OrderBy(o => o.ToString(), StringComparer.Ordinal));
    }

    [Fact]
    public async Task Export_and_import_need_a_signed_in_user()
    {
        using var browser = Factory.CreateBrowser();

        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/export/csv", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.PostAsJsonAsync("/api/items/import", new[] { new { title = "x" } }, Ct)).StatusCode);
    }
}
