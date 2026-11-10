namespace JaCrm.Api.Domain;

/// <summary>The minimum an application needs for the dashboard metrics.</summary>
public sealed record ApplicationFacts(int Id, ApplicationStatus Status, bool IsArchived, DateTime StatusChangedAt);

public sealed record DashboardMetrics(
    int TotalApplications,
    int ActiveApplications,
    int InterviewsInProgress,
    int OffersReceived,
    int RejectedApplications,
    int AwaitingResponse,
    int StaleApplications);

/// <summary>Metric definitions exactly as specified (section 5.1 of the requirements).</summary>
public static class DashboardCalculator
{
    /// <param name="applications">Every application, archived included.</param>
    /// <param name="everOfferedIds">Applications that reached Offer Received at any point (from status history).</param>
    public static DashboardMetrics Compute(
        IReadOnlyCollection<ApplicationFacts> applications,
        IReadOnlySet<int> everOfferedIds,
        DateTime utcNow,
        int staleThresholdDays)
    {
        var live = applications.Where(a => !a.IsArchived).ToList();
        var awaiting = live.Where(a => a.Status == ApplicationStatus.Applied).ToList();

        return new DashboardMetrics(
            TotalApplications: applications.Count,
            ActiveApplications: live.Count(a => ApplicationStatuses.IsActive(a.Status)),
            InterviewsInProgress: live.Count(a => ApplicationStatuses.IsInterview(a.Status)),
            OffersReceived: applications.Count(a => everOfferedIds.Contains(a.Id) || a.Status == ApplicationStatus.OfferReceived),
            RejectedApplications: applications.Count(a => a.Status == ApplicationStatus.Rejected),
            AwaitingResponse: awaiting.Count,
            StaleApplications: awaiting.Count(a => IsStale(a, utcNow, staleThresholdDays)));
    }

    /// <summary>Applied, not archived, and no status change for at least N days.</summary>
    public static bool IsStale(ApplicationFacts application, DateTime utcNow, int staleThresholdDays) =>
        application is { Status: ApplicationStatus.Applied, IsArchived: false }
        && utcNow - application.StatusChangedAt >= TimeSpan.FromDays(staleThresholdDays);
}
