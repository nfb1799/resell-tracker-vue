using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ResellTracker.Api.Contracts;

namespace ResellTracker.Api.Tests;

public class SettingsApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    private async Task<Dictionary<string, FeeScheduleDto>> GetFeesAsync() =>
        (await Client.GetFromJsonAsync<Dictionary<string, FeeScheduleDto>>("/api/settings/fees", Json, Ct))!;

    [Fact]
    public async Task A_new_user_gets_default_settings_named_after_their_email()
    {
        var settings = await Client.GetFromJsonAsync<SettingsResponse>("/api/settings", Json, Ct);

        Assert.Equal(new SettingsResponse(Me.Email.Split('@')[0], "USD", "dark", 0m), settings);
    }

    [Fact]
    public async Task Saves_general_settings()
    {
        var response = await Client.PutAsJsonAsync("/api/settings",
            new { displayName = "Nik", currency = "GBP", theme = "light", profitGoal = 500 }, Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new SettingsResponse("Nik", "GBP", "light", 500m),
            await Client.GetFromJsonAsync<SettingsResponse>("/api/settings", Json, Ct));
    }

    [Theory]
    [InlineData("""{ "currency": "dollars", "theme": "dark" }""")]
    [InlineData("""{ "currency": "USD", "theme": "blue" }""")]
    [InlineData("""{ "currency": "USD", "theme": "dark", "profitGoal": -5 }""")]
    public async Task Rejects_invalid_settings(string body)
    {
        var response = await Client.PutAsync("/api/settings", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Ct);

        await ValidationProblemAsync(response);
    }

    [Fact]
    public async Task Fees_start_at_the_registry_defaults_in_registry_order()
    {
        var fees = await GetFeesAsync();

        Assert.Equal(["depop", "ebay", "vinted", "other"], fees.Keys);
        Assert.Equal(13.25m, fees["ebay"].Percent);
        Assert.Equal(0.40m, fees["ebay"].Fixed);
        Assert.True(fees["ebay"].IncludesShipping);
    }

    [Fact]
    public async Task Saved_fees_drive_estimated_profit_including_the_shipping_flag()
    {
        var item = await CreateItemAsync(new { title = "Tee", platforms = new[] { "ebay" } });

        await Client.PutAsJsonAsync("/api/settings/fees",
            new Dictionary<string, object> { ["ebay"] = new { percent = 10, @fixed = 0, includesShipping = false } }, Json, Ct);
        var sold = await ChangeAsync(HttpMethod.Put, $"/api/items/{item.Id}/sale", item.Version,
            new { platform = "ebay", price = 40, shippingCharged = 5, date = "2026-09-20" });

        Assert.Equal(4m, sold.Profit!.Fees); // 10% of the price alone
    }

    [Fact]
    public async Task Only_changed_platforms_are_stored_and_leaving_one_out_resets_it()
    {
        await Client.PutAsJsonAsync("/api/settings/fees", new Dictionary<string, object>
        {
            ["ebay"] = new { percent = 12.9, @fixed = 0.30, includesShipping = true },
            ["depop"] = new { percent = 3.3, @fixed = 0.45, includesShipping = true }, // same as default
        }, Json, Ct);

        var fees = await GetFeesAsync();
        Assert.Equal(12.9m, fees["ebay"].Percent);
        var stored = await Factory.WithDbAsync(db =>
            db.PlatformFeeSettings.Where(f => f.OwnerId == UserId).Select(f => f.PlatformId).ToListAsync());
        Assert.Equal(["ebay"], stored);

        await Client.PutAsJsonAsync("/api/settings/fees", new Dictionary<string, object>(), Json, Ct);
        Assert.Equal(13.25m, (await GetFeesAsync())["ebay"].Percent);
    }

    [Theory]
    [InlineData("""{ "grailed": { "percent": 9, "fixed": 0 } }""")]
    [InlineData("""{ "ebay": { "percent": 13.255, "fixed": 0.4 } }""")]
    [InlineData("""{ "ebay": { "percent": 101, "fixed": 0.4 } }""")]
    [InlineData("""{ "ebay": { "percent": 13, "fixed": -1 } }""")]
    [InlineData("""{ "ebay": { "fixed": 0.4 } }""")]
    public async Task Rejects_invalid_fee_schedules(string body)
    {
        var response = await Client.PutAsync("/api/settings/fees", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Ct);

        await ValidationProblemAsync(response);
    }
}
