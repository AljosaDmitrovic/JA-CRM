using System.Net;
using JaCrm.Api.Common;
using JaCrm.Api.Features.Applications;
using JaCrm.Api.Features.Companies;
using JaCrm.Api.Features.Vacancies;

namespace JaCrm.IntegrationTests.Infrastructure;

/// <summary>Typed shortcuts for arranging test data through the real API.</summary>
public sealed class TestApi(HttpClient client)
{
    public HttpClient Http => client;

    public async Task<CompanyDetail> CreateCompanyAsync(string name, string? website = null, string? country = null)
    {
        var response = await client.PostJsonAsync("/api/companies", new { name, website, country });
        return await response.ReadAsync<CompanyDetail>(HttpStatusCode.Created);
    }

    /// <summary>Creates a vacancy; extra fields override the minimal body (title + company).</summary>
    public async Task<VacancyDetail> CreateVacancyAsync(string title, int companyId, Action<Dictionary<string, object?>>? configure = null)
    {
        var body = new Dictionary<string, object?> { ["title"] = title, ["companyId"] = companyId };
        configure?.Invoke(body);
        var response = await client.PostJsonAsync("/api/vacancies", body);
        return await response.ReadAsync<VacancyDetail>(HttpStatusCode.Created);
    }

    public async Task<int> ConvertAsync(int vacancyId, string status = "Interested", string priority = "Medium")
    {
        var response = await client.PostJsonAsync($"/api/vacancies/{vacancyId}/convert", new { status, priority });
        return (await response.ReadAsync<CreatedId>(HttpStatusCode.Created)).Id;
    }

    /// <summary>Company + vacancy + application in one go; returns the application id.</summary>
    public async Task<int> CreateApplicationAsync(string title, string company, string status = "Interested", string priority = "Medium")
    {
        var response = await client.PostJsonAsync("/api/applications", new
        {
            newVacancy = new { title, newCompany = new { name = company } },
            status,
            priority,
        });
        return (await response.ReadAsync<CreatedId>(HttpStatusCode.Created)).Id;
    }

    public Task<ApplicationDetail> GetApplicationAsync(int id) => client.GetJsonAsync<ApplicationDetail>($"/api/applications/{id}");

    public async Task<ApplicationListItem> MoveAsync(int applicationId, string status, string? note = null)
    {
        var response = await client.PostJsonAsync($"/api/applications/{applicationId}/status", new { status, note });
        return await response.ReadAsync<ApplicationListItem>();
    }

    public Task<PagedResult<T>> ListAsync<T>(string url) => client.GetJsonAsync<PagedResult<T>>(url);
}
