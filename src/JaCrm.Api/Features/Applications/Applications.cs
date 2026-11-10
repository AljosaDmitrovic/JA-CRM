using JaCrm.Api.Common;
using JaCrm.Api.Data;
using JaCrm.Api.Data.Configurations;
using JaCrm.Api.Domain;
using JaCrm.Api.Features.Companies;
using JaCrm.Api.Features.Shared;
using JaCrm.Api.Features.Vacancies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.Api.Features.Applications;

public sealed record ApplicationQuery(
    string? Search = null,
    ArchivedFilter? Archived = null,
    ApplicationStatus[]? Status = null,
    ApplicationPhase[]? Phase = null,
    int? CompanyId = null,
    string? Country = null,
    WorkArrangement? WorkArrangement = null,
    TriState? OpenToMyLocation = null,
    Seniority? Seniority = null,
    EmploymentType? EmploymentType = null,
    string[]? Tech = null,
    string? DateField = null,
    DateOnly? From = null,
    DateOnly? To = null,
    decimal? SalaryMin = null,
    decimal? SalaryMax = null,
    bool? Stale = null,
    string? Sort = null,
    string? Dir = null,
    int? Page = null,
    int? PageSize = null);

public enum SalarySource
{
    Offered,
    Posted,
}

public sealed record ApplicationListItem(
    int Id,
    int VacancyId,
    int CompanyId,
    string CompanyName,
    string Title,
    ApplicationStatus Status,
    ApplicationPhase Phase,
    Priority Priority,
    bool IsArchived,
    bool IsStale,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime StatusChangedAt,
    DateOnly? SubmittedOn,
    string? Country,
    string? City,
    WorkArrangement? WorkArrangement,
    Seniority? Seniority,
    EmploymentType? EmploymentType,
    SalarySource? SalarySource,
    SalaryRange? Salary,
    NormalizedSalaryDto? NormalizedSalary);

public sealed record ApplicationDetail(
    int Id,
    int VacancyId,
    string VacancyTitle,
    bool VacancyIsArchived,
    int CompanyId,
    string CompanyName,
    ApplicationStatus Status,
    ApplicationPhase Phase,
    Priority Priority,
    bool IsArchived,
    DateTime? ArchivedAt,
    bool IsStale,
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
    NormalizedSalaryDto? ExpectedNormalized,
    NormalizedSalaryDto? OfferedNormalized,
    NormalizedSalaryDto? PostedNormalized,
    PostingSnapshot Snapshot,
    IReadOnlyList<StatusHistoryEntry> History);

public sealed record ApplicationUpdateRequest(
    Priority? Priority,
    DateOnly? SubmittedOn,
    string? Notes,
    string? ReasonForApplying,
    SalaryValueInput? ExpectedSalary,
    SalaryValueInput? OfferedSalary,
    string? CompensationNotes,
    string? CvVersion,
    string? CoverLetter);

/// <summary>The minimum to capture a vacancy while creating an application directly.</summary>
public sealed record NewVacancyInline(string? Title, int? CompanyId, NewCompanyInline? NewCompany, string? Url);

/// <summary>Either an existing vacancy, or a new one created inline.</summary>
public sealed record CreateApplicationRequest(int? VacancyId, NewVacancyInline? NewVacancy, ApplicationStatus? Status, Priority? Priority);

public sealed record ConvertRequest(ApplicationStatus? Status, Priority? Priority);

public sealed record StatusChangeRequest(ApplicationStatus? Status, string? Note);

public sealed record CreatedId(int Id);

public sealed class ApplicationService(AppDbContext db, Clock clock, CompanyService companies, VacancyService vacancies)
{
    public const int MaxNotesLength = 100_000;
    public const int MaxTextLength = 4000;

