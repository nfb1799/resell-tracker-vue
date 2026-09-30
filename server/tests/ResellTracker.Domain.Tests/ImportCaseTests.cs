using System.Globalization;
using System.Text.Json;

namespace ResellTracker.Domain.Tests;

// Runs every case in shared/import-cases.json. The client's Vitest suite runs the
// same file against its copy of the rules, so the preview a user sees before
// importing and the server's verdict agree, error wording included.
public class ImportCaseTests
{
    private static readonly JsonElement Fixture = LoadFixture();

    private static readonly DateOnly Today = Date(Fixture.GetProperty("today"))!.Value;

    public static TheoryData<string> Cases =>
        [.. Fixture.GetProperty("cases").EnumerateArray().Select(c => c.GetProperty("name").GetString()!)];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Row(string name)
    {
        var c = Fixture.GetProperty("cases").EnumerateArray().Single(x => x.GetProperty("name").GetString() == name);
        var expected = c.GetProperty("expected");

        var row = Import.ReadRow(c.GetProperty("row"), 0, Today);

        Assert.Equal(1, row.RowNumber);
        Assert.Equal(expected.GetProperty("label").GetString(), row.Label);

        if (expected.TryGetProperty("errors", out var errors))
        {
            Assert.Equal([.. errors.EnumerateArray().Select(e => e.GetString()!)], row.Errors);
            Assert.Null(row.Item);
            return;
        }

        Assert.Empty(row.Errors);
        var item = row.Item!;
        var e = expected.GetProperty("item");
        Assert.Equal(e.GetProperty("title").GetString(), item.Title);
        Assert.Equal(e.GetProperty("brand").GetString(), item.Brand);
        Assert.Equal(e.GetProperty("category").GetString(), item.Category);
        Assert.Equal(e.GetProperty("size").GetString(), item.Size);
        Assert.Equal(e.GetProperty("condition").GetString(), item.Condition);
        Assert.Equal(e.GetProperty("notes").GetString(), item.Notes);
        Assert.Equal(e.GetProperty("cost").GetDecimal(), item.Cost);
        Assert.Equal(e.GetProperty("source").GetString(), item.Source);
        Assert.Equal(Date(e.GetProperty("acquiredDate")), item.AcquiredDate);
        Assert.Equal([.. e.GetProperty("platforms").EnumerateArray().Select(p => p.GetString()!)], item.Platforms);
        Assert.Equal(Money(e.GetProperty("listPrice")), item.ListPrice);
        Assert.Equal(Date(e.GetProperty("listedDate")), item.ListedDate);
        Assert.Equal(Enum.Parse<ItemStatus>(e.GetProperty("status").GetString()!, ignoreCase: true), item.Status);
        Assert.Equal(Sale(e.GetProperty("sale")), item.Sale);
        Assert.Equal(Donation(e.GetProperty("donation")), item.Donation);

        var photo = expected.GetProperty("photo");
        Assert.Equal(
            photo.ValueKind == JsonValueKind.Null
                ? null
                : new ImportedPhoto(photo.GetProperty("src").GetString()!, Enum.Parse<PhotoKind>(photo.GetProperty("kind").GetString()!, ignoreCase: true)),
            row.Photo);
    }

    [Fact]
    public void Rows_come_from_an_array_a_single_object_or_a_backup()
    {
        static int Count(string json) => Import.Rows(JsonDocument.Parse(json).RootElement)!.Count;

        Assert.Equal(2, Count("""[{ "title": "a" }, { "title": "b" }]"""));
        Assert.Equal(1, Count("""{ "title": "a" }"""));
        Assert.Equal(2, Count("""{ "exportedAt": "2026-09-30", "items": [{ "title": "a" }, { "title": "b" }] }"""));
        Assert.Null(Import.Rows(JsonDocument.Parse("\"text\"").RootElement));
    }

    private static ImportedSale? Sale(JsonElement s) => s.ValueKind == JsonValueKind.Null
        ? null
        : new ImportedSale(
            s.GetProperty("platform").GetString()!,
            s.GetProperty("listedFor").GetDecimal(),
            s.GetProperty("price").GetDecimal(),
            Money(s.GetProperty("payout")),
            s.GetProperty("shippingCharged").GetDecimal(),
            s.GetProperty("shippingCost").GetDecimal(),
            s.GetProperty("otherCosts").GetDecimal(),
            Date(s.GetProperty("date"))!.Value);

    private static ImportedDonation? Donation(JsonElement d) => d.ValueKind == JsonValueKind.Null
        ? null
        : new ImportedDonation(Date(d.GetProperty("date"))!.Value, d.GetProperty("org").GetString()!, Money(d.GetProperty("receiptValue")));

    private static decimal? Money(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetDecimal();

    private static DateOnly? Date(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ? null : DateOnly.Parse(e.GetString()!, CultureInfo.InvariantCulture);

    private static JsonElement LoadFixture()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "import-cases.json")));
        return doc.RootElement.Clone();
    }
}
