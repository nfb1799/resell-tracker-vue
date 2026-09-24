using System.ComponentModel.DataAnnotations;

namespace ResellTracker.Api.Contracts;

public sealed class RegisterRequest
{
    [Required, EmailAddress(ErrorMessage = "That does not look like an email address."), StringLength(256)]
    public string Email { get; init; } = "";

    [Required]
    public string Password { get; init; } = "";

    /// <summary>Defaults to the part of the email before the @, as the original did.</summary>
    [StringLength(100)]
    public string? DisplayName { get; init; }
}

public sealed class LoginRequest
{
    [Required]
    public string Email { get; init; } = "";

    [Required]
    public string Password { get; init; } = "";
}

public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress(ErrorMessage = "That does not look like an email address.")]
    public string Email { get; init; } = "";
}

public sealed class ResetPasswordRequest
{
    [Required]
    public string Email { get; init; } = "";

    /// <summary>The token from the emailed link, as it appears there (base64url).</summary>
    [Required]
    public string Token { get; init; } = "";

    [Required]
    public string NewPassword { get; init; } = "";
}

/// <summary>Who is signed in. A demo account says when it disappears.</summary>
public sealed record MeResponse(Guid Id, string Email, string DisplayName, bool IsDemo, DateTimeOffset? DemoExpiresAt);
