using System.Text.Json.Serialization;
using JaCrm.Api.Domain;

namespace JaCrm.Api.Features.Shared;

// Small DTOs shared by several modules (company page, vacancy page, application page).

public sealed record VacancySummary(
    int Id,
    string Title,
    Seniority? Seniority,
    WorkArrangement? WorkArrangement,
    string? Country,
    string? City,
    DateOnly? DateDiscovered,
    DateOnly? ApplicationDeadline,
    bool IsArchived,
    int? ApplicationId,
    ApplicationStatus? ApplicationStatus);

public sealed record ApplicationSummary(
    int Id,
    int VacancyId,
    string Title,
    ApplicationStatus Status,
    Priority Priority,
    bool IsArchived,
    DateOnly? SubmittedOn,
    DateTime UpdatedAt);

public sealed record StatusHistoryEntry(
    int Id,
    int ApplicationId,
    string JobTitle,
    ApplicationStatus? FromStatus,
    ApplicationStatus ToStatus,
    DateTime ChangedAt,
    string? Note);

/// <summary>Annual amount(s) in the base currency; for a single amount Min == Max.</summary>
public sealed record NormalizedSalaryDto(decimal? AnnualMin, decimal? AnnualMax, string BaseCurrency, Comparability Comparability)
{
    public static NormalizedSalaryDto? From(SalaryValue? value, SalaryContext context)
    {
        if (value is null)
        {
            return null;
        }

        var n = SalaryNormalizer.Normalize(value, context);
        return new NormalizedSalaryDto(n.Annual, n.Annual, n.BaseCurrency, n.Comparability);
    }

    public static NormalizedSalaryDto? From(SalaryRange? range, SalaryContext context)
    {
        if (range is null)
        {
            return null;
        }

        var n = SalaryNormalizer.Normalize(range, context);
        return new NormalizedSalaryDto(n.AnnualMin, n.AnnualMax, n.BaseCurrency, n.Comparability);
    }

    public bool Matches(decimal? filterMin, decimal? filterMax) =>
        SalaryNormalizer.Overlaps(AnnualMin, AnnualMax, Comparability == Comparability.Comparable, filterMin, filterMax);

    /// <summary>Sort key: the midpoint of the range, only for comparable amounts.</summary>
    [JsonIgnore]
    public decimal? SortKey => Comparability == Comparability.Comparable && (AnnualMin ?? AnnualMax) is { } low
        ? (low + (AnnualMax ?? low)) / 2
        : null;
}

/// <summary>A company to create on the fly from the vacancy or application form.</summary>
public sealed record NewCompanyInline(string? Name);
