using JaCrm.Api.Common;
using JaCrm.Api.Data;
using JaCrm.Api.Data.Configurations;
using JaCrm.Api.Domain;
using JaCrm.Api.Features.Applications;
using JaCrm.Api.Features.Companies;
using JaCrm.Api.Features.Shared;
using JaCrm.Api.Features.Technologies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.Api.Features.Vacancies;

public sealed record VacancyQuery(
    string? Search,
    ArchivedFilter? Archived,
    int? CompanyId,
    string? Country,
    WorkArrangement? WorkArrangement,
    TriState? OpenToMyLocation,
    Seniority? Seniority,
    EmploymentType? EmploymentType,
    string[]? Tech,
    string? DateField,
    DateOnly? From,
    DateOnly? To,
    decimal? SalaryMin,
    decimal? SalaryMax,
    bool? HasApplication,
    string? Sort,
    string? Dir,
    int? Page,
    int? PageSize);

public sealed record VacancyListItem(
    int Id,
    string Title,
    int CompanyId,
    string CompanyName,
    Seniority? Seniority,
    WorkArrangement? WorkArrangement,
    EmploymentType? EmploymentType,
    string? Country,
    string? City,
    TriState OpenToMyLocation,
    TriState RelocationSupport,
    IReadOnlyList<string> RequiredTechnologies,
    SalaryRange? PostedSalary,
    NormalizedSalaryDto? NormalizedSalary,
    DateOnly? DateDiscovered,
    DateOnly? ApplicationDeadline,
    bool IsArchived,
    int? ApplicationId,
    ApplicationStatus? ApplicationStatus,
    DateTime CreatedAt);

public sealed record VacancyRequest(
    string? Title,
    int? CompanyId,
    NewCompanyInline? NewCompany,
    Seniority? Seniority,
    string? Description,
    string? Url,
    VacancySource? Source,
    string? SourceDetail,
    string? Country,
    string? City,
    WorkArrangement? WorkArrangement,
    string? RemoteEligibility,
    TriState? OpenToMyLocation,
    TriState? RelocationSupport,
    string? RelocationNotes,
    EmploymentType? EmploymentType,
    IReadOnlyList<string>? RequiredTechnologies,
    IReadOnlyList<string>? PreferredTechnologies,
    int? RequiredExperienceYears,
    string? RequiredExperienceText,
    SalaryRangeInput? PostedSalary,
    DateOnly? DateDiscovered,
    DateOnly? ApplicationDeadline,
    string? Notes);

public sealed record VacancyDetail(
    int Id,
    string Title,
    int CompanyId,
    string CompanyName,
    bool CompanyIsArchived,
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
    IReadOnlyList<string> RequiredTechnologies,
    IReadOnlyList<string> PreferredTechnologies,
    int? RequiredExperienceYears,
    string? RequiredExperienceText,
    SalaryRange? PostedSalary,
    NormalizedSalaryDto? NormalizedSalary,
    DateOnly? DateDiscovered,
    DateOnly? ApplicationDeadline,
    string? Notes,
    bool IsArchived,
    DateTime? ArchivedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? ApplicationId,
    ApplicationStatus? ApplicationStatus);

