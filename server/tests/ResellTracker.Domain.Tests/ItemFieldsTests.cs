namespace ResellTracker.Domain.Tests;

public class ItemFieldsTests
{
    [Fact]
    public void Carries_the_original_conditions_in_order_with_excellent_as_the_default()
    {
        Assert.Equal(["New with tags", "New without tags", "Excellent", "Good", "Fair", "For parts"], ItemFields.Conditions);
        Assert.Equal("Excellent", ItemFields.DefaultCondition);
    }

    [Theory]
    [InlineData("good", "Good")]
    [InlineData("  NEW WITH TAGS ", "New with tags")]
    [InlineData("Mint", null)]
    [InlineData(null, null)]
    public void Matches_a_condition_case_insensitively(string? input, string? expected) =>
        Assert.Equal(expected, ItemFields.MatchCondition(input));

    [Fact]
    public void Text_limits_match_the_original_rules()
    {
        Assert.Equal(200, ItemFields.MaxTitle);
        Assert.Equal(100, ItemFields.MaxBrand);
        Assert.Equal(100, ItemFields.MaxCategory);
        Assert.Equal(2000, ItemFields.MaxNotes);
    }
}
