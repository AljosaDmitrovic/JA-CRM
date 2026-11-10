using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using JaCrm.Api.Common;
using JaCrm.Api.Data;
using JaCrm.Api.Domain;
using JaCrm.Api.Features.Companies;
using JaCrm.Api.Features.Dashboard;
using JaCrm.Api.Features.DataTransfer;
using JaCrm.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JaCrm.IntegrationTests;

public sealed class DataTransferTests : IAsyncLifetime
{
    private readonly AppFactory _source = new();
    private readonly AppFactory _target = new();
    private TestApi _sourceApi = null!;
    private TestApi _targetApi = null!;

    public async ValueTask InitializeAsync()
    {
        _sourceApi = new TestApi(await _source.CreateAuthenticatedClientAsync());
        _targetApi = new TestApi(await _target.CreateAuthenticatedClientAsync());
    }

    public async ValueTask DisposeAsync()
    {
        await _source.DisposeAsync();
        await _target.DisposeAsync();
    }

    [Fact]
    public async Task Export_then_import_round_trips_without_data_loss()
    {
        await SeedSourceAsync();
        await _targetApi.CreateCompanyAsync("Replaced By Import");
        var exported = await ExportAsync(_sourceApi);

        var response = await ImportAsync(_targetApi, exported);
        var summary = await response.ReadAsync<ImportSummary>();
        var reExported = await ExportAsync(_targetApi);

        var original = JsonNode.Parse(exported)!.AsObject();
        var restored = JsonNode.Parse(reExported)!.AsObject();
        original.Remove("exportedAt");
        restored.Remove("exportedAt");
        Assert.True(JsonNode.DeepEquals(original, restored), "The re-exported data differs from the original export.");
        Assert.Equal(original["companies"]!.AsArray().Count, summary.Companies);
        Assert.True(summary.StatusHistory > summary.Applications);
        Assert.True(File.Exists(Path.Combine(_target.DataDirectory, "backups", summary.BackupFile)));
        Assert.DoesNotContain((await _targetApi.ListAsync<CompanyListItem>("/api/companies?archived=All")).Items, c => c.Name == "Replaced By Import");
    }

    [Fact]
    public async Task Imported_data_behaves_like_the_original()
    {
        await SeedSourceAsync();
        await ImportAsync(_targetApi, await ExportAsync(_sourceApi));

        var source = await _sourceApi.Http.GetJsonAsync<DashboardResponse>("/api/dashboard");
        var target = await _targetApi.Http.GetJsonAsync<DashboardResponse>("/api/dashboard");

        Assert.Equal(source.Metrics, target.Metrics);
        Assert.Equal(source.UpcomingDeadlines.Select(d => d.VacancyId), target.UpcomingDeadlines.Select(d => d.VacancyId));
        // New records continue after the imported ids.
        var company = await _targetApi.CreateCompanyAsync("After Import");
        Assert.True(company.Id > target.Pipeline.Sum(p => p.Count));
    }

    [Fact]
    public async Task Import_requires_explicit_confirmation()
    {
        var response = await _targetApi.Http.PostAsync("/api/data/import", Json(await ExportAsync(_targetApi)));

        Assert.Contains("confirm", (await response.ValidationErrorsAsync()).Keys);
    }

    [Fact]
    public async Task An_invalid_file_is_rejected_before_anything_is_deleted()
    {
        await _targetApi.CreateCompanyAsync("Survivor");
        var document = JsonNode.Parse(await ExportAsync(_targetApi))!.AsObject();
        document["vacancies"] = new JsonArray(new JsonObject
        {
            ["id"] = 1, ["companyId"] = 999, ["title"] = "Orphan", ["openToMyLocation"] = "Unknown", ["relocationSupport"] = "Unknown",
            ["requiredTechnologyIds"] = new JsonArray(), ["preferredTechnologyIds"] = new JsonArray(),
            ["isArchived"] = false, ["createdAt"] = "2026-11-01T00:00:00Z", ["updatedAt"] = "2026-11-01T00:00:00Z",
        });

        var response = await ImportAsync(_targetApi, document.ToJsonString());

        Assert.Contains("vacancies[0].companyId", (await response.ValidationErrorsAsync()).Keys);
        Assert.Contains((await _targetApi.ListAsync<CompanyListItem>("/api/companies")).Items, c => c.Name == "Survivor");
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{"schemaVersion": 99}""")]
    [InlineData("""{"schemaVersion": 1}""")]
    public async Task Malformed_or_incompatible_files_are_rejected(string body)
    {
        var response = await ImportAsync(_targetApi, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Csv_exports_use_labels_and_defuse_formulas()
    {
        var company = await _sourceApi.CreateCompanyAsync("=HYPERLINK(\"http://evil\")");
        var vacancy = await _sourceApi.CreateVacancyAsync("Engineer, Platform", company.Id, b => b["workArrangement"] = "OnSite");
        await _sourceApi.ConvertAsync(vacancy.Id, status: "ReadyToApply");

        var response = await _sourceApi.Http.GetAsync("/api/data/export/vacancies.csv");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var text = Encoding.UTF8.GetString(bytes.AsSpan(3));

        await response.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        Assert.StartsWith("Id,Title,Company,Seniority,Work arrangement", text, StringComparison.Ordinal);
        Assert.Contains("\"Engineer, Platform\",\"'=HYPERLINK(\"\"http://evil\"\")\"", text, StringComparison.Ordinal);
        Assert.Contains(",On-site,", text, StringComparison.Ordinal);
        Assert.Contains("Ready to Apply", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("companies")]
    [InlineData("applications")]
    [InlineData("status-history")]
    public async Task Every_entity_has_a_csv_export(string entity)
    {
        await _sourceApi.CreateApplicationAsync("Csv Role", "Csv Co");

        var response = await _sourceApi.Http.GetAsync($"/api/data/export/{entity}.csv");

        await response.EnsureStatusAsync(HttpStatusCode.OK);
        var lines = (await response.Content.ReadAsStringAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public async Task Unknown_csv_exports_are_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _sourceApi.Http.GetAsync("/api/data/export/secrets.csv")).StatusCode);
    }

    private async Task SeedSourceAsync()
    {
        using var scope = _source.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await DevSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<Clock>()));
        Assert.True(await db.Applications.CountAsync() > 5);
        // Exercise the pursuit fields too, so the round trip covers them.
        var application = await db.Applications.OrderBy(a => a.Id).FirstAsync();
        application.ExpectedSalary = new SalaryValue(6500.5m, "CHF", PayPeriod.Monthly, SalaryType.Gross);
        application.CvVersion = "EU v3";
        application.Notes = "Ünïcödé — \"quotes\", commas, and\nnew lines";
        await db.SaveChangesAsync();
    }

    private static async Task<string> ExportAsync(TestApi api)
    {
        var response = await api.Http.GetAsync("/api/data/export");
        await response.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        return await response.Content.ReadAsStringAsync();
    }

    private static Task<HttpResponseMessage> ImportAsync(TestApi api, string json) =>
        api.Http.PostAsync("/api/data/import?confirm=replace-all", Json(json));

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
