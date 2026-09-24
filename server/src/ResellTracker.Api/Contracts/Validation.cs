using System.ComponentModel.DataAnnotations;
using ResellTracker.Domain;

namespace ResellTracker.Api.Contracts;

/// <summary>
/// A money amount: a whole number of cents that fits decimal(10,2). Null passes;
/// pair with [Required] where a value must be present.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MoneyAttribute : ValidationAttribute
{
    /// <summary>When set, zero is refused too (an accepted offer has to be something).</summary>
    public bool Positive { get; init; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not decimal amount)
        {
            return ValidationResult.Success;
        }

        var name = validationContext.DisplayName;
        if (!Money.IsWholeCents(amount))
        {
            return new ValidationResult($"{name} can't have more than 2 decimal places.", [validationContext.MemberName!]);
        }

        if (amount < 0 || (Positive && amount == 0))
        {
            return new ValidationResult($"{name} must be {(Positive ? "more than" : "at least")} 0.", [validationContext.MemberName!]);
        }

        return amount > Money.MaxAmount
            ? new ValidationResult($"{name} is too large.", [validationContext.MemberName!])
            : ValidationResult.Success;
    }
}

internal static class Rules
{
    public static IEnumerable<ValidationResult> MaxLength(string? value, int max, string member)
    {
        if (value is not null && value.Trim().Length > max)
        {
            yield return new ValidationResult($"{member} can't be longer than {max} characters.", [member]);
        }
    }

    public static IEnumerable<ValidationResult> KnownPlatform(string? id, string member)
    {
        if (id is not null && !Platforms.IsKnown(id))
        {
            yield return new ValidationResult(
                $"\"{id}\" is not one of {string.Join(", ", Platforms.All.Select(p => p.Id))}.", [member]);
        }
    }
}