    public async Task<PagedResult<ApplicationListItem>> ListAsync(ApplicationQuery query, CancellationToken ct)
    {
        var items = await LoadItemsAsync(query, ct);
        var desc = Paging.Descending(query.Dir);
        var sorted = query.Sort switch
        {
            "title" => items.OrderText(a => a.Title, desc),
            "company" => items.OrderText(a => a.CompanyName, desc),
            "status" => desc ? items.OrderByDescending(a => a.Status) : items.OrderBy(a => a.Status),
            "priority" => desc ? items.OrderByDescending(a => a.Priority).ThenByDescending(a => a.UpdatedAt) : items.OrderBy(a => a.Priority).ThenByDescending(a => a.UpdatedAt),
            "created" => desc ? items.OrderByDescending(a => a.CreatedAt) : items.OrderBy(a => a.CreatedAt),
            "submitted" => items.OrderNullsLast(a => a.SubmittedOn, desc),
            "salary" => items.OrderNullsLast(a => a.NormalizedSalary?.SortKey, desc),
            _ => desc || query.Dir is null ? items.OrderByDescending(a => a.UpdatedAt) : items.OrderBy(a => a.UpdatedAt),
        };
        return Paging.Page(sorted.ToList(), query.Page, query.PageSize);
    }

    /// <summary>Every matching application for the Kanban board (archived hidden unless asked),
    /// ordered by priority, then last updated. No manual card order is stored.</summary>
    public async Task<IReadOnlyList<ApplicationListItem>> BoardAsync(ApplicationQuery query, CancellationToken ct)
    {
        var items = await LoadItemsAsync(query, ct);
        return items.OrderByDescending(a => a.Priority).ThenByDescending(a => a.UpdatedAt).ToList();
    }

    public async Task<ApplicationDetail> GetAsync(int id, CancellationToken ct)
    {
        var a = await db.Applications.AsNoTracking()
            .Include(x => x.Vacancy).ThenInclude(v => v.Company)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Application", id);

        var history = await db.StatusHistory.AsNoTracking()
            .Where(h => h.ApplicationId == id)
            .OrderByDescending(h => h.ChangedAt).ThenByDescending(h => h.Id)
            .Select(h => new StatusHistoryEntry(h.Id, h.ApplicationId, a.Vacancy.Title, h.FromStatus, h.ToStatus, h.ChangedAt, h.Note))
            .ToListAsync(ct);

        var settings = await db.Settings.AsNoTracking().SingleAsync(ct);
        var salary = await db.LoadSalaryContextAsync(ct);
        return new ApplicationDetail(
            a.Id, a.VacancyId, a.Vacancy.Title, a.Vacancy.IsArchived, a.Vacancy.CompanyId, a.Vacancy.Company.Name,
            a.Status, ApplicationStatuses.PhaseOf(a.Status), a.Priority, a.IsArchived, a.ArchivedAt,
            IsStale(a.Status, a.IsArchived, a.StatusChangedAt, settings.StaleThresholdDays),
            a.CreatedAt, a.UpdatedAt, a.StatusChangedAt, a.SubmittedOn, a.Notes, a.ReasonForApplying,
            a.ExpectedSalary, a.OfferedSalary, a.CompensationNotes, a.CvVersion, a.CoverLetter,
            NormalizedSalaryDto.From(a.ExpectedSalary, salary),
            NormalizedSalaryDto.From(a.OfferedSalary, salary),
            NormalizedSalaryDto.From(a.PostingSnapshot.PostedSalary, salary),
            a.PostingSnapshot, history);
    }

    /// <summary>Creates an application from an existing vacancy ("Convert to application") or from a
    /// vacancy (and company) captured inline. Everything is written in one transaction.</summary>
    public async Task<CreatedId> CreateAsync(CreateApplicationRequest request, CancellationToken ct)
    {
        new Validator()
            .Defined("status", request.Status, "Status")
            .Defined("priority", request.Priority, "Priority")
            .ThrowIfInvalid();

        Vacancy vacancy;
        if (request.VacancyId is { } vacancyId)
        {
            vacancy = await vacancies.LoadForSnapshotAsync(vacancyId, ct);
            if (vacancy.Application is not null)
            {
                throw new ConflictException("This vacancy already has an application.");
            }
        }
        else if (request.NewVacancy is { } inline)
        {
            vacancy = await NewInlineVacancyAsync(inline, ct);
        }
        else
        {
            throw ValidationException.For("vacancyId", "Choose a vacancy or enter a new one.");
        }

        var application = Domain.Application.Create(
            vacancy, request.Status ?? ApplicationStatus.Interested, request.Priority ?? Priority.Medium, clock.UtcNow, clock.Today);
        db.Applications.Add(application);
        await db.SaveChangesAsync(ct);
        return new CreatedId(application.Id);
    }

