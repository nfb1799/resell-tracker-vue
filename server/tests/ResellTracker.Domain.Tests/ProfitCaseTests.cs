using System.Text.Json;

namespace ResellTracker.Domain.Tests;

// Runs every case in shared/profit-cases.json. The client's Vitest suite runs the
// same file against the TypeScript mirror, so the two cannot drift apart.
//
// Theories take the case name rather than the case itself so each one shows up
// (and fails) as its own test.
public class ProfitCaseTests
{
    private static readonly JsonElement Cases = LoadCases();

    private const decimal RatioTolerance = 0.000000001m;

    public static TheoryData<string> EstimateFeesCases => Names("estimateFees");
    public static TheoryData<string> ComputeProfitCases => Names("computeProfit");
    public static TheoryData<string> TotalProfitCases => Names("totalProfit");
    public static TheoryData<string> WriteOffCases => Names("writeOffTotal");
    public static TheoryData<string> ProjectedNetCases => Names("projectedNet");
    public static TheoryData<string> DaysListedCases => Names("daysListed");

    [Theory]
    [MemberData(nameof(EstimateFeesCases))]
    public void EstimateFees(string name)
    {
        var c = Case("estimateFees", name);
        var sale = c.GetProperty("sale");

        var fees = Profit.EstimateFees(
            sale.GetProperty("platform").GetString(),
            sale.GetProperty("price").GetDecimal(),
            sale.GetProperty("shippingCharged").GetDecimal(),
            FeesFor(c));

        Assert.Equal(c.GetProperty("expected").GetDecimal(), fees);
    }

    [Theory]
    [MemberData(nameof(ComputeProfitCases))]
    public void ComputeProfit(string name)
    {
        var c = Case("computeProfit", name);
        var expected = c.GetProperty("expected");

        var p = ComputeItem(c.GetProperty("item"), FeesFor(c));

        Assert.Equal(expected.GetProperty("gross").GetDecimal(), p.Gross);
        Assert.Equal(expected.GetProperty("payout").GetDecimal(), p.Payout);
        Assert.Equal(expected.GetProperty("fees").GetDecimal(), p.Fees);
        Assert.Equal(expected.GetProperty("cogs").GetDecimal(), p.Cogs);
        Assert.Equal(expected.GetProperty("shippingCost").GetDecimal(), p.ShippingCost);
        Assert.Equal(expected.GetProperty("otherCosts").GetDecimal(), p.OtherCosts);
        Assert.Equal(expected.GetProperty("costs").GetDecimal(), p.Costs);
        Assert.Equal(expected.GetProperty("net").GetDecimal(), p.Net);
        AssertRatio(expected.GetProperty("margin"), p.Margin);
        AssertRatio(expected.GetProperty("roi"), p.Roi);
        Assert.Equal(expected.GetProperty("feesEstimated").GetBoolean(), p.FeesEstimated);
    }

    [Theory]
    [MemberData(nameof(TotalProfitCases))]
    public void TotalProfit(string name)
    {
        var c = Case("totalProfit", name);
        var expected = c.GetProperty("expected");
        var fees = FeesFor(c);

        var t = Profit.Total(c.GetProperty("items").EnumerateArray().Select(item => ComputeItem(item, fees)));

        Assert.Equal(expected.GetProperty("gross").GetDecimal(), t.Gross);
        Assert.Equal(expected.GetProperty("payout").GetDecimal(), t.Payout);
        Assert.Equal(expected.GetProperty("fees").GetDecimal(), t.Fees);
        Assert.Equal(expected.GetProperty("costs").GetDecimal(), t.Costs);
        Assert.Equal(expected.GetProperty("net").GetDecimal(), t.Net);
        Assert.Equal(expected.GetProperty("count").GetInt32(), t.Count);
        Assert.Equal(expected.GetProperty("estimated").GetInt32(), t.Estimated);
    }

