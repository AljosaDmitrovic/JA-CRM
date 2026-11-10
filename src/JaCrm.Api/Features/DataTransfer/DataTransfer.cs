using System.Text.Json;
using System.Text.Json.Serialization;
using JaCrm.Api.Common;
using JaCrm.Api.Data;
using JaCrm.Api.Data.Configurations;
using JaCrm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.Api.Features.DataTransfer;

/// <summary>Full JSON export, and import that replaces all data (section 7). Export followed by
/// import round-trips without loss: ids, timestamps, flags, snapshots and history are kept.</summary>
public sealed class DataTransferService(AppDbContext db, Clock clock, DatabaseFiles files, ILogger<DataTransferService> logger)
{
    public static readonly JsonSerializerOptions FileJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };

    public async Task<ExportDocument> ExportAsync(CancellationToken ct)
    {
        var settings = await db.Settings.AsNoTracking().SingleAsync(ct);
        var rates = await db.ExchangeRates.AsNoTracking().OrderBy(r => r.Currency).ToListAsync(ct);
        var technologies = await db.Technologies.AsNoTracking().OrderBy(t => t.Id).ToListAsync(ct);
        var companies = await db.Companies.AsNoTracking().OrderBy(c => c.Id).ToListAsync(ct);
        var vacancies = await db.Vacancies.AsNoTracking()
            .Include(v => v.RequiredTechnologies)
            .Include(v => v.PreferredTechnologies)
            .AsSplitQuery()
            .OrderBy(v => v.Id)
            .ToListAsync(ct);
        var applications = await db.Applications.AsNoTracking().OrderBy(a => a.Id).ToListAsync(ct);
        var history = await db.StatusHistory.AsNoTracking().OrderBy(h => h.Id).ToListAsync(ct);

        return new ExportDocument(
            ExportDocument.CurrentSchemaVersion,
            clock.UtcNow,
            new ExportSettings(settings.Theme, settings.BaseCurrency, settings.WorkingHoursPerMonth, settings.StaleThresholdDays, settings.DateFormat),
            rates.Select(r => new ExportExchangeRate(r.Currency, r.RateToBase, r.UpdatedOn)).ToList(),
            technologies.Select(t => new ExportTechnology(t.Id, t.Name)).ToList(),
            companies.Select(c => new ExportCompany(
                c.Id, c.Name, c.Website, c.CareersUrl, c.Industry, c.Headquarters, c.Country, c.City, c.Size, c.Notes, c.Rating,
                c.IsArchived, c.ArchivedAt, c.CreatedAt, c.UpdatedAt)).ToList(),
            vacancies.Select(v => new ExportVacancy(
                v.Id, v.CompanyId, v.Title, v.Seniority, v.Description, v.Url, v.Source, v.SourceDetail, v.Country, v.City,
                v.WorkArrangement, v.RemoteEligibility, v.OpenToMyLocation, v.RelocationSupport, v.RelocationNotes, v.EmploymentType,
                v.RequiredTechnologies.Select(t => t.Id).Order().ToList(), v.PreferredTechnologies.Select(t => t.Id).Order().ToList(),
                v.RequiredExperienceYears, v.RequiredExperienceText, v.PostedSalary, v.DateDiscovered, v.ApplicationDeadline, v.Notes,
                v.IsArchived, v.ArchivedAt, v.CreatedAt, v.UpdatedAt)).ToList(),
            applications.Select(a => new ExportApplication(
                a.Id, a.VacancyId, a.Status, a.Priority, a.IsArchived, a.ArchivedAt, a.CreatedAt, a.UpdatedAt, a.StatusChangedAt,
                a.SubmittedOn, a.Notes, a.ReasonForApplying, a.ExpectedSalary, a.OfferedSalary, a.CompensationNotes, a.CvVersion,
                a.CoverLetter, a.PostingSnapshot)).ToList(),
            history.Select(h => new ExportStatusHistory(h.Id, h.ApplicationId, h.FromStatus, h.ToStatus, h.ChangedAt, h.Note)).ToList());
    }

    /// <summary>Replaces ALL data with the document. Validated completely first; a backup of the
    /// current database is written before anything is deleted; the replacement is one transaction.</summary>
    public async Task<ImportSummary> ImportAsync(ExportDocument? document, CancellationToken ct)
    {
        if (document is null)
        {
            throw ValidationException.For("file", "The file is empty or is not a JSON export.");
        }

        Validate(document);

        var backup = await DatabaseMaintenance.BackupAsync(db, files, "pre-import", ct);
        logger.LogInformation("Backed up the database to {Backup} before an import", backup);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.StatusHistory.ExecuteDeleteAsync(ct);
        await db.Applications.ExecuteDeleteAsync(ct);
        await db.Vacancies.ExecuteDeleteAsync(ct); // technology links cascade with the vacancy
        await db.Technologies.ExecuteDeleteAsync(ct);
        await db.Companies.ExecuteDeleteAsync(ct);
        await db.ExchangeRates.ExecuteDeleteAsync(ct);

        var settings = await db.GetSettingsAsync(ct);
        settings.Theme = document.Settings.Theme;
        settings.BaseCurrency = document.Settings.BaseCurrency;
        settings.WorkingHoursPerMonth = document.Settings.WorkingHoursPerMonth;
        settings.StaleThresholdDays = document.Settings.StaleThresholdDays;
        settings.DateFormat = document.Settings.DateFormat;

        db.ExchangeRates.AddRange(document.ExchangeRates.Select(r =>
            new ExchangeRate { Currency = r.Currency, RateToBase = r.RateToBase, UpdatedOn = r.UpdatedOn }));

        var technologies = document.Technologies.ToDictionary(t => t.Id, t =>
        {
            var technology = Technology.Create(t.Name);
            technology.Id = t.Id;
            return technology;
        });
        db.Technologies.AddRange(technologies.Values);

        foreach (var c in document.Companies)
        {
            var company = new Company
            {
                Id = c.Id, CareersUrl = c.CareersUrl, Industry = c.Industry, Headquarters = c.Headquarters, Country = c.Country,
                City = c.City, Size = c.Size, Notes = c.Notes, Rating = c.Rating, IsArchived = c.IsArchived, ArchivedAt = c.ArchivedAt,
                CreatedAt = c.CreatedAt, UpdatedAt = c.UpdatedAt,
            };
            company.Rename(c.Name);
            company.SetWebsite(c.Website);
            db.Companies.Add(company);
        }

        foreach (var v in document.Vacancies)
        {
            var vacancy = new Vacancy
            {
                Id = v.Id, CompanyId = v.CompanyId, Title = v.Title, Seniority = v.Seniority, Description = v.Description, Url = v.Url,
                Source = v.Source, SourceDetail = v.SourceDetail, Country = v.Country, City = v.City, WorkArrangement = v.WorkArrangement,
                RemoteEligibility = v.RemoteEligibility, OpenToMyLocation = v.OpenToMyLocation, RelocationSupport = v.RelocationSupport,
                RelocationNotes = v.RelocationNotes, EmploymentType = v.EmploymentType, RequiredExperienceYears = v.RequiredExperienceYears,
                RequiredExperienceText = v.RequiredExperienceText, PostedSalary = v.PostedSalary, DateDiscovered = v.DateDiscovered,
                ApplicationDeadline = v.ApplicationDeadline, Notes = v.Notes, IsArchived = v.IsArchived, ArchivedAt = v.ArchivedAt,
                CreatedAt = v.CreatedAt, UpdatedAt = v.UpdatedAt,
            };
            vacancy.RequiredTechnologies.AddRange(v.RequiredTechnologyIds.Select(id => technologies[id]));
            vacancy.PreferredTechnologies.AddRange(v.PreferredTechnologyIds.Select(id => technologies[id]));
            db.Vacancies.Add(vacancy);
        }

        foreach (var a in document.Applications)
        {
            var application = Domain.Application.Restore(a.Id, a.VacancyId, a.Status, a.CreatedAt, a.StatusChangedAt, a.PostingSnapshot);
            application.Priority = a.Priority;
            application.IsArchived = a.IsArchived;
            application.ArchivedAt = a.ArchivedAt;
            application.UpdatedAt = a.UpdatedAt;
            application.SubmittedOn = a.SubmittedOn;
            application.Notes = a.Notes;
            application.ReasonForApplying = a.ReasonForApplying;
            application.ExpectedSalary = a.ExpectedSalary;
            application.OfferedSalary = a.OfferedSalary;
            application.CompensationNotes = a.CompensationNotes;
            application.CvVersion = a.CvVersion;
            application.CoverLetter = a.CoverLetter;
            db.Applications.Add(application);
        }

        db.StatusHistory.AddRange(document.StatusHistory.Select(h => new ApplicationStatusHistory
        {
            Id = h.Id, ApplicationId = h.ApplicationId, FromStatus = h.FromStatus, ToStatus = h.ToStatus, ChangedAt = h.ChangedAt, Note = h.Note,
        }));

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new ImportSummary(
            document.Companies.Count, document.Vacancies.Count, document.Applications.Count, document.StatusHistory.Count,
            document.Technologies.Count, document.ExchangeRates.Count, Path.GetFileName(backup));
    }

    /// <summary>Checks everything the database would reject (and more), so a bad file never gets
    /// as far as deleting data. Error keys point at the offending record, e.g. "vacancies[3].companyId".</summary>
    private static void Validate(ExportDocument d)
    {
        var v = new Validator();
        if (d.SchemaVersion != ExportDocument.CurrentSchemaVersion)
        {
            v.Add("schemaVersion", $"Unsupported export version {d.SchemaVersion}; this app reads version {ExportDocument.CurrentSchemaVersion}.");
            v.ThrowIfInvalid();
        }

        if (d.Settings is null || d.ExchangeRates is null || d.Technologies is null || d.Companies is null || d.Vacancies is null
            || d.Applications is null || d.StatusHistory is null)
        {
            throw ValidationException.For("file", "The export is incomplete: settings and every record list must be present.");
        }

        var s = d.Settings;
        v.Currency("settings.baseCurrency", s.BaseCurrency, "Base currency")
            .Defined("settings.theme", (ThemePreference?)s.Theme, "Theme")
            .Defined("settings.dateFormat", (DateFormatPreference?)s.DateFormat, "Date format")
            .Range("settings.workingHoursPerMonth", s.WorkingHoursPerMonth, 1, 744, "Working hours per month")
            .Range("settings.staleThresholdDays", s.StaleThresholdDays, 1, 365, "Stale threshold");

        var currencies = new HashSet<string>(StringComparer.Ordinal);
        Each(d.ExchangeRates, "exchangeRates", (r, f) =>
        {
            v.Currency($"{f}.currency", r.Currency).Range($"{f}.rateToBase", r.RateToBase, 0.000001m, 1_000_000_000m, "Rate");
            if (r.Currency == s.BaseCurrency || !currencies.Add(r.Currency ?? ""))
            {
                v.Add($"{f}.currency", "Duplicate currency, or a rate for the base currency.");
            }
        });

        var technologyIds = UniqueIds(d.Technologies, t => t.Id, "technologies", v);
        var technologyNames = new HashSet<string>(StringComparer.Ordinal);
        Each(d.Technologies, "technologies", (t, f) =>
        {
            v.Required($"{f}.name", t.Name, "Name").MaxLength($"{f}.name", t.Name, Lengths.Short, "Name");
            if (!string.IsNullOrWhiteSpace(t.Name) && !technologyNames.Add(Technology.Normalize(t.Name)))
            {
                v.Add($"{f}.name", $"Technology \"{t.Name}\" is listed twice.");
            }
        });

        var companyIds = UniqueIds(d.Companies, c => c.Id, "companies", v);
        Each(d.Companies, "companies", (c, f) => v
            .Required($"{f}.name", c.Name, "Name")
            .MaxLength($"{f}.name", c.Name, Lengths.Name, "Name")
            .Range($"{f}.rating", c.Rating, 1, 5, "Rating")
            .Defined($"{f}.size", c.Size, "Company size"));

        var vacancyIds = UniqueIds(d.Vacancies, x => x.Id, "vacancies", v);
        Each(d.Vacancies, "vacancies", (x, f) =>
        {
            v.Required($"{f}.title", x.Title, "Title").MaxLength($"{f}.title", x.Title, Lengths.Name, "Title")
                .Range($"{f}.requiredExperienceYears", x.RequiredExperienceYears, 0, 60, "Years of experience");
            Reference(v, companyIds, x.CompanyId, $"{f}.companyId", "company");
            foreach (var id in (x.RequiredTechnologyIds ?? []).Concat(x.PreferredTechnologyIds ?? []))
            {
                Reference(v, technologyIds, id, $"{f}.technologies", "technology");
            }

            if (x.RequiredTechnologyIds is null || x.PreferredTechnologyIds is null)
            {
                v.Add($"{f}.technologies", "Technology lists are missing.");
            }

            if (x.PostedSalary is { } range)
            {
                Salary(v, range.Currency, range.Min ?? range.Max, $"{f}.postedSalary");
            }
        });

        var applicationIds = UniqueIds(d.Applications, a => a.Id, "applications", v);
        var usedVacancies = new HashSet<int>();
        Each(d.Applications, "applications", (a, f) =>
        {
            v.Defined($"{f}.status", (ApplicationStatus?)a.Status, "Status").Defined($"{f}.priority", (Priority?)a.Priority, "Priority");
            Reference(v, vacancyIds, a.VacancyId, $"{f}.vacancyId", "vacancy");
            if (!usedVacancies.Add(a.VacancyId))
            {
                v.Add($"{f}.vacancyId", "A vacancy can have only one application.");
            }

            if (a.PostingSnapshot is null)
            {
                v.Add($"{f}.postingSnapshot", "The posting snapshot is missing.");
            }

            Salary(v, a.ExpectedSalary?.Currency, a.ExpectedSalary?.Amount, $"{f}.expectedSalary", a.ExpectedSalary is null);
            Salary(v, a.OfferedSalary?.Currency, a.OfferedSalary?.Amount, $"{f}.offeredSalary", a.OfferedSalary is null);
        });

        UniqueIds(d.StatusHistory, h => h.Id, "statusHistory", v);
        Each(d.StatusHistory, "statusHistory", (h, f) =>
        {
            v.Defined($"{f}.fromStatus", h.FromStatus, "From status").Defined($"{f}.toStatus", (ApplicationStatus?)h.ToStatus, "To status");
            Reference(v, applicationIds, h.ApplicationId, $"{f}.applicationId", "application");
        });

        v.ThrowIfInvalid();
    }

    private static void Each<T>(IReadOnlyList<T> items, string name, Action<T, string> check)
    {
        for (var i = 0; i < items.Count; i++)
        {
            check(items[i], $"{name}[{i}]");
        }
    }

    private static HashSet<int> UniqueIds<T>(IReadOnlyList<T> items, Func<T, int> id, string name, Validator v)
    {
        var ids = new HashSet<int>();
        for (var i = 0; i < items.Count; i++)
        {
            if (id(items[i]) <= 0 || !ids.Add(id(items[i])))
            {
                v.Add($"{name}[{i}].id", "Ids must be positive and unique.");
            }
        }

        return ids;
    }

    private static void Reference(Validator v, HashSet<int> known, int id, string field, string what)
    {
        if (!known.Contains(id))
        {
            v.Add(field, $"Refers to a {what} ({id}) that is not in the file.");
        }
    }

    private static void Salary(Validator v, string? currency, decimal? amount, string field, bool absent = false)
    {
        if (absent)
        {
            return;
        }

        v.Currency($"{field}.currency", currency);
        if (amount is < 0)
        {
            v.Add($"{field}.amount", "Amounts cannot be negative.");
        }
    }
}

