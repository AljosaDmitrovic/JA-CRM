using JaCrm.Api.Domain;

namespace JaCrm.UnitTests.Domain;

public sealed class SalaryNormalizerTests
{
    private static readonly SalaryContext Eur = new("EUR", 168m, new Dictionary<string, decimal>
    {
        ["CHF"] = 1.06m,
        ["RSD"] = 0.0085m,
    });

    [Fact]
    public void Monthly_amounts_are_multiplied_by_twelve()
    {
        var result = SalaryNormalizer.Normalize(new SalaryValue(5000m, "EUR", PayPeriod.Monthly, SalaryType.Gross), Eur);

        Assert.Equal(60_000m, result.Annual);
        Assert.True(result.IsComparable);
    }

    [Fact]
    public void Hourly_amounts_use_the_working_hours_per_month()
    {
        var result = SalaryNormalizer.Normalize(new SalaryValue(40m, "EUR", PayPeriod.Hourly, SalaryType.Gross), Eur);

        Assert.Equal(40m * 168m * 12m, result.Annual);
    }

    [Fact]
    public void Hourly_amounts_follow_a_changed_working_hours_setting()
    {
        var context = Eur with { WorkingHoursPerMonth = 160m };

        var result = SalaryNormalizer.Normalize(new SalaryValue(50m, "EUR", PayPeriod.Hourly, SalaryType.Gross), context);

        Assert.Equal(96_000m, result.Annual);
    }

    [Fact]
    public void Foreign_currencies_are_converted_with_the_exchange_rate()
    {
        var chf = SalaryNormalizer.Normalize(new SalaryValue(120_000m, "CHF", PayPeriod.Annual, SalaryType.Gross), Eur);
        var rsd = SalaryNormalizer.Normalize(new SalaryValue(400_000m, "rsd", PayPeriod.Monthly, SalaryType.Gross), Eur);

        Assert.Equal(127_200m, chf.Annual);
        Assert.Equal(40_800m, rsd.Annual);
        Assert.Equal("EUR", chf.BaseCurrency);
    }

    [Theory]
    [InlineData(SalaryType.Net, Comparability.Net)]
    [InlineData(SalaryType.Unknown, Comparability.UnknownType)]
    public void Net_and_unknown_amounts_are_converted_but_not_comparable(SalaryType type, Comparability expected)
    {
        var result = SalaryNormalizer.Normalize(new SalaryValue(3000m, "EUR", PayPeriod.Monthly, type), Eur);

        Assert.Equal(36_000m, result.Annual);
        Assert.Equal(expected, result.Comparability);
        Assert.False(result.IsComparable);
    }

    [Fact]
    public void A_currency_without_a_rate_cannot_be_converted()
    {
        var result = SalaryNormalizer.Normalize(new SalaryValue(100_000m, "USD", PayPeriod.Annual, SalaryType.Gross), Eur);

        Assert.Null(result.Annual);
        Assert.Equal(Comparability.MissingRate, result.Comparability);
    }

    [Fact]
    public void Ranges_keep_missing_bounds_missing()
    {
        var result = SalaryNormalizer.Normalize(new SalaryRange(null, 8000m, "CHF", PayPeriod.Monthly, SalaryType.Gross), Eur);

        Assert.Null(result.AnnualMin);
        Assert.Equal(101_760m, result.AnnualMax);
        Assert.True(result.IsComparable);
    }

    [Theory]
    [InlineData(50_000, 70_000, 60_000, null, true)]
    [InlineData(50_000, 70_000, 70_000, 90_000, true)]
    [InlineData(50_000, 70_000, 71_000, null, false)]
    [InlineData(50_000, 70_000, null, 49_999, false)]
    [InlineData(null, 70_000, 65_000, 80_000, true)]
    [InlineData(80_000, null, null, 79_000, false)]
    public void Overlap_compares_ranges_with_optional_bounds(
        int? min, int? max, int? filterMin, int? filterMax, bool expected) =>
        Assert.Equal(expected, SalaryNormalizer.Overlaps(min, max, comparable: true, filterMin, filterMax));

    [Fact]
    public void Non_comparable_amounts_never_match_a_salary_filter() =>
        Assert.False(SalaryNormalizer.Overlaps(60_000m, 60_000m, comparable: false, 10_000m, null));
}
