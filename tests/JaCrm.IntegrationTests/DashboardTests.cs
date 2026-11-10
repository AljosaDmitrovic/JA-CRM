using JaCrm.Api.Domain;
using JaCrm.Api.Features.Dashboard;
using JaCrm.IntegrationTests.Infrastructure;

namespace JaCrm.IntegrationTests;

public sealed class DashboardTests : IAsyncLifetime
{
    private readonly AppFactory _factory = new();
    private TestApi _api = null!;

    public async ValueTask InitializeAsync() => _api = new TestApi(await _factory.CreateAuthenticatedClientAsync());

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task An_empty_database_has_zero_metrics_and_an_empty_pipeline()
    {
        var dashboard = await _api.Http.GetJsonAsync<DashboardResponse>("/api/dashboard");

        Assert.Equal(new DashboardMetrics(0, 0, 0, 0, 0, 0, 0), dashboard.Metrics);
        Assert.Equal(5, dashboard.Pipeline.Count);
        Assert.All(dashboard.Pipeline, p => Assert.Equal(0, p.Count));
        Assert.Empty(dashboard.RecentActivity);
    }

    [Fact]
    public async Task Metrics_follow_the_specified_definitions_on_real_records()
    {
        var applied = await _api.CreateApplicationAsync("Applied Role", "Co 1", status: "Applied");
        var interviewing = await _api.CreateApplicationAsync("Interview Role", "Co 2", status: "TechnicalInterview");
        var offerThenWithdrawn = await _api.CreateApplicationAsync("Offer Role", "Co 3", status: "FinalInterview");
        await _api.MoveAsync(offerThenWithdrawn, "OfferReceived");
        await _api.MoveAsync(offerThenWithdrawn, "Withdrawn");
        await _api.Http.PostJsonAsync($"/api/applications/{offerThenWithdrawn}/archive");
        var rejected = await _api.CreateApplicationAsync("Rejected Role", "Co 4", status: "Applied");
        await _api.MoveAsync(rejected, "Rejected");
        await _api.CreateApplicationAsync("Pipeline Role", "Co 5", status: "Interested");
        _factory.Time.Advance(TimeSpan.FromDays(20));
        await _api.CreateApplicationAsync("Fresh Applied Role", "Co 6", status: "Applied");

        var dashboard = await _api.Http.GetJsonAsync<DashboardResponse>("/api/dashboard");

        var m = dashboard.Metrics;
        Assert.Equal(6, m.TotalApplications);
        Assert.Equal(3, m.ActiveApplications);
        Assert.Equal(1, m.InterviewsInProgress);
        Assert.Equal(1, m.OffersReceived);
        Assert.Equal(1, m.RejectedApplications);
        Assert.Equal(2, m.AwaitingResponse);
        Assert.Equal(1, m.StaleApplications);
        var stale = Assert.Single(dashboard.Stale);
        Assert.Equal(applied, stale.Id);
        Assert.Equal(20, stale.DaysSinceChange);
        Assert.Equal(1, dashboard.Pipeline.Single(p => p.Phase == ApplicationPhase.Interviewing).Count);
        Assert.Equal(2, dashboard.Pipeline.Single(p => p.Phase == ApplicationPhase.Applied).Count);
        Assert.Equal(1, dashboard.Pipeline.Single(p => p.Phase == ApplicationPhase.Closed).Count);
        Assert.Contains(dashboard.RecentActivity, a => a.ApplicationId == interviewing && a.Kind == ActivityKind.Created);
    }

    [Fact]
    public async Task The_stale_threshold_comes_from_settings()
    {
        await _api.CreateApplicationAsync("Waiting", "Waiting Co", status: "Applied");
        _factory.Time.Advance(TimeSpan.FromDays(8));
        await _api.Http.PutJsonAsync("/api/settings", new
        {
            theme = "System", baseCurrency = "EUR", workingHoursPerMonth = 168, staleThresholdDays = 7, dateFormat = "DayMonthYear",
        });

        var dashboard = await _api.Http.GetJsonAsync<DashboardResponse>("/api/dashboard");

        Assert.Equal(7, dashboard.StaleThresholdDays);
        Assert.Equal(1, dashboard.Metrics.StaleApplications);
    }

    [Fact]
    public async Task Upcoming_deadlines_are_the_next_30_days_without_a_submitted_application()
    {
        var company = await _api.CreateCompanyAsync("Deadline Co");
        var noApplication = await _api.CreateVacancyAsync("No Application", company.Id, b => b["applicationDeadline"] = "2026-11-15");
        var pipeline = await _api.CreateVacancyAsync("Pipeline", company.Id, b => b["applicationDeadline"] = "2026-11-10");
        await _api.ConvertAsync(pipeline.Id, status: "ReadyToApply");
        var submitted = await _api.CreateVacancyAsync("Submitted", company.Id, b => b["applicationDeadline"] = "2026-11-20");
        await _api.ConvertAsync(submitted.Id, status: "Applied");
        await _api.CreateVacancyAsync("Too Far", company.Id, b => b["applicationDeadline"] = "2026-12-11");
        await _api.CreateVacancyAsync("Past", company.Id, b => b["applicationDeadline"] = "2026-11-09");
        var archived = await _api.CreateVacancyAsync("Archived", company.Id, b => b["applicationDeadline"] = "2026-11-12");
        await _api.Http.PostJsonAsync($"/api/vacancies/{archived.Id}/archive");

        var dashboard = await _api.Http.GetJsonAsync<DashboardResponse>("/api/dashboard");

        Assert.Equal([pipeline.Id, noApplication.Id], dashboard.UpcomingDeadlines.Select(d => d.VacancyId));
        Assert.Equal([0, 5], dashboard.UpcomingDeadlines.Select(d => d.DaysLeft));
    }

    [Fact]
    public async Task Recent_activity_merges_creations_status_changes_and_edits_newest_first()
    {
        var id = await _api.CreateApplicationAsync("Activity", "Activity Co");
        _factory.Time.Advance(TimeSpan.FromMinutes(5));
        await _api.MoveAsync(id, "Applied");
        _factory.Time.Advance(TimeSpan.FromMinutes(5));
        await _api.Http.PutJsonAsync($"/api/applications/{id}", new { priority = "High", notes = "Follow up Friday" });

        var dashboard = await _api.Http.GetJsonAsync<DashboardResponse>("/api/dashboard");

        Assert.Equal(
            [ActivityKind.Updated, ActivityKind.StatusChanged, ActivityKind.Created],
            dashboard.RecentActivity.Where(a => a.ApplicationId == id).Select(a => a.Kind));
    }
}