public sealed class VacancyService(AppDbContext db, Clock clock, TechnologyResolver technologies, CompanyService companies)
{
    public async Task<PagedResult<VacancyListItem>> ListAsync(VacancyQuery query, CancellationToken ct)
    {
        var vacancies = db.Vacancies.AsNoTracking();
        vacancies = (query.Archived ?? ArchivedFilter.Active) switch
        {
            ArchivedFilter.Active => vacancies.Where(v => !v.IsArchived),
            ArchivedFilter.Archived => vacancies.Where(v => v.IsArchived),
            _ => vacancies,
        };

        if (Text.ContainsPattern(query.Search) is { } p)
        {
            vacancies = vacancies.Where(v =>
                EF.Functions.Like(v.Title, p, Text.LikeEscape)
                || EF.Functions.Like(v.Company.Name, p, Text.LikeEscape)
                || EF.Functions.Like(v.Country, p, Text.LikeEscape)
                || EF.Functions.Like(v.City, p, Text.LikeEscape)
                || EF.Functions.Like(v.RemoteEligibility, p, Text.LikeEscape)
                || EF.Functions.Like(v.Notes, p, Text.LikeEscape)
                || EF.Functions.Like(v.Description, p, Text.LikeEscape)
                || v.RequiredTechnologies.Any(t => EF.Functions.Like(t.Name, p, Text.LikeEscape))
                || v.PreferredTechnologies.Any(t => EF.Functions.Like(t.Name, p, Text.LikeEscape)));
        }

        if (query.CompanyId is { } companyId)
        {
            vacancies = vacancies.Where(v => v.CompanyId == companyId);
        }

        if (Validator.Clean(query.Country) is { } country)
        {
            vacancies = vacancies.Where(v => v.Country == country);
        }

        if (query.WorkArrangement is { } arrangement)
        {
            vacancies = vacancies.Where(v => v.WorkArrangement == arrangement);
        }

        if (query.OpenToMyLocation is { } open)
        {
            vacancies = vacancies.Where(v => v.OpenToMyLocation == open);
        }

        if (query.Seniority is { } seniority)
        {
            vacancies = vacancies.Where(v => v.Seniority == seniority);
        }

        if (query.EmploymentType is { } employment)
        {
            vacancies = vacancies.Where(v => v.EmploymentType == employment);
        }

        if (query.HasApplication is { } hasApplication)
        {
            vacancies = vacancies.Where(v => (v.Application != null) == hasApplication);
        }

        // Every selected tag must be present (required or preferred).
        foreach (var key in (query.Tech ?? []).Select(Validator.Clean).OfType<string>().Select(Technology.Normalize).Distinct())
        {
            vacancies = vacancies.Where(v =>
                v.RequiredTechnologies.Any(t => t.NormalizedName == key) || v.PreferredTechnologies.Any(t => t.NormalizedName == key));
        }

        vacancies = query.DateField switch
        {
            "deadline" => vacancies
                .Where(v => query.From == null || v.ApplicationDeadline >= query.From)
                .Where(v => query.To == null || v.ApplicationDeadline <= query.To),
            _ => vacancies
                .Where(v => query.From == null || v.DateDiscovered >= query.From)
                .Where(v => query.To == null || v.DateDiscovered <= query.To),
        };

        var rows = await vacancies.Select(v => new
        {
            v.Id, v.Title, v.CompanyId, CompanyName = v.Company.Name, v.Seniority, v.WorkArrangement, v.EmploymentType,
            v.Country, v.City, v.OpenToMyLocation, v.RelocationSupport,
            RequiredTechnologies = v.RequiredTechnologies.Select(t => t.Name).ToList(),
            v.PostedSalary, v.DateDiscovered, v.ApplicationDeadline, v.IsArchived,
            ApplicationId = v.Application != null ? (int?)v.Application.Id : null,
            ApplicationStatus = v.Application != null ? (ApplicationStatus?)v.Application.Status : null,
            v.CreatedAt,
        }).ToListAsync(ct);

        var salary = await db.LoadSalaryContextAsync(ct);
        var items = rows.Select(v => new VacancyListItem(
                v.Id, v.Title, v.CompanyId, v.CompanyName, v.Seniority, v.WorkArrangement, v.EmploymentType, v.Country, v.City,
                v.OpenToMyLocation, v.RelocationSupport, v.RequiredTechnologies.Order(StringComparer.OrdinalIgnoreCase).ToList(),
                v.PostedSalary, NormalizedSalaryDto.From(v.PostedSalary, salary), v.DateDiscovered, v.ApplicationDeadline,
                v.IsArchived, v.ApplicationId, v.ApplicationStatus, v.CreatedAt))
            .Where(v => (query.SalaryMin is null && query.SalaryMax is null)
                || (v.NormalizedSalary?.Matches(query.SalaryMin, query.SalaryMax) ?? false));

        var desc = Paging.Descending(query.Dir);
        var sorted = query.Sort switch
        {
            "title" => items.OrderText(v => v.Title, desc),
            "company" => items.OrderText(v => v.CompanyName, desc),
            "discovered" => items.OrderNullsLast(v => v.DateDiscovered, desc),
            "deadline" => items.OrderNullsLast(v => v.ApplicationDeadline, desc),
            "salary" => items.OrderNullsLast(v => v.NormalizedSalary?.SortKey, desc),
            _ => desc || query.Dir is null ? items.OrderByDescending(v => v.CreatedAt) : items.OrderBy(v => v.CreatedAt),
        };
        return Paging.Page(sorted.ToList(), query.Page, query.PageSize);
    }

