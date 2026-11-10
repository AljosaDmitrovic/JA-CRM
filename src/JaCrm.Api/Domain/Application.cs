namespace JaCrm.Api.Domain;

/// <summary>My pursuit of one vacancy. The posting itself belongs to <see cref="Vacancy"/>; the
/// application keeps an immutable <see cref="PostingSnapshot"/> taken when it was created.
/// Status only changes through <see cref="ChangeStatus"/>, which also writes the history row.</summary>
public sealed class Application : IArchivable
{
    public int Id { get; set; }

    public int VacancyId { get; set; }

    public Vacancy Vacancy { get; set; } = null!;

    public ApplicationStatus Status { get; private set; }

    public Priority Priority { get; set; } = Priority.Medium;

    public bool IsArchived { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>When the status last changed (or the application was created). Drives staleness.</summary>
    public DateTime StatusChangedAt { get; private set; }

    public DateOnly? SubmittedOn { get; set; }

    /// <summary>Personal notes, Markdown.</summary>
    public string? Notes { get; set; }

    public string? ReasonForApplying { get; set; }

    public SalaryValue? ExpectedSalary { get; set; }

    public SalaryValue? OfferedSalary { get; set; }

    /// <summary>13th salary, bonus, equity, benefits…</summary>
    public string? CompensationNotes { get; set; }

    public string? CvVersion { get; set; }

    public string? CoverLetter { get; set; }

    public PostingSnapshot PostingSnapshot { get; private set; } = null!;

    public List<ApplicationStatusHistory> StatusHistory { get; } = [];

    /// <summary>Creates an application from a vacancy (with its company and technologies loaded),
    /// snapshotting the posting and recording the initial status in the history.</summary>
    public static Application Create(
        Vacancy vacancy, ApplicationStatus initialStatus, Priority priority, DateTime utcNow, DateOnly today)
    {
        var application = new Application
        {
            Vacancy = vacancy,
            VacancyId = vacancy.Id,
            Status = initialStatus,
            Priority = priority,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            StatusChangedAt = utcNow,
            PostingSnapshot = PostingSnapshot.From(vacancy, utcNow),
            SubmittedOn = ApplicationStatuses.ImpliesSubmission(initialStatus) ? today : null,
        };
        application.StatusHistory.Add(new ApplicationStatusHistory
        {
            FromStatus = null,
            ToStatus = initialStatus,
            ChangedAt = utcNow,
        });
        return application;
    }

    /// <summary>Any status may move to any other status. The caller saves once, so the status and
    /// its history row are written in the same transaction.</summary>
    public ApplicationStatusHistory ChangeStatus(ApplicationStatus to, string? note, DateTime utcNow, DateOnly today)
    {
        if (to == Status)
        {
            throw new InvalidOperationException($"The application is already in status {to}.");
        }

        var entry = new ApplicationStatusHistory
        {
            ApplicationId = Id,
            FromStatus = Status,
            ToStatus = to,
            ChangedAt = utcNow,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };

        Status = to;
        StatusChangedAt = utcNow;
        UpdatedAt = utcNow;
        if (SubmittedOn is null && ApplicationStatuses.ImpliesSubmission(to))
        {
            SubmittedOn = today;
        }

        StatusHistory.Add(entry);
        return entry;
    }
}

/// <summary>Immutable copy of the posting at the moment the application was created. Stored as JSON;
/// never edited, so the application keeps its history if the vacancy changes later.</summary>
public sealed record PostingSnapshot
{
    public required string JobTitle { get; init; }

    public required string CompanyName { get; init; }

    public Seniority? Seniority { get; init; }

    public string? Description { get; init; }

    public IReadOnlyList<string> RequiredTechnologies { get; init; } = [];

    public IReadOnlyList<string> PreferredTechnologies { get; init; } = [];

    public int? RequiredExperienceYears { get; init; }

    public string? RequiredExperienceText { get; init; }

    public string? Country { get; init; }

    public string? City { get; init; }

    public WorkArrangement? WorkArrangement { get; init; }

    public string? RemoteEligibility { get; init; }

    public TriState OpenToMyLocation { get; init; }

    public TriState RelocationSupport { get; init; }

    public string? RelocationNotes { get; init; }

    public EmploymentType? EmploymentType { get; init; }

    public SalaryRange? PostedSalary { get; init; }

    public string? Url { get; init; }

    public DateTime CapturedAt { get; init; }

    public static PostingSnapshot From(Vacancy vacancy, DateTime utcNow) => new()
    {
        JobTitle = vacancy.Title,
        CompanyName = vacancy.Company.Name,
        Seniority = vacancy.Seniority,
        Description = vacancy.Description,
        RequiredTechnologies = vacancy.RequiredTechnologies.Select(t => t.Name).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
        PreferredTechnologies = vacancy.PreferredTechnologies.Select(t => t.Name).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
        RequiredExperienceYears = vacancy.RequiredExperienceYears,
        RequiredExperienceText = vacancy.RequiredExperienceText,
        Country = vacancy.Country,
        City = vacancy.City,
        WorkArrangement = vacancy.WorkArrangement,
        RemoteEligibility = vacancy.RemoteEligibility,
        OpenToMyLocation = vacancy.OpenToMyLocation,
        RelocationSupport = vacancy.RelocationSupport,
        RelocationNotes = vacancy.RelocationNotes,
        EmploymentType = vacancy.EmploymentType,
        PostedSalary = vacancy.PostedSalary,
        Url = vacancy.Url,
        CapturedAt = utcNow,
    };
}
