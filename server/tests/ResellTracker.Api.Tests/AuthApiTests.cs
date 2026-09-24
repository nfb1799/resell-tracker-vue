using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using ResellTracker.Api.Contracts;

namespace ResellTracker.Api.Tests;

public class AuthApiTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.test";

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body) =>
        client.PostAsJsonAsync($"/api/auth/{path}", body, Ct);

    private static async Task<string?> DetailAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Ct))?.Detail;

    [Fact]
    public async Task Signing_up_signs_you_in_with_a_locked_down_session_cookie()
    {
        using var browser = factory.CreateBrowser();
        var email = NewEmail();

        var response = await PostAsync(browser, "register", new { email, password = "secret1", displayName = "Nik" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("rt_session=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);

        var me = await browser.GetFromJsonAsync<MeResponse>("/api/auth/me", Ct);
        Assert.Equal(email, me!.Email);
        Assert.Equal("Nik", me.DisplayName);
        Assert.False(me.IsDemo);
    }

    [Fact]
    public async Task The_display_name_defaults_to_the_start_of_the_email()
    {
        using var browser = factory.CreateBrowser();

        var me = await (await PostAsync(browser, "register", new { email = "reseller.nik@example.test", password = "secret1" }))
            .Content.ReadFromJsonAsync<MeResponse>(Ct);

        Assert.Equal("reseller.nik", me!.DisplayName);
    }

    [Fact]
    public async Task Refuses_an_email_that_already_has_an_account()
    {
        var email = NewEmail();
        using var first = factory.CreateBrowser();
        await PostAsync(first, "register", new { email, password = "secret1" });

        using var second = factory.CreateBrowser();
        var response = await PostAsync(second, "register", new { email = email.ToUpperInvariant(), password = "secret1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        Assert.Equal(["There is already an account with that email."], problem!.Errors["Email"]);
    }

    [Theory]
    [InlineData("not-an-email", "secret1", "Email")]
    [InlineData("ok@example.test", "short", "Password")]
    public async Task Refuses_a_bad_email_or_a_password_under_6_characters(string email, string password, string field)
    {
        using var browser = factory.CreateBrowser();

        var response = await PostAsync(browser, "register", new { email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(field, (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct))!.Errors.Keys);
    }

    [Fact]
    public async Task Six_characters_with_no_digits_or_symbols_is_enough()
    {
        using var browser = factory.CreateBrowser();

        var response = await PostAsync(browser, "register", new { email = NewEmail(), password = "abcdef" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Signs_in_and_out()
    {
        var email = NewEmail();
        using (var signup = factory.CreateBrowser())
        {
            await PostAsync(signup, "register", new { email, password = "secret1" });
        }

        using var browser = factory.CreateBrowser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await PostAsync(browser, "login", new { email, password = "secret1" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/items", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsync("/api/auth/logout", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/items", Ct)).StatusCode);
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_email_get_the_same_answer()
    {
        var email = NewEmail();
        using var browser = factory.CreateBrowser();
        await PostAsync(browser, "register", new { email, password = "secret1" });
        await browser.PostAsync("/api/auth/logout", null, Ct);

        var wrongPassword = await PostAsync(browser, "login", new { email, password = "nope-nope" });
        var unknownEmail = await PostAsync(browser, "login", new { email = NewEmail(), password = "secret1" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(await DetailAsync(wrongPassword), await DetailAsync(unknownEmail));
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_against_the_right_one()
    {
        var email = NewEmail();
        using var browser = factory.CreateBrowser();
        await PostAsync(browser, "register", new { email, password = "secret1" });
        await browser.PostAsync("/api/auth/logout", null, Ct);

        for (var i = 0; i < 5; i++)
        {
            await PostAsync(browser, "login", new { email, password = "guess-" + i });
        }

        var right = await PostAsync(browser, "login", new { email, password = "secret1" });

        Assert.Equal(HttpStatusCode.TooManyRequests, right.StatusCode);
    }

    [Fact]
    public async Task A_password_can_be_reset_through_the_emailed_link()
    {
        var email = NewEmail();
        using var browser = factory.CreateBrowser();
        await PostAsync(browser, "register", new { email, password = "secret1" });
        await browser.PostAsync("/api/auth/logout", null, Ct);

        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync(browser, "forgot-password", new { email })).StatusCode);

        var link = new Uri(factory.Emails.ResetLinkFor(email)!);
        Assert.Equal("https://resell.test/reset-password", link.GetLeftPart(UriPartial.Path));
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(link.Query);

        var reset = await PostAsync(browser, "reset-password", new { email, token = query["token"].ToString(), newPassword = "brand-new" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(browser, "login", new { email, password = "secret1" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(browser, "login", new { email, password = "brand-new" })).StatusCode);
    }

    [Fact]
    public async Task Asking_for_a_reset_does_not_reveal_whether_an_account_exists()
    {
        using var browser = factory.CreateBrowser();
        var email = NewEmail();

        var response = await PostAsync(browser, "forgot-password", new { email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Null(factory.Emails.ResetLinkFor(email));
    }

    [Theory]
    [InlineData("not-base64-at-all!")]
    [InlineData("dGhpcy1pcy1ub3QtYS10b2tlbg")]
    public async Task A_forged_reset_token_is_refused(string token)
    {
        var email = NewEmail();
        using var browser = factory.CreateBrowser();
        await PostAsync(browser, "register", new { email, password = "secret1" });

        var response = await PostAsync(browser, "reset-password", new { email, token, newPassword = "hijacked" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
