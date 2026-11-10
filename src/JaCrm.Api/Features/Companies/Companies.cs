using JaCrm.Api.Common;
using JaCrm.Api.Data;
using JaCrm.Api.Data.Configurations;
using JaCrm.Api.Domain;
using JaCrm.Api.Features.Shared;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.Api.Features.Companies;

public sealed record CompanyQuery(
    string? Search,
    ArchivedFilter? Archived,
    string? Country,
    string? Sort,
    string? Dir,
    int? Page,
    int? PageSize);

public sealed record CompanyListItem(
    int Id,
    string Name,
    string? Website,
    string? Industry,
    string? Country,
    string? City,
    CompanySize? Size,
    int? Rating,
    bool IsArchived,
    DateTime CreatedAt,
    int VacancyCount,
    int ApplicationCount,
    int ActiveApplicationCount);

public sealed record CompanyOption(int Id, string Name, bool IsArchived);

public sealed record CompanyRequest(
    string? Name,
    string? Website,
    string? CareersUrl,
    string? Industry,
    string? Headquarters,
    string? Country,
    string? City,
    CompanySize? Size,
    string? Notes,
    int? Rating);

public sealed record CompanyDetail(
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
    DateTime UpdatedAt,
    IReadOnlyList<VacancySummary> Vacancies,
    IReadOnlyList<ApplicationSummary> Applications,
    IReadOnlyList<StatusHistoryEntry> History);

public enum DuplicateReason
{
    Name,
    Domain,
}

public sealed record DuplicateCompany(int Id, string Name, string? Website, bool IsArchived, DuplicateReason Reason);

public sealed class CompanyService(AppDbContext db, Clock clock)
{
    private static readonly ApplicationStatus[] ActiveStatuses = ApplicationStatuses.All.Where(ApplicationStatuses.IsActive).ToArray();

    public async Task<PagedResult<CompanyListItem>> ListAsync(CompanyQuery query, CancellationToken ct)
    {
        var companies = db.Companies.AsNoTracking();
        companies = (query.Archived ?? ArchivedFilter.Active) switch
        {
            ArchivedFilter.Active => companies.Where(c => !c.IsArchived),
            ArchivedFilter.Archived => companies.Where(c => c.IsArchived),
            _ => companies,
        };

        if (Text.ContainsPattern(query.Search) is { } p)
        {
            companies = companies.Where(c =>
                EF.Functions.Like(c.Name, p, Text.LikeEscape)
                || EF.Functions.Like(c.Industry, p, Text.LikeEscape)
                || EF.Functions.Like(c.Country, p, Text.LikeEscape)
                || EF.Functions.Like(c.City, p, Text.LikeEscape)
                || EF.Functions.Like(c.Headquarters, p, Text.LikeEscape)
                || EF.Functions.Like(c.Website, p, Text.LikeEscape)
                || EF.Functions.Like(c.Notes, p, Text.LikeEscape));
        }

        if (Validator.Clean(query.Country) is { } country)
        {
            companies = companies.Where(c => c.Country == country);
        }

        var rows = await companies.Select(c => new CompanyListItem(
            c.Id, c.Name, c.Website, c.Industry, c.Country, c.City, c.Size, c.Rating, c.IsArchived, c.CreatedAt,
            c.Vacancies.Count,
            c.Vacancies.Count(v => v.Application != null),
            c.Vacancies.Count(v => v.Application != null && !v.Application.IsArchived && ActiveStatuses.Contains(v.Application.Status))))
            .ToListAsync(ct);

        var desc = Paging.Descending(query.Dir);
        IEnumerable<CompanyListItem> sorted = query.Sort switch
        {
            "createdAt" => desc ? rows.OrderByDescending(c => c.CreatedAt) : rows.OrderBy(c => c.CreatedAt),
            "rating" => rows.OrderNullsLast(c => c.Rating, desc),
            "country" => rows.OrderText(c => c.Country, desc),
            "applications" => desc ? rows.OrderByDescending(c => c.ApplicationCount) : rows.OrderBy(c => c.ApplicationCount),
            _ => rows.OrderText(c => c.Name, desc),
        };
        return Paging.Page(sorted.ToList(), query.Page, query.PageSize);
    }

    /// <summary>All companies for pickers (small list). Archived ones only when asked (decision D-30).</summary>
    public async Task<IReadOnlyList<CompanyOption>> OptionsAsync(bool includeArchived, CancellationToken ct)
    {
        var rows = await db.Companies.AsNoTracking()
            .Where(c => includeArchived || !c.IsArchived)
            .Select(c => new CompanyOption(c.Id, c.Name, c.IsArchived))
            .ToListAsync(ct);
        return rows.OrderText(c => c.Name, false).ToList();
    }

