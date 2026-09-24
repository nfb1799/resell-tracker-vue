using System.Net;
using System.Net.Http.Json;
using ResellTracker.Api.Contracts;

namespace ResellTracker.Api.Tests;

// Every query is scoped to the signed-in owner, the equivalent of the original's
// Firestore rules. Someone else's item looks exactly like one that doesn't exist.
public class OwnerIsolationTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Another_user_can_neither_see_nor_change_my_items()
    {
        var mine = await CreateItemAsync(new { title = "Mine", platforms = new[] { "ebay" } });
        using var stranger = (await Factory.SignUpAsync()).Client;

        Assert.Empty((await stranger.GetFromJsonAsync<List<ItemResponse>>("/api/items", Json, Ct))!);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/items/{mine.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/items/{mine.Id}/photo", Ct)).StatusCode);

        // Even holding a valid version, every kind of change is refused as not found.
        async Task<HttpStatusCode> Send(HttpMethod method, string url, object? body = null)
        {
            var request = new HttpRequestMessage(method, url).WithVersion(mine.Version);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: Json);
            }

            return (await stranger.SendAsync(request, Ct)).StatusCode;
        }

        Assert.Equal(HttpStatusCode.NotFound, await Send(HttpMethod.Put, $"/api/items/{mine.Id}", new { title = "Stolen" }));
        Assert.Equal(HttpStatusCode.NotFound, await Send(HttpMethod.Put, $"/api/items/{mine.Id}/sale",
            new { platform = "ebay", price = 1, date = "2026-09-20" }));
        Assert.Equal(HttpStatusCode.NotFound, await Send(HttpMethod.Put, $"/api/items/{mine.Id}/donation", new { date = "2026-09-20" }));
        Assert.Equal(HttpStatusCode.NotFound, await Send(HttpMethod.Delete, $"/api/items/{mine.Id}/photo"));
        Assert.Equal(HttpStatusCode.NotFound, await Send(HttpMethod.Delete, $"/api/items/{mine.Id}"));

        var untouched = await GetItemAsync(mine.Id);
        Assert.Equal("Mine", untouched.Title);
        Assert.Equal(mine.Version, untouched.Version);
    }

    [Fact]
    public async Task Fee_settings_are_per_user()
    {
        await Client.PutAsJsonAsync("/api/settings/fees",
            new Dictionary<string, object> { ["ebay"] = new { percent = 1, @fixed = 0, includesShipping = true } }, Json, Ct);
        using var stranger = (await Factory.SignUpAsync()).Client;

        var theirs = await stranger.GetFromJsonAsync<Dictionary<string, FeeScheduleDto>>("/api/settings/fees", Json, Ct);

        Assert.Equal(13.25m, theirs!["ebay"].Percent);
    }
}
