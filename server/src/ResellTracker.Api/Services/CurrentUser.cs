using System.Security.Claims;

namespace ResellTracker.Api.Services;

/// <summary>The signed-in user every query is scoped to.</summary>
public interface ICurrentUser
{
    Guid Id { get; }
}

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid Id =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new InvalidOperationException("No signed-in user. Is the endpoint missing [Authorize]?");
}
