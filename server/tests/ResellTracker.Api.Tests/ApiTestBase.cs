using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using ResellTracker.Api.Contracts;

namespace ResellTracker.Api.Tests;

/// <summary>Request helpers shared by the API tests. Each test class gets a fresh signed-in user.</summary>
public abstract class ApiTestBase(ApiFactory factory) : IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    protected ApiFactory Factory { get; } = factory;

    protected HttpClient Client { get; private set; } = null!;

    /// <summary>The signed-in user this test class acts as.</summary>
    protected Guid UserId => Guid.Parse(Client.DefaultRequestHeaders.GetValues(TestAuthHandler.UserHeader).Single());

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => Client = await Factory.CreateUserClientAsync();

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    protected async Task<ItemResponse> CreateItemAsync(object body, HttpClient? client = null)
    {
        var response = await (client ?? Client).PostAsJsonAsync("/api/items", body, Json, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ItemResponse>(Json, Ct))!;
    }

    protected async Task<ItemResponse> GetItemAsync(Guid id)
    {
        var response = await Client.GetAsync($"/api/items/{id}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ItemResponse>(Json, Ct))!;
    }

    protected async Task<List<ItemResponse>> ListAsync(string query = "")
    {
        var response = await Client.GetAsync($"/api/items{query}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<ItemResponse>>(Json, Ct))!;
    }

    /// <summary>Sends a change with the given version in If-Match (or none, when null).</summary>
    protected Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? version, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        if (version is not null)
        {
            request.WithVersion(version);
        }

        return Client.SendAsync(request, Ct);
    }

    /// <summary>Sends a change that must succeed, and returns the updated item.</summary>
    protected async Task<ItemResponse> ChangeAsync(HttpMethod method, string url, string version, object? body = null)
    {
        var response = await SendAsync(method, url, version, body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        var item = (await response.Content.ReadFromJsonAsync<ItemResponse>(Json, Ct))!;
        Assert.Equal(item.Version, response.Headers.ETag?.ToString());
        return item;
    }

    protected static async Task<ProblemDetails> ProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>(Json, Ct))!;
    }

    protected static async Task<ValidationProblemDetails> ValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json, Ct))!;
    }
}
