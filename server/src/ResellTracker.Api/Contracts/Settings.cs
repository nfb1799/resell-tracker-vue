using System.ComponentModel.DataAnnotations;

namespace ResellTracker.Api.Contracts;

public sealed record SettingsResponse(string DisplayName, string Currency, string Theme, decimal ProfitGoal);

public sealed class SettingsRequest : IValidatableObject
{
    [StringLength(100)]
    public string? DisplayName { get; init; }

    [Required, RegularExpression("^[A-Z]{3}$", ErrorMessage = "Currency must be a 3-letter ISO code such as USD.")]
    public string Currency { get; init; } = "USD";

    [Required]
    public string Theme { get; init; } = "dark";

    [Money]
    public decimal? ProfitGoal { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Theme is not ("dark" or "light"))
        {
            yield return new ValidationResult("Theme must be dark or light.", [nameof(Theme)]);
        }
    }
}

/// <summary>One platform's fee schedule: fee = base * Percent / 100 + Fixed.</summary>
public sealed class FeeScheduleDto
{
    [Required, Range(0, 100), Money]
    public decimal? Percent { get; init; }

    [Required, Money]
    public decimal? Fixed { get; init; }

    public bool IncludesShipping { get; init; }
}
