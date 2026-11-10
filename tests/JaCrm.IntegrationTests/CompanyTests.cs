using System.Net;
using JaCrm.Api.Features.Companies;
using JaCrm.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JaCrm.IntegrationTests;

public sealed class CompanyTests : IAsyncLifetime
{
    private readonly AppFactory _factory = new();
    private TestApi _api = null!;

    public async ValueTask InitializeAsync() => _api = new TestApi(await _factory.CreateAuthenticatedClientAsync());

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task A_company_needs_only_a_name()
    {
        var company = await _api.CreateCompanyAsync("Quarry Labs");

        Assert.True(company.Id > 0);
        Assert.Equal("Quarry Labs", company.Name);
        Assert.Equal(AppFactory.Start.UtcDateTime, company.CreatedAt);
        Assert.False(company.IsArchived);
    }

    [Fact]
    public async Task Invalid_fields_are_reported_per_field()
    {
        var response = await _api.Http.PostJsonAsync("/api/companies", new { name = " ", website = "not a url", rating = 6 });

        var errors = await response.ValidationErrorsAsync();
        Assert.Equal(["name", "rating", "website"], errors.Keys.Order());
    }

    [Fact]
    public async Task Bare_domains_are_stored_as_https_urls()
    {
        var company = await _api.CreateCompanyAsync("Domainy", website: "domainy.example");

        Assert.Equal("https://domainy.example", company.Website);
    }

    [Fact]
    public async Task Likely_duplicates_are_found_by_name_or_domain_but_never_block()
    {
        var acme = await _api.CreateCompanyAsync("ACME, Inc.", website: "https://www.acme.example/jobs");

        var byName = await _api.Http.GetJsonAsync<List<DuplicateCompany>>("/api/companies/duplicates?name=acme%20gmbh");
        var byDomain = await _api.Http.GetJsonAsync<List<DuplicateCompany>>("/api/companies/duplicates?name=Other&website=acme.example");
        var self = await _api.Http.GetJsonAsync<List<DuplicateCompany>>($"/api/companies/duplicates?name=Acme&excludeId={acme.Id}");
        var duplicate = await _api.CreateCompanyAsync("Acme");

        Assert.Equal(DuplicateReason.Name, Assert.Single(byName).Reason);
        Assert.Equal(DuplicateReason.Domain, Assert.Single(byDomain).Reason);
        Assert.Empty(self);
        Assert.NotEqual(acme.Id, duplicate.Id);
    }

    [Fact]
    public async Task Renaming_updates_the_company()
    {
        var company = await _api.CreateCompanyAsync("Old Name");
        _factory.Time.Advance(TimeSpan.FromHours(1));

        var response = await _api.Http.PutJsonAsync($"/api/companies/{company.Id}", new { name = "New Name", rating = 4, size = "From51To200" });

        var updated = await response.ReadAsync<CompanyDetail>();
        Assert.Equal("New Name", updated.Name);
        Assert.Equal(4, updated.Rating);
        Assert.Equal(company.CreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt > company.UpdatedAt);
    }

    [Fact]
    public async Task A_company_without_references_can_be_deleted()
    {
        var company = await _api.CreateCompanyAsync("Short Lived");

        await (await _api.Http.DeleteAsync($"/api/companies/{company.Id}")).EnsureStatusAsync(HttpStatusCode.NoContent);

        Assert.Equal(HttpStatusCode.NotFound, (await _api.Http.GetAsync($"/api/companies/{company.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_company_with_vacancies_is_archived_not_deleted()
    {
        var company = await _api.CreateCompanyAsync("Referenced Co");
        await _api.CreateVacancyAsync("Engineer", company.Id);

        var delete = await _api.Http.DeleteAsync($"/api/companies/{company.Id}");
        await (await _api.Http.PostJsonAsync($"/api/companies/{company.Id}/archive")).EnsureStatusAsync(HttpStatusCode.NoContent);

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        var archived = await _api.Http.GetJsonAsync<CompanyDetail>($"/api/companies/{company.Id}");
        Assert.True(archived.IsArchived);
        Assert.Equal(AppFactory.Start.UtcDateTime, archived.ArchivedAt);
        Assert.Single(archived.Vacancies);
    }

    [Fact]
    public async Task The_database_itself_refuses_to_cascade_a_company_delete()
    {
        var company = await _api.CreateCompanyAsync("Guarded");
        await _api.CreateVacancyAsync("Engineer", company.Id);

        var error = await Record.ExceptionAsync(() => _factory.WithDbAsync(db =>
            db.Database.ExecuteSqlAsync($"DELETE FROM Companies WHERE Id = {company.Id}")));

        Assert.NotNull(error);
        Assert.Contains("FOREIGN KEY", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Archived_companies_are_hidden_by_default_and_listed_on_request()
    {
        var hidden = await _api.CreateCompanyAsync("Hidden Archive Co");
        await _api.Http.PostJsonAsync($"/api/companies/{hidden.Id}/archive");

        var active = await _api.ListAsync<CompanyListItem>("/api/companies?search=archive%20co");
        var archived = await _api.ListAsync<CompanyListItem>("/api/companies?search=archive%20co&archived=Archived");

        Assert.Empty(active.Items);
        Assert.Equal(hidden.Id, Assert.Single(archived.Items).Id);

        await _api.Http.PostJsonAsync($"/api/companies/{hidden.Id}/unarchive");
        Assert.Single((await _api.ListAsync<CompanyListItem>("/api/companies?search=archive%20co")).Items);
    }

    [Fact]
    public async Task The_list_searches_filters_sorts_and_counts()
    {
        var zed = await _api.CreateCompanyAsync("Zed Sorting Test", country: "Germany");
        var abc = await _api.CreateCompanyAsync("Abc Sorting Test", country: "Serbia");
        var vacancy = await _api.CreateVacancyAsync("Engineer", abc.Id);
        await _api.ConvertAsync(vacancy.Id, status: "Applied");

        var sorted = await _api.ListAsync<CompanyListItem>("/api/companies?search=sorting%20test&sort=name&dir=desc");
        var serbia = await _api.ListAsync<CompanyListItem>("/api/companies?search=sorting&country=Serbia");

        Assert.Equal([zed.Id, abc.Id], sorted.Items.Select(c => c.Id));
        var item = Assert.Single(serbia.Items);
        Assert.Equal((1, 1, 1), (item.VacancyCount, item.ApplicationCount, item.ActiveApplicationCount));
    }

    [Fact]
    public async Task The_detail_page_has_vacancies_applications_and_their_history()
    {
        var company = await _api.CreateCompanyAsync("Detail Co");
        var vacancy = await _api.CreateVacancyAsync("Platform Engineer", company.Id);
        var applicationId = await _api.ConvertAsync(vacancy.Id);
        await _api.MoveAsync(applicationId, "Applied");

        var detail = await _api.Http.GetJsonAsync<CompanyDetail>($"/api/companies/{company.Id}");

        Assert.Equal(vacancy.Id, Assert.Single(detail.Vacancies).Id);
        Assert.Equal(applicationId, Assert.Single(detail.Applications).Id);
        Assert.Equal(2, detail.History.Count);
        Assert.All(detail.History, h => Assert.Equal("Platform Engineer", h.JobTitle));
    }
}
