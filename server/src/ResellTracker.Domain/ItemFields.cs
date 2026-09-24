using System.Text.Json;

namespace ResellTracker.Domain;

/// <summary>
/// The shape of an item: allowed conditions, the default one, and text limits.
/// Lives in shared/item-fields.json so the client's form and importer and this
/// server's validation read the same rules.
/// </summary>
public static class ItemFields
{
    private sealed record FieldsFile(List<string> Conditions, string DefaultCondition, Dictionary<string, int> MaxLength);

    private static readonly FieldsFile Fields = SharedResource.Load<FieldsFile>("item-fields.json");

    public static IReadOnlyList<string> Conditions { get; } = Fields.Conditions.AsReadOnly();

    public static string DefaultCondition => Fields.DefaultCondition;

    public static int MaxTitle => Fields.MaxLength["title"];
    public static int MaxBrand => Fields.MaxLength["brand"];
    public static int MaxCategory => Fields.MaxLength["category"];
    public static int MaxSize => Fields.MaxLength["size"];
    public static int MaxSource => Fields.MaxLength["source"];
    public static int MaxNotes => Fields.MaxLength["notes"];
    public static int MaxDonationOrg => Fields.MaxLength["donationOrg"];

    /// <summary>The canonical spelling of a condition, matched case-insensitively; null if unknown.</summary>
    public static string? MatchCondition(string? value) =>
        Conditions.FirstOrDefault(c => string.Equals(c, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Reads the shared/*.json files embedded in this assembly.</summary>
internal static class SharedResource
{
    public static T Load<T>(string fileName)
    {
        using var stream = typeof(SharedResource).Assembly.GetManifestResourceStream($"ResellTracker.Domain.{fileName}")
            ?? throw new InvalidOperationException($"{fileName} is not embedded in the domain assembly.");
        return JsonSerializer.Deserialize<T>(stream, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException($"{fileName} is empty.");
    }
}
