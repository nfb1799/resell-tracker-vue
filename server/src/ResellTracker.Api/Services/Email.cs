using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ResellTracker.Api.Data;

namespace ResellTracker.Api.Services;

public sealed class EmailOptions
{
    /// <summary>Resend API key. Without one, reset links are only logged, and only in development.</summary>
    public string? ResendApiKey { get; init; }

    /// <summary>The sender. Resend's shared address works with no domain of your own (it only delivers to your account's address).</summary>
    public string From { get; init; } = "Resell Tracker <onboarding@resend.dev>";
}

public static class EmailRegistration
{
    public static IServiceCollection AddAppEmail(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<EmailOptions>(config.GetSection("Email"));
        if (string.IsNullOrWhiteSpace(config["Email:ResendApiKey"]))
        {
            return services.AddTransient<IEmailSender<AppUser>, LoggingEmailSender>();
        }

        services.AddHttpClient<IEmailSender<AppUser>, ResendEmailSender>((provider, http) =>
        {
            http.BaseAddress = new Uri("https://api.resend.com/");
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", provider.GetRequiredService<IOptions<EmailOptions>>().Value.ResendApiKey);
        });
        return services;
    }
}

/// <summary>
/// Sends password-reset links through Resend. A send that fails is logged rather
/// than surfaced: the reset endpoint answers the same whatever happens, so it
/// can't be used to probe which addresses have accounts.
/// </summary>
internal sealed partial class ResendEmailSender(HttpClient http, IOptions<EmailOptions> options, ILogger<ResendEmailSender> logger)
    : IEmailSender<AppUser>
{
    public async Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
    {
        var link = HtmlEncoder.Default.Encode(resetLink);
        var response = await http.PostAsJsonAsync("emails", new
        {
            from = options.Value.From,
            to = new[] { email },
            subject = "Reset your Resell Tracker password",
            text = $"Someone asked to reset the password for this Resell Tracker account. If it was you, open this link to choose a new one:\n\n{resetLink}\n\nIf it wasn't, ignore this email; nothing changes.",
            html = $"<p>Someone asked to reset the password for this Resell Tracker account. If it was you, choose a new one here:</p><p><a href=\"{link}\">Reset my password</a></p><p>If it wasn't, ignore this email; nothing changes.</p>",
        });

        if (!response.IsSuccessStatusCode)
        {
            LogFailed(logger, (int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
    }

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink) =>
        throw new NotSupportedException("Email confirmation is not used.");

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode) =>
        throw new NotSupportedException("Reset codes are not used; the app sends a link.");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Resend refused a password reset email ({Status}): {Body}")]
    private static partial void LogFailed(ILogger logger, int status, string body);
}

/// <summary>
/// No email provider configured. In development the reset link goes to the log so
/// the flow can be tried; anywhere else that would put working reset links in
/// production logs, so it only notes that nothing was sent.
/// </summary>
internal sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger, IHostEnvironment env) : IEmailSender<AppUser>
{
    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
    {
        if (env.IsDevelopment())
        {
            LogResetLink(logger, email, resetLink);
        }
        else
        {
            LogNotSent(logger);
        }

        return Task.CompletedTask;
    }

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink) =>
        throw new NotSupportedException("Email confirmation is not used.");

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode) =>
        throw new NotSupportedException("Reset codes are not used; the app sends a link.");

    [LoggerMessage(Level = LogLevel.Warning, Message = "No email provider configured. Password reset link for {Email}: {ResetLink}")]
    private static partial void LogResetLink(ILogger logger, string email, string resetLink);

    [LoggerMessage(Level = LogLevel.Error, Message = "A password reset was requested but no email provider is configured (Email:ResendApiKey); nothing was sent.")]
    private static partial void LogNotSent(ILogger logger);
}