    public async Task<ApplicationDetail> UpdateAsync(int id, ApplicationUpdateRequest r, CancellationToken ct)
    {
        var validator = new Validator()
            .Defined("priority", r.Priority, "Priority")
            .MaxLength("notes", r.Notes, MaxNotesLength, "Notes")
            .MaxLength("reasonForApplying", r.ReasonForApplying, MaxTextLength, "Reason for applying")
            .MaxLength("compensationNotes", r.CompensationNotes, MaxTextLength, "Compensation notes")
            .MaxLength("cvVersion", r.CvVersion, Lengths.Name, "CV version")
            .MaxLength("coverLetter", r.CoverLetter, MaxTextLength, "Cover letter");
        if (r.SubmittedOn is { } submitted && submitted > clock.Today)
        {
            validator.Add("submittedOn", "The submission date cannot be in the future.");
        }

        var expected = validator.ToValue(r.ExpectedSalary, "expectedSalary");
        var offered = validator.ToValue(r.OfferedSalary, "offeredSalary");
        validator.ThrowIfInvalid();

        var application = await FindAsync(id, ct);
        application.Priority = r.Priority ?? application.Priority;
        application.SubmittedOn = r.SubmittedOn;
        application.Notes = Validator.Clean(r.Notes);
        application.ReasonForApplying = Validator.Clean(r.ReasonForApplying);
        application.ExpectedSalary = expected;
        application.OfferedSalary = offered;
        application.CompensationNotes = Validator.Clean(r.CompensationNotes);
        application.CvVersion = Validator.Clean(r.CvVersion);
        application.CoverLetter = Validator.Clean(r.CoverLetter);
        application.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Moves the application to any other status. The status and its history row are saved
    /// together in one <c>SaveChanges</c>, so they commit in one transaction.</summary>
    public async Task<ApplicationListItem> ChangeStatusAsync(int id, StatusChangeRequest request, CancellationToken ct)
    {
        var validator = new Validator().MaxLength("note", request.Note, 2000, "Note");
        if (request.Status is not { } status || !Enum.IsDefined(status))
        {
            validator.Add("status", "Choose a valid status.");
        }

        validator.ThrowIfInvalid();

        var application = await FindAsync(id, ct);
        if (application.Status == request.Status)
        {
            throw ValidationException.For("status", "The application already has this status.");
        }

        application.ChangeStatus(request.Status!.Value, request.Note, clock.UtcNow, clock.Today);
        await db.SaveChangesAsync(ct);
        return (await LoadItemsAsync(new ApplicationQuery(Archived: ArchivedFilter.All), ct, onlyId: id)).Single();
    }

    public async Task SetArchivedAsync(int id, bool archived, CancellationToken ct)
    {
        var application = await FindAsync(id, ct);
        if (archived)
        {
            application.Archive(clock.UtcNow);
        }
        else
        {
            application.Unarchive();
        }

        application.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Deletes the application and its status history (decision D-16). The vacancy stays.</summary>
    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var application = await FindAsync(id, ct);
        db.Applications.Remove(application);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Vacancy> NewInlineVacancyAsync(NewVacancyInline inline, CancellationToken ct)
    {
        var url = Text.NormalizeUrl(inline.Url);
        new Validator()
            .Required("newVacancy.title", inline.Title, "Job title")
            .MaxLength("newVacancy.title", inline.Title, Lengths.Name, "Job title")
            .HttpUrl("newVacancy.url", url, "Vacancy URL")
            .MaxLength("newVacancy.url", url, Lengths.Url, "Vacancy URL")
            .ThrowIfInvalid();

        var company = await companies.ResolveAsync(inline.CompanyId, inline.NewCompany, "newVacancy.", ct);
        var now = clock.UtcNow;
        var vacancy = new Vacancy
        {
            Company = company,
            Title = inline.Title!.Trim(),
            Url = url,
            DateDiscovered = clock.Today,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Vacancies.Add(vacancy);
        return vacancy;
    }

    private async Task<Domain.Application> FindAsync(int id, CancellationToken ct) =>
        await db.Applications.SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("Application", id);

    private bool IsStale(ApplicationStatus status, bool archived, DateTime statusChangedAt, int thresholdDays) =>
        DashboardCalculator.IsStale(new ApplicationFacts(0, status, archived, statusChangedAt), clock.UtcNow, thresholdDays);

    /// <summary>Filters in SQL, then normalizes salaries and staleness in memory (they depend on
    /// settings and exchange rates). Lists are personal-scale, so this stays fast (decision D-45).</summary>
    private async Task<List<ApplicationListItem>> LoadItemsAsync(ApplicationQuery query, CancellationToken ct, int? onlyId = null)
    {
        var apps = Filter(db.Applications.AsNoTracking(), query);
        if (onlyId is { } id)
        {
            apps = apps.Where(a => a.Id == id);
        }

        var rows = await apps.Select(a => new
        {
            a.Id, a.VacancyId, a.Vacancy.CompanyId, CompanyName = a.Vacancy.Company.Name, a.Vacancy.Title, a.Status, a.Priority,
            a.IsArchived, a.CreatedAt, a.UpdatedAt, a.StatusChangedAt, a.SubmittedOn, a.Vacancy.Country, a.Vacancy.City,
            a.Vacancy.WorkArrangement, a.Vacancy.Seniority, a.Vacancy.EmploymentType, a.OfferedSalary, a.PostingSnapshot,
        }).ToListAsync(ct);

        var staleDays = (await db.Settings.AsNoTracking().SingleAsync(ct)).StaleThresholdDays;
        var salary = await db.LoadSalaryContextAsync(ct);
        var items = rows.Select(a =>
        {
            // Decision D-26: the offered salary represents the opportunity once known, otherwise the
            // range that was posted when I applied.
            var (source, range) = a.OfferedSalary is { } offered
                ? (SalarySource.Offered, new SalaryRange(offered.Amount, offered.Amount, offered.Currency, offered.Period, offered.Type))
                : a.PostingSnapshot.PostedSalary is { } posted ? (SalarySource.Posted, posted) : ((SalarySource?)null, (SalaryRange?)null);
            return new ApplicationListItem(
                a.Id, a.VacancyId, a.CompanyId, a.CompanyName, a.Title, a.Status, ApplicationStatuses.PhaseOf(a.Status), a.Priority,
                a.IsArchived, IsStale(a.Status, a.IsArchived, a.StatusChangedAt, staleDays), a.CreatedAt, a.UpdatedAt,
                a.StatusChangedAt, a.SubmittedOn, a.Country, a.City, a.WorkArrangement, a.Seniority, a.EmploymentType,
                source, range, NormalizedSalaryDto.From(range, salary));
        });

        if (query.SalaryMin is not null || query.SalaryMax is not null)
        {
            items = items.Where(a => a.NormalizedSalary?.Matches(query.SalaryMin, query.SalaryMax) ?? false);
        }

        if (query.Stale is { } stale)
        {
            items = items.Where(a => a.IsStale == stale);
        }

        return items.ToList();
    }

    private IQueryable<Domain.Application> Filter(IQueryable<Domain.Application> apps, ApplicationQuery query)
    {
        apps = (query.Archived ?? ArchivedFilter.Active) switch
        {
            ArchivedFilter.Active => apps.Where(a => !a.IsArchived),
            ArchivedFilter.Archived => apps.Where(a => a.IsArchived),
            _ => apps,
        };

        if (Text.ContainsPattern(query.Search) is { } p)
        {
            apps = apps.Where(a =>
                EF.Functions.Like(a.Vacancy.Title, p, Text.LikeEscape)
                || EF.Functions.Like(a.Vacancy.Company.Name, p, Text.LikeEscape)
                || EF.Functions.Like(a.Vacancy.Country, p, Text.LikeEscape)
                || EF.Functions.Like(a.Vacancy.City, p, Text.LikeEscape)
                || EF.Functions.Like(a.Vacancy.RemoteEligibility, p, Text.LikeEscape)
                || EF.Functions.Like(a.Notes, p, Text.LikeEscape)
                || EF.Functions.Like(a.ReasonForApplying, p, Text.LikeEscape)
                || EF.Functions.Like(a.CompensationNotes, p, Text.LikeEscape)
                || EF.Functions.Like(a.Vacancy.Notes, p, Text.LikeEscape)
                || a.Vacancy.RequiredTechnologies.Any(t => EF.Functions.Like(t.Name, p, Text.LikeEscape))
                || a.Vacancy.PreferredTechnologies.Any(t => EF.Functions.Like(t.Name, p, Text.LikeEscape)));
        }

        var statuses = (query.Status ?? []).ToHashSet();
        foreach (var phase in query.Phase ?? [])
        {
            statuses.UnionWith(ApplicationStatuses.InPhase(phase));
        }

        if (statuses.Count > 0)
        {
            var list = statuses.ToList();
            apps = apps.Where(a => list.Contains(a.Status));
        }

        if (query.CompanyId is { } companyId)
        {
            apps = apps.Where(a => a.Vacancy.CompanyId == companyId);
        }

        if (Validator.Clean(query.Country) is { } country)
        {
            apps = apps.Where(a => a.Vacancy.Country == country);
        }

        if (query.WorkArrangement is { } arrangement)
        {
            apps = apps.Where(a => a.Vacancy.WorkArrangement == arrangement);
        }

        if (query.OpenToMyLocation is { } open)
        {
            apps = apps.Where(a => a.Vacancy.OpenToMyLocation == open);
        }

        if (query.Seniority is { } seniority)
        {
            apps = apps.Where(a => a.Vacancy.Seniority == seniority);
        }

        if (query.EmploymentType is { } employment)
        {
            apps = apps.Where(a => a.Vacancy.EmploymentType == employment);
        }

        foreach (var key in (query.Tech ?? []).Select(Validator.Clean).OfType<string>().Select(Technology.Normalize).Distinct())
        {
            apps = apps.Where(a =>
                a.Vacancy.RequiredTechnologies.Any(t => t.NormalizedName == key)
                || a.Vacancy.PreferredTechnologies.Any(t => t.NormalizedName == key));
        }

        if (query.DateField == "submitted")
        {
            apps = apps
                .Where(a => query.From == null || a.SubmittedOn >= query.From)
                .Where(a => query.To == null || a.SubmittedOn <= query.To);
        }
        else
        {
            // Timestamps: compare against local-day boundaries in the owner's time zone.
            DateTime? from = query.From is { } f ? clock.StartOfDayUtc(f) : null;
            DateTime? to = query.To is { } t ? clock.StartOfDayUtc(t.AddDays(1)) : null;
            apps = query.DateField == "updated"
                ? apps.Where(a => (from == null || a.UpdatedAt >= from) && (to == null || a.UpdatedAt < to))
                : apps.Where(a => (from == null || a.CreatedAt >= from) && (to == null || a.CreatedAt < to));
        }

        return apps;
    }
}

public static class ApplicationEndpoints
{
    public static RouteGroupBuilder MapApplicationEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/applications").WithTags("Applications");

        group.MapGet("/", ([AsParameters] ApplicationQuery query, ApplicationService service, CancellationToken ct) =>
            service.ListAsync(query, ct));
        group.MapGet("/board", ([AsParameters] ApplicationQuery query, ApplicationService service, CancellationToken ct) =>
            service.BoardAsync(query, ct));
        group.MapGet("/{id:int}", (int id, ApplicationService service, CancellationToken ct) => service.GetAsync(id, ct));
        group.MapPost("/", async Task<Created<CreatedId>> (CreateApplicationRequest request, ApplicationService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return TypedResults.Created($"/api/applications/{created.Id}", created);
        });
        group.MapPut("/{id:int}", (int id, ApplicationUpdateRequest request, ApplicationService service, CancellationToken ct) =>
            service.UpdateAsync(id, request, ct));
        group.MapPost("/{id:int}/status", (int id, StatusChangeRequest request, ApplicationService service, CancellationToken ct) =>
            service.ChangeStatusAsync(id, request, ct));
        group.MapPost("/{id:int}/archive", async (int id, ApplicationService service, CancellationToken ct) =>
        {
            await service.SetArchivedAsync(id, true, ct);
            return TypedResults.NoContent();
        });
        group.MapPost("/{id:int}/unarchive", async (int id, ApplicationService service, CancellationToken ct) =>
        {
            await service.SetArchivedAsync(id, false, ct);
            return TypedResults.NoContent();
        });
        group.MapDelete("/{id:int}", async (int id, ApplicationService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        return api;
    }
}
