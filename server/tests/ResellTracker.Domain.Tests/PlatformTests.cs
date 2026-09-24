namespace ResellTracker.Domain.Tests;

public class PlatformTests
{
    [Fact]
    public void Registry_carries_the_original_default_rates()
    {
        Assert.Equal(["depop", "ebay", "vinted", "other"], Platforms.All.Select(p => p.Id));
        Assert.Equal(new FeeSchedule(3.3m, 0.45m, true), Platforms.Find("depop")!.DefaultFees);
        Assert.Equal(new FeeSchedule(13.25m, 0.40m, true), Platforms.Find("ebay")!.DefaultFees);
        Assert.Equal(new FeeSchedule(0m, 0m, false), Platforms.Find("vinted")!.DefaultFees);
    }

    [Fact]
    public void Unknown_platforms_fall_back_to_other()
    {
        Assert.Equal("other", Platforms.Fallback.Id);
        Assert.False(Platforms.IsKnown("grailed"));
        Assert.Equal(Platforms.Fallback.DefaultFees, FeeSettings.Defaults.For("grailed"));
        Assert.Equal(Platforms.Fallback.DefaultFees, FeeSettings.Defaults.For(null));
    }

    [Fact]
    public void Saved_overrides_replace_only_their_platform()
    {
        var mine = new FeeSettings(new Dictionary<string, FeeSchedule>
        {
            ["ebay"] = new(10m, 0m, false),
        });

        Assert.Equal(new FeeSchedule(10m, 0m, false), mine.For("ebay"));
        // A platform the user never saved (or one added later) still has its default.
        Assert.Equal(Platforms.Find("depop")!.DefaultFees, mine.For("depop"));
    }

    [Fact]
    public void Overrides_for_unknown_platforms_are_ignored()
    {
        var mine = new FeeSettings(new Dictionary<string, FeeSchedule>
        {
            ["grailed"] = new(50m, 5m, true),
        });

        Assert.Equal(Platforms.Fallback.DefaultFees, mine.For("grailed"));
    }

    [Theory]
    [InlineData("1.725", "1.73")]
    [InlineData("1.7249", "1.72")]
    [InlineData("20.275", "20.28")]
    [InlineData("1.231", "1.23")] // a ceiling would give 1.24
    [InlineData("-0.125", "-0.12")] // halves go up, toward positive infinity, as JS Math.round does
    public void Rounds_to_the_cent_with_halves_going_up(string value, string expected) =>
        Assert.Equal(
            decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            Money.RoundToCent(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
}
