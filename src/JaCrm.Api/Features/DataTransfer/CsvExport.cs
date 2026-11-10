using System.Globalization;
using System.Text;
using JaCrm.Api.Data;
using JaCrm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.Api.Features.DataTransfer;

/// <summary>One CSV per entity for spreadsheets: human-readable labels, ISO dates, salaries split
/// into columns plus the normalized annual amount in the base currency.</summary>
public sealed class CsvExportService(AppDbContext db)
{
    public static readonly IReadOnlyList<string> Entities = ["companies", "vacancies", "applications", "status-history"];

    public async Task<byte[]?> ExportAsync(string entity, CancellationToken ct)
    {
        var csv = new Csv();
        switch (entity)
        {
            case "companies":
                await CompaniesAsync(csv, ct);
                break;
            case "vacancies":
                await VacanciesAsync(csv, ct);
                break;
            case "applications":
                await ApplicationsAsync(csv, ct);
                break;
            case "status-history":
                await HistoryAsync(csv, ct);
                break;
            default:
                return null;
        }

        return csv.ToBytes();
    }

    private async Task CompaniesAsync(Csv csv, CancellationToken ct)
    {
        csv.Header("Id", "Name", "Website", "Careers page", "Industry", "Headquarters", "Country", "City", "Size", "Rating",
            "Archived", "Archived at", "Date added", "Updated", "Vacancies", "Applications", "Notes");
        var rows = await db.Companies.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new { c, Vacancies = c.Vacancies.Count, Applications = c.Vacancies.Count(v => v.Application != null) })
            .ToListAsync(ct);
        foreach (var (c, vacancies, applications) in rows.Select(r => (r.c, r.Vacancies, r.Applications)))
        {
            csv.Row(c.Id, c.Name, c.Website, c.CareersUrl, c.Industry, c.Headquarters, c.Country, c.City, EnumLabels.For(c.Size), c.Rating,
                c.IsArchived, c.ArchivedAt, c.CreatedAt, c.UpdatedAt, vacancies, applications, c.Notes);
        }
    }

    private async Task VacanciesAsync(Csv csv, CancellationToken ct)
    {
        var salary = await db.LoadSalaryContextAsync(ct);
        csv.Header("Id", "Title", "Company", "Seniority", "Work arrangement", "Employment type", "Country", "City",
            "Remote eligibility", "Open to my location", "Relocation support", "Relocation notes", "Required technologies",
            "Preferred technologies", "Experience (years)", "Experience", "Salary min", "Salary max", "Currency", "Period",
            "Salary type", $"Annual min ({salary.BaseCurrency})", $"Annual max ({salary.BaseCurrency})", "Comparable",
            "Source", "Source detail", "URL", "Date discovered", "Deadline", "Archived", "Created", "Updated",
            "Application status", "Notes", "Description");
        var vacancies = await db.Vacancies.AsNoTracking()
            .Include(v => v.Company).Include(v => v.RequiredTechnologies).Include(v => v.PreferredTechnologies).Include(v => v.Application)
            .AsSplitQuery()
            .OrderBy(v => v.Id)
            .ToListAsync(ct);
        foreach (var v in vacancies)
        {
            var range = v.PostedSalary;
            var normalized = range is null ? null : SalaryNormalizer.Normalize(range, salary);
            csv.Row(v.Id, v.Title, v.Company.Name, EnumLabels.For(v.Seniority), EnumLabels.For(v.WorkArrangement),
                EnumLabels.For(v.EmploymentType), v.Country, v.City, v.RemoteEligibility, EnumLabels.For(v.OpenToMyLocation),
                EnumLabels.For(v.RelocationSupport), v.RelocationNotes, Tags(v.RequiredTechnologies), Tags(v.PreferredTechnologies),
                v.RequiredExperienceYears, v.RequiredExperienceText, range?.Min, range?.Max, range?.Currency,
                EnumLabels.For(range?.Period), EnumLabels.For(range?.Type), normalized?.AnnualMin, normalized?.AnnualMax,
                normalized?.IsComparable, EnumLabels.For(v.Source), v.SourceDetail, v.Url, v.DateDiscovered, v.ApplicationDeadline,
                v.IsArchived, v.CreatedAt, v.UpdatedAt, EnumLabels.For(v.Application?.Status), v.Notes, v.Description);
        }
    }

    private async Task ApplicationsAsync(Csv csv, CancellationToken ct)
    {
        var salary = await db.LoadSalaryContextAsync(ct);
        csv.Header("Id", "Job title", "Company", "Status", "Phase", "Priority", "Archived", "Archived at", "Created", "Submitted",
            "Status changed", "Updated", "Expected amount", "Expected currency", "Expected period", "Expected type",
            $"Expected annual ({salary.BaseCurrency})", "Offered amount", "Offered currency", "Offered period", "Offered type",
            $"Offered annual ({salary.BaseCurrency})", "Compensation notes", "CV version", "Cover letter", "Reason for applying",
            "Notes", "Vacancy id");
        var applications = await db.Applications.AsNoTracking()
            .Include(a => a.Vacancy).ThenInclude(v => v.Company)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);
        foreach (var a in applications)
        {
            var expected = a.ExpectedSalary;
            var offered = a.OfferedSalary;
            csv.Row(a.Id, a.Vacancy.Title, a.Vacancy.Company.Name, EnumLabels.For(a.Status), EnumLabels.For(ApplicationStatuses.PhaseOf(a.Status)),
                EnumLabels.For(a.Priority), a.IsArchived, a.ArchivedAt, a.CreatedAt, a.SubmittedOn, a.StatusChangedAt, a.UpdatedAt,
                expected?.Amount, expected?.Currency, EnumLabels.For(expected?.Period), EnumLabels.For(expected?.Type),
                expected is null ? null : SalaryNormalizer.Normalize(expected, salary).Annual,
                offered?.Amount, offered?.Currency, EnumLabels.For(offered?.Period), EnumLabels.For(offered?.Type),
                offered is null ? null : SalaryNormalizer.Normalize(offered, salary).Annual,
                a.CompensationNotes, a.CvVersion, a.CoverLetter, a.ReasonForApplying, a.Notes, a.VacancyId);
        }
    }

    private async Task HistoryAsync(Csv csv, CancellationToken ct)
    {
        csv.Header("Id", "Application id", "Job title", "Company", "From", "To", "Changed at", "Note");
        var rows = await db.StatusHistory.AsNoTracking()
            .OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
            .Select(h => new { h.Id, h.ApplicationId, h.Application.Vacancy.Title, Company = h.Application.Vacancy.Company.Name, h.FromStatus, h.ToStatus, h.ChangedAt, h.Note })
            .ToListAsync(ct);
        foreach (var h in rows)
        {
            csv.Row(h.Id, h.ApplicationId, h.Title, h.Company, EnumLabels.For(h.FromStatus), EnumLabels.For(h.ToStatus), h.ChangedAt, h.Note);
        }
    }

    private static string Tags(IEnumerable<Technology> technologies) =>
        string.Join(", ", technologies.Select(t => t.Name).Order(StringComparer.OrdinalIgnoreCase));

    /// <summary>RFC 4180 writer. Text that a spreadsheet would run as a formula (=, +, -, @, tab, CR)
    /// is prefixed with an apostrophe (CSV injection); numbers and dates are written as-is.</summary>
    private sealed class Csv
    {
        private readonly StringBuilder _text = new();

        public void Header(params string[] names) => Row(names);

        public void Row(params object?[] values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    _text.Append(',');
                }

                _text.Append(Format(values[i]));
            }

            _text.Append("\r\n");
        }

        /// <summary>UTF-8 with a byte-order mark, so Excel detects the encoding.</summary>
        public byte[] ToBytes() => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(_text.ToString())];

        private static string Format(object? value) => value switch
        {
            null => "",
            string s => Quote(Defuse(s)),
            bool b => b ? "Yes" : "No",
            DateTime t => t.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => Quote(Defuse(value.ToString() ?? "")),
        };

        private static string Defuse(string s) => s.Length > 0 && s[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + s : s;

        private static string Quote(string s) =>
            s.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : s;
    }
}