public static class DataTransferEndpoints
{
    public static RouteGroupBuilder MapDataTransferEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/data").WithTags("Data");

        group.MapGet("/export", async (DataTransferService service, Clock clock, CancellationToken ct) =>
        {
            var document = await service.ExportAsync(ct);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(document, DataTransferService.FileJson);
            return Results.File(bytes, "application/json", $"jacrm-export-{clock.Today:yyyy-MM-dd}.json");
        });

        group.MapGet("/export/{entity}.csv", async (string entity, CsvExportService csv, Clock clock, CancellationToken ct) =>
        {
            var content = await csv.ExportAsync(entity, ct);
            return content is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: $"Unknown export \"{entity}\".")
                : Results.File(content, "text/csv; charset=utf-8", $"jacrm-{entity}-{clock.Today:yyyy-MM-dd}.csv");
        });

        // Replacing everything is destructive: the client must say so explicitly, on top of the
        // confirmation dialog in the UI.
        group.MapPost("/import", async (HttpRequest request, string? confirm, DataTransferService service, CancellationToken ct) =>
        {
            if (confirm != "replace-all")
            {
                throw ValidationException.For("confirm", "Importing replaces all data. Send confirm=replace-all to proceed.");
            }

            ExportDocument? document;
            try
            {
                document = await JsonSerializer.DeserializeAsync<ExportDocument>(request.Body, DataTransferService.FileJson, ct);
            }
            catch (JsonException e)
            {
                throw ValidationException.For("file", $"The file is not a valid export: {e.Message}");
            }

            return await service.ImportAsync(document, ct);
        });

        return api;
    }
}
