using JaCrm.Api.Common;
using JaCrm.Api.Data;
using JaCrm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.Api.Features.Search;

public sealed record ApplicationHit(int Id, string Title, string CompanyName, ApplicationStatus Status, bool IsArchived);

public sealed record VacancyHit(int Id, string Title, string CompanyName, bool IsArchived);

public sealed record CompanyHit(int Id, string Name, string? Country, bool IsArchived);

public sealed record SearchResults(IReadOnlyList<ApplicationHit> Applications, IReadOnlyList<VacancyHit> Vacancies, IReadOnlyList<CompanyHit> Companies);

/// <summary>Distinct values already in use, offered as suggestions so free-text fields stay consistent (D-18).</summary>
public sealed record Lookups(
    IReadOnlyList<string> Countries,
    IReadOnlyList<string> Cities,
    IReadOnlyList<string> Industries,
    IReadOnlyList<string> Technologies);

/// <summary>Global search for the command palette, across all three record types. Archived records
/// are included (flagged) and ranked after active ones.</summary>
public sealed class SearchService(AppDbContext db)
{
    public const int PerType = 6;

    public async Task<SearchResults> SearchAsync(string? q, CancellationToken ct)
    {
        if (Text.ContainsPattern(q) is not { } p)
        {
            return new SearchResults([], [], []);
        }

        var applications = await db.Applications.AsNoTracking()
            .Where(a => EF.Functions.Like(a.Vacancy.Title, p, Text.LikeEscape)
                || EF.Functions.Like(a.Vacancy.Company.Name, p, Text.LikeEscape)
                || a.Vacancy.RequiredTechnologies.Any(t => EF.Functions.Like(t.Name, p, Text.LikeEscape))
                || EF.Functions.Like(a.Notes, p, Text.LikeEscape))
            .OrderBy(a => a.IsArchived).ThenByDescending(a => a.UpdatedAt)
            .Take(PerType)
            .Select(a => new ApplicationHit(a.Id, a.Vacancy.Title, a.Vacancy.Company.Name, a.Status, a.IsArchived))
            .ToListAsync(ct);

        var vacancies = await db.Vacancies.AsNoTracking()
            .Where(v => EF.Functions.Like(v.Title, p, Text.LikeEscape)
                || EF.Functions.Like(v.Company.Name, p, Text.LikeEscape)
                || EF.Functions.Like(v.Country, p, Text.LikeEscape)
                || EF.Functions.Like(v.City, p, Text.LikeEscape)
                || v.RequiredTechnologies.Any(t => EF.Functions.Like(t.Name, p, Text.LikeEscape))
                || v.PreferredTechnologies.Any(t => EF.Functions.Like(t.Name, p, Text.LikeEscape)))
            .OrderBy(v => v.IsArchived).ThenByDescending(v => v.UpdatedAt)
            .Take(PerType)
            .Select(v => new VacancyHit(v.Id, v.Title, v.Company.Name, v.IsArchived))
            .ToListAsync(ct);

        var companies = await db.Companies.AsNoTracking()
            .Where(c => EF.Functions.Like(c.Name, p, Text.LikeEscape)
                || EF.Functions.Like(c.Website, p, Text.LikeEscape)
                || EF.Functions.Like(c.Industry, p, Text.LikeEscape)
                || EF.Functions.Like(c.Country, p, Text.LikeEscape)
                || EF.Functions.Like(c.City, p, Text.LikeEscape))
            .OrderBy(c => c.IsArchived).ThenBy(c => c.Name)
            .Take(PerType)
            .Select(c => new CompanyHit(c.Id, c.Name, c.Country, c.IsArchived))
            .ToListAsync(ct);

        return new SearchResults(applications, vacancies, companies);
    }

    public async Task<Lookups> LookupsAsync(CancellationToken ct)
    {
        var companyPlaces = await db.Companies.AsNoTracking().Select(c => new { c.Country, c.City, c.Industry }).ToListAsync(ct);
        var vacancyPlaces = await db.Vacancies.AsNoTracking().Select(v => new { v.Country, v.City }).ToListAsync(ct);
        var technologies = await db.Technologies.AsNoTracking().Select(t => t.Name).ToListAsync(ct);

        return new Lookups(
            Distinct(companyPlaces.Select(c => c.Country).Concat(vacancyPlaces.Select(v => v.Country))),
            Distinct(companyPlaces.Select(c => c.City).Concat(vacancyPlaces.Select(v => v.City))),
            Distinct(companyPlaces.Select(c => c.Industry)),
            Distinct(technologies));
    }

    private static List<string> Distinct(IEnumerable<string?> values) =>
        values.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToList();
}

public static class SearchEndpoints
{
    public static RouteGroupBuilder MapSearchEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/search", (string? q, SearchService service, CancellationToken ct) => service.SearchAsync(q, ct)).WithTags("Search");
        api.MapGet("/lookups", (SearchService service, CancellationToken ct) => service.LookupsAsync(ct)).WithTags("Search");
        return api;
    }
}
