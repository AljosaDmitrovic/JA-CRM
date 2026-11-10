using JaCrm.Api.Common;
using JaCrm.Api.Data;
using JaCrm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.Api.Features.Settings;

public sealed record ExchangeRateDto(string Currency, decimal RateToBase, DateOnly UpdatedOn);

public sealed record SettingsResponse(
    ThemePreference Theme,
    string BaseCurrency,
    decimal WorkingHoursPerMonth,
    int StaleThresholdDays,
    DateFormatPreference DateFormat,
    IReadOnlyList<ExchangeRateDto> ExchangeRates);

public sealed record SettingsUpdateRequest(
    ThemePreference Theme,
    string? BaseCurrency,
    decimal WorkingHoursPerMonth,
    int StaleThresholdDays,
    DateFormatPreference DateFormat);

public sealed record ExchangeRateInput(string? Currency, decimal RateToBase, DateOnly? UpdatedOn);

public sealed record ExchangeRatesRequest(IReadOnlyList<ExchangeRateInput>? Rates);

public sealed class SettingsService(AppDbContext db, Clock clock)
{
    public async Task<SettingsResponse> GetAsync(CancellationToken ct)
    {
        var settings = await db.GetSettingsAsync(ct);
        var rates = await db.ExchangeRates.AsNoTracking().OrderBy(r => r.Currency).ToListAsync(ct);
        return new SettingsResponse(
            settings.Theme,
            settings.BaseCurrency,
            settings.WorkingHoursPerMonth,
            settings.StaleThresholdDays,
            settings.DateFormat,
            rates.Select(r => new ExchangeRateDto(r.Currency, r.RateToBase, r.UpdatedOn)).ToList());
    }

    public async Task<SettingsResponse> UpdateAsync(SettingsUpdateRequest request, CancellationToken ct)
    {
        var baseCurrency = request.BaseCurrency?.Trim().ToUpperInvariant();
        new Validator()
            .Currency("baseCurrency", baseCurrency, "Base currency")
            .Defined("theme", (ThemePreference?)request.Theme, "Theme")
            .Defined("dateFormat", (DateFormatPreference?)request.DateFormat, "Date format")
            .Range("workingHoursPerMonth", request.WorkingHoursPerMonth, 1, 744, "Working hours per month")
            .Range("staleThresholdDays", request.StaleThresholdDays, 1, 365, "Stale threshold")
            .ThrowIfInvalid();

        var settings = await db.GetSettingsAsync(ct);
        if (!string.Equals(settings.BaseCurrency, baseCurrency, StringComparison.Ordinal))
        {
            await RebaseRatesAsync(settings.BaseCurrency, baseCurrency!, ct);
            settings.BaseCurrency = baseCurrency!;
        }

        settings.Theme = request.Theme;
        settings.WorkingHoursPerMonth = request.WorkingHoursPerMonth;
        settings.StaleThresholdDays = request.StaleThresholdDays;
        settings.DateFormat = request.DateFormat;
        await db.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }

    /// <summary>Replaces the whole list of exchange rates.</summary>
    public async Task<SettingsResponse> ReplaceRatesAsync(ExchangeRatesRequest request, CancellationToken ct)
    {
        var settings = await db.GetSettingsAsync(ct);
        var inputs = request.Rates ?? [];
        var validator = new Validator();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < inputs.Count; i++)
        {
            var currency = inputs[i].Currency?.Trim().ToUpperInvariant();
            var field = $"rates[{i}]";
            validator.Currency($"{field}.currency", currency);
            validator.Range($"{field}.rateToBase", inputs[i].RateToBase, 0.000001m, 1_000_000_000m, "Rate");
            if (currency == settings.BaseCurrency)
            {
                validator.Add($"{field}.currency", $"{currency} is the base currency; its rate is always 1.");
            }
            else if (currency is not null && !seen.Add(currency))
            {
                validator.Add($"{field}.currency", $"{currency} is listed more than once.");
            }
        }

        validator.ThrowIfInvalid();

        var existing = await db.ExchangeRates.ToListAsync(ct);
        db.ExchangeRates.RemoveRange(existing.Where(r => !seen.Contains(r.Currency)));
        foreach (var input in inputs)
        {
            var currency = input.Currency!.Trim().ToUpperInvariant();
            var rate = existing.FirstOrDefault(r => r.Currency == currency);
            if (rate is null)
            {
                db.ExchangeRates.Add(rate = new ExchangeRate { Currency = currency });
            }

            // Only a changed rate gets a new "last updated" date unless the client sends one.
            if (rate.RateToBase != input.RateToBase || input.UpdatedOn is not null || rate.UpdatedOn == default)
            {
                rate.UpdatedOn = input.UpdatedOn ?? clock.Today;
            }

            rate.RateToBase = input.RateToBase;
        }

        await db.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }

    /// <summary>Converts every rate to the new base (decision D-24). Needs a rate for the new base
    /// unless there are no rates at all.</summary>
    private async Task RebaseRatesAsync(string oldBase, string newBase, CancellationToken ct)
    {
        var rates = await db.ExchangeRates.ToListAsync(ct);
        if (rates.Count == 0)
        {
            return;
        }

        var pivot = rates.FirstOrDefault(r => r.Currency == newBase)
            ?? throw ValidationException.For("baseCurrency",
                $"Add an exchange rate for {newBase} first, so the other rates can be converted to it.");

        foreach (var rate in rates.Where(r => r != pivot))
        {
            rate.RateToBase = Math.Round(rate.RateToBase / pivot.RateToBase, 10);
            rate.UpdatedOn = rate.UpdatedOn < pivot.UpdatedOn ? rate.UpdatedOn : pivot.UpdatedOn;
        }

        db.ExchangeRates.Remove(pivot);
        db.ExchangeRates.Add(new ExchangeRate
        {
            Currency = oldBase,
            RateToBase = Math.Round(1m / pivot.RateToBase, 10),
            UpdatedOn = pivot.UpdatedOn,
        });
    }
}

public static class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/settings").WithTags("Settings");
        group.MapGet("/", (SettingsService service, CancellationToken ct) => service.GetAsync(ct));
        group.MapPut("/", (SettingsUpdateRequest request, SettingsService service, CancellationToken ct) => service.UpdateAsync(request, ct));
        group.MapPut("/exchange-rates", (ExchangeRatesRequest request, SettingsService service, CancellationToken ct) =>
            service.ReplaceRatesAsync(request, ct));
        return api;
    }
}
