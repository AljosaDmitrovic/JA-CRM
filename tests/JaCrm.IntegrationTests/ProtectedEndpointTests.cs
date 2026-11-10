using System.Net;
using System.Net.Http.Json;
using JaCrm.IntegrationTests.Infrastructure;

namespace JaCrm.IntegrationTests;

/// <summary>Every API endpoint except login/logout/session requires the session cookie.</summary>
public sealed class ProtectedEndpointTests(AppFactory factory) : IClassFixture<AppFactory>
{
    [Theory]
    [InlineData("GET", "/api/meta")]
    [InlineData("GET", "/api/settings")]
    [InlineData("PUT", "/api/settings")]
    [InlineData("PUT", "/api/settings/exchange-rates")]
    [InlineData("GET", "/api/dashboard")]
    [InlineData("GET", "/api/search?q=a")]
    [InlineData("GET", "/api/lookups")]
    [InlineData("GET", "/api/companies")]
    [InlineData("GET", "/api/companies/options")]
    [InlineData("GET", "/api/companies/duplicates?name=a")]
    [InlineData("GET", "/api/companies/1")]
    [InlineData("POST", "/api/companies")]
    [InlineData("PUT", "/api/companies/1")]
    [InlineData("POST", "/api/companies/1/archive")]
    [InlineData("DELETE", "/api/companies/1")]
    [InlineData("GET", "/api/vacancies")]
    [InlineData("GET", "/api/vacancies/1")]
    [InlineData("POST", "/api/vacancies")]
    [InlineData("PUT", "/api/vacancies/1")]
    [InlineData("POST", "/api/vacancies/1/convert")]
    [InlineData("DELETE", "/api/vacancies/1")]
    [InlineData("GET", "/api/applications")]
    [InlineData("GET", "/api/applications/board")]
    [InlineData("GET", "/api/applications/1")]
    [InlineData("POST", "/api/applications")]
    [InlineData("PUT", "/api/applications/1")]
    [InlineData("POST", "/api/applications/1/status")]
    [InlineData("POST", "/api/applications/1/archive")]
    [InlineData("DELETE", "/api/applications/1")]
    [InlineData("GET", "/api/data/export")]
    [InlineData("GET", "/api/data/export/companies.csv")]
    [InlineData("POST", "/api/data/import?confirm=replace-all")]
    public async Task Requires_a_session(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { });
        }

        var response = await factory.CreateAnonymousClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
