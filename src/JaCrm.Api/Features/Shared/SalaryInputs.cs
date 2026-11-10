using JaCrm.Api.Common;
using JaCrm.Api.Domain;

namespace JaCrm.Api.Features.Shared;

/// <summary>Salary as received from a form; every part is validated before it becomes a value object.</summary>
public sealed record SalaryValueInput(decimal? Amount, string? Currency, PayPeriod? Period, SalaryType? Type);

/// <summary>Posted range as received from a form. Either bound may be empty, not both.</summary>
public sealed record SalaryRangeInput(decimal? Min, decimal? Max, string? Currency, PayPeriod? Period, SalaryType? Type);

public static class SalaryInputs
{
    public const decimal MaxAmount = 100_000_000m;

    public static SalaryValue? ToValue(this Validator validator, SalaryValueInput? input, string field)
    {
        if (input is null)
        {
            return null;
        }

        var currency = input.Currency?.Trim().ToUpperInvariant();
        if (input.Amount is null)
        {
            validator.Add($"{field}.amount", "Amount is required.");
        }

        validator
            .Range($"{field}.amount", input.Amount, 0, MaxAmount, "Amount")
            .Currency($"{field}.currency", currency);
        Terms(validator, input.Period, input.Type, field);

        return validator.IsValid && input.Amount is { } amount
            ? new SalaryValue(amount, currency!, input.Period!.Value, input.Type!.Value)
            : null;
    }

    public static SalaryRange? ToRange(this Validator validator, SalaryRangeInput? input, string field)
    {
        if (input is null)
        {
            return null;
        }

        var currency = input.Currency?.Trim().ToUpperInvariant();
        if (input.Min is null && input.Max is null)
        {
            validator.Add($"{field}.min", "Enter a minimum, a maximum, or both.");
        }

        validator
            .Range($"{field}.min", input.Min, 0, MaxAmount, "Minimum")
            .Range($"{field}.max", input.Max, 0, MaxAmount, "Maximum")
            .Currency($"{field}.currency", currency);
        if (input.Min is { } min && input.Max is { } max && min > max)
        {
            validator.Add($"{field}.max", "Maximum must not be lower than the minimum.");
        }

        Terms(validator, input.Period, input.Type, field);

        return validator.IsValid
            ? new SalaryRange(input.Min, input.Max, currency!, input.Period!.Value, input.Type!.Value)
            : null;
    }

    private static void Terms(Validator validator, PayPeriod? period, SalaryType? type, string field)
    {
        if (period is null || !Enum.IsDefined(period.Value))
        {
            validator.Add($"{field}.period", "Payment period is required.");
        }

        if (type is null || !Enum.IsDefined(type.Value))
        {
            validator.Add($"{field}.type", "Salary type (gross / net / unknown) is required.");
        }
    }
}
