using JaCrm.Api.Domain;

namespace JaCrm.Api.Features.DataTransfer;

// The full JSON export. Ids are preserved so links survive a restore (D-11). Bump SchemaVersion on
// any breaking change and keep the importer able to read older versions.

public sealed record ExportDocument(
    int SchemaVersion,
    DateTime ExportedAt,
    ExportSettings Settings,
    IReadOnlyList<ExportExchangeRate> ExchangeRates,
    IReadOnlyList<ExportTechnology> Technologies,
    IReadOnlyList<ExportCompany> Companies,
    IReadOnlyList<ExportVacancy> Vacancies,
    IReadOnlyList<ExportApplication> Applications,
    IReadOnlyList<ExportStatusHistory> StatusHistory)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record ExportSettings(
    ThemePreference Theme,
    string BaseCurrency,
    decimal WorkingHoursPerMonth,
    int StaleThresholdDays,
    DateFormatPreference DateFormat);

public sealed record ExportExchangeRate(string Currency, decimal RateToBase, DateOnly UpdatedOn);

public sealed record ExportTechnology(int Id, string Name);

public sealed record ExportCompany(
    int Id,
    string Name,
    string? Website,
    string? CareersUrl,
    string? Industry,
    string? Headquarters,
    string? Country,
    string? City,
    CompanySize? Size,
    string? Notes,
    int? Rating,
    bool IsArchived,
    DateTime? ArchivedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ExportVacancy(
    int Id,
    int CompanyId,
    string Title,
    Seniority? Seniority,
    string? Description,
    string? Url,
    VacancySource? Source,
    string? SourceDetail,
    string? Country,
    string? City,
    WorkArrangement? WorkArrangement,
    string? RemoteEligibility,
    TriState OpenToMyLocation,
    TriState RelocationSupport,
    string? RelocationNotes,
    EmploymentType? EmploymentType,
    IReadOnlyList<int> RequiredTechnologyIds,
    IReadOnlyList<int> PreferredTechnologyIds,
    int? RequiredExperienceYears,
    string? RequiredExperienceText,
    SalaryRange? PostedSalary,
    DateOnly? DateDiscovered,
    DateOnly? ApplicationDeadline,
    string? Notes,
    bool IsArchived,
    DateTime? ArchivedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ExportApplication(
    int Id,
    int VacancyId,
    ApplicationStatus Status,
    Priority Priority,
    bool IsArchived,
    DateTime? ArchivedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime StatusChangedAt,
    DateOnly? SubmittedOn,
    string? Notes,
    string? ReasonForApplying,
    SalaryValue? ExpectedSalary,
    SalaryValue? OfferedSalary,
    string? CompensationNotes,
    string? CvVersion,
    string? CoverLetter,
    PostingSnapshot PostingSnapshot);

public sealed record ExportStatusHistory(
    int Id,
    int ApplicationId,
    ApplicationStatus? FromStatus,
    ApplicationStatus ToStatus,
    DateTime ChangedAt,
    string? Note);

public sealed record ImportSummary(
    int Companies,
    int Vacancies,
    int Applications,
    int StatusHistory,
    int Technologies,
    int ExchangeRates,
    string BackupFile);
