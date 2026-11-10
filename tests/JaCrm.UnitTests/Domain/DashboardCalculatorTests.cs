using JaCrm.Api.Domain;

namespace JaCrm.UnitTests.Domain;

public sealed class DashboardCalculatorTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    private static ApplicationFacts App(int id, ApplicationStatus status, bool archived = false, int daysSinceChange = 0) =>
        new(id, status, archived, Now.AddDays(-daysSinceChange));

    [Fact]
    public void Total_counts_every_application_including_archived()
    {
        var metrics = Compute([App(1, ApplicationStatus.Interested), App(2, ApplicationStatus.Rejected, archived: true)]);

        Assert.Equal(2, metrics.TotalApplications);
    }

    [Fact]
    public void Active_is_applied_interviewing_or_offer_and_not_archived()
    {
        var metrics = Compute([
            App(1, ApplicationStatus.Interested),
            App(2, ApplicationStatus.ReadyToApply),
            App(3, ApplicationStatus.Applied),
            App(4, ApplicationStatus.TechnicalInterview),
            App(5, ApplicationStatus.OfferReceived),
            App(6, ApplicationStatus.Rejected),
            App(7, ApplicationStatus.Applied, archived: true),
        ]);

        Assert.Equal(3, metrics.ActiveApplications);
    }

    [Fact]
    public void Interviews_in_progress_are_the_four_interview_statuses_not_archived()
    {
        var metrics = Compute([
            App(1, ApplicationStatus.RecruiterInterview),
            App(2, ApplicationStatus.TechnicalInterview),
            App(3, ApplicationStatus.HiringManagerInterview),
            App(4, ApplicationStatus.FinalInterview),
            App(5, ApplicationStatus.FinalInterview, archived: true),
            App(6, ApplicationStatus.OfferReceived),
        ]);

        Assert.Equal(4, metrics.InterviewsInProgress);
    }

    [Fact]
    public void Offers_come_from_status_history_and_include_archived()
    {
        var applications = new[]
        {
            App(1, ApplicationStatus.OfferReceived),
            App(2, ApplicationStatus.Withdrawn, archived: true),
            App(3, ApplicationStatus.Rejected),
            App(4, ApplicationStatus.Applied),
        };

        var metrics = DashboardCalculator.Compute(applications, new HashSet<int> { 1, 2, 3 }, Now, 14);

        Assert.Equal(3, metrics.OffersReceived);
    }

    [Fact]
    public void Rejected_is_the_current_status_including_archived()
    {
        var metrics = Compute([
            App(1, ApplicationStatus.Rejected),
            App(2, ApplicationStatus.Rejected, archived: true),
            App(3, ApplicationStatus.Withdrawn),
        ]);

        Assert.Equal(2, metrics.RejectedApplications);
    }

    [Fact]
    public void Awaiting_response_is_applied_and_not_archived_with_stale_after_the_threshold()
    {
        var metrics = Compute([
            App(1, ApplicationStatus.Applied, daysSinceChange: 3),
            App(2, ApplicationStatus.Applied, daysSinceChange: 14),
            App(3, ApplicationStatus.Applied, daysSinceChange: 30),
            App(4, ApplicationStatus.Applied, archived: true, daysSinceChange: 30),
            App(5, ApplicationStatus.RecruiterInterview, daysSinceChange: 30),
        ]);

        Assert.Equal(3, metrics.AwaitingResponse);
        Assert.Equal(2, metrics.StaleApplications);
    }

    [Fact]
    public void Stale_follows_the_configured_threshold()
    {
        var application = App(1, ApplicationStatus.Applied, daysSinceChange: 7);

        Assert.True(DashboardCalculator.IsStale(application, Now, staleThresholdDays: 7));
        Assert.False(DashboardCalculator.IsStale(application, Now, staleThresholdDays: 8));
    }

    private static DashboardMetrics Compute(ApplicationFacts[] applications) =>
        DashboardCalculator.Compute(applications, new HashSet<int>(), Now, 14);
}
