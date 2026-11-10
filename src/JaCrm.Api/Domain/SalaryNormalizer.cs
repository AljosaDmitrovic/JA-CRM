namespace JaCrm.Api.Domain;

/// <summary>Converts salaries to an annual amount in the base currency so offers in different
/// currencies and pay periods can be compared. Taxes are never estimated: net and unknown amounts
/// are converted but flagged as not comparable (decisions D-25, D-27).</summary>
public static class SalaryNormalizer
{
    public const int MonthsPerYear = 12;

    public static NormalizedAmount Normalize(SalaryValue salary, SalaryContext context)
    {
        var (factor, comparability) = Conversion(salary.Currency, salary.Period, salary.Type, context);
        return new NormalizedAmount(factor is { } f ? Round(salary.Amount * f) : null, context.BaseCurrency, comparability);
    }

    public static NormalizedRange Normalize(SalaryRange range, SalaryContext context)
    {
        var (factor, comparability) = Conversion(range.Currency, range.Period, range.Type, context);
        return new NormalizedRange(
            factor is { } f && range.Min is { } min ? Round(min * f) : null,
            factor is { } g && range.Max is { } max ? Round(max * g) : null,
            context.BaseCurrency,
            comparability);
    }

    /// <summary>Annualizes an amount in its own currency (no exchange rate involved).</summary>
    public static decimal Annualize(decimal amount, PayPeriod period, decimal workingHoursPerMonth) => period switch
    {
        PayPeriod.Hourly => amount * workingHoursPerMonth * MonthsPerYear,
        PayPeriod.Monthly => amount * MonthsPerYear,
        PayPeriod.Annual => amount,
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, null),
    };

    /// <summary>True when the normalized range or amount overlaps [min, max] (either bound optional).
    /// Only comparable values can match (decision D-25).</summary>
    public static bool Overlaps(decimal? annualMin, decimal? annualMax, bool comparable, decimal? filterMin, decimal? filterMax)
    {
        if (!comparable || (annualMin is null && annualMax is null))
        {
            return false;
        }

        var low = annualMin ?? annualMax!.Value;
        var high = annualMax ?? annualMin!.Value;
        return (filterMin is null || high >= filterMin) && (filterMax is null || low <= filterMax);
    }

    private static (decimal? Factor, Comparability Comparability) Conversion(
        string currency, PayPeriod period, SalaryType type, SalaryContext context)
    {
        if (context.RateFor(currency) is not { } rate)
        {
            return (null, Comparability.MissingRate);
        }

        var factor = Annualize(1m, period, context.WorkingHoursPerMonth) * rate;
        var comparability = type switch
        {
            SalaryType.Gross => Comparability.Comparable,
            SalaryType.Net => Comparability.Net,
            _ => Comparability.UnknownType,
        };
        return (factor, comparability);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
