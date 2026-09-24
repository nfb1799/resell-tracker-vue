using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ResellTracker.Api.Contracts;
using ResellTracker.Api.Services;

namespace ResellTracker.Api.Tests;

public class PhotoApiTests(ApiFactory factory) : ApiTestBase(factory)
{
    /// <summary>Bytes that start like a JPEG (FF D8 FF), padded to a size.</summary>
    private static byte[] Jpeg(int size, byte fill = 0x42)
    {
        var bytes = Enumerable.Repeat(fill, size).ToArray();
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        return bytes;
    }

    private Task<HttpResponseMessage> PutPhotoAsync(ItemResponse item, byte[] thumbnail, byte[] full)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, bytes) in new[] { ("thumbnail", thumbnail), ("full", full) })
        {
            var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(part, name, $"{name}.jpg");
        }

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/items/{item.Id}/photo") { Content = form }.WithVersion(item.Version);
        return Client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task The_thumbnail_rides_on_the_item_and_the_full_photo_is_its_own_resource()
    {
        var item = await CreateItemAsync(new { title = "Tee" });
        var thumbnail = Jpeg(3000);
        var full = Jpeg(90_000, fill: 0x17);

        var response = await PutPhotoAsync(item, thumbnail, full);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var listed = Assert.Single(await ListAsync());
        Assert.Equal($"data:image/jpeg;base64,{Convert.ToBase64String(thumbnail)}", listed.Thumbnail);

        var photo = await Client.GetAsync($"/api/items/{item.Id}/photo", Ct);
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(full, await photo.Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task Setting_a_photo_moves_the_item_version()
    {
        var item = await CreateItemAsync(new { title = "Tee" });

        var updated = (await (await PutPhotoAsync(item, Jpeg(100), Jpeg(100))).Content.ReadFromJsonAsync<ItemResponse>(Json, Ct))!;

        Assert.NotEqual(item.Version, updated.Version);
    }

    [Fact]
    public async Task Removing_the_photo_clears_both_sizes()
    {
        var item = await CreateItemAsync(new { title = "Tee" });
        var withPhoto = (await (await PutPhotoAsync(item, Jpeg(100), Jpeg(100))).Content.ReadFromJsonAsync<ItemResponse>(Json, Ct))!;

        var removed = await ChangeAsync(HttpMethod.Delete, $"/api/items/{item.Id}/photo", withPhoto.Version);

        Assert.Null(removed.Thumbnail);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/items/{item.Id}/photo", Ct)).StatusCode);
    }

    [Fact]
    public async Task Rejects_something_that_is_not_a_jpeg()
    {
        var item = await CreateItemAsync(new { title = "Tee" });
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };

        await ProblemAsync(await PutPhotoAsync(item, png, Jpeg(100)), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rejects_a_thumbnail_too_big_to_ride_on_every_list()
    {
        var item = await CreateItemAsync(new { title = "Tee" });

        var response = await PutPhotoAsync(item, Jpeg(ItemService.MaxThumbnailBytes + 1), Jpeg(100));

        await ProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rejects_a_full_photo_over_the_limit()
    {
        var item = await CreateItemAsync(new { title = "Tee" });

        var response = await PutPhotoAsync(item, Jpeg(100), Jpeg(ItemService.MaxPhotoBytes + 1));

        await ProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_item_without_a_photo_has_no_photo_resource()
    {
        var item = await CreateItemAsync(new { title = "Tee" });

        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/items/{item.Id}/photo", Ct)).StatusCode);
    }
}
