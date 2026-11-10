using JaCrm.Api.Data;
using JaCrm.Api.Domain;
using JaCrm.Api.Features.Applications;
using JaCrm.Api.Features.Dashboard;
using JaCrm.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JaCrm.IntegrationTests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task Data_and_sessions_survive_a_restart_on_the_same_data_folder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "jacrm-tests", Guid.NewGuid().ToString("N"));
        int applicationId;
        DashboardMetrics before;
        string cookie;
        try
        {
            await using (var first = new AppFactory { DataDirectory = folder, KeepData = true })
            {
                var client = await first.CreateAuthenticatedClientAsync();
                var api = new TestApi(client);
                applicationId = await api.CreateApplicationAsync("Restart Role", "Restart Co", status: "Applied");
                await api.MoveAsync(applicationId, "RecruiterInterview", "Before the restart");
                before = (await client.GetJsonAsync<DashboardResponse>("/api/dashboard")).Metrics;
                cookie = await LoginCookieAsync(first);
            }

            await using var second = new AppFactory { DataDirectory = folder };
            var restarted = new TestApi(await second.CreateAuthenticatedClientAsync());
            var application = await restarted.GetApplicationAsync(applicationId);
            var after = (await restarted.Http.GetJsonAsync<DashboardResponse>("/api/dashboard")).Metrics;

            Assert.Equal(ApplicationStatus.RecruiterInterview, application.Status);
            Assert.Equal("Before the restart", application.History.First().Note);
            Assert.Equal(before, after);

            // The cookie issued before the restart is still valid: the data-protection keys live
            // in the data folder too, so a rebuild does not sign the owner out.
            var anonymous = second.CreateAnonymousClient();
            anonymous.DefaultRequestHeaders.Add("Cookie", cookie);
            Assert.Equal(System.Net.HttpStatusCode.OK, (await anonymous.GetAsync("/api/settings")).StatusCode);
        }
        finally
        {
            // The second instance removes the folder on dispose; clean up if it did not get that far.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    private static async Task<string> LoginCookieAsync(AppFactory factory)
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Requested-With", "JaCrm");
        var response = await client.PostJsonAsync("/api/auth/login", new { password = AppFactory.Password });
        return response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
    }

    [Fact]
    public async Task The_database_passes_sqlite_integrity_and_foreign_key_checks()
    {
        await using var source = new AppFactory();
        await using var target = new AppFactory();
        await source.WithDbAsync(async db => await DevSeeder.SeedAsync(db, source.Services.GetRequiredService<Api.Common.Clock>()));
        var api = new TestApi(await source.CreateAuthenticatedClientAsync());
        var id = await api.CreateApplicationAsync("Integrity Role", "Integrity Co");
        await api.MoveAsync(id, "Applied");
        await (await api.Http.DeleteAsync($"/api/applications/{id}")).EnsureStatusAsync(System.Net.HttpStatusCode.NoContent);
        var export = await (await api.Http.GetAsync("/api/data/export")).Content.ReadAsStringAsync();
        var targetClient = await target.CreateAuthenticatedClientAsync();
        await (await targetClient.PostAsync("/api/data/import?confirm=replace-all",
            new StringContent(export, System.Text.Encoding.UTF8, "application/json"))).EnsureStatusAsync(System.Net.HttpStatusCode.OK);

        foreach (var factory in new[] { source, target })
        {
            Assert.Equal("ok", await Scalar(factory, "PRAGMA integrity_check"));
            Assert.Equal(0L, await Scalar(factory, "SELECT COUNT(*) FROM pragma_foreign_key_check"));
            Assert.Equal(1L, await Scalar(factory, "PRAGMA foreign_keys"));
        }
    }

    private static Task<object?> Scalar(AppFactory factory, string sql) => factory.WithDbAsync(async db =>
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    });
}
