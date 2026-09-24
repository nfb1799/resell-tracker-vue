namespace ResellTracker.Domain;

public static class Money
{
    /// <summary>
    /// Rounds to the cent, halves going up (toward positive infinity), which is
    /// what the client's integer-cents mirror and the original app's Math.round do.
    /// </summary>
    /// <remarks>
    /// Not <c>Math.Round(value, 2, MidpointRounding.ToPositiveInfinity)</c>: despite
    /// the name, that is a ceiling, not a midpoint rule, and would turn 1.231 into 1.24.
    /// </remarks>
    public static decimal RoundToCent(decimal value) => Math.Floor(value * 100m + 0.5m) / 100m;

    /// <summary>True for a whole number of cents (12, 12.5, 12.50), false for 12.505.</summary>
    public static bool IsWholeCents(decimal value) => decimal.Round(value, 2) == value;

    /// <summary>The largest amount a decimal(10,2) column holds.</summary>
    public const decimal MaxAmount = 99_999_999.99m;
}
