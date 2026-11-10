using System.Net;
using JaCrm.IntegrationTests.Infrastructure;

namespace JaCrm.IntegrationTests;

public sealed class SettingsTests : IAsyncLifetime
{
    private readonly AppFactory _factory = new();
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync() => _client = await _factory.CreateAuthenticatedClientAsync();

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Defaults_match_the_specification()
    {
        var settings = await _client.GetJsonAsync<SettingsDto>("/api/settings");

        Assert.Equal("EUR", settings.BaseCurrency);
        Assert.Equal(168m, settings.WorkingHoursPerMonth);
        Assert.Equal(14, settings.StaleThresholdDays);
        Assert.Equal("DayMonthYear", settings.DateFormat);
        Assert.Equal("System", settings.Theme);
        Assert.Empty(settings.ExchangeRates);
    }

    [Fact]
    public async Task Invalid_settings_are_rejected_per_field()
    {
        var response = await _client.PutJsonAsync("/api/settings", new
        {
            theme = "Dark",
            baseCurrency = "euro",
            workingHoursPerMonth = 0,
            staleThresholdDays = 0,
            dateFormat = "Iso",
        });

        var errors = await response.ValidationErrorsAsync();
        Assert.Contains("baseCurrency", errors.Keys);
        Assert.Contains("workingHoursPerMonth", errors.Keys);
        Assert.Contains("staleThresholdDays", errors.Keys);
    }

    [Fact]
    public async Task Exchange_rates_are_replaced_and_rebased_when_the_base_currency_changes()
    {
        await (await _client.PutJsonAsync("/api/settings/exchange-rates", new
        {
            rates = new[]
            {
                new { currency = "CHF", rateToBase = 1.0625m },
                new { currency = "rsd", rateToBase = 0.0085m },
            },
        })).EnsureStatusAsync(HttpStatusCode.OK);

        var rebased = await (await _client.PutJsonAsync("/api/settings", new
        {
            theme = "System",
            baseCurrency = "CHF",
            workingHoursPerMonth = 168,
            staleThresholdDays = 14,
            dateFormat = "DayMonthYear",
        })).ReadAsync<SettingsDto>();

        Assert.Equal("CHF", rebased.BaseCurrency);
        var rates = rebased.ExchangeRates.ToDictionary(r => r.Currency, r => r.RateToBase);
        Assert.Equal(["EUR", "RSD"], rates.Keys.Order());
        Assert.Equal(0.9411764706m, rates["EUR"]);
        Assert.Equal(0.0080000000m, rates["RSD"]);
    }

    [Fact]
    public async Task The_base_currency_cannot_have_its_own_rate()
    {
        var response = await _client.PutJsonAsync("/api/settings/exchange-rates", new
        {
            rates = new[] { new { currency = "EUR", rateToBase = 1m } },
        });

        Assert.Contains("rates[0].currency", (await response.ValidationErrorsAsync()).Keys);
    }

    private sealed record RateDto(string Currency, decimal RateToBase, DateOnly UpdatedOn);

    private sealed record SettingsDto(
        string Theme, string BaseCurrency, decimal WorkingHoursPerMonth, int StaleThresholdDays, string DateFormat, List<RateDto> ExchangeRates);
}
