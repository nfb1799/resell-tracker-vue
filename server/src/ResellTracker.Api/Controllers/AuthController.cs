using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using ResellTracker.Api.Contracts;
using ResellTracker.Api.Data;
using ResellTracker.Api.Services;

namespace ResellTracker.Api.Controllers;

/// <summary>
/// Accounts, on ASP.NET Core Identity with an HttpOnly, SameSite=Strict session
/// cookie. The SPA is served from this same origin, so the cookie is first-party
/// and never readable from script.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    AppDbContext db,
    DemoService demos,
    IEmailSender<AppUser> email,
    IConfiguration config,
    IWebHostEnvironment env) : ControllerBase
{
    // The original app's wording, so the sign-in screen reads the same.
    private const string BadCredentials = "That email and password do not match an account.";
    private const string EmailTaken = "There is already an account with that email.";
    private const string TooManyAttempts = "Too many attempts. Wait a few minutes and try again.";
    private const string BadResetLink = "That reset link is invalid or has expired. Ask for a new one.";

    [HttpPost("register")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<MeResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var address = request.Email.Trim();
        var user = new AppUser { Id = Guid.CreateVersion7(), UserName = address, Email = address };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var (field, message) = error.Code switch
                {
                    nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName) =>
                        (nameof(request.Email), EmailTaken),
                    _ when error.Code.StartsWith("Password", StringComparison.Ordinal) =>
                        (nameof(request.Password), "Use at least 6 characters."),
                    _ => (nameof(request.Email), error.Description),
                };
                if (ModelState[field]?.Errors.Any(e => e.ErrorMessage == message) != true)
                {
                    ModelState.TryAddModelError(field, message);
                }
            }

            return ValidationProblem();
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? address.Split('@')[0] : request.DisplayName.Trim();
        db.UserSettings.Add(new UserSettings { UserId = user.Id, DisplayName = displayName });
        await db.SaveChangesAsync(ct);

        await signIn.SignInAsync(user, isPersistent: true);
        return await MeAsync(user, ct);
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<ActionResult<MeResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.IsDemo)
        {
            return Problem(BadCredentials, statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in failed");
        }

        var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return Problem(TooManyAttempts, statusCode: StatusCodes.Status429TooManyRequests, title: "Locked out");
        }

        return result.Succeeded
            ? await MeAsync(user, ct)
            : Problem(BadCredentials, statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in failed");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct) =>
        await users.GetUserAsync(User) is { } user ? await MeAsync(user, ct) : Unauthorized();

    /// <summary>Makes a private, seeded demo account and signs into it until it expires.</summary>
    [HttpPost("demo")]
    [EnableRateLimiting(RateLimits.Demo)]
    public async Task<ActionResult<MeResponse>> Demo(CancellationToken ct)
    {
        var user = await demos.CreateAsync(ct);
        await signIn.SignInAsync(user, new AuthenticationProperties { IsPersistent = true, ExpiresUtc = user.DemoExpiresAt });
        return await MeAsync(user, ct);
    }

    /// <summary>
    /// Emails a reset link. Always 202, whether or not the address has an account,
    /// so the endpoint can't be used to find out who has one.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is { IsDemo: false, Email: { } address })
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var link = $"{PublicBaseUrl()}/reset-password?email={Uri.EscapeDataString(address)}" +
                $"&token={WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))}";
            await email.SendPasswordResetLinkAsync(user, address, link);
        }

        return Accepted();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimits.Auth)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            token = "";
        }

        if (user is null || user.IsDemo || token.Length == 0)
        {
            return Problem(BadResetLink, statusCode: StatusCodes.Status400BadRequest, title: "Reset failed");
        }

        var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
        if (result.Succeeded)
        {
            return NoContent();
        }

        var weak = result.Errors.Any(e => e.Code.StartsWith("Password", StringComparison.Ordinal));
        return Problem(weak ? "Use at least 6 characters." : BadResetLink, statusCode: StatusCodes.Status400BadRequest, title: "Reset failed");
    }

    private async Task<MeResponse> MeAsync(AppUser user, CancellationToken ct)
    {
        var displayName = await db.UserSettings.Where(s => s.UserId == user.Id).Select(s => s.DisplayName).SingleOrDefaultAsync(ct);
        return new MeResponse(user.Id, user.Email ?? "", displayName ?? "", user.IsDemo, user.DemoExpiresAt);
    }

    /// <summary>
    /// Where reset links point. Taken from configuration outside development, never
    /// from the request's Host header, which a caller controls and could aim at
    /// their own site to harvest reset tokens.
    /// </summary>
    private string PublicBaseUrl()
    {
        var configured = config["App:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/');
        }

        return env.IsDevelopment()
            ? $"{Request.Scheme}://{Request.Host}"
            : throw new InvalidOperationException("Set App:BaseUrl so password reset links point at the real site.");
    }
}
