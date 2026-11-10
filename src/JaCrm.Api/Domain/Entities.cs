namespace JaCrm.Api.Domain;

/// <summary>Companies and vacancies are archived instead of deleted when something references them.</summary>
public interface IArchivable
{
    bool IsArchived { get; set; }

    DateTime? ArchivedAt { get; set; }
}

public static class ArchivableExtensions
{
    public static void Archive(this IArchivable entity, DateTime utcNow)
    {
        if (!entity.IsArchived)
        {
            entity.IsArchived = true;
            entity.ArchivedAt = utcNow;
        }
    }

    public static void Unarchive(this IArchivable entity)
    {
        entity.IsArchived = false;
        entity.ArchivedAt = null;
    }
}

public sealed class Company : IArchivable
{
    public int Id { get; set; }

    public string Name { get; private set; } = "";

    /// <summary>Derived from <see cref="Name"/> for duplicate detection. Set through <see cref="Rename"/>.</summary>
    public string NormalizedName { get; private set; } = "";

    public string? Website { get; private set; }

    /// <summary>Derived from <see cref="Website"/> for duplicate detection.</summary>
    public string? WebsiteDomain { get; private set; }

    public string? CareersUrl { get; set; }

    public string? Industry { get; set; }

    public string? Headquarters { get; set; }

    public string? Country { get; set; }

    public string? City { get; set; }

    public CompanySize? Size { get; set; }

    public string? Notes { get; set; }

    /// <summary>Personal rating, 1–5.</summary>
    public int? Rating { get; set; }

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAt { get; set; }

    /// <summary>"Date added".</summary>
    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<Vacancy> Vacancies { get; } = [];

    public void Rename(string name)
    {
        Name = name.Trim();
        NormalizedName = CompanyMatching.NormalizeName(Name);
    }

    public void SetWebsite(string? website)
    {
        Website = string.IsNullOrWhiteSpace(website) ? null : website.Trim();
        WebsiteDomain = CompanyMatching.NormalizeDomain(Website);
    }
}

public sealed class Vacancy : IArchivable
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string Title { get; set; } = "";

    public Seniority? Seniority { get; set; }

    /// <summary>Markdown, so pasted postings keep their structure.</summary>
    public string? Description { get; set; }

    public string? Url { get; set; }

    public VacancySource? Source { get; set; }

    public string? SourceDetail { get; set; }

    public string? Country { get; set; }

    public string? City { get; set; }

    public WorkArrangement? WorkArrangement { get; set; }

    /// <summary>Where the role may be performed from, e.g. "EU only", "CET ±2h".</summary>
    public string? RemoteEligibility { get; set; }

    public TriState OpenToMyLocation { get; set; }

    public TriState RelocationSupport { get; set; }

    public string? RelocationNotes { get; set; }

    public EmploymentType? EmploymentType { get; set; }

    public int? RequiredExperienceYears { get; set; }

    public string? RequiredExperienceText { get; set; }

    public SalaryRange? PostedSalary { get; set; }

    public DateOnly? DateDiscovered { get; set; }

    public DateOnly? ApplicationDeadline { get; set; }

    public string? Notes { get; set; }

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<Technology> RequiredTechnologies { get; } = [];

    public List<Technology> PreferredTechnologies { get; } = [];

    public Application? Application { get; set; }
}

public sealed class Technology
{
    public int Id { get; set; }

    public string Name { get; private set; } = "";

    public string NormalizedName { get; private set; } = "";

    public static Technology Create(string name) => new() { Name = name.Trim(), NormalizedName = Normalize(name) };

    /// <summary>Case and surrounding whitespace only: "C#" and "C" must stay different tags.</summary>
    public static string Normalize(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}

public sealed class ApplicationStatusHistory
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public Application Application { get; set; } = null!;

    /// <summary>Null for the initial status recorded when the application was created.</summary>
    public ApplicationStatus? FromStatus { get; set; }

    public ApplicationStatus ToStatus { get; set; }

    public DateTime ChangedAt { get; set; }

    public string? Note { get; set; }
}

/// <summary>Single row (Id = 1).</summary>
public sealed class AppSettings
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public string BaseCurrency { get; set; } = "EUR";

    public decimal WorkingHoursPerMonth { get; set; } = 168m;

    public int StaleThresholdDays { get; set; } = 14;

    public DateFormatPreference DateFormat { get; set; } = DateFormatPreference.DayMonthYear;
}

/// <summary>1 unit of <see cref="Currency"/> = <see cref="RateToBase"/> units of the base currency.</summary>
public sealed class ExchangeRate
{
    public int Id { get; set; }

    public string Currency { get; set; } = "";

    public decimal RateToBase { get; set; }

    public DateOnly UpdatedOn { get; set; }
}
