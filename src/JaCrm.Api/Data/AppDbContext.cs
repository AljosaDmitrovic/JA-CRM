using JaCrm.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JaCrm.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();

    public DbSet<Vacancy> Vacancies => Set<Vacancy>();

    public DbSet<Domain.Application> Applications => Set<Domain.Application>();

    public DbSet<ApplicationStatusHistory> StatusHistory => Set<ApplicationStatusHistory>();

    public DbSet<Technology> Technologies => Set<Technology>();

    public DbSet<AppSettings> Settings => Set<AppSettings>();

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    public Task<AppSettings> GetSettingsAsync(CancellationToken ct) =>
        Settings.SingleAsync(s => s.Id == AppSettings.SingletonId, ct);

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Timestamps are stored as UTC; SQLite loses DateTimeKind, so restore it on read (D-17).
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

        // Money: decimal everywhere. SQLite stores it as TEXT, so no precision is lost.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);

        // Enums by name (D-12).
        StoreAsString<ApplicationStatus>(configurationBuilder);
        StoreAsString<Priority>(configurationBuilder);
        StoreAsString<CompanySize>(configurationBuilder);
        StoreAsString<Seniority>(configurationBuilder);
        StoreAsString<VacancySource>(configurationBuilder);
        StoreAsString<WorkArrangement>(configurationBuilder);
        StoreAsString<TriState>(configurationBuilder);
        StoreAsString<EmploymentType>(configurationBuilder);
        StoreAsString<PayPeriod>(configurationBuilder);
        StoreAsString<SalaryType>(configurationBuilder);
        StoreAsString<ThemePreference>(configurationBuilder);
        StoreAsString<DateFormatPreference>(configurationBuilder);
    }

    private static void StoreAsString<TEnum>(ModelConfigurationBuilder builder) where TEnum : struct, Enum =>
        builder.Properties<TEnum>().HaveConversion<string>().HaveMaxLength(32);

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
