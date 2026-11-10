using System.Text.Json;
using System.Text.Json.Serialization;
using JaCrm.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JaCrm.Api.Data.Configurations;

internal static class Lengths
{
    public const int Name = 200;
    public const int Short = 120;
    public const int Url = 2048;
    public const int Currency = 3;
}

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(Lengths.Name).IsRequired();
        builder.Property(c => c.NormalizedName).HasMaxLength(Lengths.Name).IsRequired();
        builder.Property(c => c.Website).HasMaxLength(Lengths.Url);
        builder.Property(c => c.WebsiteDomain).HasMaxLength(Lengths.Name);
        builder.Property(c => c.CareersUrl).HasMaxLength(Lengths.Url);
        builder.Property(c => c.Industry).HasMaxLength(Lengths.Short);
        builder.Property(c => c.Headquarters).HasMaxLength(Lengths.Name);
        builder.Property(c => c.Country).HasMaxLength(Lengths.Short);
        builder.Property(c => c.City).HasMaxLength(Lengths.Short);

        builder.HasIndex(c => c.NormalizedName);
        builder.HasIndex(c => c.WebsiteDomain);
        builder.HasIndex(c => c.IsArchived);

        builder.ToTable(t => t.HasCheckConstraint("CK_Companies_Rating", "\"Rating\" IS NULL OR (\"Rating\" BETWEEN 1 AND 5)"));
    }
}

internal sealed class VacancyConfiguration : IEntityTypeConfiguration<Vacancy>
{
    public void Configure(EntityTypeBuilder<Vacancy> builder)
    {
        builder.Property(v => v.Title).HasMaxLength(Lengths.Name).IsRequired();
        builder.Property(v => v.Url).HasMaxLength(Lengths.Url);
        builder.Property(v => v.SourceDetail).HasMaxLength(Lengths.Name);
        builder.Property(v => v.Country).HasMaxLength(Lengths.Short);
        builder.Property(v => v.City).HasMaxLength(Lengths.Short);
        builder.Property(v => v.RemoteEligibility).HasMaxLength(Lengths.Name);
        builder.Property(v => v.RelocationNotes).HasMaxLength(1000);
        builder.Property(v => v.RequiredExperienceText).HasMaxLength(1000);

        // A company with vacancies can only be archived, never deleted (D-16).
        builder.HasOne(v => v.Company)
            .WithMany(c => c.Vacancies)
            .HasForeignKey(v => v.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ComplexProperty(v => v.PostedSalary, salary =>
        {
            salary.Property(s => s.Currency).HasMaxLength(Lengths.Currency);
        });

        builder.HasMany(v => v.RequiredTechnologies)
            .WithMany()
            .UsingEntity(
                "VacancyRequiredTechnologies",
                r => r.HasOne(typeof(Technology)).WithMany().HasForeignKey("TechnologyId").OnDelete(DeleteBehavior.Restrict),
                l => l.HasOne(typeof(Vacancy)).WithMany().HasForeignKey("VacancyId").OnDelete(DeleteBehavior.Cascade),
                j => j.HasKey("VacancyId", "TechnologyId"));

        builder.HasMany(v => v.PreferredTechnologies)
            .WithMany()
            .UsingEntity(
                "VacancyPreferredTechnologies",
                r => r.HasOne(typeof(Technology)).WithMany().HasForeignKey("TechnologyId").OnDelete(DeleteBehavior.Restrict),
                l => l.HasOne(typeof(Vacancy)).WithMany().HasForeignKey("VacancyId").OnDelete(DeleteBehavior.Cascade),
                j => j.HasKey("VacancyId", "TechnologyId"));

        builder.HasIndex(v => v.IsArchived);
        builder.HasIndex(v => v.ApplicationDeadline);

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Vacancies_ExperienceYears", "\"RequiredExperienceYears\" IS NULL OR \"RequiredExperienceYears\" BETWEEN 0 AND 60"));
    }
}

internal sealed class ApplicationConfiguration : IEntityTypeConfiguration<Domain.Application>
{
    internal static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public void Configure(EntityTypeBuilder<Domain.Application> builder)
    {
        // One application per vacancy, and a vacancy with an application cannot be deleted.
        builder.HasOne(a => a.Vacancy)
            .WithOne(v => v.Application)
            .HasForeignKey<Domain.Application>(a => a.VacancyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.VacancyId).IsUnique();

        builder.HasMany(a => a.StatusHistory)
            .WithOne(h => h.Application)
            .HasForeignKey(h => h.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(a => a.CvVersion).HasMaxLength(Lengths.Name);
        builder.Property(a => a.CoverLetter).HasMaxLength(4000);

        builder.ComplexProperty(a => a.ExpectedSalary, s => s.Property(x => x.Currency).HasMaxLength(Lengths.Currency));
        builder.ComplexProperty(a => a.OfferedSalary, s => s.Property(x => x.Currency).HasMaxLength(Lengths.Currency));

        builder.Property(a => a.PostingSnapshot)
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, SnapshotJson),
                v => JsonSerializer.Deserialize<PostingSnapshot>(v, SnapshotJson)!,
                new ValueComparer<PostingSnapshot>(
                    (a, b) => ReferenceEquals(a, b) || JsonSerializer.Serialize(a, SnapshotJson) == JsonSerializer.Serialize(b, SnapshotJson),
                    v => JsonSerializer.Serialize(v, SnapshotJson).GetHashCode(StringComparison.Ordinal),
                    v => v));

        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.IsArchived);
        builder.HasIndex(a => a.UpdatedAt);
    }
}

internal sealed class StatusHistoryConfiguration : IEntityTypeConfiguration<ApplicationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ApplicationStatusHistory> builder)
    {
        builder.ToTable("ApplicationStatusHistory");
        builder.Property(h => h.Note).HasMaxLength(2000);
        builder.HasIndex(h => new { h.ApplicationId, h.ChangedAt });
        builder.HasIndex(h => h.ChangedAt);
        builder.HasIndex(h => h.ToStatus);
    }
}

internal sealed class TechnologyConfiguration : IEntityTypeConfiguration<Technology>
{
    public void Configure(EntityTypeBuilder<Technology> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(Lengths.Short).IsRequired();
        builder.Property(t => t.NormalizedName).HasMaxLength(Lengths.Short).IsRequired();
        builder.HasIndex(t => t.NormalizedName).IsUnique();
    }
}

internal sealed class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
{
    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.ToTable("AppSettings", t =>
        {
            t.HasCheckConstraint("CK_AppSettings_Singleton", "\"Id\" = 1");
            t.HasCheckConstraint("CK_AppSettings_Hours", "CAST(\"WorkingHoursPerMonth\" AS REAL) > 0");
            t.HasCheckConstraint("CK_AppSettings_Stale", "\"StaleThresholdDays\" >= 1");
        });
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.BaseCurrency).HasMaxLength(Lengths.Currency).IsRequired();

        // The settings row is configuration, not demo data: it must exist in every database.
        builder.HasData(new AppSettings());
    }
}

internal sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.Property(r => r.Currency).HasMaxLength(Lengths.Currency).IsRequired();
        builder.HasIndex(r => r.Currency).IsUnique();
        builder.ToTable(t => t.HasCheckConstraint("CK_ExchangeRates_Positive", "CAST(\"RateToBase\" AS REAL) > 0"));
    }
}
