using JaCrm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.Api.Data;

public static class SalaryContextLoader
{
    /// <summary>Base currency, working hours and exchange rates in one object (two small queries).</summary>
    public static async Task<SalaryContext> LoadSalaryContextAsync(this AppDbContext db, CancellationToken ct)
    {
        var settings = await db.Settings.AsNoTracking().SingleAsync(s => s.Id == AppSettings.SingletonId, ct);
        var rates = await db.ExchangeRates.AsNoTracking().ToDictionaryAsync(r => r.Currency, r => r.RateToBase, ct);
        return new SalaryContext(settings.BaseCurrency, settings.WorkingHoursPerMonth, rates);
    }
}