    [Theory]
    [MemberData(nameof(WriteOffCases))]
    public void WriteOffTotal(string name)
    {
        var c = Case("writeOffTotal", name);
        var expected = c.GetProperty("expected");

        var w = Profit.WriteOffTotal(c.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("cost").GetDecimal()));

        Assert.Equal(expected.GetProperty("cost").GetDecimal(), w.Cost);
        Assert.Equal(expected.GetProperty("count").GetInt32(), w.Count);
    }

    [Theory]
    [MemberData(nameof(ProjectedNetCases))]
    public void ProjectedNet(string name)
    {
        var c = Case("projectedNet", name);
        var item = c.GetProperty("item");

        var projected = Profit.ProjectedNet(
            NullableDecimal(item.GetProperty("listPrice")),
            item.GetProperty("cost").GetDecimal(),
            [.. item.GetProperty("platforms").EnumerateArray().Select(p => p.GetString()!)],
            FeesFor(c));

        Assert.Equal(NullableDecimal(c.GetProperty("expected")), projected);
    }

    [Theory]
    [MemberData(nameof(DaysListedCases))]
    public void DaysListed(string name)
    {
        var c = Case("daysListed", name);
        var item = c.GetProperty("item");

        var days = Aging.DaysListed(
            Enum.Parse<ItemStatus>(item.GetProperty("status").GetString()!, ignoreCase: true),
            NullableDate(item.GetProperty("acquiredDate")),
            NullableDate(item.GetProperty("listedDate")),
            NullableDate(item.GetProperty("saleDate")),
            DateOnly.Parse(c.GetProperty("today").GetString()!, System.Globalization.CultureInfo.InvariantCulture));

        var expected = c.GetProperty("expected");
        Assert.Equal(expected.ValueKind is JsonValueKind.Null ? null : expected.GetInt32(), days);
    }

    private static ProfitBreakdown ComputeItem(JsonElement item, FeeSettings fees)
    {
        var sale = item.GetProperty("sale");
        var figures = sale.ValueKind is JsonValueKind.Null
            ? null
            : new SaleFigures(
                sale.GetProperty("platform").GetString()!,
                sale.GetProperty("price").GetDecimal(),
                sale.GetProperty("shippingCharged").GetDecimal(),
                NullableDecimal(sale.GetProperty("payout")),
                sale.GetProperty("shippingCost").GetDecimal(),
                sale.GetProperty("otherCosts").GetDecimal());

        return Profit.Compute(item.GetProperty("cost").GetDecimal(), figures, fees);
    }

    private static FeeSettings FeesFor(JsonElement c)
    {
        if (!c.TryGetProperty("feeOverrides", out var overrides))
        {
            return FeeSettings.Defaults;
        }

        return new FeeSettings(overrides.EnumerateObject().ToDictionary(
            o => o.Name,
            o => new FeeSchedule(
                o.Value.GetProperty("percent").GetDecimal(),
                o.Value.GetProperty("fixed").GetDecimal(),
                o.Value.GetProperty("includesShipping").GetBoolean())));
    }

    private static void AssertRatio(JsonElement expected, decimal? actual)
    {
        if (expected.ValueKind is JsonValueKind.Null)
        {
            Assert.Null(actual);
            return;
        }

        Assert.NotNull(actual);
        Assert.InRange(actual.Value, expected.GetDecimal() - RatioTolerance, expected.GetDecimal() + RatioTolerance);
    }

    private static decimal? NullableDecimal(JsonElement e) => e.ValueKind is JsonValueKind.Null ? null : e.GetDecimal();

    private static DateOnly? NullableDate(JsonElement e) =>
        e.ValueKind is JsonValueKind.Null
            ? null
            : DateOnly.Parse(e.GetString()!, System.Globalization.CultureInfo.InvariantCulture);

    private static JsonElement Case(string section, string name) =>
        Cases.GetProperty(section).EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);

    private static TheoryData<string> Names(string section) =>
        [.. Cases.GetProperty(section).EnumerateArray().Select(c => c.GetProperty("name").GetString()!)];

    private static JsonElement LoadCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "profit-cases.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }
}
