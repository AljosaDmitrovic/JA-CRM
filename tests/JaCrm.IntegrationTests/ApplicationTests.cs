using System.Net;
using JaCrm.Api.Domain;
using JaCrm.Api.Features.Applications;
using JaCrm.Api.Features.Vacancies;
using JaCrm.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.IntegrationTests;

public sealed class ApplicationTests : IAsyncLifetime
{
    private readonly AppFactory _factory = new();
    private TestApi _api = null!;

    public async ValueTask InitializeAsync() => _api = new TestApi(await _factory.CreateAuthenticatedClientAsync());

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Converting_a_vacancy_snapshots_the_posting()
    {
        var company = await _api.CreateCompanyAsync("Snapshot Co");
        var vacancy = await _api.CreateVacancyAsync("Senior .NET Engineer", company.Id, b =>
        {
            b["description"] = "## Role\n\nBuild things.";
            b["requiredTechnologies"] = new[] { "C#", "Azure" };
            b["workArrangement"] = "Remote";
            b["remoteEligibility"] = "EU only";
            b["postedSalary"] = new { min = 70000, max = 90000, currency = "EUR", period = "Annual", type = "Gross" };
            b["url"] = "https://jobs.example/123";
        });

        var id = await _api.ConvertAsync(vacancy.Id, status: "ReadyToApply", priority: "High");
        var application = await _api.GetApplicationAsync(id);

        Assert.Equal(ApplicationStatus.ReadyToApply, application.Status);
        Assert.Equal(Priority.High, application.Priority);
        Assert.Equal("Senior .NET Engineer", application.Snapshot.JobTitle);
        Assert.Equal("Snapshot Co", application.Snapshot.CompanyName);
        Assert.Equal("## Role\n\nBuild things.", application.Snapshot.Description);
        Assert.Equal(["Azure", "C#"], application.Snapshot.RequiredTechnologies);
        Assert.Equal("EU only", application.Snapshot.RemoteEligibility);
        Assert.Equal(90000m, application.Snapshot.PostedSalary!.Max);
        Assert.Equal("https://jobs.example/123", application.Snapshot.Url);
        var initial = Assert.Single(application.History);
        Assert.Null(initial.FromStatus);
        Assert.Null(application.SubmittedOn);
    }

    [Fact]
    public async Task The_snapshot_never_changes_when_the_vacancy_is_edited_or_archived()
    {
        var company = await _api.CreateCompanyAsync("Immutable Co");
        var vacancy = await _api.CreateVacancyAsync("Original Title", company.Id, b => b["description"] = "Original description");
        var id = await _api.ConvertAsync(vacancy.Id);

        await (await _api.Http.PutJsonAsync($"/api/vacancies/{vacancy.Id}", new
        {
            title = "Edited Title",
            companyId = company.Id,
            description = "Edited description",
        })).EnsureStatusAsync(HttpStatusCode.OK);
        await _api.Http.PostJsonAsync($"/api/vacancies/{vacancy.Id}/archive");
        var application = await _api.GetApplicationAsync(id);

        Assert.Equal("Original Title", application.Snapshot.JobTitle);
        Assert.Equal("Original description", application.Snapshot.Description);
        Assert.Equal("Edited Title", application.VacancyTitle);
        Assert.True(application.VacancyIsArchived);
    }

