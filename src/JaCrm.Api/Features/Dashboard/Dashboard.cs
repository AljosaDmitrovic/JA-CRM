using JaCrm.Api.Common;
using JaCrm.Api.Data;
using JaCrm.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.Api.Features.Dashboard;

public sealed record PipelineStatus(ApplicationStatus Status, int Count);

public sealed record PipelinePhase(ApplicationPhase Phase, int Count, IReadOnlyList<PipelineStatus> Statuses);

public sealed record StaleApplication(int Id, string Title, string CompanyName, DateTime StatusChangedAt, int DaysSinceChange);

public enum ActivityKind
{
    Created,
    StatusChanged,
    Updated,
}

public sealed record ActivityItem(
    ActivityKind Kind,
    DateTime At,
    int ApplicationId,
    string Title,
    string CompanyName,
    ApplicationStatus? FromStatus,
    ApplicationStatus? ToStatus);

public sealed record UpcomingDeadline(
    int VacancyId,
    string Title,
    string CompanyName,
    DateOnly Deadline,
    int DaysLeft,
    int? ApplicationId,
    ApplicationStatus? ApplicationStatus);

public sealed record DashboardResponse(
    DashboardMetrics Metrics,
    int StaleThresholdDays,
    IReadOnlyList<PipelinePhase> Pipeline,
    IReadOnlyList<StaleApplication> Stale,
    IReadOnlyList<ActivityItem> RecentActivity,
    IReadOnlyList<UpcomingDeadline> UpcomingDeadlines);

/// <summary>Everything on the home screen, computed from real records (section 5.1).</summary>
public sealed class DashboardService(AppDbContext db, Clock clock)
{
    public const int RecentActivityCount = 12;
    public const int DeadlineWindowDays = 30;
    public const int MaxStaleShown = 20;

    public async Task<DashboardResponse> GetAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var today = clock.Today;
        var settings = await db.Settings.AsNoTracking().SingleAsync(ct);

        var facts = await db.Applications.AsNoTracking()
            .Select(a => new ApplicationFacts(a.Id, a.Status, a.IsArchived, a.StatusChangedAt))
            .ToListAsync(ct);
        var everOffered = await db.StatusHistory.AsNoTracking()
            .Where(h => h.ToStatus == ApplicationStatus.OfferReceived)
            .Select(h => h.ApplicationId)
            .Distinct()
            .ToListAsync(ct);
        var metrics = DashboardCalculator.Compute(facts, everOffered.ToHashSet(), now, settings.StaleThresholdDays);

        return new DashboardResponse(
            metrics,
            settings.StaleThresholdDays,
            Pipeline(facts),
            await StaleAsync(facts, now, settings.StaleThresholdDays, ct),
            await RecentActivityAsync(ct),
            await DeadlinesAsync(today, ct));
    }

    /// <summary>Non-archived applications by phase and status, every status listed (zero included).</summary>
    private static List<PipelinePhase> Pipeline(List<ApplicationFacts> facts)
    {
        var counts = facts.Where(f => !f.IsArchived).GroupBy(f => f.Status).ToDictionary(g => g.Key, g => g.Count());
        return ApplicationStatuses.Phases.Select(phase =>
        {
            var statuses = ApplicationStatuses.InPhase(phase)
                .Select(s => new PipelineStatus(s, counts.GetValueOrDefault(s)))
                .ToList();
            return new PipelinePhase(phase, statuses.Sum(s => s.Count), statuses);
        }).ToList();
    }

    private async Task<List<StaleApplication>> StaleAsync(List<ApplicationFacts> facts, DateTime now, int thresholdDays, CancellationToken ct)
    {
        var staleIds = facts
            .Where(f => DashboardCalculator.IsStale(f, now, thresholdDays))
            .OrderBy(f => f.StatusChangedAt)
            .Take(MaxStaleShown)
            .Select(f => f.Id)
            .ToList();
        if (staleIds.Count == 0)
        {
            return [];
        }

        var rows = await db.Applications.AsNoTracking()
            .Where(a => staleIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Vacancy.Title, CompanyName = a.Vacancy.Company.Name, a.StatusChangedAt })
            .ToListAsync(ct);
        return rows
            .OrderBy(r => r.StatusChangedAt)
            .Select(r => new StaleApplication(r.Id, r.Title, r.CompanyName, r.StatusChangedAt, (int)(now - r.StatusChangedAt).TotalDays))
            .ToList();
    }

    /// <summary>Decision D-32: status changes and creations come from the history (the initial row
    /// marks creation); edits are applications updated after their last status change.</summary>
    private async Task<List<ActivityItem>> RecentActivityAsync(CancellationToken ct)
    {
        var history = await db.StatusHistory.AsNoTracking()
            .OrderByDescending(h => h.ChangedAt).ThenByDescending(h => h.Id)
            .Take(RecentActivityCount)
            .Select(h => new ActivityItem(
                h.FromStatus == null ? ActivityKind.Created : ActivityKind.StatusChanged,
                h.ChangedAt, h.ApplicationId, h.Application.Vacancy.Title, h.Application.Vacancy.Company.Name, h.FromStatus, h.ToStatus))
            .ToListAsync(ct);

        var edits = await db.Applications.AsNoTracking()
            .Where(a => a.UpdatedAt > a.StatusChangedAt)
            .OrderByDescending(a => a.UpdatedAt)
            .Take(RecentActivityCount)
            .Select(a => new ActivityItem(
                ActivityKind.Updated, a.UpdatedAt, a.Id, a.Vacancy.Title, a.Vacancy.Company.Name, null, null))
            .ToListAsync(ct);

        return history.Concat(edits).OrderByDescending(a => a.At).Take(RecentActivityCount).ToList();
    }

    /// <summary>Non-archived vacancies due today or in the next 30 days that have not been applied
    /// to yet: no application, or one still in the Pipeline phase (decision D-33).</summary>
    private async Task<List<UpcomingDeadline>> DeadlinesAsync(DateOnly today, CancellationToken ct)
    {
        var last = today.AddDays(DeadlineWindowDays);
        var pipeline = ApplicationStatuses.InPhase(ApplicationPhase.Pipeline).ToList();
        var rows = await db.Vacancies.AsNoTracking()
            .Where(v => !v.IsArchived && v.ApplicationDeadline >= today && v.ApplicationDeadline <= last)
            .Where(v => v.Application == null || pipeline.Contains(v.Application.Status))
            .OrderBy(v => v.ApplicationDeadline)
            .Select(v => new
            {
                v.Id, v.Title, CompanyName = v.Company.Name, Deadline = v.ApplicationDeadline!.Value,
                ApplicationId = v.Application != null ? (int?)v.Application.Id : null,
                ApplicationStatus = v.Application != null ? (ApplicationStatus?)v.Application.Status : null,
            })
            .ToListAsync(ct);
        return rows
            .Select(r => new UpcomingDeadline(
                r.Id, r.Title, r.CompanyName, r.Deadline, r.Deadline.DayNumber - today.DayNumber, r.ApplicationId, r.ApplicationStatus))
            .ToList();
    }
}

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/dashboard", (DashboardService service, CancellationToken ct) => service.GetAsync(ct)).WithTags("Dashboard");
        return api;
    }
}
