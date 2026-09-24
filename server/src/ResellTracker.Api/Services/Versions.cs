using Microsoft.Net.Http.Headers;

namespace ResellTracker.Api.Services;

/// <summary>Row versions on the wire: an opaque quoted string, used as ETag and If-Match.</summary>
public static class Versions
{
    public static string Format(byte[] rowVersion) => $"\"{Convert.ToBase64String(rowVersion)}\"";

    /// <summary>The row version an If-Match header names, or null if there is none or it's malformed.</summary>
    public static byte[]? FromIfMatch(IHeaderDictionary headers)
    {
        if (!EntityTagHeaderValue.TryParseList(headers.IfMatch, out var tags) || tags.Count != 1)
        {
            return null;
        }

        var tag = tags[0].Tag.Value?.Trim('"');
        try
        {
            return tag is null ? null : Convert.FromBase64String(tag);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
