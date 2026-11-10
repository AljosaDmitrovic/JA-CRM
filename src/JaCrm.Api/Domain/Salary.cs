namespace JaCrm.Api.Domain;

/// <summary>A single salary amount with its terms. Persisted as an EF Core complex type.</summary>
public sealed record SalaryValue(decimal Amount, string Currency, PayPeriod Period, SalaryType Type);

/// <summary>A posted salary range. Either bound may be missing ("from 60k", "up to 80k"), but not both.</summary>
public sealed record SalaryRange(decimal? Min, decimal? Max, string Currency, PayPeriod Period, SalaryType Type);

/// <summary>Why a normalized amount can or cannot be compared with others.</summary>
public enum Comparability
{
    /// <summary>Gross amount with a known exchange rate: directly comparable.</summary>
    Comparable,

    /// <summary>Net amount: converted to annual base currency, but not comparable with gross.</summary>
    Net,

    /// <summary>Gross/net unknown: converted, but not comparable.</summary>
    UnknownType,

    /// <summary>No exchange rate for the currency: cannot be converted at all.</summary>
    MissingRate,
}

/// <summary>An amount normalized to annual value in the base currency.</summary>
public sealed record NormalizedAmount(decimal? Annual, string BaseCurrency, Comparability Comparability)
{
    public bool IsComparable => Comparability == Comparability.Comparable;
}

/// <summary>A range normalized to annual value in the base currency.</summary>
public sealed record NormalizedRange(decimal? AnnualMin, decimal? AnnualMax, string BaseCurrency, Comparability Comparability)
{
    public bool IsComparable => Comparability == Comparability.Comparable;
}

/// <summary>The settings needed to normalize salaries.</summary>
public sealed record SalaryContext(string BaseCurrency, decimal WorkingHoursPerMonth, IReadOnlyDictionary<string, decimal> RatesToBase)
{
    public decimal? RateFor(string currency) =>
        string.Equals(currency, BaseCurrency, StringComparison.OrdinalIgnoreCase) ? 1m
        : RatesToBase.TryGetValue(currency.ToUpperInvariant(), out var rate) ? rate
        : null;
}