    [Fact]
    public async Task A_vacancy_can_have_only_one_application()
    {
        var company = await _api.CreateCompanyAsync("Once Co");
        var vacancy = await _api.CreateVacancyAsync("Engineer", company.Id);
        await _api.ConvertAsync(vacancy.Id);

        var second = await _api.Http.PostJsonAsync($"/api/vacancies/{vacancy.Id}/convert", new { });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, await _factory.WithDbAsync(db => db.Applications.CountAsync(a => a.VacancyId == vacancy.Id)));
    }

    [Fact]
    public async Task The_unique_constraint_backs_up_the_one_application_rule()
    {
        var company = await _api.CreateCompanyAsync("Constraint Co");
        var vacancy = await _api.CreateVacancyAsync("Engineer", company.Id);
        await _api.ConvertAsync(vacancy.Id);

        var error = await Record.ExceptionAsync(() => _factory.WithDbAsync(async db =>
        {
            var loaded = await db.Vacancies.Include(v => v.Company).Include(v => v.RequiredTechnologies).Include(v => v.PreferredTechnologies)
                .SingleAsync(v => v.Id == vacancy.Id);
            db.Applications.Add(Application.Create(loaded, ApplicationStatus.Interested, Priority.Low, DateTime.UtcNow, new DateOnly(2026, 11, 10)));
            return await db.SaveChangesAsync();
        }));

        Assert.IsType<DbUpdateException>(error);
    }

    [Fact]
    public async Task Creating_directly_captures_a_new_vacancy_and_company_in_one_step()
    {
        var response = await _api.Http.PostJsonAsync("/api/applications", new
        {
            newVacancy = new { title = "Staff Engineer", newCompany = new { name = "Direct Co" }, url = "direct.example/jobs/1" },
            status = "Applied",
            priority = "Low",
        });

        var created = await response.ReadAsync<CreatedId>(HttpStatusCode.Created);
        var application = await _api.GetApplicationAsync(created.Id);
        Assert.Equal("Staff Engineer", application.VacancyTitle);
        Assert.Equal("Direct Co", application.CompanyName);
        Assert.Equal("https://direct.example/jobs/1", application.Snapshot.Url);
        Assert.Equal(new DateOnly(2026, 11, 10), application.SubmittedOn);
    }

    [Fact]
    public async Task Direct_creation_validates_the_inline_vacancy_and_writes_nothing_on_error()
    {
        var response = await _api.Http.PostJsonAsync("/api/applications", new
        {
            newVacancy = new { title = "", newCompany = new { name = "Never Saved Co" } },
        });

        Assert.Contains("newVacancy.title", (await response.ValidationErrorsAsync()).Keys);
        Assert.False(await _factory.WithDbAsync(db => db.Companies.AnyAsync(c => c.Name == "Never Saved Co")));
    }

    [Fact]
    public async Task Missing_company_on_direct_creation_points_at_the_inline_field()
    {
        var response = await _api.Http.PostJsonAsync("/api/applications", new { newVacancy = new { title = "Engineer" } });

        Assert.Contains("newVacancy.companyId", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task A_status_change_writes_history_and_sets_the_submission_date()
    {
        var id = await _api.CreateApplicationAsync("Move Me", "Move Co");
        _factory.Time.Advance(TimeSpan.FromDays(2));

        var moved = await _api.MoveAsync(id, "Applied", note: "Sent via careers page");
        _factory.Time.Advance(TimeSpan.FromDays(3));
        await _api.MoveAsync(id, "TechnicalInterview");
        var application = await _api.GetApplicationAsync(id);

        Assert.Equal(ApplicationStatus.Applied, moved.Status);
        Assert.Equal(ApplicationPhase.Applied, moved.Phase);
        Assert.Equal(ApplicationStatus.TechnicalInterview, application.Status);
        Assert.Equal(new DateOnly(2026, 11, 12), application.SubmittedOn);
        Assert.Equal(
            [(null, ApplicationStatus.Interested), (ApplicationStatus.Interested, ApplicationStatus.Applied), (ApplicationStatus.Applied, ApplicationStatus.TechnicalInterview)],
            application.History.OrderBy(h => h.ChangedAt).Select(h => (h.FromStatus, h.ToStatus)));
        Assert.Equal("Sent via careers page", application.History.Single(h => h.ToStatus == ApplicationStatus.Applied).Note);
        Assert.Equal(AppFactory.Start.UtcDateTime.AddDays(5), application.StatusChangedAt);
    }

    [Fact]
    public async Task Moving_to_the_same_status_or_an_unknown_one_is_rejected()
    {
        var id = await _api.CreateApplicationAsync("Same", "Same Co", status: "Applied");

        var same = await _api.Http.PostJsonAsync($"/api/applications/{id}/status", new { status = "Applied" });
        var missing = await _api.Http.PostJsonAsync($"/api/applications/{id}/status", new { });

        Assert.Contains("status", (await same.ValidationErrorsAsync()).Keys);
        Assert.Contains("status", (await missing.ValidationErrorsAsync()).Keys);
        Assert.Single((await _api.GetApplicationAsync(id)).History);
    }

    [Fact]
    public async Task Status_and_history_are_written_in_one_transaction()
    {
        var id = await _api.CreateApplicationAsync("Atomic", "Atomic Co");
        // Make every history insert fail inside SQLite, after the application row was updated.
        await _factory.WithDbAsync(db => db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER fail_history BEFORE INSERT ON ApplicationStatusHistory BEGIN SELECT RAISE(ABORT, 'history unavailable'); END;"));

        var response = await _api.Http.PostJsonAsync($"/api/applications/{id}/status", new { status = "Applied" });

        await _factory.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_history;"));
        Assert.True((int)response.StatusCode >= 400);
        var application = await _api.GetApplicationAsync(id);
        Assert.Equal(ApplicationStatus.Interested, application.Status);
        Assert.Null(application.SubmittedOn);
        Assert.Single(application.History);
    }

    [Fact]
    public async Task Editing_validates_salaries_and_dates()
    {
        var id = await _api.CreateApplicationAsync("Edit", "Edit Co");

        var response = await _api.Http.PutJsonAsync($"/api/applications/{id}", new
        {
            priority = "High",
            submittedOn = "2026-12-01",
            expectedSalary = new { amount = -1, currency = "EUR", period = "Annual", type = "Gross" },
            offeredSalary = new { amount = 5000, currency = "EUR", period = "Monthly" },
        });

        var errors = await response.ValidationErrorsAsync();
        Assert.Equal(["expectedSalary.amount", "offeredSalary.type", "submittedOn"], errors.Keys.Order());
    }

    [Fact]
    public async Task Editing_saves_the_pursuit_and_normalizes_compensation()
    {
        var id = await _api.CreateApplicationAsync("Pursuit", "Pursuit Co");
        await (await _api.Http.PutJsonAsync("/api/settings/exchange-rates", new
        {
            rates = new[] { new { currency = "CHF", rateToBase = 1.05m } },
        })).EnsureStatusAsync(HttpStatusCode.OK);

        var response = await _api.Http.PutJsonAsync($"/api/applications/{id}", new
        {
            priority = "High",
            submittedOn = "2026-11-01",
            notes = "**Strong** match",
            reasonForApplying = "Domain",
            expectedSalary = new { amount = 9000, currency = "CHF", period = "Monthly", type = "Gross" },
            offeredSalary = new { amount = 6000, currency = "EUR", period = "Monthly", type = "Net" },
            compensationNotes = "13th salary",
            cvVersion = "EU-2026-11",
            coverLetter = "Short note",
        });

        var updated = await response.ReadAsync<ApplicationDetail>();
        Assert.Equal(Priority.High, updated.Priority);
        Assert.Equal(113_400m, updated.ExpectedNormalized!.AnnualMin);
        Assert.Equal(Comparability.Comparable, updated.ExpectedNormalized.Comparability);
        Assert.Equal(72_000m, updated.OfferedNormalized!.AnnualMin);
        Assert.Equal(Comparability.Net, updated.OfferedNormalized.Comparability);
        Assert.Equal("EU-2026-11", updated.CvVersion);
    }

    [Fact]
    public async Task Archiving_keeps_the_last_status_and_hides_the_application_by_default()
    {
        var id = await _api.CreateApplicationAsync("Archive Me", "Archive App Co", status: "Rejected");

        await (await _api.Http.PostJsonAsync($"/api/applications/{id}/archive")).EnsureStatusAsync(HttpStatusCode.NoContent);

        var application = await _api.GetApplicationAsync(id);
        Assert.True(application.IsArchived);
        Assert.Equal(ApplicationStatus.Rejected, application.Status);
        Assert.DoesNotContain((await _api.ListAsync<ApplicationListItem>("/api/applications?search=archive%20me")).Items, a => a.Id == id);
        Assert.DoesNotContain(await _api.Http.GetJsonAsync<List<ApplicationListItem>>("/api/applications/board"), a => a.Id == id);
        Assert.Contains((await _api.ListAsync<ApplicationListItem>("/api/applications?search=archive%20me&archived=Archived")).Items, a => a.Id == id);

        await _api.Http.PostJsonAsync($"/api/applications/{id}/unarchive");
        Assert.False((await _api.GetApplicationAsync(id)).IsArchived);
    }

    [Fact]
    public async Task Deleting_an_application_removes_its_history_but_keeps_the_vacancy()
    {
        var company = await _api.CreateCompanyAsync("Delete App Co");
        var vacancy = await _api.CreateVacancyAsync("Engineer", company.Id);
        var id = await _api.ConvertAsync(vacancy.Id);
        await _api.MoveAsync(id, "Applied");

        await (await _api.Http.DeleteAsync($"/api/applications/{id}")).EnsureStatusAsync(HttpStatusCode.NoContent);

        Assert.False(await _factory.WithDbAsync(db => db.StatusHistory.AnyAsync(h => h.ApplicationId == id)));
        var kept = await _api.Http.GetJsonAsync<VacancyDetail>($"/api/vacancies/{vacancy.Id}");
        Assert.Null(kept.ApplicationId);
    }

    [Fact]
    public async Task The_board_orders_cards_by_priority_then_last_update()
    {
        var low = await _api.CreateApplicationAsync("Board Low", "Board Co A", priority: "Low");
        _factory.Time.Advance(TimeSpan.FromMinutes(1));
        var highOld = await _api.CreateApplicationAsync("Board High Old", "Board Co B", priority: "High");
        _factory.Time.Advance(TimeSpan.FromMinutes(1));
        var highNew = await _api.CreateApplicationAsync("Board High New", "Board Co C", priority: "High");

        var board = await _api.Http.GetJsonAsync<List<ApplicationListItem>>("/api/applications/board?search=board");

        Assert.Equal([highNew, highOld, low], board.Select(a => a.Id));
    }

    [Fact]
    public async Task Lists_filter_by_phase_and_status_and_flag_stale_applications()
    {
        var applied = await _api.CreateApplicationAsync("Phase Applied", "Phase Co A", status: "Applied");
        var interview = await _api.CreateApplicationAsync("Phase Interview", "Phase Co B", status: "RecruiterInterview");
        await _api.CreateApplicationAsync("Phase Pipeline", "Phase Co C", status: "Researching");
        _factory.Time.Advance(TimeSpan.FromDays(15));

        var interviewing = await _api.ListAsync<ApplicationListItem>("/api/applications?search=phase&phase=Interviewing");
        var several = await _api.ListAsync<ApplicationListItem>("/api/applications?search=phase&status=Applied&status=RecruiterInterview");
        var stale = await _api.ListAsync<ApplicationListItem>("/api/applications?search=phase&stale=true");

        Assert.Equal([interview], interviewing.Items.Select(a => a.Id));
        Assert.Equal([applied, interview], several.Items.Select(a => a.Id).Order());
        var staleItem = Assert.Single(stale.Items);
        Assert.Equal(applied, staleItem.Id);
        Assert.True(staleItem.IsStale);
    }

    [Fact]
    public async Task The_offered_salary_represents_the_application_in_salary_filters()
    {
        var company = await _api.CreateCompanyAsync("Salary Filter Co");
        var vacancy = await _api.CreateVacancyAsync("Salary Filter Role", company.Id, b =>
            b["postedSalary"] = new { min = 50000, max = 60000, currency = "EUR", period = "Annual", type = "Gross" });
        var id = await _api.ConvertAsync(vacancy.Id);

        var beforeOffer = await _api.ListAsync<ApplicationListItem>("/api/applications?search=salary%20filter&salaryMin=55000");
        await _api.Http.PutJsonAsync($"/api/applications/{id}", new
        {
            offeredSalary = new { amount = 7000, currency = "EUR", period = "Monthly", type = "Gross" },
        });
        var afterOffer = await _api.ListAsync<ApplicationListItem>("/api/applications?search=salary%20filter&salaryMin=80000");

        Assert.Equal(SalarySource.Posted, Assert.Single(beforeOffer.Items).SalarySource);
        var offered = Assert.Single(afterOffer.Items);
        Assert.Equal(SalarySource.Offered, offered.SalarySource);
        Assert.Equal(84_000m, offered.NormalizedSalary!.AnnualMin);
    }

    [Fact]
    public async Task Lists_sort_by_company_and_filter_by_creation_date()
    {
        var b = await _api.CreateApplicationAsync("Dated", "Bbb Dated Co");
        _factory.Time.Advance(TimeSpan.FromDays(3));
        var a = await _api.CreateApplicationAsync("Dated", "Aaa Dated Co");

        var byCompany = await _api.ListAsync<ApplicationListItem>("/api/applications?search=dated&sort=company");
        var recent = await _api.ListAsync<ApplicationListItem>("/api/applications?search=dated&from=2026-11-12");

        Assert.Equal([a, b], byCompany.Items.Select(x => x.Id));
        Assert.Equal([a], recent.Items.Select(x => x.Id));
    }
}
