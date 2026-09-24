using System.Text.Json;

namespace ResellTracker.Domain;

/// <summary>
/// How a platform's cut is estimated before the real payout is known:
/// fee = base * Percent / 100 + Fixed, where base is the price alone, or price +
/// shipping charged when <see cref="IncludesShipping"/>.
/// </summary>
public sealed record FeeSchedule(decimal Percent, decimal Fixed, bool IncludesShipping);

public sealed record Platform(string Id, string Label, string Note, FeeSchedule DefaultFees);

/// <summary>
/// The marketplaces an item can be listed on or sold through. The list lives in
/// shared/platforms.json, embedded here and imported by the client, so adding a
/// platform is a one-place change.
/// </summary>
public static class Platforms
{
    private sealed record RegistryFile(string Fallback, List<Platform> Platforms);

    private static readonly RegistryFile Registry = Load();

    /// <summary>Every platform, in display order.</summary>
    public static IReadOnlyList<Platform> All { get; } = Registry.Platforms.AsReadOnly();

    /// <summary>The platform whose schedule stands in for an id the registry doesn't know.</summary>
    public static Platform Fallback { get; } = Registry.Platforms.Single(p => p.Id == Registry.Fallback);

    public static bool IsKnown(string? id) => Find(id) is not null;

    public static Platform? Find(string? id) => All.FirstOrDefault(p => p.Id == id);

    private static RegistryFile Load()
    {
        using var stream = typeof(Platforms).Assembly.GetManifestResourceStream("ResellTracker.Domain.platforms.json")
            ?? throw new InvalidOperationException("platforms.json is not embedded in the domain assembly.");
        return JsonSerializer.Deserialize<RegistryFile>(stream, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("platforms.json is empty.");
    }
}

/// <summary>
/// A user's fee schedules: their saved overrides laid over the registry defaults,
/// so a platform added after they last saved still has a schedule.
/// </summary>
public sealed class FeeSettings
{
    private readonly Dictionary<string, FeeSchedule> _schedules;

    public FeeSettings(IReadOnlyDictionary<string, FeeSchedule>? overrides = null)
    {
        _schedules = Platforms.All.ToDictionary(p => p.Id, p => p.DefaultFees);
        foreach (var (id, schedule) in overrides ?? new Dictionary<string, FeeSchedule>())
        {
            if (Platforms.IsKnown(id))
            {
                _schedules[id] = schedule;
            }
        }
    }

    public static FeeSettings Defaults { get; } = new();

    /// <summary>The schedule for a platform, or the fallback platform's for an unknown id.</summary>
    public FeeSchedule For(string? platformId) =>
        platformId is not null && _schedules.TryGetValue(platformId, out var schedule)
            ? schedule
            : _schedules[Platforms.Fallback.Id];
}