    public async Task<CompanyDetail> GetAsync(int id, CancellationToken ct)
    {
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Company", id);

        var vacancies = await db.Vacancies.AsNoTracking()
            .Where(v => v.CompanyId == id)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new VacancySummary(
                v.Id, v.Title, v.Seniority, v.WorkArrangement, v.Country, v.City, v.DateDiscovered, v.ApplicationDeadline,
                v.IsArchived, v.Application != null ? v.Application.Id : null, v.Application != null ? v.Application.Status : null))
            .ToListAsync(ct);

        var applications = await db.Applications.AsNoTracking()
            .Where(a => a.Vacancy.CompanyId == id)
            .OrderByDescending(a => a.UpdatedAt)
            .Select(a => new ApplicationSummary(a.Id, a.VacancyId, a.Vacancy.Title, a.Status, a.Priority, a.IsArchived, a.SubmittedOn, a.UpdatedAt))
            .ToListAsync(ct);

        var history = await db.StatusHistory.AsNoTracking()
            .Where(h => h.Application.Vacancy.CompanyId == id)
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new StatusHistoryEntry(h.Id, h.ApplicationId, h.Application.Vacancy.Title, h.FromStatus, h.ToStatus, h.ChangedAt, h.Note))
            .ToListAsync(ct);

        return new CompanyDetail(
            company.Id, company.Name, company.Website, company.CareersUrl, company.Industry, company.Headquarters,
            company.Country, company.City, company.Size, company.Notes, company.Rating, company.IsArchived, company.ArchivedAt,
            company.CreatedAt, company.UpdatedAt, vacancies, applications, history);
    }

    public async Task<CompanyDetail> CreateAsync(CompanyRequest request, CancellationToken ct)
    {
        Validate(request);
        var now = clock.UtcNow;
        var company = new Company { CreatedAt = now, UpdatedAt = now };
        Apply(company, request);
        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);
        return await GetAsync(company.Id, ct);
    }

    public async Task<CompanyDetail> UpdateAsync(int id, CompanyRequest request, CancellationToken ct)
    {
        Validate(request);
        var company = await FindAsync(id, ct);
        Apply(company, request);
        company.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task SetArchivedAsync(int id, bool archived, CancellationToken ct)
    {
        var company = await FindAsync(id, ct);
        if (archived)
        {
            company.Archive(clock.UtcNow);
        }
        else
        {
            company.Unarchive();
        }

        company.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Hard delete only when nothing references the company; otherwise archive it (409).</summary>
    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var company = await FindAsync(id, ct);
        var vacancies = await db.Vacancies.CountAsync(v => v.CompanyId == id, ct);
        if (vacancies > 0)
        {
            throw new ConflictException(
                $"{company.Name} has {vacancies} vacanc{(vacancies == 1 ? "y" : "ies")}, so it can only be archived, not deleted.");
        }

        db.Companies.Remove(company);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Likely duplicates by normalized name or website domain. A warning only, never a block.</summary>
    public async Task<IReadOnlyList<DuplicateCompany>> FindDuplicatesAsync(string? name, string? website, int? excludeId, CancellationToken ct)
    {
        var normalizedName = string.IsNullOrWhiteSpace(name) ? "" : CompanyMatching.NormalizeName(name);
        var domain = CompanyMatching.NormalizeDomain(website);
        if (normalizedName.Length == 0 && domain is null)
        {
            return [];
        }

        var matches = await db.Companies.AsNoTracking()
            .Where(c => c.Id != excludeId
                && ((normalizedName != "" && c.NormalizedName == normalizedName) || (domain != null && c.WebsiteDomain == domain)))
            .OrderBy(c => c.Name)
            .Take(5)
            .ToListAsync(ct);

        return matches
            .Select(c => new DuplicateCompany(c.Id, c.Name, c.Website, c.IsArchived,
                normalizedName != "" && c.NormalizedName == normalizedName ? DuplicateReason.Name : DuplicateReason.Domain))
            .ToList();
    }

    /// <summary>The company for a new or edited vacancy: an existing one by id, or one created inline
    /// with only a name. An inline company is added but not saved, so it is written in the same
    /// transaction as the record that needs it. <paramref name="prefix"/> namespaces the error keys
    /// (e.g. "newVacancy." gives "newVacancy.companyId").</summary>
    public async Task<Company> ResolveAsync(int? companyId, NewCompanyInline? newCompany, string prefix, CancellationToken ct)
    {
        if (companyId is { } id)
        {
            return await db.Companies.SingleOrDefaultAsync(c => c.Id == id, ct)
                ?? throw ValidationException.For($"{prefix}companyId", "The selected company does not exist.");
        }

        if (newCompany is null)
        {
            throw ValidationException.For($"{prefix}companyId", "Company is required.");
        }

        new Validator()
            .Required($"{prefix}newCompany.name", newCompany.Name, "Company name")
            .MaxLength($"{prefix}newCompany.name", newCompany.Name, Lengths.Name, "Company name")
            .ThrowIfInvalid();
        var now = clock.UtcNow;
        var company = new Company { CreatedAt = now, UpdatedAt = now };
        company.Rename(newCompany.Name!);
        db.Companies.Add(company);
        return company;
    }

    private async Task<Company> FindAsync(int id, CancellationToken ct) =>
        await db.Companies.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Company", id);

    private static void Validate(CompanyRequest r)
    {
        var website = Text.NormalizeUrl(r.Website);
        var careers = Text.NormalizeUrl(r.CareersUrl);
        new Validator()
            .Required("name", r.Name, "Name")
            .MaxLength("name", r.Name, Lengths.Name, "Name")
            .HttpUrl("website", website, "Website")
            .MaxLength("website", website, Lengths.Url, "Website")
            .HttpUrl("careersUrl", careers, "Careers page")
            .MaxLength("careersUrl", careers, Lengths.Url, "Careers page")
            .MaxLength("industry", r.Industry, Lengths.Short, "Industry")
            .MaxLength("headquarters", r.Headquarters, Lengths.Name, "Headquarters")
            .MaxLength("country", r.Country, Lengths.Short, "Country")
            .MaxLength("city", r.City, Lengths.Short, "City")
            .Defined("size", r.Size, "Company size")
            .Range("rating", r.Rating, 1, 5, "Rating")
            .ThrowIfInvalid();
    }

    private static void Apply(Company company, CompanyRequest r)
    {
        company.Rename(r.Name!);
        company.SetWebsite(Text.NormalizeUrl(r.Website));
        company.CareersUrl = Text.NormalizeUrl(r.CareersUrl);
        company.Industry = Validator.Clean(r.Industry);
        company.Headquarters = Validator.Clean(r.Headquarters);
        company.Country = Validator.Clean(r.Country);
        company.City = Validator.Clean(r.City);
        company.Size = r.Size;
        company.Notes = Validator.Clean(r.Notes);
        company.Rating = r.Rating;
    }
}

public static class CompanyEndpoints
{
    public static RouteGroupBuilder MapCompanyEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/companies").WithTags("Companies");

        group.MapGet("/", ([AsParameters] CompanyQuery query, CompanyService service, CancellationToken ct) => service.ListAsync(query, ct));
        group.MapGet("/options", (bool? includeArchived, CompanyService service, CancellationToken ct) =>
            service.OptionsAsync(includeArchived ?? false, ct));
        group.MapGet("/duplicates", (string? name, string? website, int? excludeId, CompanyService service, CancellationToken ct) =>
            service.FindDuplicatesAsync(name, website, excludeId, ct));
        group.MapGet("/{id:int}", (int id, CompanyService service, CancellationToken ct) => service.GetAsync(id, ct));

        group.MapPost("/", async Task<Created<CompanyDetail>> (CompanyRequest request, CompanyService service, CancellationToken ct) =>
        {
            var company = await service.CreateAsync(request, ct);
            return TypedResults.Created($"/api/companies/{company.Id}", company);
        });
        group.MapPut("/{id:int}", (int id, CompanyRequest request, CompanyService service, CancellationToken ct) =>
            service.UpdateAsync(id, request, ct));
        group.MapPost("/{id:int}/archive", async (int id, CompanyService service, CancellationToken ct) =>
        {
            await service.SetArchivedAsync(id, true, ct);
            return TypedResults.NoContent();
        });
        group.MapPost("/{id:int}/unarchive", async (int id, CompanyService service, CancellationToken ct) =>
        {
            await service.SetArchivedAsync(id, false, ct);
            return TypedResults.NoContent();
        });
        group.MapDelete("/{id:int}", async (int id, CompanyService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        return api;
    }
}