    public async Task<VacancyDetail> GetAsync(int id, CancellationToken ct)
    {
        var v = await db.Vacancies.AsNoTracking()
            .Include(x => x.Company)
            .Include(x => x.RequiredTechnologies)
            .Include(x => x.PreferredTechnologies)
            .Include(x => x.Application)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Vacancy", id);

        var salary = await db.LoadSalaryContextAsync(ct);
        return new VacancyDetail(
            v.Id, v.Title, v.CompanyId, v.Company.Name, v.Company.IsArchived, v.Seniority, v.Description, v.Url, v.Source,
            v.SourceDetail, v.Country, v.City, v.WorkArrangement, v.RemoteEligibility, v.OpenToMyLocation, v.RelocationSupport,
            v.RelocationNotes, v.EmploymentType,
            v.RequiredTechnologies.Select(t => t.Name).Order(StringComparer.OrdinalIgnoreCase).ToList(),
            v.PreferredTechnologies.Select(t => t.Name).Order(StringComparer.OrdinalIgnoreCase).ToList(),
            v.RequiredExperienceYears, v.RequiredExperienceText, v.PostedSalary, NormalizedSalaryDto.From(v.PostedSalary, salary),
            v.DateDiscovered, v.ApplicationDeadline, v.Notes, v.IsArchived, v.ArchivedAt, v.CreatedAt, v.UpdatedAt,
            v.Application?.Id, v.Application?.Status);
    }

    public async Task<VacancyDetail> CreateAsync(VacancyRequest request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var vacancy = new Vacancy { CreatedAt = now, UpdatedAt = now, DateDiscovered = request.DateDiscovered ?? clock.Today };
        await ApplyAsync(vacancy, request, ct);
        db.Vacancies.Add(vacancy);
        await db.SaveChangesAsync(ct);
        return await GetAsync(vacancy.Id, ct);
    }

