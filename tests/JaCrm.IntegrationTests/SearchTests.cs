using JaCrm.Api.Features.Search;
using JaCrm.IntegrationTests.Infrastructure;

namespace JaCrm.IntegrationTests;

public sealed class SearchTests : IAsyncLifetime
{
    private readonly AppFactory _factory = new();
    private TestApi _api = null!;

    public async ValueTask InitializeAsync() => _api = new TestApi(await _factory.CreateAuthenticatedClientAsync());

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Global_search_finds_every_record_type_including_archived()
    {
        var company = await _api.CreateCompanyAsync("Nebula Systems", country: "Austria");
        var vacancy = await _api.CreateVacancyAsync("Nebula Platform Engineer", company.Id);
        var applicationId = await _api.ConvertAsync(vacancy.Id);
        var archived = await _api.CreateVacancyAsync("Nebula Old Role", company.Id);
        await _api.Http.PostJsonAsync($"/api/vacancies/{archived.Id}/archive");

        var results = await _api.Http.GetJsonAsync<SearchResults>("/api/search?q=nebula");

        Assert.Equal(applicationId, Assert.Single(results.Applications).Id);
        Assert.Equal([vacancy.Id, archived.Id], results.Vacancies.Select(v => v.Id));
        Assert.True(results.Vacancies[1].IsArchived);
        Assert.Equal(company.Id, Assert.Single(results.Companies).Id);
    }

    [Fact]
    public async Task Search_treats_wildcards_literally()
    {
        await _api.CreateCompanyAsync("Hundred Percent Co");
        await _api.CreateCompanyAsync("100% Remote Co");

        var results = await _api.Http.GetJsonAsync<SearchResults>("/api/search?q=%25");

        Assert.Equal(["100% Remote Co"], results.Companies.Select(c => c.Name));
    }

    [Fact]
    public async Task An_empty_query_returns_nothing()
    {
        await _api.CreateCompanyAsync("Anything");

        var results = await _api.Http.GetJsonAsync<SearchResults>("/api/search?q=%20");

        Assert.Empty(results.Companies);
    }

    [Fact]
    public async Task Lookups_offer_distinct_values_already_in_use()
    {
        var company = await _api.CreateCompanyAsync("Lookup Co", country: "Serbia");
        await _api.CreateVacancyAsync("Role", company.Id, b =>
        {
            b["country"] = "serbia";
            b["city"] = "Novi Sad";
            b["requiredTechnologies"] = new[] { "Angular" };
        });

        var lookups = await _api.Http.GetJsonAsync<Lookups>("/api/lookups");

        Assert.Equal(["Serbia"], lookups.Countries);
        Assert.Equal(["Novi Sad"], lookups.Cities);
        Assert.Equal(["Angular"], lookups.Technologies);
    }
}
