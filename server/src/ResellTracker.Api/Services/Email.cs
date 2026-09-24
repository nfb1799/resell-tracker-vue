using Microsoft.AspNetCore.Identity;
using ResellTracker.Api.Data;

namespace ResellTracker.Api.Services;

/// <summary>
/// Stands in for a real email provider until one is chosen with hosting (Phase 7):
/// the message, reset link included, goes to the log instead of an inbox.
/// </summary>
internal sealed partial class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender<AppUser>
{
    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
    {
        LogResetLink(logger, email, resetLink);
        return Task.CompletedTask;
    }

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink) =>
        throw new NotSupportedException("Email confirmation is not used.");

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode) =>
        throw new NotSupportedException("Reset codes are not used; the app sends a link.");

    [LoggerMessage(Level = LogLevel.Warning, Message = "No email provider configured. Password reset link for {Email}: {ResetLink}")]
    private static partial void LogResetLink(ILogger logger, string email, string resetLink);
}
