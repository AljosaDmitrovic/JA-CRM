using System.Net;
using JaCrm.Api.Domain;
using JaCrm.Api.Features.Vacancies;
using JaCrm.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.IntegrationTests;

public sealed class VacancyTests : IAsyncLifetime
{
    private readonly AppFactory _factory = new();
    private TestApi _api = null!;

    public async ValueTask InitializeAsync()
    {
        _api = new TestApi(await _factory.CreateAuthenticatedClientAsync());
        await (await _api.Http.PutJsonAsync("/api/settings/exchange-rates", new
        {
            rates = new[] { new { currency = "CHF", rateToBase = 1.05m }, new { currency = "RSD", rateToBase = 0.0085m } },
        })).EnsureStatusAsync(HttpStatusCode.OK);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task A_vacancy_can_create_its_company_inline_in_the_same_request()
    {
        var response = await _api.Http.PostJsonAsync("/api/vacancies", new { title = "Backend Engineer", newCompany = new { name = "Inline Co" } });

        var vacancy = await response.ReadAsync<VacancyDetail>(HttpStatusCode.Created);
        Assert.Equal("Inline Co", vacancy.CompanyName);
        Assert.Equal(new DateOnly(2026, 11, 10), vacancy.DateDiscovered);
        Assert.Equal(TriState.Unknown, vacancy.OpenToMyLocation);
    }

    [Fact]
    public async Task Title_and_company_are_required()
    {
        var noTitle = await _api.Http.PostJsonAsync("/api/vacancies", new { title = "", newCompany = new { name = "X" } });
        var noCompany = await _api.Http.PostJsonAsync("/api/vacancies", new { title = "Engineer" });

        Assert.Contains("title", (await noTitle.ValidationErrorsAsync()).Keys);
        Assert.Contains("companyId", (await noCompany.ValidationErrorsAsync()).Keys);
        Assert.Equal(0, await _factory.WithDbAsync(db => db.Companies.CountAsync(c => c.Name == "X")));
    }

    [Fact]
    public async Task Technology_tags_are_reused_case_insensitively_and_required_wins_over_preferred()
    {
        var company = await _api.CreateCompanyAsync("Tags Co");
        await _api.CreateVacancyAsync("First", company.Id, b => b["requiredTechnologies"] = new[] { "C#", "PostgreSQL" });

        var second = await _api.CreateVacancyAsync("Second", company.Id, b =>
        {
            b["requiredTechnologies"] = new[] { "c#", "Kafka", "kafka" };
            b["preferredTechnologies"] = new[] { "Kafka", "postgresql" };
        });

        Assert.Equal(["C#", "Kafka"], second.RequiredTechnologies);
        Assert.Equal(["PostgreSQL"], second.PreferredTechnologies);
        Assert.Equal(3, await _factory.WithDbAsync(db => db.Technologies.CountAsync()));
    }

    [Fact]
    public async Task Posted_salary_is_validated_completely()
    {
        var company = await _api.CreateCompanyAsync("Salary Co");

        var response = await _api.Http.PostJsonAsync("/api/vacancies", new
        {
            title = "Engineer",
            companyId = company.Id,
            postedSalary = new { min = 90000, max = 80000, currency = "euro", period = (string?)null, type = "Gross" },
        });

        var errors = await response.ValidationErrorsAsync();
        Assert.Equal(["postedSalary.currency", "postedSalary.max", "postedSalary.period"], errors.Keys.Order());
    }

    [Fact]
    public async Task An_empty_salary_range_is_rejected()
    {
        var company = await _api.CreateCompanyAsync("Empty Salary Co");

        var response = await _api.Http.PostJsonAsync("/api/vacancies", new
        {
            title = "Engineer",
            companyId = company.Id,
            postedSalary = new { min = (decimal?)null, max = (decimal?)null, currency = "EUR", period = "Annual", type = "Gross" },
        });

        Assert.Contains("postedSalary.min", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task The_posted_range_is_normalized_to_annual_gross_in_the_base_currency()
    {
        var company = await _api.CreateCompanyAsync("Zurich Co");

        var vacancy = await _api.CreateVacancyAsync("Engineer", company.Id, b =>
            b["postedSalary"] = new { min = 8000, max = 10000, currency = "CHF", period = "Monthly", type = "Gross" });

        Assert.Equal(100_800m, vacancy.NormalizedSalary!.AnnualMin);
        Assert.Equal(126_000m, vacancy.NormalizedSalary.AnnualMax);
        Assert.Equal(Comparability.Comparable, vacancy.NormalizedSalary.Comparability);
    }

    [Fact]
    public async Task Filters_cover_technology_arrangement_location_seniority_employment_and_salary()
    {
        var company = await _api.CreateCompanyAsync("Filter Co");
        var match = await _api.CreateVacancyAsync("Filter Match", company.Id, b =>
        {
            b["requiredTechnologies"] = new[] { "Rust" };
            b["workArrangement"] = "Remote";
            b["openToMyLocation"] = "Yes";
            b["seniority"] = "Senior";
            b["employmentType"] = "Contractor";
            b["country"] = "Netherlands";
            b["postedSalary"] = new { min = 6000, max = 7000, currency = "EUR", period = "Monthly", type = "Gross" };
        });
        await _api.CreateVacancyAsync("Filter Miss", company.Id, b =>
        {
            b["requiredTechnologies"] = new[] { "Go" };
            b["workArrangement"] = "OnSite";
            b["postedSalary"] = new { min = 6000, max = 7000, currency = "EUR", period = "Monthly", type = "Net" };
        });

        async Task<int[]> Ids(string query) =>
            (await _api.ListAsync<VacancyListItem>($"/api/vacancies?search=filter&{query}")).Items.Select(v => v.Id).ToArray();

        Assert.Equal([match.Id], await Ids("tech=rust"));
        Assert.Equal([match.Id], await Ids("workArrangement=Remote"));
        Assert.Equal([match.Id], await Ids("openToMyLocation=Yes"));
        Assert.Equal([match.Id], await Ids("seniority=Senior"));
        Assert.Equal([match.Id], await Ids("employmentType=Contractor"));
        Assert.Equal([match.Id], await Ids("country=Netherlands"));
        // 72k–84k annual gross matches; the net range is not comparable, so it never matches (D-25).
        Assert.Equal([match.Id], await Ids("salaryMin=80000"));
        Assert.Empty(await Ids("salaryMin=90000"));
        Assert.Equal(2, (await Ids("")).Length);
    }

    [Fact]
    public async Task Search_covers_title_company_technology_location_and_notes()
    {
        var company = await _api.CreateCompanyAsync("Searchable Holdings");
        var vacancy = await _api.CreateVacancyAsync("Quantum Plumber", company.Id, b =>
        {
            b["requiredTechnologies"] = new[] { "Elixir" };
            b["city"] = "Ljubljana";
            b["notes"] = "Met the CTO at a meetup";
        });

        foreach (var term in new[] { "quantum", "searchable", "elixir", "ljubljana", "meetup" })
        {
            var result = await _api.ListAsync<VacancyListItem>($"/api/vacancies?search={term}");
            Assert.Contains(result.Items, v => v.Id == vacancy.Id);
        }
    }

    [Fact]
    public async Task Sorting_by_deadline_puts_empty_deadlines_last()
    {
        var company = await _api.CreateCompanyAsync("Deadline Sort Co");
        var later = await _api.CreateVacancyAsync("Sortable Later", company.Id, b => b["applicationDeadline"] = "2026-12-20");
        var none = await _api.CreateVacancyAsync("Sortable None", company.Id);
        var sooner = await _api.CreateVacancyAsync("Sortable Sooner", company.Id, b => b["applicationDeadline"] = "2026-11-20");

        var asc = await _api.ListAsync<VacancyListItem>("/api/vacancies?search=sortable&sort=deadline&dir=asc");
        var desc = await _api.ListAsync<VacancyListItem>("/api/vacancies?search=sortable&sort=deadline&dir=desc");

        Assert.Equal([sooner.Id, later.Id, none.Id], asc.Items.Select(v => v.Id));
        Assert.Equal([later.Id, sooner.Id, none.Id], desc.Items.Select(v => v.Id));
    }

    [Fact]
    public async Task Paging_returns_the_requested_slice_and_the_total()
    {
        var company = await _api.CreateCompanyAsync("Paging Co");
        for (var i = 0; i < 5; i++)
        {
            await _api.CreateVacancyAsync($"Pageable {i}", company.Id);
        }

        var page = await _api.ListAsync<VacancyListItem>("/api/vacancies?search=pageable&sort=title&page=2&pageSize=2");

        Assert.Equal(5, page.Total);
        Assert.Equal(["Pageable 2", "Pageable 3"], page.Items.Select(v => v.Title));
    }

    [Fact]
    public async Task A_vacancy_with_an_application_is_archived_not_deleted()
    {
        var company = await _api.CreateCompanyAsync("Keep Co");
        var withApplication = await _api.CreateVacancyAsync("Kept", company.Id);
        await _api.ConvertAsync(withApplication.Id);
        var without = await _api.CreateVacancyAsync("Disposable", company.Id);

        Assert.Equal(HttpStatusCode.Conflict, (await _api.Http.DeleteAsync($"/api/vacancies/{withApplication.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _api.Http.DeleteAsync($"/api/vacancies/{without.Id}")).StatusCode);
        await (await _api.Http.PostJsonAsync($"/api/vacancies/{withApplication.Id}/archive")).EnsureStatusAsync(HttpStatusCode.NoContent);
        Assert.True((await _api.Http.GetJsonAsync<VacancyDetail>($"/api/vacancies/{withApplication.Id}")).IsArchived);
    }

    [Fact]
    public async Task Editing_replaces_fields_and_tags()
    {
        var company = await _api.CreateCompanyAsync("Edit Co");
        var vacancy = await _api.CreateVacancyAsync("Draft Title", company.Id, b => b["requiredTechnologies"] = new[] { "Java" });

        var response = await _api.Http.PutJsonAsync($"/api/vacancies/{vacancy.Id}", new
        {
            title = "Final Title",
            companyId = company.Id,
            requiredTechnologies = new[] { "Kotlin" },
            relocationSupport = "Yes",
            relocationNotes = "Visa sponsorship",
        });

        var updated = await response.ReadAsync<VacancyDetail>();
        Assert.Equal("Final Title", updated.Title);
        Assert.Equal(["Kotlin"], updated.RequiredTechnologies);
        Assert.Equal(TriState.Yes, updated.RelocationSupport);
        Assert.Equal(vacancy.DateDiscovered, updated.DateDiscovered);
    }
}
