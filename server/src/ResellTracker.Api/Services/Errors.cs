using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ResellTracker.Domain;

namespace ResellTracker.Api.Services;

/// <summary>The item doesn't exist, or belongs to someone else; the two are indistinguishable on purpose.</summary>
public sealed class ItemNotFoundException() : Exception("No item with that id.");

/// <summary>A create reused an id that is already taken.</summary>
public sealed class DuplicateItemException() : Exception("An item with that id already exists.");

/// <summary>The If-Match version is stale: someone else changed the item first.</summary>
public sealed class StaleVersionException() : Exception("The item changed since you loaded it. Reload and try again.");

/// <summary>A change to an existing item arrived without an If-Match version.</summary>
public sealed class VersionRequiredException() : Exception("Send the item's version in an If-Match header.");

/// <summary>An uploaded photo isn't usable.</summary>
public sealed class InvalidPhotoException(string message) : Exception(message);

/// <summary>
/// Turns the exceptions above into ProblemDetails with the right status. Anything
/// else falls through to the default handler as a 500.
/// </summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ItemNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            DuplicateItemException => (StatusCodes.Status409Conflict, "Duplicate item"),
            LifecycleException => (StatusCodes.Status409Conflict, "Not allowed in this state"),
            StaleVersionException => (StatusCodes.Status412PreconditionFailed, "Stale version"),
            VersionRequiredException => (StatusCodes.Status428PreconditionRequired, "Version required"),
            InvalidPhotoException => (StatusCodes.Status400BadRequest, "Invalid photo"),
            _ => (0, ""),
        };

        if (status == 0)
        {
            return false;
        }

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = exception.Message },
        });
    }
}
