namespace ResellTracker.Domain;

/// <summary>What a sale recorded. <see cref="Payout"/> null means "not known yet".</summary>
public sealed record SaleFigures(
    string Platform,
    decimal Price,
    decimal ShippingCharged,
    decimal? Payout,
    decimal ShippingCost,
    decimal OtherCosts);

/// <summary>
/// Full profit breakdown for one sold item. <see cref="Margin"/> is net over gross
/// (null with no revenue); <see cref="Roi"/> is net over cost of goods (null for
/// an item that cost nothing, since dividing by zero isn't a useful number).
/// </summary>
public sealed record ProfitBreakdown(
    decimal Gross,
    decimal Payout,
    decimal Fees,
    decimal Cogs,
    decimal ShippingCost,
    decimal OtherCosts,
    decimal Costs,
    decimal Net,
    decimal? Margin,
    decimal? Roi,
    bool FeesEstimated);

public sealed record ProfitTotals(
    decimal Gross,
    decimal Payout,
    decimal Fees,
    decimal Costs,
    decimal Net,
    int Count,
    int Estimated);

public sealed record WriteOff(decimal Cost, int Count);

/// <summary>
/// The profit math. The server is the source of truth for money; the client
/// mirrors this for live preview only, and both run shared/profit-cases.json.
/// </summary>
public static class Profit
{
    /// <summary>
    /// What the platform's rate table says a sale should cost. Only ever a stand-in
    /// for the real figure: see <see cref="Compute"/>.
    /// </summary>
    public static decimal EstimateFees(string? platform, decimal price, decimal shippingCharged, FeeSettings fees)
    {
        var schedule = fees.For(platform);
        var feeBase = schedule.IncludesShipping ? price + shippingCharged : price;
        if (feeBase <= 0)
        {
            return 0;
        }

        return Money.RoundToCent(feeBase * schedule.Percent / 100m) + schedule.Fixed;
    }

    /// <summary>
    /// gross = what the buyer paid; net = payout less cost of goods, shipping and
    /// other costs. The payout is the pivot: entered, the fee is derived from it and
    /// is exact; left null, the rate table estimates the fee and the payout follows.
    /// </summary>
    public static ProfitBreakdown Compute(decimal cost, SaleFigures? sale, FeeSettings fees)
    {
        var price = sale?.Price ?? 0;
        var shippingCharged = sale?.ShippingCharged ?? 0;
        var gross = price + shippingCharged;

        decimal payout;
        decimal platformFees;
        var payoutKnown = sale?.Payout is not null;
        if (payoutKnown)
        {
            payout = sale!.Payout!.Value;
            platformFees = gross - payout;
        }
        else
        {
            platformFees = EstimateFees(sale?.Platform, price, shippingCharged, fees);
            payout = gross - platformFees;
        }

        var shippingCost = sale?.ShippingCost ?? 0;
        var otherCosts = sale?.OtherCosts ?? 0;
        var costs = cost + shippingCost + otherCosts;
        var net = payout - costs;

        return new ProfitBreakdown(
            Gross: gross,
            Payout: payout,
            Fees: platformFees,
            Cogs: cost,
            ShippingCost: shippingCost,
            OtherCosts: otherCosts,
            Costs: costs,
            Net: net,
            Margin: gross > 0 ? net / gross : null,
            Roi: cost > 0 ? net / cost : null,
            FeesEstimated: !payoutKnown);
    }

    public static ProfitTotals Total(IEnumerable<ProfitBreakdown> sales)
    {
        var totals = new ProfitTotals(0, 0, 0, 0, 0, 0, 0);
        foreach (var p in sales)
        {
            totals = new ProfitTotals(
                totals.Gross + p.Gross,
                totals.Payout + p.Payout,
                totals.Fees + p.Fees,
                totals.Costs + p.Costs,
                totals.Net + p.Net,
                totals.Count + 1,
                totals.Estimated + (p.FeesEstimated ? 1 : 0));
        }

        return totals;
    }

    /// <summary>
    /// Donated stock never earns its cost back, so that cost is a write-off. Kept
    /// apart from sale profit: "I made $40 on that jacket" and "I gave up on $18 of
    /// stock" are two different facts.
    /// </summary>
    public static WriteOff WriteOffTotal(IEnumerable<decimal> donatedCosts)
    {
        var list = donatedCosts.ToList();
        return new WriteOff(list.Sum(), list.Count);
    }

    /// <summary>
    /// What a listing would net at its asking price on the first platform it is
    /// listed on. Shipping is unknown before it sells, so this is price only.
    /// </summary>
    public static decimal? ProjectedNet(decimal? listPrice, decimal cost, IReadOnlyList<string> platforms, FeeSettings fees)
    {
        if (listPrice is not { } price || price <= 0)
        {
            return null;
        }

        var platform = platforms.Count > 0 ? platforms[0] : Platforms.Fallback.Id;
        return price - EstimateFees(platform, price, 0, fees) - cost;
    }
}