    public async Task<VacancyDetail> UpdateAsync(int id, VacancyRequest request, CancellationToken ct)
    {
        var vacancy = await db.Vacancies
            .Include(v => v.RequiredTechnologies)
            .Include(v => v.PreferredTechnologies)
            .AsSplitQuery()
            .SingleOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException("Vacancy", id);
        await ApplyAsync(vacancy, request, ct);
        vacancy.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task SetArchivedAsync(int id, bool archived, CancellationToken ct)
    {
        var vacancy = await db.Vacancies.SingleOrDefaultAsync(v => v.Id == id, ct) ?? throw new NotFoundException("Vacancy", id);
        if (archived)
        {
            vacancy.Archive(clock.UtcNow);
        }
        else
        {
            vacancy.Unarchive();
        }

        vacancy.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Hard delete only without an application; otherwise archive it (409).</summary>
    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var vacancy = await db.Vacancies.Include(v => v.Application).SingleOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException("Vacancy", id);
        if (vacancy.Application is not null)
        {
            throw new ConflictException("This vacancy has an application, so it can only be archived, not deleted.");
        }

        db.Vacancies.Remove(vacancy);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Loads a vacancy with everything the posting snapshot needs.</summary>
    public async Task<Vacancy> LoadForSnapshotAsync(int id, CancellationToken ct) =>
        await db.Vacancies
            .Include(v => v.Company)
            .Include(v => v.RequiredTechnologies)
            .Include(v => v.PreferredTechnologies)
            .Include(v => v.Application)
            .AsSplitQuery()
            .SingleOrDefaultAsync(v => v.Id == id, ct)
        ?? throw new NotFoundException("Vacancy", id);

    private async Task ApplyAsync(Vacancy vacancy, VacancyRequest r, CancellationToken ct)
    {
        var url = Text.NormalizeUrl(r.Url);
        var validator = new Validator()
            .Required("title", r.Title, "Job title")
            .MaxLength("title", r.Title, Lengths.Name, "Job title")
            .HttpUrl("url", url, "Vacancy URL")
            .MaxLength("url", url, Lengths.Url, "Vacancy URL")
            .MaxLength("sourceDetail", r.SourceDetail, Lengths.Name, "Source detail")
            .MaxLength("country", r.Country, Lengths.Short, "Country")
            .MaxLength("city", r.City, Lengths.Short, "City")
            .MaxLength("remoteEligibility", r.RemoteEligibility, Lengths.Name, "Remote eligibility")
            .MaxLength("relocationNotes", r.RelocationNotes, 1000, "Relocation notes")
            .MaxLength("requiredExperienceText", r.RequiredExperienceText, 1000, "Experience")
            .Range("requiredExperienceYears", r.RequiredExperienceYears, 0, 60, "Years of experience")
            .Defined("seniority", r.Seniority, "Seniority")
            .Defined("source", r.Source, "Source")
            .Defined("workArrangement", r.WorkArrangement, "Work arrangement")
            .Defined("openToMyLocation", r.OpenToMyLocation, "Open to my location")
            .Defined("relocationSupport", r.RelocationSupport, "Relocation support")
            .Defined("employmentType", r.EmploymentType, "Employment type");
        var postedSalary = validator.ToRange(r.PostedSalary, "postedSalary");
        var (required, preferred) = await technologies.ResolveAsync(r.RequiredTechnologies, r.PreferredTechnologies, validator, ct);

        var company = await companies.ResolveAsync(r.CompanyId, r.NewCompany, "", ct);

        vacancy.Company = company;
        vacancy.Title = r.Title!.Trim();
        vacancy.Seniority = r.Seniority;
        vacancy.Description = Validator.Clean(r.Description);
        vacancy.Url = url;
        vacancy.Source = r.Source;
        vacancy.SourceDetail = Validator.Clean(r.SourceDetail);
        vacancy.Country = Validator.Clean(r.Country);
        vacancy.City = Validator.Clean(r.City);
        vacancy.WorkArrangement = r.WorkArrangement;
        vacancy.RemoteEligibility = Validator.Clean(r.RemoteEligibility);
        vacancy.OpenToMyLocation = r.OpenToMyLocation ?? TriState.Unknown;
        vacancy.RelocationSupport = r.RelocationSupport ?? TriState.Unknown;
        vacancy.RelocationNotes = Validator.Clean(r.RelocationNotes);
        vacancy.EmploymentType = r.EmploymentType;
        vacancy.RequiredExperienceYears = r.RequiredExperienceYears;
        vacancy.RequiredExperienceText = Validator.Clean(r.RequiredExperienceText);
        vacancy.PostedSalary = postedSalary;
        vacancy.DateDiscovered = r.DateDiscovered ?? vacancy.DateDiscovered;
        vacancy.ApplicationDeadline = r.ApplicationDeadline;
        vacancy.Notes = Validator.Clean(r.Notes);
        vacancy.RequiredTechnologies.Clear();
        vacancy.RequiredTechnologies.AddRange(required);
        vacancy.PreferredTechnologies.Clear();
        vacancy.PreferredTechnologies.AddRange(preferred);
    }
}

public static class VacancyEndpoints
{
    public static RouteGroupBuilder MapVacancyEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/vacancies").WithTags("Vacancies");

        group.MapGet("/", ([AsParameters] VacancyQuery query, VacancyService service, CancellationToken ct) => service.ListAsync(query, ct));
        group.MapGet("/{id:int}", (int id, VacancyService service, CancellationToken ct) => service.GetAsync(id, ct));
        group.MapPost("/", async Task<Created<VacancyDetail>> (VacancyRequest request, VacancyService service, CancellationToken ct) =>
        {
            var vacancy = await service.CreateAsync(request, ct);
            return TypedResults.Created($"/api/vacancies/{vacancy.Id}", vacancy);
        });
        group.MapPut("/{id:int}", (int id, VacancyRequest request, VacancyService service, CancellationToken ct) =>
            service.UpdateAsync(id, request, ct));
        group.MapPost("/{id:int}/archive", async (int id, VacancyService service, CancellationToken ct) =>
        {
            await service.SetArchivedAsync(id, true, ct);
            return TypedResults.NoContent();
        });
        group.MapPost("/{id:int}/unarchive", async (int id, VacancyService service, CancellationToken ct) =>
        {
            await service.SetArchivedAsync(id, false, ct);
            return TypedResults.NoContent();
        });
        group.MapDelete("/{id:int}", async (int id, VacancyService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });
        group.MapPost("/{id:int}/convert", async Task<Created<CreatedId>> (
            int id, ConvertRequest request, ApplicationService applications, CancellationToken ct) =>
        {
            var created = await applications.CreateAsync(new CreateApplicationRequest(id, null, request.Status, request.Priority), ct);
            return TypedResults.Created($"/api/applications/{created.Id}", created);
        });

        return api;
    }
}
